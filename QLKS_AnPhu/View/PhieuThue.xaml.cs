using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QLKS_AnPhu.DAL;
using QLKS_AnPhu.DTO;
using QLKS_AnPhu.Services;
using QLKS_AnPhu.UserControls;

namespace QLKS_AnPhu.View
{
    public partial class PhieuThue : UserControl
    {
        private readonly BUS.PhongBUS phongBUS = new();
        private DataTable danhSachPhieu = new();
        private bool dangNapBoLoc;

        public PhieuThue()
        {
            InitializeComponent();
            BtnXuatExcel.Click -= BtnXuatExcel_Click;
            BtnXuatExcel.Click += BtnXuatExcel_Click;
            BtnInDanhSach.Click -= BtnInDanhSach_Click;
            BtnInDanhSach.Click += BtnInDanhSach_Click;
            SearchSuggestionService.Attach(TxtTimKiem, TaoGoiYTimKiem, _ => TaiDuLieu());
            Loaded += PhieuThue_Loaded;
        }

        private void PhieuThue_Loaded(object sender, RoutedEventArgs e)
        {
            TxtTimKiem.Text = string.Empty;
            NapLoaiPhong();
            TaiDuLieu();
        }

        private void NapLoaiPhong()
        {
            dangNapBoLoc = true;
            CboLoaiPhong.Items.Clear();
            CboLoaiPhong.Items.Add("T\u1ea5t c\u1ea3 lo\u1ea1i ph\u00f2ng");
            CboLoaiPhong.SelectedIndex = 0;

            try
            {
                if (TableExists("LOAIPHONG"))
                {
                    string sql = ColumnExists("LOAIPHONG", "TenLoaiPhong")
                        ? "SELECT DISTINCT TenLoaiPhong FROM dbo.LOAIPHONG ORDER BY TenLoaiPhong"
                        : "SELECT DISTINCT LoaiPhong AS TenLoaiPhong FROM dbo.LOAIPHONG ORDER BY LoaiPhong";
                    DataTable data = ConnectDB.GetData(sql);
                    foreach (DataRow row in data.Rows)
                    {
                        string value = row[0]?.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            CboLoaiPhong.Items.Add(value);
                        }
                    }
                }
            }
            catch
            {
            }

            dangNapBoLoc = false;
        }

