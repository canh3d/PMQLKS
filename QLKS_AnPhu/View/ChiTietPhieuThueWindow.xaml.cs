using System.Data;
using System.Windows;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DAL;

namespace QLKS_AnPhu.View
{
    public partial class ChiTietPhieuThueWindow : Window
    {
        private readonly string loaiPhieu;
        private readonly int maGoc;
        private readonly PhongBUS phongBUS = new();
        private readonly ThanhToanFlowBUS thanhToanBUS = new();
        private DataRow? currentRow;

        public bool DuLieuDaThayDoi { get; private set; }

        public ChiTietPhieuThueWindow(string loaiPhieu, int maGoc)
        {
            this.loaiPhieu = loaiPhieu;
            this.maGoc = maGoc;
            InitializeComponent();
            Loaded += ChiTietPhieuThueWindow_Loaded;
        }

        private void ChiTietPhieuThueWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadChiTiet();
        }

        private void LoadChiTiet()
        {
            try
            {
                DataTable data = ConnectDB.GetData(loaiPhieu == "DAT" ? SqlDatPhong() : SqlPhieuThue(), new SqlParameter("@Ma", maGoc));
                if (data.Rows.Count == 0)
                {
                    MessageBox.Show("Không tìm thấy dữ liệu phiếu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Close();
                    return;
                }

                currentRow = data.Rows[0];
                ThongTinPhongChiTiet thongTinPhong = LayThongTinPhongChiTiet();
                decimal tienPhong = thongTinPhong.TienPhong > 0 ? thongTinPhong.TienPhong : GetDecimal(currentRow, "TienPhong");
                decimal phuThu = thongTinPhong.PhuPhi > 0 ? thongTinPhong.PhuPhi : GetDecimal(currentRow, "PhuPhi");
                decimal tienCoc = GetDecimal(currentRow, "TienCoc");
                decimal tienDichVu = LoadDichVuSuDung();
                bool laVip = GetString(currentRow, "LoaiKhach").Contains("VIP", StringComparison.OrdinalIgnoreCase);
                decimal giamGia = laVip ? Math.Round((tienPhong + phuThu) * 0.1m, 0) : 0;
                decimal tongTien = Math.Max(0, tienPhong + phuThu + tienDichVu - giamGia);
                decimal conLai = Math.Max(0, tongTien - tienCoc);

                TxtTieuDe.Text = "Chi tiết phiếu thuê / đặt phòng";
                TxtMaPhieu.Text = TaoMaPhieuHienThi(GetString(currentRow, "MaPhieu"), thongTinPhong.SoLuongPhong);
                TxtTrangThai.Text = GetString(currentRow, "TrangThaiHienThi");
                TxtHoTen.Text = GetString(currentRow, "HoTen");
                TxtSDT.Text = GetString(currentRow, "SDT");
                TxtCCCD.Text = GetString(currentRow, "CCCD");
                TxtLoaiKhach.Text = GetString(currentRow, "LoaiKhach");
                TxtDiaChi.Text = GetString(currentRow, "DiaChi");
                TxtPhong.Text = thongTinPhong.SoLuongPhong > 1
                    ? thongTinPhong.SoPhong + " (" + thongTinPhong.SoLuongPhong.ToString("N0") + " phòng)"
                    : thongTinPhong.SoPhong;
                TxtLoaiPhong.Text = thongTinPhong.TenLoaiPhong;
                if (thongTinPhong.SoLuongPhong > 1)
                {
                    TxtPhong.Text = thongTinPhong.SoPhong + " (" + thongTinPhong.SoLuongPhong.ToString("N0") + " phòng)";
                }
                TxtNgayNhan.Text = GetDateText(currentRow, "NgayNhanThucTe");
                TxtNgayTra.Text = GetDateText(currentRow, "NgayTraThucTe");
                TxtNgayNhanDuKien.Text = GetDateText(currentRow, "NgayNhanDuKien");
                TxtNgayTraDuKien.Text = GetDateText(currentRow, "NgayTraDuKien");
                TxtThoiLuong.Text = TinhThoiGianConLai(
                    GetDate(currentRow, "NgayNhan"),
                    GetDate(currentRow, "NgayTraDuKien"),
                    TxtTrangThai.Text);
                TxtLoaiPhieu.Text = loaiPhieu == "DAT" ? "Phiếu đặt phòng" : "Phiếu thuê";
                TxtTienPhong.Text = tienPhong.ToString("N0") + " VND";
                TxtPhuThuLabel.Text = TaoNhanPhuPhiNhanSom(
                    GetNullableDate(currentRow, "NgayNhanThucTe"),
                    GetNullableDate(currentRow, "NgayNhanDuKien"));
                TxtPhuThu.Text = phuThu.ToString("N0") + " VND";
                TxtGiamGia.Text = giamGia.ToString("N0") + " VND";
                TxtTienCoc.Text = tienCoc.ToString("N0") + " VND";
                TxtTongTien.Text = tongTien.ToString("N0") + " VND";
                TxtConLai.Text = conLai.ToString("N0") + " VND";
                TxtGhiChu.Text = GetString(currentRow, "GhiChu");

                bool daDat = string.Equals(TxtTrangThai.Text, "Đã đặt", StringComparison.OrdinalIgnoreCase);
                bool dangThue = string.Equals(TxtTrangThai.Text, "Đang thuê", StringComparison.OrdinalIgnoreCase);
                BtnNhanPhong.IsEnabled = loaiPhieu == "DAT" && daDat;
                BtnTraPhong.IsEnabled = loaiPhieu == "THUE" && dangThue;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không tải được chi tiết phiếu: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string TaoNhanPhuPhiNhanSom(DateTime? ngayNhanThucTe, DateTime? ngayNhanDuKien)
        {
            if (!ngayNhanThucTe.HasValue || !ngayNhanDuKien.HasValue || ngayNhanThucTe.Value >= ngayNhanDuKien.Value)
            {
                return "Phụ phí nhận sớm";
            }

            int tongPhut = Math.Max(0, (int)Math.Round((ngayNhanDuKien.Value - ngayNhanThucTe.Value).TotalMinutes));
            int soGio = tongPhut / 60;
            int soPhut = tongPhut % 60;
            string thoiGian = soGio > 0 && soPhut > 0
                ? $"{soGio} giờ {soPhut} phút"
                : soGio > 0
                    ? $"{soGio} giờ"
                    : $"{soPhut} phút";
            return $"Phụ phí nhận sớm ({thoiGian})";
        }

        private string SqlPhieuThue()
        {
            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists("DATPHONG") ? "DATPHONG" : string.Empty;
            bool coPhieuDatLienKet = !string.IsNullOrWhiteSpace(bangDatPhong) && ColumnExists("PHIEUTHUE", "MaDatPhong");
            string ngayNhanDatColumn = coPhieuDatLienKet && ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong";
            string ngayTraDatColumn = coPhieuDatLienKet && ColumnExists(bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong";
            string ngayNhanDuKienExpr = coPhieuDatLienKet ? "ISNULL(DP." + ngayNhanDatColumn + ", PT.NgayNhan)" : "PT.NgayNhan";
            string ngayTraDuKienExpr = coPhieuDatLienKet ? "ISNULL(DP." + ngayTraDatColumn + ", PT.NgayTraDuKien)" : "PT.NgayTraDuKien";
            string joinDatPhong = coPhieuDatLienKet ? "LEFT JOIN dbo." + bangDatPhong + " DP ON PT.MaDatPhong = DP.MaDatPhong" : string.Empty;
            string tenPhongExpr = TenPhongSql("P");
            string ngayTraThucTeExpr = ColumnExists("PHIEUTHUE", "NgayTraPhong") ? "PT.NgayTraPhong" : "CAST(NULL AS datetime)";
            string tienPhongExpr = PricingHelper.TienPhongSql(ngayNhanDuKienExpr, ngayTraDuKienExpr, ngayTraDuKienExpr);
            string phuPhiExpr = PricingHelper.PhuThuNhanSomSql("PT.NgayNhan", ngayNhanDuKienExpr);
            string diaChiExpr = ColumnExists("KHACHHANG", "DiaChi") ? "KH.DiaChi" : "CAST(NULL AS nvarchar(255))";
            string ghiChuExpr = ColumnExists("PHIEUTHUE", "GhiChu") ? "PT.GhiChu" : "CAST(NULL AS nvarchar(255))";
            string tenLoaiPhongExpr = ColumnExists("LOAIPHONG", "TenLoaiPhong") ? "LP.TenLoaiPhong" : "CAST(P.MaLoaiPhong AS nvarchar(50))";

            return @"SELECT N'PT' + RIGHT('000000' + CAST(PT.MaThue AS nvarchar(20)), 6) AS MaPhieu,
                            CASE
                                WHEN PT.TrangThai IN (N'Đã hủy', N'Da huy') THEN N'Đã hủy'
                                WHEN PT.TrangThai IN (N'Đã trả', N'Da tra', N'Đã trả phòng', N'Da tra phong') THEN N'Đã trả phòng'
                                WHEN PT.TrangThai IN (N'Đang thuê', N'Dang thue') OR P.TrangThai IN (N'Có khách', N'Co khach', N'Đang thuê', N'Dang thue') THEN N'Đang thuê'
                                ELSE ISNULL(PT.TrangThai, N'Đang thuê')
                            END AS TrangThaiHienThi,
                            KH.HoTen, KH.SDT, KH.CCCD, KH.LoaiKhach, " + diaChiExpr + @" AS DiaChi,
                            " + tenPhongExpr + @" AS SoPhong, " + tenLoaiPhongExpr + @" AS TenLoaiPhong,
                            " + ghiChuExpr + @" AS GhiChu,
                            " + tienPhongExpr + @" AS TienPhong,
                            " + phuPhiExpr + @" AS PhuPhi,
                            PT.NgayNhan,
                            " + ngayTraThucTeExpr + @" AS NgayTra,
                            PT.NgayNhan AS NgayNhanThucTe,
                            " + ngayTraThucTeExpr + @" AS NgayTraThucTe,
                            " + ngayNhanDuKienExpr + @" AS NgayNhanDuKien,
                            " + ngayTraDuKienExpr + @" AS NgayTraDuKien,
                            ISNULL(PT.TienCoc, 0) AS TienCoc
                     FROM dbo.PHIEUTHUE PT
                     JOIN dbo.KHACHHANG KH ON PT.MaKH = KH.MaKH
                     " + joinDatPhong + @"
                     JOIN dbo.PHONG P ON PT.MaPhong = P.MaPhong
                     LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
                     WHERE PT.MaThue = @Ma";
        }

        private static string TaoMaPhieuHienThi(string maPhieu, int soLuongPhong)
        {
            if (soLuongPhong <= 1 || maPhieu.StartsWith("PTTD", StringComparison.OrdinalIgnoreCase))
            {
                return maPhieu;
            }

            return maPhieu.StartsWith("PT", StringComparison.OrdinalIgnoreCase)
                ? "PTTD" + maPhieu[2..]
                : maPhieu;
        }

        private string SqlDatPhong()
        {
            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : "DATPHONG";
            string tenPhongExpr = TenPhongSql("P");
            string ngayNhan = ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "DP.NgayNhanDuKien" : "DP.NgayNhanPhong";
            string ngayTra = ColumnExists(bangDatPhong, "NgayTraDuKien") ? "DP.NgayTraDuKien" : "DP.NgayTraPhong";
            string tienCoc = ColumnExists(bangDatPhong, "TienCoc") ? "DP.TienCoc" : "DP.DatCoc";
            string tienPhongExpr = PricingHelper.TienPhongSql(ngayNhan, ngayTra, ngayTra);
            string diaChiExpr = ColumnExists("KHACHHANG", "DiaChi") ? "KH.DiaChi" : "CAST(NULL AS nvarchar(255))";
            string ghiChuExpr = ColumnExists(bangDatPhong, "GhiChu") ? "DP.GhiChu" : "CAST(NULL AS nvarchar(255))";
            string tenLoaiPhongExpr = ColumnExists("LOAIPHONG", "TenLoaiPhong") ? "LP.TenLoaiPhong" : "CAST(P.MaLoaiPhong AS nvarchar(50))";
            string joinPhong = TableExists("CHITIETDATPHONG")
                ? @"JOIN dbo.CHITIETDATPHONG CT ON DP.MaDatPhong = CT.MaDatPhong
                     JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong"
                : "JOIN dbo.PHONG P ON DP.MaPhong = P.MaPhong";

            return @"SELECT N'DP' + RIGHT('000000' + CAST(DP.MaDatPhong AS nvarchar(20)), 6) AS MaPhieu,
                            CASE
                                WHEN DP.TrangThai IN (N'Đã hủy', N'Da huy') THEN N'Đã hủy'
                                WHEN P.TrangThai IN (N'Có khách', N'Co khach', N'Đang thuê', N'Dang thue') THEN N'Đang thuê'
                                ELSE N'Đã đặt'
                            END AS TrangThaiHienThi,
                            KH.HoTen, KH.SDT, KH.CCCD, KH.LoaiKhach, " + diaChiExpr + @" AS DiaChi,
                            " + tenPhongExpr + @" AS SoPhong, " + tenLoaiPhongExpr + @" AS TenLoaiPhong,
                            " + ghiChuExpr + @" AS GhiChu,
                            " + tienPhongExpr + @" AS TienPhong,
                            CAST(0 AS decimal(18,2)) AS PhuPhi,
                            " + ngayNhan + @" AS NgayNhan,
                            " + ngayTra + @" AS NgayTra,
                            CAST(NULL AS datetime) AS NgayNhanThucTe,
                            CAST(NULL AS datetime) AS NgayTraThucTe,
                            " + ngayNhan + @" AS NgayNhanDuKien,
                            " + ngayTra + @" AS NgayTraDuKien,
                            ISNULL(" + tienCoc + @", 0) AS TienCoc
                     FROM dbo." + bangDatPhong + @" DP
                     JOIN dbo.KHACHHANG KH ON DP.MaKH = KH.MaKH
                     " + joinPhong + @"
                     LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
                     WHERE DP.MaDatPhong = @Ma";
        }

        private decimal LoadDichVuSuDung()
        {
            string table = TableExists("PHATSINHDICHVU") ? "PHATSINHDICHVU" : TableExists("CHITIETPHATSINH") ? "CHITIETPHATSINH" : string.Empty;
            if (string.IsNullOrWhiteSpace(table) || !TableExists("DICHVUVATTU"))
            {
                DgDichVu.ItemsSource = null;
                TxtTongDichVu.Text = "Tổng cộng dịch vụ: 0 VND";
                return 0;
            }

            string keyColumn = loaiPhieu == "THUE" && ColumnExists(table, "MaThue") ? "MaThue" : ColumnExists(table, "MaDatPhong") ? "MaDatPhong" : string.Empty;
            if (string.IsNullOrWhiteSpace(keyColumn))
            {
                DgDichVu.ItemsSource = null;
                TxtTongDichVu.Text = "Tổng cộng dịch vụ: 0 VND";
                return 0;
            }

            string tenDichVu = ColumnExists("DICHVUVATTU", "TenDVVT") ? "TenDVVT" : "TenDichVu";
            string maDvPs = ColumnExists(table, "MaDVVT") ? "MaDVVT" : "MaDichVu";
            string maDv = ColumnExists("DICHVUVATTU", "MaDVVT") ? "MaDVVT" : "MaDichVu";
            string soLuong = ColumnExists(table, "SoLuong") ? "PS.SoLuong" : "1";
            string donGia = ColumnExists(table, "DonGia") ? "ISNULL(PS.DonGia, DV.DonGia)" : "DV.DonGia";
            string thanhTien = ColumnExists(table, "ThanhTien") ? "PS.ThanhTien" : "(" + soLuong + " * " + donGia + ")";

            DataTable data = ConnectDB.GetData(
                @"SELECT DV." + tenDichVu + @" AS [Tên dịch vụ],
                         " + soLuong + @" AS [SL],
                         " + donGia + @" AS [Đơn giá],
                         " + thanhTien + @" AS [Thành tiền]
                  FROM dbo." + table + @" PS
                  JOIN dbo.DICHVUVATTU DV ON PS." + maDvPs + " = DV." + maDv + @"
                  WHERE PS." + keyColumn + " = @Ma",
                new SqlParameter("@Ma", maGoc));
            DgDichVu.ItemsSource = data.DefaultView;
            decimal total = data.AsEnumerable().Sum(row => GetDecimal(row, "Thành tiền"));
            TxtTongDichVu.Text = "Tổng cộng dịch vụ: " + total.ToString("N0") + " VND";
            return total;
        }

        private ThongTinPhongChiTiet LayThongTinPhongChiTiet()
        {
            ThongTinPhongChiTiet fallback = new()
            {
                SoPhong = GetString(currentRow!, "SoPhong"),
                TenLoaiPhong = GetString(currentRow!, "TenLoaiPhong"),
                SoLuongPhong = 1,
                TienPhong = GetDecimal(currentRow!, "TienPhong"),
                PhuPhi = GetDecimal(currentRow!, "PhuPhi")
            };

            try
            {
                DateTime ngayNhanDuKien = GetDate(currentRow!, "NgayNhanDuKien");
                DateTime ngayTraDuKien = GetDate(currentRow!, "NgayTraDuKien");
                DateTime? ngayNhanThucTe = GetNullableDate(currentRow!, "NgayNhanThucTe");
                DataTable rooms = loaiPhieu == "DAT" ? LayPhongTheoDatPhong() : LayPhongTheoPhieuThue();
                if (rooms.Rows.Count == 0)
                {
                    return fallback;
                }

                List<string> soPhong = rooms.AsEnumerable()
                    .Select(row => row["SoPhong"]?.ToString() ?? string.Empty)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                List<string> loaiPhong = rooms.AsEnumerable()
                    .Select(row => row["TenLoaiPhong"]?.ToString() ?? string.Empty)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                decimal tienPhong = rooms.AsEnumerable().Sum(row => PricingHelper.TinhTienPhong(ngayNhanDuKien, ngayTraDuKien, GetDecimal(row, "DonGiaGio"), GetDecimal(row, "DonGiaNgay")));
                decimal phuPhi = loaiPhieu == "THUE" && ngayNhanThucTe.HasValue
                    ? rooms.AsEnumerable().Sum(row => PricingHelper.TinhPhuThuNhanSom(ngayNhanThucTe.Value, ngayNhanDuKien, GetDecimal(row, "DonGiaGio"), GetDecimal(row, "DonGiaNgay")))
                    : 0;
                return new ThongTinPhongChiTiet
                {
                    SoPhong = string.Join(", ", soPhong),
                    TenLoaiPhong = string.Join(", ", loaiPhong),
                    SoLuongPhong = soPhong.Count,
                    TienPhong = tienPhong,
                    PhuPhi = phuPhi
                };
            }
            catch
            {
                return fallback;
            }
        }

        private DataTable LayPhongTheoPhieuThue()
        {
            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists("DATPHONG") ? "DATPHONG" : string.Empty;
            string tenPhongExpr = TenPhongSql("P");
            string tenLoaiPhongExpr = ColumnExists("LOAIPHONG", "TenLoaiPhong") ? "LP.TenLoaiPhong" : "CAST(P.MaLoaiPhong AS nvarchar(50))";
            string giaNgayExpr = "ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0))";
            bool coMaDatPhong = ColumnExists("PHIEUTHUE", "MaDatPhong");

            if (coMaDatPhong && !string.IsNullOrWhiteSpace(bangDatPhong) && ColumnExists(bangDatPhong, "MaPhong"))
            {
                string ngayNhanColumn = ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong";
                string ngayTraColumn = ColumnExists(bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong";
                DataTable nhomCu = ConnectDB.GetData(@"
SELECT " + tenPhongExpr + @" AS SoPhong,
       " + tenLoaiPhongExpr + @" AS TenLoaiPhong,
       ISNULL(LP.DonGiaGio, 0) AS DonGiaGio,
       " + giaNgayExpr + @" AS DonGiaNgay
FROM dbo.PHIEUTHUE PT
JOIN dbo." + bangDatPhong + @" DP0 ON PT.MaDatPhong = DP0.MaDatPhong
JOIN dbo." + bangDatPhong + @" DPG ON DPG.MaKH = DP0.MaKH
    AND CONVERT(date, DPG." + ngayNhanColumn + @") = CONVERT(date, DP0." + ngayNhanColumn + @")
    AND CONVERT(date, DPG." + ngayTraColumn + @") = CONVERT(date, DP0." + ngayTraColumn + @")
JOIN dbo.PHONG P ON DPG.MaPhong = P.MaPhong
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE PT.MaThue = @Ma
ORDER BY P.MaPhong", new SqlParameter("@Ma", maGoc));
                if (nhomCu.Rows.Count > 1)
                {
                    return nhomCu;
                }
            }

            if (coMaDatPhong && TableExists("CHITIETDATPHONG"))
            {
                DataTable chiTiet = ConnectDB.GetData(@"
SELECT " + tenPhongExpr + @" AS SoPhong,
       " + tenLoaiPhongExpr + @" AS TenLoaiPhong,
       ISNULL(LP.DonGiaGio, 0) AS DonGiaGio,
       " + giaNgayExpr + @" AS DonGiaNgay
FROM dbo.PHIEUTHUE PT
JOIN dbo.CHITIETDATPHONG CT ON PT.MaDatPhong = CT.MaDatPhong
JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE PT.MaThue = @Ma
ORDER BY P.MaPhong", new SqlParameter("@Ma", maGoc));
                if (chiTiet.Rows.Count > 0)
                {
                    return chiTiet;
                }
            }

            return ConnectDB.GetData(@"
SELECT " + tenPhongExpr + @" AS SoPhong,
       " + tenLoaiPhongExpr + @" AS TenLoaiPhong,
       ISNULL(LP.DonGiaGio, 0) AS DonGiaGio,
       " + giaNgayExpr + @" AS DonGiaNgay
FROM dbo.PHIEUTHUE PT
JOIN dbo.PHONG P ON PT.MaPhong = P.MaPhong
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE PT.MaThue = @Ma", new SqlParameter("@Ma", maGoc));
        }

        private DataTable LayPhongTheoDatPhong()
        {
            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : "DATPHONG";
            string tenPhongExpr = TenPhongSql("P");
            string tenLoaiPhongExpr = ColumnExists("LOAIPHONG", "TenLoaiPhong") ? "LP.TenLoaiPhong" : "CAST(P.MaLoaiPhong AS nvarchar(50))";
            string giaNgayExpr = "ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0))";

            if (ColumnExists(bangDatPhong, "MaPhong"))
            {
                string ngayNhanColumn = ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong";
                string ngayTraColumn = ColumnExists(bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong";
                DataTable nhomCu = ConnectDB.GetData(@"
SELECT " + tenPhongExpr + @" AS SoPhong,
       " + tenLoaiPhongExpr + @" AS TenLoaiPhong,
       ISNULL(LP.DonGiaGio, 0) AS DonGiaGio,
       " + giaNgayExpr + @" AS DonGiaNgay
FROM dbo." + bangDatPhong + @" DP0
JOIN dbo." + bangDatPhong + @" DPG ON DPG.MaKH = DP0.MaKH
    AND CONVERT(date, DPG." + ngayNhanColumn + @") = CONVERT(date, DP0." + ngayNhanColumn + @")
    AND CONVERT(date, DPG." + ngayTraColumn + @") = CONVERT(date, DP0." + ngayTraColumn + @")
JOIN dbo.PHONG P ON DPG.MaPhong = P.MaPhong
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE DP0.MaDatPhong = @Ma
ORDER BY P.MaPhong", new SqlParameter("@Ma", maGoc));
                if (nhomCu.Rows.Count > 0)
                {
                    return nhomCu;
                }
            }

            if (TableExists("CHITIETDATPHONG"))
            {
                return ConnectDB.GetData(@"
SELECT " + tenPhongExpr + @" AS SoPhong,
       " + tenLoaiPhongExpr + @" AS TenLoaiPhong,
       ISNULL(LP.DonGiaGio, 0) AS DonGiaGio,
       " + giaNgayExpr + @" AS DonGiaNgay
FROM dbo.CHITIETDATPHONG CT
JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE CT.MaDatPhong = @Ma
ORDER BY P.MaPhong", new SqlParameter("@Ma", maGoc));
            }

            return new DataTable();
        }

        private static decimal TinhTienPhong(DateTime start, DateTime end, decimal giaGio, decimal giaNgay)
        {
            if (end <= start)
            {
                return giaNgay;
            }

            if (start.Date == end.Date)
            {
                return Math.Ceiling((decimal)(end - start).TotalHours) * giaGio;
            }

            if ((end - start).TotalHours <= 12)
            {
                return giaNgay;
            }

            int soNgay = Math.Max(1, (int)Math.Ceiling((end.Date - start.Date).TotalDays));
            return soNgay * giaNgay;
        }

        private void BtnNhanPhong_Click(object sender, RoutedEventArgs e)
        {
            if (loaiPhieu != "DAT")
            {
                return;
            }

            if (MessageBox.Show("Xác nhận khách đã đến và nhận phòng?", "Nhận phòng", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                decimal tongTienDuKien = 0;
                decimal tienDatCocTruoc = 0;
                decimal tienPhongCheckIn = 0;
                decimal tienDichVuCheckIn = 0;
                decimal phuThuCheckIn = 0;
                decimal giamGiaCheckIn = 0;
                if (currentRow != null)
                {
                    ThongTinPhongChiTiet thongTinPhong = LayThongTinPhongChiTiet();
                    tienPhongCheckIn = thongTinPhong.TienPhong > 0 ? thongTinPhong.TienPhong : GetDecimal(currentRow, "TienPhong");
                    phuThuCheckIn = thongTinPhong.PhuPhi > 0 ? thongTinPhong.PhuPhi : GetDecimal(currentRow, "PhuPhi");
                    tienDichVuCheckIn = LoadDichVuSuDung();
                    bool laVip = GetString(currentRow, "LoaiKhach").Contains("VIP", StringComparison.OrdinalIgnoreCase);
                    giamGiaCheckIn = laVip ? Math.Round((tienPhongCheckIn + phuThuCheckIn) * 0.1m, 0) : 0;
                    tongTienDuKien = Math.Max(0, tienPhongCheckIn + phuThuCheckIn + tienDichVuCheckIn - giamGiaCheckIn);
                    tienDatCocTruoc = GetDecimal(currentRow, "TienCoc");
                }

                if (!DialogService.XacNhanThanhToanCheckIn(this, "Phiếu đặt " + maGoc, tienPhongCheckIn, tienDichVuCheckIn, phuThuCheckIn, tienDatCocTruoc, giamGiaCheckIn))
                {
                    return;
                }

                phongBUS.NhanPhongTuDatPhong(
                    maGoc,
                    tongTienDuKien,
                    tienDatCocTruoc,
                    Math.Max(0, tienPhongCheckIn + phuThuCheckIn - giamGiaCheckIn));
                DuLieuDaThayDoi = true;
                MessageBox.Show("Đã nhận phòng. Phòng đã chuyển sang trạng thái đang thuê.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadChiTiet();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể nhận phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnTraPhong_Click(object sender, RoutedEventArgs e)
        {
            if (loaiPhieu != "THUE")
            {
                return;
            }

            if (MessageBox.Show("Xác nhận trả phòng và chuyển phòng sang trạng thái chưa dọn dẹp?", "Trả phòng", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                thanhToanBUS.CheckOut(maGoc);
                DuLieuDaThayDoi = true;
                MessageBox.Show("Đã trả phòng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadChiTiet();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể trả phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string TienPhongSql(string startExpr, string plannedEndExpr, string actualEndExpr)
        {
            string giaNgayExpr = "ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0)";
            return @"CAST(CASE
    WHEN " + plannedEndExpr + @" IS NULL OR DATEDIFF(minute, " + startExpr + @", " + plannedEndExpr + @") <= 0 THEN " + giaNgayExpr + @"
    WHEN CAST(" + startExpr + @" AS date) = CAST(" + plannedEndExpr + @" AS date) THEN CEILING(DATEDIFF(minute, " + startExpr + @", " + plannedEndExpr + @") / 60.0) * ISNULL(LP.DonGiaGio, 0)
    WHEN DATEDIFF(hour, " + startExpr + @", " + plannedEndExpr + @") <= 12 THEN " + giaNgayExpr + @"
    ELSE CASE WHEN DATEDIFF(day, CAST(" + startExpr + @" AS date), CAST(" + plannedEndExpr + @" AS date)) <= 0 THEN 1
              ELSE DATEDIFF(day, CAST(" + startExpr + @" AS date), CAST(" + plannedEndExpr + @" AS date))
         END * " + giaNgayExpr + @"
END AS decimal(18, 2))";
        }

        private void TraPhong()
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();
            try
            {
                string trangThaiPhong = "Chưa dọn dẹp";
                CapNhatTrangThaiPhongTheoNhomThue(conn, tran, maGoc, trangThaiPhong);
                using (SqlCommand cmd = new(
                           @"UPDATE P
                             SET P.TrangThai = @TrangThai
                             FROM dbo.PHONG P
                             JOIN dbo.PHIEUTHUE PT ON P.MaPhong = PT.MaPhong
                             WHERE PT.MaThue = @Ma",
                           conn,
                           tran))
                {
                    cmd.Parameters.AddWithValue("@TrangThai", trangThaiPhong);
                    cmd.Parameters.AddWithValue("@Ma", maGoc);
                    cmd.ExecuteNonQuery();
                }

                string setNgayTra = ColumnExists("PHIEUTHUE", "NgayTraPhong") ? ", NgayTraPhong = @NgayTra" : string.Empty;
                using (SqlCommand cmd = new("UPDATE dbo.PHIEUTHUE SET TrangThai = @TrangThai" + setNgayTra + " WHERE MaThue = @Ma", conn, tran))
                {
                    cmd.Parameters.AddWithValue("@TrangThai", "Đã trả phòng");
                    cmd.Parameters.AddWithValue("@NgayTra", DateTime.Now);
                    cmd.Parameters.AddWithValue("@Ma", maGoc);
                    cmd.ExecuteNonQuery();
                }

                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        private static void CapNhatTrangThaiPhongTheoNhomThue(SqlConnection conn, SqlTransaction tran, int maThue, string trangThaiPhong)
        {
            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists("DATPHONG") ? "DATPHONG" : string.Empty;
            if (ColumnExists("PHIEUTHUE", "MaDatPhong") && !string.IsNullOrWhiteSpace(bangDatPhong) && ColumnExists(bangDatPhong, "MaPhong"))
            {
                string ngayNhanColumn = ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong";
                string ngayTraColumn = ColumnExists(bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong";
                using SqlCommand cmd = new(
                    @"UPDATE P
                      SET P.TrangThai = @TrangThai
                      FROM dbo.PHIEUTHUE PT
                      JOIN dbo." + bangDatPhong + @" DP0 ON PT.MaDatPhong = DP0.MaDatPhong
                      JOIN dbo." + bangDatPhong + @" DPG ON DPG.MaKH = DP0.MaKH
                         AND CONVERT(date, DPG." + ngayNhanColumn + @") = CONVERT(date, DP0." + ngayNhanColumn + @")
                         AND CONVERT(date, DPG." + ngayTraColumn + @") = CONVERT(date, DP0." + ngayTraColumn + @")
                      JOIN dbo.PHONG P ON DPG.MaPhong = P.MaPhong
                      WHERE PT.MaThue = @Ma",
                    conn,
                    tran);
                cmd.Parameters.AddWithValue("@TrangThai", trangThaiPhong);
                cmd.Parameters.AddWithValue("@Ma", maThue);
                cmd.ExecuteNonQuery();
                return;
            }

            if (ColumnExists("PHIEUTHUE", "MaDatPhong") && TableExists("CHITIETDATPHONG"))
            {
                using SqlCommand cmd = new(
                    @"UPDATE P
                      SET P.TrangThai = @TrangThai
                      FROM dbo.PHIEUTHUE PT
                      JOIN dbo.CHITIETDATPHONG CT ON PT.MaDatPhong = CT.MaDatPhong
                      JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong
                      WHERE PT.MaThue = @Ma",
                    conn,
                    tran);
                cmd.Parameters.AddWithValue("@TrangThai", trangThaiPhong);
                cmd.Parameters.AddWithValue("@Ma", maThue);
                cmd.ExecuteNonQuery();
            }
        }

        private void BtnDong_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static string TinhThoiGianConLai(DateTime start, DateTime end, string trangThai)
        {
            if (end <= start)
            {
                return "--";
            }

            bool dangThue = BoDau(trangThai).Contains("thue", StringComparison.OrdinalIgnoreCase);
            if (!dangThue)
            {
                return DinhDangKhoangThoiGian(end - start);
            }

            TimeSpan remaining = end - DateTime.Now;
            if (remaining.TotalMinutes < 0)
            {
                return "Qua gio " + DinhDangKhoangThoiGian(DateTime.Now - end);
            }

            return "Con lai " + DinhDangKhoangThoiGian(remaining);
        }

        private static string DinhDangKhoangThoiGian(TimeSpan value)
        {
            int totalHours = Math.Max(1, (int)Math.Ceiling(value.TotalHours));
            int days = totalHours / 24;
            int hours = totalHours % 24;

            if (days > 0 && hours > 0)
            {
                return days + " ngay " + hours + " gio";
            }

            if (days > 0)
            {
                return days + " ngay";
            }

            return totalHours + " gio";
        }

        private static string TinhThoiLuong(DateTime start, DateTime end)
        {
            if (end <= start) return "1 ngày";
            if (start.Date == end.Date) return Math.Max(1, (int)Math.Ceiling((end - start).TotalHours)) + " giờ";
            if ((end - start).TotalHours <= 12) return "1 đêm";
            return Math.Max(1, (int)Math.Ceiling((end - start).TotalDays)) + " ngày";
        }

        private static string GetString(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? row[column]?.ToString() ?? "--" : "--";
        }

        private static decimal GetDecimal(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && row[column] != DBNull.Value && decimal.TryParse(row[column]?.ToString(), out decimal value) ? value : 0;
        }

        private static DateTime GetDate(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && DateTime.TryParse(row[column]?.ToString(), out DateTime value) ? value : DateTime.Now;
        }

        private static DateTime? GetNullableDate(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) &&
                   row[column] != DBNull.Value &&
                   DateTime.TryParse(row[column]?.ToString(), out DateTime value)
                ? value
                : null;
        }

        private static string GetDateText(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) &&
                   row[column] != DBNull.Value &&
                   DateTime.TryParse(row[column]?.ToString(), out DateTime value)
                ? value.ToString("dd/MM/yyyy HH:mm")
                : "--";
        }

        private static bool TableExists(string tableName)
        {
            object? result = ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM sys.tables WHERE name = @Name", new SqlParameter("@Name", tableName));
            return Convert.ToInt32(result) > 0;
        }

        private static bool ColumnExists(string tableName, string columnName)
        {
            object? result = ConnectDB.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM sys.tables t
                  JOIN sys.columns c ON t.object_id = c.object_id
                  WHERE t.name = @TableName AND c.name = @ColumnName",
                new SqlParameter("@TableName", tableName),
                new SqlParameter("@ColumnName", columnName));
            return Convert.ToInt32(result) > 0;
        }

        private static string TenPhongSql(string alias)
        {
            if (ColumnExists("PHONG", "TenPhong"))
            {
                return alias + ".TenPhong";
            }

            if (ColumnExists("PHONG", "SoPhong"))
            {
                return alias + ".SoPhong";
            }

            if (ColumnExists("PHONG", "MaSoPhong"))
            {
                return alias + ".MaSoPhong";
            }

            return "N'P' + CAST(" + alias + ".MaPhong AS nvarchar(20))";
        }

        private static string BoDau(string value)
        {
            string formD = (value ?? string.Empty).Normalize(System.Text.NormalizationForm.FormD);
            char[] chars = formD
                .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .Select(ch => ch)
                .ToArray();
            return new string(chars).Normalize(System.Text.NormalizationForm.FormC);
        }

        private class ThongTinPhongChiTiet
        {
            public string SoPhong { get; set; } = "--";
            public string TenLoaiPhong { get; set; } = "--";
            public int SoLuongPhong { get; set; } = 1;
            public decimal TienPhong { get; set; }
            public decimal PhuPhi { get; set; }
        }
    }
}
