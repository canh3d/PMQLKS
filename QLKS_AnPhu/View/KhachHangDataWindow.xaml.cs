using System.Data;
using Microsoft.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using QLKS_AnPhu.DAL;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.View
{
    public partial class KhachHangDataWindow : Window
    {
        private readonly KhachHangDTO khachHang;
        private readonly DataMode mode;

        public KhachHangDataWindow(KhachHangDTO khachHang, DataMode mode)
        {
            this.khachHang = khachHang;
            this.mode = mode;
            InitializeComponent();
            Loaded += KhachHangDataWindow_Loaded;
        }

        private void KhachHangDataWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TxtTieuDe.Text = mode == DataMode.LichSuThue
                ? $"Lịch sử thuê phòng - {khachHang.HoTen}"
                : $"Danh sách hóa đơn - {khachHang.HoTen}";

            try
            {
                ConfigureColumns();
                DataTable data = LoadData();
                DgData.ItemsSource = data.DefaultView;
                TxtThongBao.Text = data.Rows.Count == 0
                    ? "Không có dữ liệu phù hợp trong database."
                    : $"Tổng: {data.Rows.Count} dòng";
            }
            catch (Exception ex)
            {
                TxtThongBao.Text = "Không tải được dữ liệu: " + ex.Message;
            }
        }

        private DataTable LoadData()
        {
            return mode == DataMode.LichSuThue
                ? LoadLichSuThueData()
                : LoadHoaDonData();
        }

        private DataTable LoadHoaDonData()
        {
            List<TableMap> maps = GetTableMaps();
            TableMap? map = maps
                .FirstOrDefault(item => item.Name.Equals("HOADON", StringComparison.OrdinalIgnoreCase) ||
                                        item.Name.Equals("HoaDon", StringComparison.OrdinalIgnoreCase));

            if (map == null)
            {
                return new DataTable();
            }

            string alias = "HD";
            string maHoaDonExpr = ColumnExpr(map, alias, "MaHD", "MaHoaDon", "InvoiceID", "Id");
            string maThueExpr = ColumnExpr(map, alias, "MaThue", "MaPhieuThue");
            string ngayLapExpr = ColumnExpr(map, alias, "NgayLap", "NgayTao", "CreatedDate");
            string tienPhongExpr = ColumnExpr(map, alias, "TongTienPhong", "TienPhong");
            string tienDvExpr = ColumnExpr(map, alias, "TongTienDV", "TongTienDichVu", "TienDichVu");
            string phuThuExpr = ColumnExpr(map, alias, "TongPhuThu", "PhuThu");
            string giamGiaExpr = ColumnExpr(map, alias, "GiamGia");
            string tienCocExpr = ColumnExpr(map, alias, "TienCoc");
            string tongThanhToanExpr = ColumnExpr(map, alias, "TongThanhToan", "TongTien", "ThanhTien");
            string daThanhToanExpr = ColumnExpr(map, alias, "DaThanhToan", "SoTienDaThanhToan");
            string trangThaiExpr = ColumnExpr(map, alias, "TrangThai", "TinhTrang");
            string phuongThucExpr = ColumnExpr(map, alias, "PhuongThuc", "PhuongThucThanhToan");
            string ghiChuExpr = ColumnExpr(map, alias, "GhiChu", "MoTa", "Note");

            List<SqlParameter> parameters = new();
            List<string> filters = BuildKhachHangFilters(map, alias, parameters);
            string where = filters.Count > 0 ? "WHERE " + string.Join(" OR ", filters) : string.Empty;
            string orderBy = !ngayLapExpr.Contains("NULL", StringComparison.OrdinalIgnoreCase)
                ? "ORDER BY NgayLap DESC, MaHoaDon DESC"
                : "ORDER BY MaHoaDon DESC";

            string sql = @"
SELECT " + maHoaDonExpr + @" AS MaHoaDon,
       " + maThueExpr + @" AS MaThue,
       " + ngayLapExpr + @" AS NgayLap,
       " + tienPhongExpr + @" AS TongTienPhong,
       " + tienDvExpr + @" AS TongTienDV,
       " + phuThuExpr + @" AS TongPhuThu,
       " + giamGiaExpr + @" AS GiamGia,
       " + tienCocExpr + @" AS TienCoc,
       " + tongThanhToanExpr + @" AS TongThanhToan,
       " + daThanhToanExpr + @" AS DaThanhToan,
       " + trangThaiExpr + @" AS TrangThai,
       " + phuongThucExpr + @" AS PhuongThuc,
       " + ghiChuExpr + @" AS GhiChu
FROM [" + map.Schema + @"].[" + map.Name + @"] " + alias + @"
" + where + @"
" + orderBy;

            return ConnectDB.GetData(sql, parameters.ToArray());
        }

        private DataTable LoadLichSuThueData()
        {
            List<TableMap> maps = GetTableMaps();
            string[] candidates = { "PHIEUDATPHONG", "DATPHONG", "PhieuDatPhong", "DatPhong", "PHIEUTHUE", "PhieuThue", "ThuePhong", "Booking", "Bookings", "RentalHistory" };
            TableMap? map = maps
                .FirstOrDefault(item => candidates.Any(name => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

            if (map == null)
            {
                return new DataTable();
            }

            TableMap? phongMap = maps.FirstOrDefault(item => item.Name.Equals("PHONG", StringComparison.OrdinalIgnoreCase));
            string alias = "T";
            string tableName = $"[{map.Schema}].[{map.Name}]";
            string joins = string.Empty;
            string roomExpr = BuildRoomExpression(map, phongMap, alias, ref joins);
            string maPhieuExpr = ColumnExpr(map, alias, "MaThue", "MaDatPhong", "MaPhieuThue", "Id");
            string loaiDatExpr = ColumnExpr(map, alias, "LoaiDat", "LoaiDatPhong", "LoaiThue", "Loai");
            string ngayNhanExpr = ColumnExpr(map, alias, "NgayNhan", "NgayNhanDuKien", "NgayNhanPhong");
            string ngayTraExpr = ColumnExpr(map, alias, "NgayTraPhong", "NgayTraDuKien", "NgayTra");
            string tienCocExpr = ColumnExpr(map, alias, "TienCoc", "DatCoc");
            string trangThaiExpr = ColumnExpr(map, alias, "TrangThai", "TinhTrang");
            string soNguoiExpr = ColumnExpr(map, alias, "SoNguoi", "SoKhach", "SoLuongKhach");
            string ghiChuExpr = BuildGhiChuExpression(map, alias);

            List<SqlParameter> parameters = new();
            List<string> filters = BuildKhachHangFilters(map, alias, parameters);
            string where = filters.Count > 0 ? "WHERE " + string.Join(" OR ", filters) : string.Empty;
            string orderBy = !ngayNhanExpr.Contains("NULL", StringComparison.OrdinalIgnoreCase)
                ? "ORDER BY NgayNhan DESC, MaPhieuThue DESC"
                : "ORDER BY MaPhieuThue DESC";

            string sql = @"
SELECT " + roomExpr + @" AS SoPhong,
       " + maPhieuExpr + @" AS MaPhieuThue,
       " + loaiDatExpr + @" AS LoaiDat,
       " + ngayNhanExpr + @" AS NgayNhan,
       " + ngayTraExpr + @" AS NgayTra,
       " + tienCocExpr + @" AS TienCoc,
       " + trangThaiExpr + @" AS TrangThai,
       " + soNguoiExpr + @" AS SoNguoi,
       " + ghiChuExpr + @" AS GhiChu
FROM " + tableName + @" " + alias + @"
" + joins + @"
" + where + @"
" + orderBy;

            return ConnectDB.GetData(sql, parameters.ToArray());
        }

        private void ConfigureColumns()
        {
            DgData.Columns.Clear();
            DgData.AutoGenerateColumns = false;

            if (mode == DataMode.HoaDon)
            {
                AddColumn("Mã hóa đơn", "MaHoaDon", 110);
                AddColumn("Mã thuê", "MaThue", 100);
                AddColumn("Ngày lập", "NgayLap", 150, "dd/MM/yyyy HH:mm");
                AddColumn("Tiền phòng", "TongTienPhong", 120, "N0");
                AddColumn("Tiền DV", "TongTienDV", 110, "N0");
                AddColumn("Phụ thu", "TongPhuThu", 100, "N0");
                AddColumn("Giảm giá", "GiamGia", 100, "N0");
                AddColumn("Tiền cọc", "TienCoc", 110, "N0");
                AddColumn("Tổng thanh toán", "TongThanhToan", 140, "N0");
                AddColumn("Đã thanh toán", "DaThanhToan", 130, "N0");
                AddColumn("Trạng thái", "TrangThai", 130);
                AddColumn("Phương thức", "PhuongThuc", 130);
                AddColumn("Ghi chú", "GhiChu", 180);
                return;
            }

            AddColumn("Số phòng", "SoPhong", 110);
            AddColumn("Mã phiếu thuê", "MaPhieuThue", 130);
            AddColumn("Loại đặt", "LoaiDat", 120);
            AddColumn("Ngày nhận", "NgayNhan", 150, "dd/MM/yyyy HH:mm");
            AddColumn("Ngày trả", "NgayTra", 150, "dd/MM/yyyy HH:mm");
            AddColumn("Tiền cọc", "TienCoc", 120, "N0");
            AddColumn("Trạng thái", "TrangThai", 130);
            AddColumn("Số người", "SoNguoi", 90);
            AddColumn("Ghi chú", "GhiChu", 220);
        }

        private void AddColumn(string header, string bindingPath, double width, string? format = null)
        {
            Binding binding = new(bindingPath);
            if (!string.IsNullOrWhiteSpace(format))
            {
                binding.StringFormat = format;
            }

            DgData.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = binding,
                Width = new DataGridLength(width)
            });
        }

        private List<string> BuildKhachHangFilters(TableMap map, string alias, List<SqlParameter> parameters)
        {
            List<string> filters = new();
            string idColumn = FirstExisting(map.Columns, "MaKhachHang", "MaKH", "KhachHangID", "CustomerID", "MaKhach");
            if (!string.IsNullOrWhiteSpace(idColumn) && khachHang.Ma > 0)
            {
                filters.Add($"{alias}.[{idColumn}] = @MaKhachHang");
                parameters.Add(new SqlParameter("@MaKhachHang", khachHang.Ma));
            }

            string nameColumn = FirstExisting(map.Columns, "HoTen", "TenKhachHang", "TenKH", "KhachHang", "CustomerName", "FullName");
            if (!string.IsNullOrWhiteSpace(nameColumn) && !string.IsNullOrWhiteSpace(khachHang.HoTen))
            {
                filters.Add($"{alias}.[{nameColumn}] LIKE @TenKhachHang");
                parameters.Add(new SqlParameter("@TenKhachHang", "%" + khachHang.HoTen + "%"));
            }

            return filters;
        }

        private static string BuildRoomExpression(TableMap map, TableMap? phongMap, string alias, ref string joins)
        {
            string maPhongColumn = FirstExisting(map.Columns, "MaPhong", "PhongID", "RoomID");
            if (string.IsNullOrWhiteSpace(maPhongColumn))
            {
                return ColumnExpr(map, alias, "SoPhong", "TenPhong", "RoomNumber", "RoomName");
            }

            if (phongMap == null)
            {
                return $"CAST({alias}.[{maPhongColumn}] AS nvarchar(100))";
            }

            string phongDisplayColumn = FirstExisting(phongMap.Columns, "TenPhong", "SoPhong", "MaSoPhong", "MaPhong");
            joins += $"LEFT JOIN [{phongMap.Schema}].[{phongMap.Name}] P ON P.[MaPhong] = {alias}.[{maPhongColumn}]";
            return $"COALESCE(CAST(P.[{phongDisplayColumn}] AS nvarchar(100)), CAST({alias}.[{maPhongColumn}] AS nvarchar(100)))";
        }

        private static string BuildGhiChuExpression(TableMap map, string alias)
        {
            string column = FirstExisting(map.Columns, "GhiChu", "MoTa", "Note");
            if (string.IsNullOrWhiteSpace(column))
            {
                return "CAST(N'' AS nvarchar(max))";
            }

            string value = $"LTRIM(RTRIM(CAST({alias}.[{column}] AS nvarchar(max))))";
            return @"CASE
                     WHEN " + alias + @".[" + column + @"] IS NULL THEN N''
                     WHEN " + value + @" LIKE N'[[]DATPHONG]%' THEN N''
                     WHEN " + value + @" LIKE N'[[]PHIEUTHUE]%' THEN N''
                     WHEN " + value + @" LIKE N'[[]THUEPHONG]%' THEN N''
                     ELSE " + value + @"
                   END";
        }

        private static string ColumnExpr(TableMap map, string alias, params string[] candidates)
        {
            string column = FirstExisting(map.Columns, candidates);
            return string.IsNullOrWhiteSpace(column)
                ? "CAST(NULL AS nvarchar(max))"
                : $"{alias}.[{column}]";
        }

        private static List<TableMap> GetTableMaps()
        {
            DataTable columns = ConnectDB.GetData(
                @"SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME
                  FROM INFORMATION_SCHEMA.COLUMNS
                  ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION");

            return columns.AsEnumerable()
                .GroupBy(row => new
                {
                    Schema = row["TABLE_SCHEMA"].ToString() ?? "dbo",
                    Name = row["TABLE_NAME"].ToString() ?? string.Empty
                })
                .Select(group => new TableMap
                {
                    Schema = group.Key.Schema,
                    Name = group.Key.Name,
                    Columns = group
                        .Select(row => row["COLUMN_NAME"].ToString() ?? string.Empty)
                        .Where(column => !string.IsNullOrWhiteSpace(column))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)
                })
                .ToList();
        }

        private static string FirstExisting(HashSet<string> columns, params string[] candidates)
        {
            return candidates.FirstOrDefault(columns.Contains) ?? string.Empty;
        }

        private void BtnDong_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public enum DataMode
        {
            LichSuThue,
            HoaDon
        }

        private class TableMap
        {
            public string Schema { get; set; } = "dbo";
            public string Name { get; set; } = string.Empty;
            public HashSet<string> Columns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }
    }
}