        private void TaiDuLieu()
        {
            try
            {
                danhSachPhieu = LoadDanhSachPhieu();
                ThemCotStt(danhSachPhieu);
                DgPhieuThue.ItemsSource = danhSachPhieu.DefaultView;
                TxtTongBanGhi.Text = $"T\u1ed5ng s\u1ed1: {danhSachPhieu.Rows.Count} b\u1ea3n ghi";
            }
            catch (Exception ex)
            {
                DgPhieuThue.ItemsSource = null;
                TxtTongBanGhi.Text = "Kh\u00f4ng t\u1ea3i \u0111\u01b0\u1ee3c d\u1eef li\u1ec7u.";
                MessageBox.Show("Kh\u00f4ng th\u1ec3 t\u1ea3i danh s\u00e1ch phi\u1ebfu thu\u00ea.\nChi ti\u1ebft: " + ex.Message, "L\u1ed7i d\u1eef li\u1ec7u", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private DataTable LoadDanhSachPhieu()
        {
            string keyword = TxtTimKiem.Text.Trim();
            string status = LayTrangThaiLoc();
            string loaiPhong = CboLoaiPhong.SelectedIndex <= 0 ? string.Empty : CboLoaiPhong.Text;
            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : "DATPHONG";
            bool coBangDatPhong = TableExists(bangDatPhong);
            bool coChiTietDatPhong = TableExists("CHITIETDATPHONG");
            bool coPhieuThue = TableExists("PHIEUTHUE");
            string tenPhong = ColumnExists("PHONG", "TenPhong") ? "TenPhong" : ColumnExists("PHONG", "SoPhong") ? "SoPhong" : "MaPhong";
            string tenPhongExpr = TenPhongSql("P");
            string tenPhongP2Expr = TenPhongSql("P2");
            string tenLoaiPhongExpr = ColumnExists("LOAIPHONG", "TenLoaiPhong") ? "LP.TenLoaiPhong" : "CAST(P.MaLoaiPhong AS nvarchar(50))";
                string ghiChuThueExpr = coPhieuThue && ColumnExists("PHIEUTHUE", "GhiChu") ? "PT.GhiChu" : "CAST(NULL AS nvarchar(255))";
                string maDoanThueExpr = ColumnExists("PHIEUTHUE", "MaDoan")
                    ? "ISNULL(PT.MaDoan, 0)"
                    : coBangDatPhong && ColumnExists("PHIEUTHUE", "MaDatPhong") && ColumnExists(bangDatPhong, "MaDoan")
                        ? "ISNULL((SELECT TOP 1 DP0.MaDoan FROM dbo." + bangDatPhong + " DP0 WHERE DP0.MaDatPhong = PT.MaDatPhong), 0)"
                        : "CAST(0 AS int)";

            List<string> queries = new();

            if (coPhieuThue)
            {
                string ngayTraThucTe = ColumnExists("PHIEUTHUE", "NgayTraPhong") ? "ISNULL(PT.NgayTraPhong, PT.NgayTraDuKien)" : "PT.NgayTraDuKien";
                string tienPhongExpr = TienPhongSql("PT.NgayNhan", "PT.NgayTraDuKien", ngayTraThucTe);
                bool thueCoChiTietDoan = coChiTietDatPhong && ColumnExists("PHIEUTHUE", "MaDatPhong");
                string tongGiaNgayThueExpr = @"(SELECT ISNULL(SUM(ISNULL(NULLIF(LP2.DonGiaNgay, 0), ISNULL(NULLIF(LP2.DonGiaDem, 0), ISNULL(LP2.DonGiaGio, 0) * 24.0))), 0)
                                                FROM dbo.CHITIETDATPHONG CT2
                                                JOIN dbo.PHONG P2 ON CT2.MaPhong = P2.MaPhong
                                                LEFT JOIN dbo.LOAIPHONG LP2 ON P2.MaLoaiPhong = LP2.MaLoaiPhong
                                                WHERE CT2.MaDatPhong = PT.MaDatPhong)";
                string tongGiaGioThueExpr = @"(SELECT ISNULL(SUM(ISNULL(LP2.DonGiaGio, 0)), 0)
                                               FROM dbo.CHITIETDATPHONG CT2
                                               JOIN dbo.PHONG P2 ON CT2.MaPhong = P2.MaPhong
                                               LEFT JOIN dbo.LOAIPHONG LP2 ON P2.MaLoaiPhong = LP2.MaLoaiPhong
                                               WHERE CT2.MaDatPhong = PT.MaDatPhong)";
                string tonTaiChiTietThueExpr = "EXISTS (SELECT 1 FROM dbo.CHITIETDATPHONG CT2 WHERE CT2.MaDatPhong = PT.MaDatPhong)";
                bool thueCoNhomDatCu = coBangDatPhong && ColumnExists("PHIEUTHUE", "MaDatPhong") && ColumnExists(bangDatPhong, "MaPhong");
                string ngayNhanDatColumn = ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong";
                string ngayTraDatColumn = ColumnExists(bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong";
                string khongCoMaDoanDatCu = ColumnExists(bangDatPhong, "MaDoan")
                    ? " AND ISNULL(DPG.MaDoan, 0) = 0 AND ISNULL(DP0.MaDoan, 0) = 0"
                    : string.Empty;
                string dieuKienNhomDatCu = @"DPG.MaKH = DP0.MaKH
                              AND CONVERT(date, DPG." + ngayNhanDatColumn + @") = CONVERT(date, DP0." + ngayNhanDatColumn + @")
                              AND CONVERT(date, DPG." + ngayTraDatColumn + @") = CONVERT(date, DP0." + ngayTraDatColumn + @")" + khongCoMaDoanDatCu;
                string demNhomDatCuExpr = thueCoNhomDatCu
                    ? @"(SELECT COUNT(*)
                         FROM dbo." + bangDatPhong + @" DP0
                         JOIN dbo." + bangDatPhong + @" DPG ON " + dieuKienNhomDatCu + @"
                         WHERE DP0.MaDatPhong = PT.MaDatPhong)"
                    : "0";
                string tenPhongNhomDatCuExpr = thueCoNhomDatCu
                    ? @"(SELECT STRING_AGG(CAST(" + tenPhongP2Expr + @" AS nvarchar(max)), N', ')
                         FROM dbo." + bangDatPhong + @" DP0
                         JOIN dbo." + bangDatPhong + @" DPG ON " + dieuKienNhomDatCu + @"
                         JOIN dbo.PHONG P2 ON DPG.MaPhong = P2.MaPhong
                         WHERE DP0.MaDatPhong = PT.MaDatPhong)"
                    : tenPhongExpr;
                string tongGiaNgayNhomDatCuExpr = thueCoNhomDatCu
                    ? @"(SELECT ISNULL(SUM(ISNULL(NULLIF(LP2.DonGiaNgay, 0), ISNULL(NULLIF(LP2.DonGiaDem, 0), ISNULL(LP2.DonGiaGio, 0) * 24.0))), 0)
                         FROM dbo." + bangDatPhong + @" DP0
                         JOIN dbo." + bangDatPhong + @" DPG ON " + dieuKienNhomDatCu + @"
                         JOIN dbo.PHONG P2 ON DPG.MaPhong = P2.MaPhong
                         LEFT JOIN dbo.LOAIPHONG LP2 ON P2.MaLoaiPhong = LP2.MaLoaiPhong
                         WHERE DP0.MaDatPhong = PT.MaDatPhong)"
                    : "0";
                string tongGiaGioNhomDatCuExpr = thueCoNhomDatCu
                    ? @"(SELECT ISNULL(SUM(ISNULL(LP2.DonGiaGio, 0)), 0)
                         FROM dbo." + bangDatPhong + @" DP0
                         JOIN dbo." + bangDatPhong + @" DPG ON " + dieuKienNhomDatCu + @"
                         JOIN dbo.PHONG P2 ON DPG.MaPhong = P2.MaPhong
                         LEFT JOIN dbo.LOAIPHONG LP2 ON P2.MaLoaiPhong = LP2.MaLoaiPhong
                         WHERE DP0.MaDatPhong = PT.MaDatPhong)"
                    : "0";
                string loaiPhongNhomDatCuExpr = thueCoNhomDatCu
                    ? @"(SELECT TOP 1 " + tenLoaiPhongExpr.Replace("LP.", "LP2.").Replace("P.", "P2.") + @"
                         FROM dbo." + bangDatPhong + @" DP0
                         JOIN dbo." + bangDatPhong + @" DPG ON " + dieuKienNhomDatCu + @"
                         JOIN dbo.PHONG P2 ON DPG.MaPhong = P2.MaPhong
                         LEFT JOIN dbo.LOAIPHONG LP2 ON P2.MaLoaiPhong = LP2.MaLoaiPhong
                         WHERE DP0.MaDatPhong = PT.MaDatPhong
                         ORDER BY P2.MaPhong)"
                    : tenLoaiPhongExpr;
                string tienPhongNhomDatCuExpr = TienPhongSql("PT.NgayNhan", "PT.NgayTraDuKien", ngayTraThucTe, tongGiaNgayNhomDatCuExpr, tongGiaGioNhomDatCuExpr);
                string tienPhongDoanThueExpr = TienPhongSql("PT.NgayNhan", "PT.NgayTraDuKien", ngayTraThucTe, tongGiaNgayThueExpr, tongGiaGioThueExpr);
                string tienPhongThueExpr = thueCoChiTietDoan || thueCoNhomDatCu
                    ? @"CASE WHEN PT.MaDatPhong IS NOT NULL AND " + demNhomDatCuExpr + @" > 1 THEN
                            " + tienPhongNhomDatCuExpr + @"
                           WHEN PT.MaDatPhong IS NOT NULL AND " + tonTaiChiTietThueExpr + @" THEN
                            " + tienPhongDoanThueExpr + @"
                           ELSE " + tienPhongExpr + " END"
                    : tienPhongExpr;
                string thueDaHuyExpr = "(PT.TrangThai IN (N'" + "\u0110\u00e3 h\u1ee7y" + @"', N'Da huy', N'" + "H\u1ee7y" + @"', N'Huy', N'No-Show', N'No Show', N'Khach khong den'))";
                string hoaDonKey = TableExists("HOADON") ? ViewSchemaHelper.GetFirstExistingColumn("HOADON", "MaHoaDon", "MaHD", "IDHoaDon", "HoaDonID", "IdHoaDon", "ID", "Ma") : string.Empty;
                string orderHoaDon = string.IsNullOrWhiteSpace(hoaDonKey) ? string.Empty : " ORDER BY HD." + hoaDonKey + " DESC";
                string tienCocGiuThueExpr = TableExists("HOADON") && ColumnExists("HOADON", "MaThue") && ColumnExists("HOADON", "TongTienPhong")
                    ? "ISNULL((SELECT TOP 1 ISNULL(HD.TongTienPhong, 0) FROM dbo.HOADON HD WHERE HD.MaThue = PT.MaThue" + orderHoaDon + "), 0)"
                    : "CAST(0 AS decimal(18,2))";
                string tongTamTinhThueExpr = "CASE WHEN " + thueDaHuyExpr + " THEN " + tienCocGiuThueExpr + " ELSE " + tienPhongThueExpr + " END";
                string tenPhongThueExpr = thueCoChiTietDoan || thueCoNhomDatCu
                    ? @"CASE WHEN PT.MaDatPhong IS NOT NULL AND " + demNhomDatCuExpr + @" > 1 THEN
                            " + tenPhongNhomDatCuExpr + @"
                           WHEN PT.MaDatPhong IS NOT NULL AND " + tonTaiChiTietThueExpr + @" THEN
                            (SELECT STRING_AGG(CAST(" + tenPhongP2Expr + @" AS nvarchar(max)), N', ')
                             FROM dbo.CHITIETDATPHONG CT2
                             JOIN dbo.PHONG P2 ON CT2.MaPhong = P2.MaPhong
                             WHERE CT2.MaDatPhong = PT.MaDatPhong)
                           ELSE " + tenPhongExpr + " END"
                    : tenPhongExpr;
                string soLuongPhongThueExpr = thueCoChiTietDoan || thueCoNhomDatCu
                    ? @"CASE WHEN PT.MaDatPhong IS NOT NULL AND " + demNhomDatCuExpr + @" > 1 THEN
                            " + demNhomDatCuExpr + @"
                           WHEN PT.MaDatPhong IS NOT NULL AND " + tonTaiChiTietThueExpr + @" THEN
                            (SELECT COUNT(*) FROM dbo.CHITIETDATPHONG CT2 WHERE CT2.MaDatPhong = PT.MaDatPhong)
                           ELSE 1 END"
                    : "CAST(1 AS int)";
                string loaiPhongThueExpr = thueCoNhomDatCu
                    ? @"CASE WHEN PT.MaDatPhong IS NOT NULL AND " + demNhomDatCuExpr + @" > 1 THEN
                            " + loaiPhongNhomDatCuExpr + @"
                           ELSE " + tenLoaiPhongExpr + " END"
                    : tenLoaiPhongExpr;
                bool coLichSuDoiPhong = TableExists("DOIPHONG") &&
                                         ColumnExists("DOIPHONG", "MaThue") &&
                                         ColumnExists("DOIPHONG", "MaPhongCu");
                string tenPhongDoiExpr = coLichSuDoiPhong
                    ? @"ISNULL((SELECT N', ' + STRING_AGG(CAST(X.SoPhong AS nvarchar(max)), N', ')
                                FROM (
                                    SELECT DISTINCT " + TenPhongSql("PD") + @" AS SoPhong
                                    FROM dbo.DOIPHONG DPX
                                    JOIN dbo.PHONG PD ON DPX.MaPhongCu = PD.MaPhong
                                    WHERE DPX.MaThue = PT.MaThue
                                ) X), N'')"
                    : "CAST(N'' AS nvarchar(max))";
                string loaiPhongDoiExpr = coLichSuDoiPhong
                    ? @"ISNULL((SELECT N', ' + STRING_AGG(CAST(X.LoaiPhong AS nvarchar(max)), N', ')
                                FROM (
                                    SELECT DISTINCT " + tenLoaiPhongExpr.Replace("LP.", "LPD.").Replace("P.", "PD.") + @" AS LoaiPhong
                                    FROM dbo.DOIPHONG DPX
                                    JOIN dbo.PHONG PD ON DPX.MaPhongCu = PD.MaPhong
                                    LEFT JOIN dbo.LOAIPHONG LPD ON PD.MaLoaiPhong = LPD.MaLoaiPhong
                                    WHERE DPX.MaThue = PT.MaThue
                                ) X), N'')"
                    : "CAST(N'' AS nvarchar(max))";
                string soPhongDoiExpr = coLichSuDoiPhong
                    ? @"(SELECT COUNT(DISTINCT DPX.MaPhongCu)
                         FROM dbo.DOIPHONG DPX
                         WHERE DPX.MaThue = PT.MaThue)"
                    : "0";
                tenPhongThueExpr = "(" + tenPhongThueExpr + " + " + tenPhongDoiExpr + ")";
                loaiPhongThueExpr = "(" + loaiPhongThueExpr + " + " + loaiPhongDoiExpr + ")";
                soLuongPhongThueExpr = "(" + soLuongPhongThueExpr + " + " + soPhongDoiExpr + ")";
                string maPhieuThueExpr = thueCoChiTietDoan || thueCoNhomDatCu
                    ? @"CASE WHEN (" + soLuongPhongThueExpr + @") > 1 THEN
                            N'PTTD' + RIGHT('000000' + CAST(PT.MaThue AS nvarchar(20)), 6)
                           ELSE N'PT' + RIGHT('000000' + CAST(PT.MaThue AS nvarchar(20)), 6) END"
                    : "N'PT' + RIGHT('000000' + CAST(PT.MaThue AS nvarchar(20)), 6)";
                queries.Add(@"
SELECT 'THUE' AS LoaiPhieu,
       PT.MaThue AS MaGoc,
       " + maDoanThueExpr + @" AS MaDoan,
       " + maPhieuThueExpr + @" AS MaPhieuThue,
       KH.HoTen AS TenKhachHang,
       KH.SDT AS SoDienThoai,
       " + tenPhongThueExpr + @" AS TenPhong,
       " + soLuongPhongThueExpr + @" AS SoLuongPhong,
       " + loaiPhongThueExpr + @" AS LoaiPhong,
       PT.NgayNhan AS NgayNhanPhong,
       PT.NgayTraDuKien AS NgayTraPhong,
       ISNULL(PT.TienCoc, 0) AS DatCoc,
       " + tongTamTinhThueExpr + @" AS TongTamTinh,
       " + ghiChuThueExpr + @" AS GhiChu,
       CASE
           WHEN " + thueDaHuyExpr + @" THEN N'" + "\u0110\u00e3 h\u1ee7y" + @"'
           WHEN PT.TrangThai IN (N'" + "\u0110\u00e3 tr\u1ea3" + @"', N'Da tra', N'" + "\u0110\u00e3 tr\u1ea3 ph\u00f2ng" + @"', N'Da tra phong') THEN N'" + "\u0110\u00e3 tr\u1ea3 ph\u00f2ng" + @"'
           WHEN PT.TrangThai IN (N'" + "\u0110ang thu\u00ea" + @"', N'Dang thue') OR P.TrangThai IN (N'" + "C\u00f3 kh\u00e1ch" + @"', N'Co khach', N'" + "\u0110ang thu\u00ea" + @"', N'Dang thue') THEN N'" + "\u0110ang thu\u00ea" + @"'
           WHEN PT.TrangThai IN (N'" + "\u0110\u00e3 \u0111\u1eb7t" + @"', N'Da dat', N'" + "\u0110\u00e3 x\u00e1c nh\u1eadn" + @"', N'Da xac nhan') THEN N'" + "\u0110\u00e3 \u0111\u1eb7t" + @"'
           ELSE ISNULL(PT.TrangThai, N'" + "\u0110ang thu\u00ea" + @"')
       END AS TrangThai
FROM dbo.PHIEUTHUE PT
JOIN dbo.KHACHHANG KH ON PT.MaKH = KH.MaKH
JOIN dbo.PHONG P ON PT.MaPhong = P.MaPhong
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong");
            }

            if (coBangDatPhong)
            {
                string ngayNhan = ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "DP.NgayNhanDuKien" : "DP.NgayNhanPhong";
                string ngayTra = ColumnExists(bangDatPhong, "NgayTraDuKien") ? "DP.NgayTraDuKien" : "DP.NgayTraPhong";
                string tienCoc = ColumnExists(bangDatPhong, "TienCoc") ? "DP.TienCoc" : "DP.DatCoc";
                string ghiChuDatExpr = ColumnExists(bangDatPhong, "GhiChu") ? "DP.GhiChu" : "CAST(NULL AS nvarchar(255))";
                string maDoanDatExpr = ColumnExists(bangDatPhong, "MaDoan") ? "ISNULL(DP.MaDoan, 0)" : "CAST(0 AS int)";
                bool coMaPhongDat = ColumnExists(bangDatPhong, "MaPhong");
                string tonTaiChiTietDatExpr = "EXISTS (SELECT 1 FROM dbo.CHITIETDATPHONG CT2 WHERE CT2.MaDatPhong = DP.MaDatPhong)";
                string tongGiaNgayDatExpr = @"(SELECT ISNULL(SUM(ISNULL(NULLIF(LP2.DonGiaNgay, 0), ISNULL(NULLIF(LP2.DonGiaDem, 0), ISNULL(LP2.DonGiaGio, 0) * 24.0))), 0)
                                               FROM dbo.CHITIETDATPHONG CT2
                                               JOIN dbo.PHONG P2 ON CT2.MaPhong = P2.MaPhong
                                               LEFT JOIN dbo.LOAIPHONG LP2 ON P2.MaLoaiPhong = LP2.MaLoaiPhong
                                               WHERE CT2.MaDatPhong = DP.MaDatPhong)";
                string tongGiaGioDatExpr = @"(SELECT ISNULL(SUM(ISNULL(LP2.DonGiaGio, 0)), 0)
                                              FROM dbo.CHITIETDATPHONG CT2
                                              JOIN dbo.PHONG P2 ON CT2.MaPhong = P2.MaPhong
                                              LEFT JOIN dbo.LOAIPHONG LP2 ON P2.MaLoaiPhong = LP2.MaLoaiPhong
                                              WHERE CT2.MaDatPhong = DP.MaDatPhong)";
                string tienPhongDonDatExpr = coMaPhongDat ? TienPhongSql(ngayNhan, ngayTra, ngayTra) : "CAST(0 AS decimal(18,2))";
                string tienPhongDoanDatExpr = TienPhongSql(ngayNhan, ngayTra, ngayTra, tongGiaNgayDatExpr, tongGiaGioDatExpr);
                string tienPhongDat = coChiTietDatPhong
                    ? "CASE WHEN " + tonTaiChiTietDatExpr + " THEN " + tienPhongDoanDatExpr + " ELSE " + tienPhongDonDatExpr + " END"
                    : tienPhongDonDatExpr;
                string datDaHuyExpr = "(DP.TrangThai IN (N'" + "\u0110\u00e3 h\u1ee7y" + @"', N'Da huy', N'" + "H\u1ee7y" + @"', N'Huy', N'No-Show', N'No Show', N'Khach khong den'))";
                string hoaDonKeyDat = TableExists("HOADON") ? ViewSchemaHelper.GetFirstExistingColumn("HOADON", "MaHoaDon", "MaHD", "IDHoaDon", "HoaDonID", "IdHoaDon", "ID", "Ma") : string.Empty;
                string orderHoaDonDat = string.IsNullOrWhiteSpace(hoaDonKeyDat) ? string.Empty : " ORDER BY HD." + hoaDonKeyDat + " DESC";
                string tienCocGiuDatExpr = TableExists("HOADON") && ColumnExists("HOADON", "MaDatPhong") && ColumnExists("HOADON", "TongTienPhong")
                    ? "ISNULL((SELECT TOP 1 ISNULL(HD.TongTienPhong, 0) FROM dbo.HOADON HD WHERE HD.MaDatPhong = DP.MaDatPhong" + orderHoaDonDat + "), 0)"
                    : "CAST(0 AS decimal(18,2))";
                string tongTamTinhDatExpr = "CASE WHEN " + datDaHuyExpr + " THEN " + tienCocGiuDatExpr + " ELSE " + tienPhongDat + " END";
                string tenPhongDatExpr = coChiTietDatPhong
                    ? @"CASE WHEN " + tonTaiChiTietDatExpr + @" THEN
                        (SELECT STRING_AGG(CAST(" + tenPhongP2Expr + @" AS nvarchar(max)), N', ')
                         FROM dbo.CHITIETDATPHONG CT2
                         JOIN dbo.PHONG P2 ON CT2.MaPhong = P2.MaPhong
                         WHERE CT2.MaDatPhong = DP.MaDatPhong)
                       ELSE " + (coMaPhongDat ? tenPhongExpr : "CAST(N'' AS nvarchar(max))") + @" END"
                    : tenPhongExpr;
                string soLuongPhongExpr = coChiTietDatPhong
                    ? "CASE WHEN " + tonTaiChiTietDatExpr + " THEN (SELECT COUNT(*) FROM dbo.CHITIETDATPHONG CT2 WHERE CT2.MaDatPhong = DP.MaDatPhong) ELSE 1 END"
                    : "CAST(1 AS int)";
                string loaiPhongDatExpr = coChiTietDatPhong
                    ? @"CASE WHEN " + tonTaiChiTietDatExpr + @" THEN
                        (SELECT TOP 1 " + tenLoaiPhongExpr.Replace("LP.", "LP2.").Replace("P.", "P2.") + @"
                         FROM dbo.CHITIETDATPHONG CT2
                         JOIN dbo.PHONG P2 ON CT2.MaPhong = P2.MaPhong
                         LEFT JOIN dbo.LOAIPHONG LP2 ON P2.MaLoaiPhong = LP2.MaLoaiPhong
                         WHERE CT2.MaDatPhong = DP.MaDatPhong
                         ORDER BY P2.MaPhong)"
                       + (coMaPhongDat ? " ELSE " + tenLoaiPhongExpr : " ELSE CAST(N'' AS nvarchar(50))") + " END"
                    : tenLoaiPhongExpr;
                string phongDangThueExpr = coChiTietDatPhong
                    ? @"EXISTS (SELECT 1
                                FROM dbo.CHITIETDATPHONG CT2
                                JOIN dbo.PHONG P2 ON CT2.MaPhong = P2.MaPhong
                                WHERE CT2.MaDatPhong = DP.MaDatPhong
                                  AND P2.TrangThai IN (N'" + "C\u00f3 kh\u00e1ch" + @"', N'Co khach', N'" + "\u0110ang thu\u00ea" + @"', N'Dang thue'))"
                    : "P.TrangThai IN (N'C\u00f3 kh\u00e1ch', N'Co khach', N'\u0110ang thu\u00ea', N'Dang thue')";
                string joinPhong = coMaPhongDat
                    ? "LEFT JOIN dbo.PHONG P ON DP.MaPhong = P.MaPhong"
                    : string.Empty;
                string existsThue = coPhieuThue && ColumnExists("PHIEUTHUE", "MaDatPhong")
                    ? @"WHERE NOT EXISTS (
                            SELECT 1
                            FROM dbo.PHIEUTHUE PTX
                            JOIN dbo." + bangDatPhong + @" DPX ON PTX.MaDatPhong = DPX.MaDatPhong
                            WHERE DPX.MaKH = DP.MaKH
                              AND CONVERT(date, " + ngayNhan.Replace("DP.", "DPX.") + @") = CONVERT(date, " + ngayNhan + @")
                              AND CONVERT(date, " + ngayTra.Replace("DP.", "DPX.") + @") = CONVERT(date, " + ngayTra + @"))"
                    : string.Empty;

                queries.Add(@"
SELECT 'DAT' AS LoaiPhieu,
       DP.MaDatPhong AS MaGoc,
       " + maDoanDatExpr + @" AS MaDoan,
       N'DP' + RIGHT('000000' + CAST(DP.MaDatPhong AS nvarchar(20)), 6) AS MaPhieuThue,
       KH.HoTen AS TenKhachHang,
       KH.SDT AS SoDienThoai,
       " + tenPhongDatExpr + @" AS TenPhong,
       " + soLuongPhongExpr + @" AS SoLuongPhong,
       " + loaiPhongDatExpr + @" AS LoaiPhong,
       " + ngayNhan + @" AS NgayNhanPhong,
       " + ngayTra + @" AS NgayTraPhong,
       ISNULL(" + tienCoc + @", 0) AS DatCoc,
       " + tongTamTinhDatExpr + @" AS TongTamTinh,
       " + ghiChuDatExpr + @" AS GhiChu,
       CASE
           WHEN " + datDaHuyExpr + @" THEN N'" + "\u0110\u00e3 h\u1ee7y" + @"'
           WHEN DP.TrangThai IN (N'" + "\u0110\u00e3 tr\u1ea3" + @"', N'Da tra', N'" + "\u0110\u00e3 tr\u1ea3 ph\u00f2ng" + @"', N'Da tra phong') THEN N'" + "\u0110\u00e3 tr\u1ea3 ph\u00f2ng" + @"'
           WHEN " + phongDangThueExpr + @" THEN N'" + "\u0110ang thu\u00ea" + @"'
           ELSE N'" + "\u0110\u00e3 \u0111\u1eb7t" + @"'
       END AS TrangThai
FROM dbo." + bangDatPhong + @" DP
JOIN dbo.KHACHHANG KH ON DP.MaKH = KH.MaKH
" + joinPhong + @"
" + (coMaPhongDat ? "LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong" : string.Empty) + @"
" + existsThue);
            }

            if (queries.Count == 0)
            {
                return new DataTable();
            }

            string sql = "SELECT * FROM (" + string.Join("\nUNION ALL\n", queries) + @") X
