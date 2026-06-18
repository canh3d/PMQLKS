using System.Data;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DAL;
using QLKS_AnPhu.View;

namespace QLKS_AnPhu.Services
{
    public sealed class DashboardDataService
    {
        public Task<DashboardSnapshot> LoadSnapshotAsync()
        {
            return Task.Run(LoadSnapshot);
        }

        private DashboardSnapshot LoadSnapshot()
        {
            DateTime homNay = DateTime.Today;
            DateTime homQua = homNay.AddDays(-1);
            decimal doanhThuHomNay = TinhDoanhThuHoaDonDongBo(homNay);
            decimal doanhThuHomQua = TinhDoanhThuHoaDonDongBo(homQua);

            int tongPhong = TableExists("PHONG") ? ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM dbo.PHONG")) : 0;
            int phongDangThue = TableExists("PHONG")
                ? ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM dbo.PHONG WHERE TrangThai IN (N'Đang thuê', N'Có khách', N'Dang thue', N'Co khach')"))
                : 0;
            string canDonFilter = ColumnExists("PHONG", "GhiChu")
                ? " AND ISNULL(GhiChu, N'') NOT LIKE N'%[CAN_DON_DEP]%'"
                : string.Empty;
            int phongTrong = TableExists("PHONG")
                ? ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM dbo.PHONG WHERE TrangThai IN (N'Trống', N'Phòng trống', N'Trong', N'Phong trong')" + canDonFilter))
                : 0;
            int khachHang = TableExists("KHACHHANG") ? ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM dbo.KHACHHANG")) : 0;
            int khachMoiHomNay = CountKhachMoiHomNay();
            int datPhongMoiHomNay = CountDatPhongMoiHomNay();
            decimal tyLeLapDay = tongPhong == 0 ? 0 : Math.Round(phongDangThue * 100m / tongPhong, 2);
            decimal doanhThuDelta = doanhThuHomQua <= 0
                ? (doanhThuHomNay > 0 ? 100 : 0)
                : Math.Round((doanhThuHomNay - doanhThuHomQua) * 100m / doanhThuHomQua, 1);

            return new DashboardSnapshot
            {
                DoanhThuHomNay = doanhThuHomNay,
                DoanhThuDelta = doanhThuDelta,
                PhongDangThue = phongDangThue,
                PhongTrong = phongTrong,
                TongPhong = tongPhong,
                KhachHang = khachHang,
                KhachMoiHomNay = khachMoiHomNay,
                DatPhongMoiHomNay = datPhongMoiHomNay,
                TyLeLapDay = tyLeLapDay,
                KhachHangGanDay = LoadKhachHangGanDay(),
                KhachSapCheckoutHomNay = CountCheckoutToday(),
                PhongDaDatChuaNhan = CountBookedNotCheckedIn(),
                HoaDonChuaThanhToan = CountUnpaidInvoices()
            };
        }

        private static decimal TinhDoanhThuHoaDonDongBo(DateTime ngay)
        {
            if (!TableExists("HOADON"))
            {
                return 0;
            }

            return HoaDon.LayHoaDonDongBo(ngay, ngay)
                .Where(item => item.DaThanhToan || item.LaPhieuDatDaHuyGiuCoc)
                .Sum(item => item.TongGiaTriHoaDon);
        }