WHERE (@TrangThai = N'' OR X.TrangThai = @TrangThai)
  AND (@LoaiPhong = N'' OR X.LoaiPhong = @LoaiPhong)
  AND (@TuKhoa = N'' OR X.MaPhieuThue LIKE N'%' + @TuKhoa + N'%'
       OR X.TenKhachHang LIKE N'%' + @TuKhoa + N'%'
       OR X.SoDienThoai LIKE N'%' + @TuKhoa + N'%'
       OR X.TenPhong LIKE N'%' + @TuKhoa + N'%')
  AND (@TuNgay IS NULL OR CONVERT(date, X.NgayNhanPhong) >= @TuNgay)
  AND (@DenNgay IS NULL OR CONVERT(date, X.NgayNhanPhong) <= @DenNgay)
ORDER BY X.NgayNhanPhong DESC";

            DataTable data = ConnectDB.GetData(sql,
                new SqlParameter("@TrangThai", status),
                new SqlParameter("@LoaiPhong", loaiPhong),
                new SqlParameter("@TuKhoa", keyword),
                new SqlParameter("@TuNgay", DpTuNgay.SelectedDate.HasValue ? DpTuNgay.SelectedDate.Value.Date : DBNull.Value),
                new SqlParameter("@DenNgay", DpDenNgay.SelectedDate.HasValue ? DpDenNgay.SelectedDate.Value.Date : DBNull.Value));

            return GopPhieuDoanCu(data);
        }

        private static DataTable GopPhieuDoanCu(DataTable data)
        {
            if (data.Rows.Count == 0 || !data.Columns.Contains("LoaiPhieu"))
            {
                return data;
            }

            DataTable result = data.Clone();
            var groups = data.AsEnumerable()
                .GroupBy(TaoKhoaGopPhieu)
                .OrderByDescending(group => LayNgay(group.First(), "NgayNhanPhong"))
                .ThenBy(group => LayInt(group.First(), "MaGoc"));

            foreach (var group in groups)
            {
                List<DataRow> rows = group.OrderBy(row => LayInt(row, "MaGoc")).ToList();
                if (rows.Count == 1)
                {
                    result.ImportRow(rows[0]);
                    continue;
                }

                DataRow first = rows[0];
                DataRow last = rows[^1];
                DataRow newRow = result.NewRow();
                foreach (DataColumn column in data.Columns)
                {
                    newRow[column.ColumnName] = first[column];
                }

                DatGiaTriNeuCo(result, newRow, "MaGoc", LayInt(first, "MaGoc"));
                string loaiPhieu = LayString(first, "LoaiPhieu");
                int maDoan = LayInt(first, "MaDoan");
                DatGiaTriNeuCo(result, newRow, "MaPhieuThue", loaiPhieu == "THUE"
                    ? maDoan > 0 ? "PTD" + maDoan.ToString("000000") : TaoMaPhieuThueDoan(LayInt(first, "MaGoc"))
                    : TaoMaGop(LayString(first, "MaPhieuThue"), LayString(last, "MaPhieuThue")));
                DatGiaTriNeuCo(result, newRow, "TenPhong", NoiGiaTriKhacNhau(rows.Select(row => LayString(row, "TenPhong"))));
                DatGiaTriNeuCo(result, newRow, "LoaiPhong", NoiGiaTriKhacNhau(rows.Select(row => LayString(row, "LoaiPhong"))));
                DatGiaTriNeuCo(result, newRow, "SoLuongPhong", rows.Sum(row => LayInt(row, "SoLuongPhong")));
                DatGiaTriNeuCo(result, newRow, "DatCoc", rows.Sum(row => LayDecimal(row, "DatCoc")));
                DatGiaTriNeuCo(result, newRow, "TongTamTinh", rows.Sum(row => LayDecimal(row, "TongTamTinh")));
                DatGiaTriNeuCo(result, newRow, "GhiChu", NoiGiaTriKhacNhau(rows.Select(row => LayString(row, "GhiChu"))));

                result.Rows.Add(newRow);
            }

            return result;
        }

        private static string TaoKhoaGopPhieu(DataRow row)
        {
            string loaiPhieu = LayString(row, "LoaiPhieu");
            if (loaiPhieu != "DAT" && loaiPhieu != "THUE")
            {
                return loaiPhieu + "|" + LayInt(row, "MaGoc");
            }

            int maDoan = LayInt(row, "MaDoan");
            if (maDoan > 0)
            {
                return loaiPhieu + "|DOAN|" + maDoan + "|" + LayString(row, "TrangThai").Trim().ToUpperInvariant();
            }

            return string.Join("|",
                loaiPhieu,
                LayString(row, "TenKhachHang").Trim().ToUpperInvariant(),
                LayString(row, "SoDienThoai").Trim(),
                LayNgay(row, "NgayNhanPhong").ToString("yyyyMMdd"),
                LayNgay(row, "NgayTraPhong").ToString("yyyyMMdd"),
                LayString(row, "TrangThai").Trim().ToUpperInvariant());
        }

        private static void DatGiaTriNeuCo(DataTable table, DataRow row, string column, object value)
        {
            if (table.Columns.Contains(column))
            {
                row[column] = value;
            }
        }

        private static string NoiGiaTriKhacNhau(IEnumerable<string> values)
        {
            return string.Join(", ", values
                .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static string TaoMaGop(string first, string last)
        {
            return string.Equals(first, last, StringComparison.OrdinalIgnoreCase) ? first : first + "-" + last;
        }

        private static string TaoMaPhieuThueDoan(int maGoc)
        {
            return "PTTD" + maGoc.ToString("000000");
        }

        private static string LayString(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? row[column]?.ToString() ?? string.Empty : string.Empty;
        }

        private static int LayInt(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && int.TryParse(row[column]?.ToString(), out int value) ? value : 0;
        }

        private static decimal LayDecimal(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && decimal.TryParse(row[column]?.ToString(), out decimal value) ? value : 0;
        }

        private static DateTime LayNgay(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && DateTime.TryParse(row[column]?.ToString(), out DateTime value) ? value : DateTime.MinValue;
        }

        private void ThemCotStt(DataTable table)
        {
            if (!table.Columns.Contains("STT"))
            {
                table.Columns.Add("STT", typeof(int));
                table.Columns["STT"]!.SetOrdinal(0);
            }

            for (int i = 0; i < table.Rows.Count; i++)
            {
                table.Rows[i]["STT"] = i + 1;
            }
        }

        private string LayTrangThaiLoc()
        {
            return CboTrangThai.SelectedItem is ComboBoxItem item && item.Content?.ToString() != "T\u1ea5t c\u1ea3"
                ? item.Content?.ToString() ?? string.Empty
                : string.Empty;
        }

        private void BtnTimKiem_Click(object sender, RoutedEventArgs e)
        {
            TaiDuLieu();
        }

        private void BtnLamMoi_Click(object sender, RoutedEventArgs e)
        {
            TxtTimKiem.Clear();
            CboTrangThai.SelectedIndex = 0;
            CboLoaiPhong.SelectedIndex = 0;
            DpTuNgay.SelectedDate = null;
            DpDenNgay.SelectedDate = null;
            TaiDuLieu();
        }

        private void BtnXuatExcel_Click(object sender, RoutedEventArgs e)
        {
            List<DataRowView> rows = LayDongDangHienThi();
            if (rows.Count == 0)
            {
                MessageBox.Show("Kh\u00f4ng c\u00f3 d\u1eef li\u1ec7u \u0111\u1ec3 xu\u1ea5t Excel.", "Xu\u1ea5t Excel", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new()
            {
                Title = "Xu\u1ea5t danh s\u00e1ch phi\u1ebfu thu\u00ea",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "DanhSachPhieuThue_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx"
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            {
                return;
            }

            try
            {
                ExcelSection section = new()
                {
                    Title = "Chi tiết phiếu thuê",
                    Headers = ["STT", "Mã phiếu", "Khách / đoàn đặt", "Số điện thoại", "Danh sách phòng", "Loại phòng", "Ngày nhận", "Ngày trả", "Đặt cọc", "Tổng tạm tính", "Trạng thái"],
                    ColumnWidths = [8, 15, 28, 16, 24, 18, 19, 19, 16, 18, 18],
                    Summary = $"Tổng số phiếu: {rows.Count:N0}"
                };
                section.Rows.AddRange(rows.Select(row => (IReadOnlyList<object?>)
                [
                    LayString(row.Row, "STT"),
                    LayString(row.Row, "MaPhieuThue"),
                    LayString(row.Row, "TenKhachHang"),
                    LayString(row.Row, "SoDienThoai"),
                    LayString(row.Row, "TenPhong"),
                    LayString(row.Row, "LoaiPhong"),
                    LayNgay(row.Row, "NgayNhanPhong") == DateTime.MinValue ? string.Empty : LayNgay(row.Row, "NgayNhanPhong"),
                    LayNgay(row.Row, "NgayTraPhong") == DateTime.MinValue ? string.Empty : LayNgay(row.Row, "NgayTraPhong"),
                    new ExcelMoney(LayDecimal(row.Row, "DatCoc")),
                    new ExcelMoney(LayDecimal(row.Row, "TongTamTinh")),
                    LayString(row.Row, "TrangThai")
                ]));

                ExcelDocument document = new()
                {
                    Title = "DANH SÁCH PHIẾU THUÊ",
                    Subtitle = "Danh sách theo bộ lọc hiện tại",
                    SheetName = "Phiếu thuê"
                };
                document.Sections.Add(section);
                ExcelExportService.Export(dialog.FileName, document);
                MessageBox.Show("Xu\u1ea5t Excel th\u00e0nh c\u00f4ng.", "Xu\u1ea5t Excel", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kh\u00f4ng xu\u1ea5t \u0111\u01b0\u1ee3c Excel: " + ex.Message, "L\u1ed7i", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnInDanhSach_Click(object sender, RoutedEventArgs e)
        {
            List<DataRowView> rows = LayDongDangHienThi();
            if (rows.Count == 0)
            {
                MessageBox.Show("Kh\u00f4ng c\u00f3 d\u1eef li\u1ec7u \u0111\u1ec3 in.", "In danh s\u00e1ch", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                FlowDocument document = TaoTaiLieuIn(rows);
                PrintExportService.Print(document, "Danh sách phiếu thuê");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kh\u00f4ng in \u0111\u01b0\u1ee3c danh s\u00e1ch: " + ex.Message, "L\u1ed7i", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<DataRowView> LayDongDangHienThi()
        {
            return DgPhieuThue.Items
                .OfType<DataRowView>()
                .ToList();
        }

        private static IEnumerable<string> TaoGiaTriXuat(DataRowView row)
        {
            yield return LayString(row.Row, "STT");
            yield return LayString(row.Row, "MaPhieuThue");
            yield return LayString(row.Row, "TenKhachHang");
            yield return LayString(row.Row, "SoDienThoai");
            yield return LayString(row.Row, "TenPhong");
            yield return LayString(row.Row, "LoaiPhong");
            yield return LayString(row.Row, "TrangThai");
        }

        private FlowDocument TaoTaiLieuIn(IEnumerable<DataRowView> rows)
        {
            FlowDocument document = new()
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Foreground = Brushes.Black
            };

            document.Blocks.Add(new Paragraph(new Run("DANH S\u00c1CH PHI\u1ebeU THU\u00ca"))
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(0, 0, 0, 8)
            });
            document.Blocks.Add(new Paragraph(new Run("Ng\u00e0y in: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm")))
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 0, 0, 14)
            });

            Table table = new()
            {
                CellSpacing = 0,
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(0.5)
            };

            double[] widths = { 35, 80, 155, 105, 130, 110, 105 };
            foreach (double width in widths)
            {
                table.Columns.Add(new TableColumn { Width = new GridLength(width) });
            }

            TableRowGroup group = new();
            table.RowGroups.Add(group);
            group.Rows.Add(TaoDongIn(true, "STT", "M\u00e3 phi\u1ebfu", "Kh\u00e1ch/\u0111o\u00e0n \u0111\u1eb7t", "S\u0110T", "Danh s\u00e1ch ph\u00f2ng", "Lo\u1ea1i ph\u00f2ng", "Tr\u1ea1ng th\u00e1i"));

            foreach (DataRowView row in rows)
            {
                string[] values = TaoGiaTriXuat(row).ToArray();
                group.Rows.Add(TaoDongIn(false, values));
            }

            document.Blocks.Add(table);
            return document;
        }

        private static TableRow TaoDongIn(bool header, params string[] values)
        {
            TableRow row = new();
            foreach (string value in values)
            {
                row.Cells.Add(TaoOIn(value, header));
            }

            return row;
        }

        private static TableCell TaoOIn(string text, bool header)
        {
            return new TableCell(new Paragraph(new Run(text ?? string.Empty))
            {
                Margin = new Thickness(0),
                TextAlignment = TextAlignment.Left
            })
            {
                Padding = new Thickness(6, 5, 6, 5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(0.5),
                Background = header ? new SolidColorBrush(Color.FromRgb(230, 238, 246)) : Brushes.White,
                FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal
            };
        }

        private void TxtTimKiem_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        private IEnumerable<SearchSuggestionItem> TaoGoiYTimKiem()
        {
            if (danhSachPhieu.Rows.Count == 0)
            {
                yield break;
            }

            foreach (DataRow row in danhSachPhieu.Rows)
            {
                string maPhieu = LayString(row, "MaPhieuThue");
                string tenKhach = LayString(row, "TenKhachHang");
                string soDienThoai = LayString(row, "SoDienThoai");
                string tenPhong = LayString(row, "TenPhong");

                if (!string.IsNullOrWhiteSpace(maPhieu))
                {
                    yield return new SearchSuggestionItem(maPhieu, $"{maPhieu} - {tenKhach} - {tenPhong}");
                }

                if (!string.IsNullOrWhiteSpace(tenKhach))
                {
                    yield return new SearchSuggestionItem(tenKhach, $"{tenKhach} - {soDienThoai}");
                }

                if (!string.IsNullOrWhiteSpace(tenPhong))
                {
                    yield return new SearchSuggestionItem(tenPhong, $"Ph\u00f2ng {tenPhong} - {tenKhach}");
                }
            }
        }

        private void BoLoc_Changed(object sender, EventArgs e)
        {
            if (!dangNapBoLoc && IsLoaded)
            {
                TaiDuLieu();
            }
        }

        private void BtnDatPhongMoi_Click(object sender, RoutedEventArgs e)
        {
            PhongDTO? phongTrong = null;
            try
            {
                phongTrong = phongBUS.LayDanhSach().FirstOrDefault(item =>
                    !item.TrangThai.Contains("thu\u00ea", StringComparison.OrdinalIgnoreCase) &&
                    !item.TrangThai.Contains("thue", StringComparison.OrdinalIgnoreCase) &&
                    !item.TrangThai.Contains("\u0111\u1eb7t", StringComparison.OrdinalIgnoreCase) &&
                    !item.TrangThai.Contains("dat", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kh\u00f4ng t\u1ea3i \u0111\u01b0\u1ee3c danh s\u00e1ch ph\u00f2ng: " + ex.Message, "L\u1ed7i", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (phongTrong == null)
            {
                MessageBox.Show("Kh\u00f4ng c\u00f3 ph\u00f2ng tr\u1ed1ng \u0111\u1ec3 \u0111\u1eb7t.", "Th\u00f4ng b\u00e1o", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UCDatPhongMoi ucDatPhong = new(phongTrong);
            ucDatPhong.CloseRequested += UcDatPhong_CloseRequested;
            ucDatPhong.DatPhongRequested += UcDatPhong_DatPhongRequested;
            ucDatPhong.DatPhongTheoDoanRequested += UcDatPhong_DatPhongTheoDoanRequested;

            Window dialog = DialogService.CreateContentDialog(ucDatPhong, "\u0110\u1eb7t ph\u00f2ng m\u1edbi", 1100, 650);
            DialogService.ShowDimmedDialogResult(dialog, Window.GetWindow(this));

            ucDatPhong.CloseRequested -= UcDatPhong_CloseRequested;
            ucDatPhong.DatPhongRequested -= UcDatPhong_DatPhongRequested;
            ucDatPhong.DatPhongTheoDoanRequested -= UcDatPhong_DatPhongTheoDoanRequested;
            TaiDuLieu();
        }

        private void UcDatPhong_CloseRequested(object? sender, EventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                Window.GetWindow(element)?.Close();
            }
        }

        private void UcDatPhong_DatPhongRequested(object? sender, PhongDTO phong)
        {
            try
            {
                HoaDonItem? billSauThanhToan = null;
                if (sender is UCDatPhongMoi ucDatPhong)
                {
                    DatPhongRequestDTO request = ucDatPhong.TaoYeuCauDatPhong();
                    if (ucDatPhong.NhanNgay)
                    {
                        decimal giamGia = request.KhachHang.LoaiKhach.Contains("VIP", StringComparison.OrdinalIgnoreCase) ? Math.Round(request.TienPhong * 0.1m, 0) : 0;
                        if (!DialogService.XacNhanThanhToanCheckIn(Window.GetWindow(this), "Ph\u00f2ng " + request.Phong.MaHienThi, request.TienPhong, request.TienDichVu, giamGia: giamGia))
                        {
                            return;
                        }
                        KetQuaCheckInThanhToanDTO result = phongBUS.NhanPhong(request);
                        billSauThanhToan = HoaDonItem.TaoCheckInTam(
                            request,
                            result.MaHoaDon > 0 ? "HD-" + result.MaHoaDon.ToString("0000") : "HD-TAM",
                            result.MaThue.GetValueOrDefault());
                    }
                    else
                    {
                        phongBUS.DatPhong(request);
                    }
                }
                else
                {
                    phongBUS.DatPhong(phong);
                }

                if (billSauThanhToan != null)
                {
                    HoaDonPrintWindow window = new(billSauThanhToan);
                    DialogService.ShowDimmedDialogResult(window, Window.GetWindow(this));
                }
                if (sender is FrameworkElement element)
                {
                    Window.GetWindow(element)?.Close();
                }
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kh\u00f4ng \u0111\u1eb7t \u0111\u01b0\u1ee3c ph\u00f2ng: " + ex.Message, "L\u1ed7i", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UcDatPhong_DatPhongTheoDoanRequested(object? sender, EventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                Window.GetWindow(element)?.Close();
            }

            List<PhongDTO> phongTrong;
            try
            {
                phongTrong = phongBUS.LayDanhSach().Where(LaPhongTrongSanSang).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Khong tai duoc danh sach phong: " + ex.Message, "Loi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (phongTrong.Count == 0)
            {
                MessageBox.Show("Khong co phong trong de dat theo doan.", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UCDatPhongTheoDoan ucDatPhong = new(phongTrong);
            ucDatPhong.CloseRequested += UcDatPhongTheoDoan_CloseRequested;
            ucDatPhong.DatPhongDoanRequested += UcDatPhongTheoDoan_DatPhongDoanRequested;

            Window dialog = DialogService.CreateContentDialog(ucDatPhong, "Dat phong cho doan", 1450, 800);
            DialogService.ShowDimmedDialogResult(dialog, Window.GetWindow(this));

            ucDatPhong.CloseRequested -= UcDatPhongTheoDoan_CloseRequested;
            ucDatPhong.DatPhongDoanRequested -= UcDatPhongTheoDoan_DatPhongDoanRequested;
            TaiDuLieu();
        }

        private void UcDatPhongTheoDoan_CloseRequested(object? sender, EventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                Window.GetWindow(element)?.Close();
            }
        }

        private void UcDatPhongTheoDoan_DatPhongDoanRequested(object? sender, List<DatPhongRequestDTO> requests)
        {
            try
            {
                bool nhanNgay = requests.Any(item => item.NhanNgay);
                if (nhanNgay && !DialogService.XacNhanThanhToanCheckIn(
                        Window.GetWindow(this),
                        "Nhan phong cho doan",
                        requests.Sum(item => item.TienPhong),
                        requests.Sum(item => item.TienDichVu),
                        giamGia: requests.Sum(item => item.KhachHang.LoaiKhach.Contains("VIP", StringComparison.OrdinalIgnoreCase) ? Math.Round(item.TienPhong * 0.1m, 0) : 0)))
                {
                    return;
                }

                phongBUS.LuuDatPhongDoan(requests);
                MessageBox.Show(nhanNgay ? "Nhan phong cho doan thanh cong." : "Dat phong cho doan thanh cong.", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Information);
                if (sender is FrameworkElement element)
                {
                    Window.GetWindow(element)?.Close();
                }

                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Khong dat phong cho doan duoc: " + ex.Message, "Loi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool LaPhongTrongSanSang(PhongDTO phong)
        {
            string trangThai = phong.TrangThai ?? string.Empty;
            return !trangThai.Contains("thu\u00ea", StringComparison.OrdinalIgnoreCase) &&
                   !trangThai.Contains("thue", StringComparison.OrdinalIgnoreCase) &&
                   !trangThai.Contains("\u0111\u1eb7t", StringComparison.OrdinalIgnoreCase) &&
                   !trangThai.Contains("dat", StringComparison.OrdinalIgnoreCase) &&
                   !trangThai.Contains("s\u1eeda", StringComparison.OrdinalIgnoreCase) &&
                   !trangThai.Contains("sua", StringComparison.OrdinalIgnoreCase);
        }

        private void DgPhieuThue_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgPhieuThue.SelectedItem is not DataRowView row)
            {
                return;
            }

            MoChiTietPhieu(row);
        }

        private void BtnMoChiTiet_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is DataRowView row)
            {
                MoChiTietPhieu(row);
            }
        }

        private void BtnThaoTac_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.DataContext = button.DataContext;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void MenuChiTiet_Click(object sender, RoutedEventArgs e)
        {
            if (LayDongTuMenu(sender) is DataRowView row)
            {
                MoChiTietPhieu(row);
            }
        }

        private void MenuSua_Click(object sender, RoutedEventArgs e)
        {
            if (LayDongTuMenu(sender) is DataRowView row)
            {
                MoChiTietPhieu(row);
            }
        }

        private void MenuXoa_Click(object sender, RoutedEventArgs e)
        {
            if (LayDongTuMenu(sender) is not DataRowView row)
            {
                return;
            }

            string maPhieu = row["MaPhieuThue"]?.ToString() ?? string.Empty;
            MessageBox.Show(
                "Ch\u1ee9c n\u0103ng x\u00f3a phi\u1ebfu thu\u00ea c\u1ea7n x\u1eed l\u00fd \u0111\u1ed3ng b\u1ed9 tr\u1ea1ng th\u00e1i ph\u00f2ng, h\u00f3a \u0111\u01a1n v\u00e0 \u0111\u1eb7t c\u1ecdc. Vui l\u00f2ng d\u00f9ng m\u00e0n chi ti\u1ebft \u0111\u1ec3 h\u1ee7y/tr\u1ea3 ph\u00f2ng theo \u0111\u00fang quy tr\u00ecnh.",
                string.IsNullOrWhiteSpace(maPhieu) ? "X\u00f3a phi\u1ebfu thu\u00ea" : "X\u00f3a phi\u1ebfu thu\u00ea " + maPhieu,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private static DataRowView? LayDongTuMenu(object sender)
        {
            if (sender is MenuItem { Parent: ContextMenu menu } && menu.DataContext is DataRowView row)
            {
                return row;
            }

            return null;
        }

        private void MoChiTietPhieu(DataRowView row)
        {
            string loaiPhieu = row["LoaiPhieu"]?.ToString() ?? string.Empty;
            int maGoc = Convert.ToInt32(row["MaGoc"]);
            ChiTietPhieuThueWindow window = new(loaiPhieu, maGoc);
            DialogService.ShowDimmedDialogResult(window, Window.GetWindow(this));
            if (window.DuLieuDaThayDoi)
            {
                TaiDuLieu();
            }
        }

        private static string TienPhongSql(string startExpr, string plannedEndExpr, string actualEndExpr)
        {
            string giaNgayExpr = "ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0))";
            string giaGioExpr = "ISNULL(LP.DonGiaGio, 0)";
            return TienPhongSql(startExpr, plannedEndExpr, actualEndExpr, giaNgayExpr, giaGioExpr);
        }

        private static string TienPhongSql(string startExpr, string plannedEndExpr, string actualEndExpr, string giaNgayExpr, string giaGioExpr)
        {
            return @"CAST(CASE
    WHEN " + plannedEndExpr + @" IS NULL OR DATEDIFF(minute, " + startExpr + @", " + plannedEndExpr + @") <= 0 THEN " + giaNgayExpr + @"
    WHEN CAST(" + startExpr + @" AS date) = CAST(" + plannedEndExpr + @" AS date) THEN CEILING(DATEDIFF(minute, " + startExpr + @", " + plannedEndExpr + @") / 60.0) * " + giaGioExpr + @"
    WHEN DATEDIFF(hour, " + startExpr + @", " + plannedEndExpr + @") <= 12 THEN " + giaNgayExpr + @"
    ELSE CASE WHEN DATEDIFF(day, CAST(" + startExpr + @" AS date), CAST(" + plannedEndExpr + @" AS date)) <= 0 THEN 1
              ELSE DATEDIFF(day, CAST(" + startExpr + @" AS date), CAST(" + plannedEndExpr + @" AS date))
         END * " + giaNgayExpr + @"
END AS decimal(18, 2))";
        }

        private static bool TableExists(string tableName)
        {
            return ViewSchemaHelper.TableExists(tableName);
        }

        private static bool ColumnExists(string tableName, string columnName)
        {
            return ViewSchemaHelper.ColumnExists(tableName, columnName);
        }

        private static string TenPhongSql(string alias)
        {
            return ViewSchemaHelper.TenPhongSql(alias);
        }
    }
}