        private static int CountKhachMoiHomNay()
        {
            if (!TableExists("KHACHHANG"))
            {
                return 0;
            }

            string dateColumn = FirstExistingColumn("KHACHHANG", "NgayTao", "CreatedAt", "NgayDangKy");
            if (string.IsNullOrWhiteSpace(dateColumn))
            {
                return 0;
            }

            return ToInt(ConnectDB.ExecuteScalar(@"
SELECT COUNT(*)
FROM dbo.KHACHHANG
WHERE CAST(" + dateColumn + " AS date) = CAST(GETDATE() AS date)"));
        }

        private static int CountDatPhongMoiHomNay()
        {
            if (!TableExists("DATPHONG"))
            {
                return 0;
            }

            string dateColumn = FirstExistingColumn("DATPHONG", "NgayDat", "NgayLap", "CreatedAt");
            if (string.IsNullOrWhiteSpace(dateColumn))
            {
                return 0;
            }

            return ToInt(ConnectDB.ExecuteScalar(@"
SELECT COUNT(*)
FROM dbo.DATPHONG
WHERE CAST(" + dateColumn + " AS date) = CAST(GETDATE() AS date)"));
        }

        private static List<RecentCustomerDashboardItem> LoadKhachHangGanDay()
        {
            List<RecentCustomerDashboardItem> items = new();
            if (!TableExists("PHIEUTHUE") || !TableExists("KHACHHANG") || !TableExists("PHONG"))
            {
                return items;
            }

            string tenPhong = TenPhongSql("P");
            DataTable table = ConnectDB.GetData(@"
SELECT TOP 8
       KH.HoTen,
       " + tenPhong + @" AS Phong,
       ISNULL(PT.TrangThai, N'Đang thuê') AS TrangThai,
       PT.NgayNhan AS NgayNhanPhong
FROM dbo.PHIEUTHUE PT
JOIN dbo.KHACHHANG KH ON KH.MaKH = PT.MaKH
JOIN dbo.PHONG P ON P.MaPhong = PT.MaPhong
ORDER BY PT.NgayNhan DESC, PT.MaThue DESC");

            foreach (DataRow row in table.Rows)
            {
                items.Add(new RecentCustomerDashboardItem
                {
                    HoTen = GetString(row, "HoTen"),
                    Phong = GetString(row, "Phong"),
                    TrangThai = GetString(row, "TrangThai"),
                    NgayNhanPhong = row["NgayNhanPhong"] == DBNull.Value ? null : Convert.ToDateTime(row["NgayNhanPhong"])
                });
            }

            return items;
        }

        private static int CountCheckoutToday()
        {
            if (!TableExists("PHIEUTHUE") || !ColumnExists("PHIEUTHUE", "NgayTraDuKien"))
            {
                return 0;
            }

            string activeFilter = ColumnExists("PHIEUTHUE", "TrangThai")
                ? " AND ISNULL(TrangThai, N'') NOT IN (N'Đã trả phòng', N'Da tra phong', N'Đã hủy', N'Da huy', N'Hủy', N'Huy', N'No-Show', N'No Show', N'Khach khong den')"
                : string.Empty;

            return ToInt(ConnectDB.ExecuteScalar(@"
SELECT COUNT(*)
FROM dbo.PHIEUTHUE
WHERE CAST(NgayTraDuKien AS date) = CAST(GETDATE() AS date)" + activeFilter));
        }

        private static int CountBookedNotCheckedIn()
        {
            if (!TableExists("DATPHONG"))
            {
                return 0;
            }

            string statusFilter = ColumnExists("DATPHONG", "TrangThai")
                ? "WHERE ISNULL(TrangThai, N'') NOT IN (N'Đã check-in', N'Da check-in', N'Đã hủy', N'Da huy', N'Hủy', N'Huy', N'No-Show', N'No Show', N'Khach khong den', N'Đã trả phòng', N'Da tra phong')"
                : string.Empty;

            return ToInt(ConnectDB.ExecuteScalar(@"
SELECT COUNT(*)
FROM dbo.DATPHONG
" + statusFilter));
        }

        private static int CountUnpaidInvoices()
        {
            if (!TableExists("HOADON"))
            {
                return 0;
            }

            return HoaDon.LayHoaDonDongBo(DateTime.Today.AddYears(-20), DateTime.Today.AddYears(20))
                .Count(item => !item.DaThanhToan && !item.LaPhieuDatDaHuyGiuCoc);

#pragma warning disable CS0162
            if (ColumnExists("HOADON", "TrangThai"))
            {
                return ToInt(ConnectDB.ExecuteScalar(@"
SELECT COUNT(*)
FROM dbo.HOADON
WHERE ISNULL(TrangThai, N'') NOT IN (N'Đã thanh toán', N'Da thanh toan')"));
            }

            if (ColumnExists("HOADON", "DaThanhToan"))
            {
                return ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM dbo.HOADON WHERE ISNULL(DaThanhToan, 0) <= 0"));
            }

            return 0;
#pragma warning restore CS0162
        }

        private static bool TableExists(string tableName)
        {
            object? result = ConnectDB.ExecuteScalar(
                "SELECT COUNT(*) FROM sys.tables WHERE name = @Name",
                new SqlParameter("@Name", tableName));
            return ToInt(result) > 0;
        }

        private static bool ColumnExists(string tableName, string columnName)
        {
            object? result = ConnectDB.ExecuteScalar(@"
SELECT COUNT(*)
FROM sys.tables t
JOIN sys.columns c ON c.object_id = t.object_id
WHERE t.name = @TableName AND c.name = @ColumnName",
                new SqlParameter("@TableName", tableName),
                new SqlParameter("@ColumnName", columnName));
            return ToInt(result) > 0;
        }

        private static string FirstExistingColumn(string tableName, params string[] candidates)
        {
            return candidates.FirstOrDefault(column => ColumnExists(tableName, column)) ?? string.Empty;
        }

        private static string TenPhongSql(string alias)
        {
            if (ColumnExists("PHONG", "TenPhong")) return alias + ".TenPhong";
            if (ColumnExists("PHONG", "SoPhong")) return alias + ".SoPhong";
            return "N'P' + CAST(" + alias + ".MaPhong AS nvarchar(20))";
        }

        private static string GetString(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) ? row[column]?.ToString() ?? string.Empty : string.Empty;
        }

        private static decimal ToDecimal(object? value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static int ToInt(object? value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }
    }

    public sealed class DashboardSnapshot
    {
        public decimal DoanhThuHomNay { get; init; }
        public decimal DoanhThuDelta { get; init; }
        public int PhongDangThue { get; init; }
        public int PhongTrong { get; init; }
        public int TongPhong { get; init; }
        public int KhachHang { get; init; }
        public int KhachMoiHomNay { get; init; }
        public int DatPhongMoiHomNay { get; init; }
        public decimal TyLeLapDay { get; init; }
        public int KhachSapCheckoutHomNay { get; init; }
        public int PhongDaDatChuaNhan { get; init; }
        public int HoaDonChuaThanhToan { get; init; }
        public List<RecentCustomerDashboardItem> KhachHangGanDay { get; init; } = new();
    }

    public sealed class RecentCustomerDashboardItem
    {
        public string HoTen { get; init; } = string.Empty;
        public string Phong { get; init; } = string.Empty;
        public string TrangThai { get; init; } = string.Empty;
        public DateTime? NgayNhanPhong { get; init; }
    }
}
