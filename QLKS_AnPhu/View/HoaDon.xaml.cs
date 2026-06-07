using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DAL;

namespace QLKS_AnPhu.View
{
    public partial class HoaDon : UserControl
    {
        private readonly ObservableCollection<HoaDonItem> danhSachHoaDon = new();
        private readonly List<HoaDonItem> tatCaHoaDonGiaiDoan = new();
        private readonly ThanhToanFlowBUS thanhToanBUS = new();
        private bool dangNapDuLieu;

        public HoaDon()
        {
            InitializeComponent();
            SearchSuggestionService.Attach(TxtTimKiem, TaoGoiYTimKiem, _ => TaiDuLieu());
            Loaded += HoaDon_Loaded;
        }

        private async void HoaDon_Loaded(object sender, RoutedEventArgs e)
        {
            if (!dangNapDuLieu)
            {
                DpTuNgay.SelectedDate = DateTime.Today.AddMonths(-1);
                DpDenNgay.SelectedDate = DateTime.Today;
                await TaiDuLieuAsync();
            }
        }

        private async void TaiDuLieu()
        {
            await TaiDuLieuAsync();
        }

        private async Task TaiDuLieuAsync()
        {
            if (dangNapDuLieu)
            {
                return;
            }

            try
            {
                dangNapDuLieu = true;
                SetLoading(true);
                danhSachHoaDon.Clear();
                tatCaHoaDonGiaiDoan.Clear();
                CapNhatThongKe();
                CapNhatTrangThaiNut();

                HoaDonFilter filter = TaoBoLocHienTai();
                List<HoaDonItem> hoaDon = await Task.Run(() => TaoHoaDonTheoGiaiDoan(LoadHoaDon(filter)));

                foreach (HoaDonItem item in hoaDon)
                {
                    tatCaHoaDonGiaiDoan.Add(item);
                }

                ApDungTabHoaDon();
            }
            catch (Exception ex)
            {
                DgHoaDon.ItemsSource = null;
                MessageBox.Show("Không thể tải danh sách hóa đơn.\nChi tiết: " + ex.Message, "Lỗi dữ liệu", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetLoading(false);
                dangNapDuLieu = false;
            }
        }

        private void SetLoading(bool isLoading)
        {
            LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            DgHoaDon.IsEnabled = !isLoading;
            BtnChiTiet.IsEnabled = !isLoading && BtnChiTiet.IsEnabled;
            BtnThanhToan.IsEnabled = !isLoading && BtnThanhToan.IsEnabled;
            BtnInHoaDon.IsEnabled = !isLoading && BtnInHoaDon.IsEnabled;
            BtnTachBill.IsEnabled = !isLoading && BtnTachBill.IsEnabled;
            BtnXoaHoaDon.IsEnabled = !isLoading && BtnXoaHoaDon.IsEnabled;
        }

        private HoaDonFilter TaoBoLocHienTai()
        {
            return new HoaDonFilter(
                TxtTimKiem.Text.Trim(),
                LayTrangThaiLoc(),
                DpTuNgay.SelectedDate?.Date,
                DpDenNgay.SelectedDate?.Date);
        }

        private List<HoaDonItem> LoadHoaDon(HoaDonFilter filter)
        {
            List<HoaDonItem> result = new();
            if (!TableExists("PHIEUTHUE") && !TableExists("PHIEUDATPHONG") && !TableExists("DATPHONG"))
            {
                return result;
            }

            string keyword = filter.Keyword;
            string status = filter.Status;
            List<string> queries = new();
            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists("DATPHONG") ? "DATPHONG" : string.Empty;
            bool coBangDatPhong = !string.IsNullOrWhiteSpace(bangDatPhong);

            if (TableExists("PHIEUTHUE"))
            {
                string tenPhongExpr = TenPhongSql("P");
                string tenLoaiPhongExpr = ColumnExists("LOAIPHONG", "TenLoaiPhong") ? "LP.TenLoaiPhong" : "CAST(P.MaLoaiPhong AS nvarchar(50))";
                string ngayLapExpr = ColumnExists("PHIEUTHUE", "NgayTraPhong") ? "ISNULL(PT.NgayTraPhong, PT.NgayNhan)" : "PT.NgayNhan";
                bool thueCoDatPhongLienKet = coBangDatPhong && ColumnExists("PHIEUTHUE", "MaDatPhong");
                string ngayNhanDatColumn = thueCoDatPhongLienKet && ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong";
                string ngayTraDatColumn = thueCoDatPhongLienKet && ColumnExists(bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong";
                string ngayNhanDuKienExpr = thueCoDatPhongLienKet
                    ? "ISNULL((SELECT TOP 1 DP0." + ngayNhanDatColumn + " FROM dbo." + bangDatPhong + " DP0 WHERE DP0.MaDatPhong = PT.MaDatPhong), PT.NgayNhan)"
                    : "PT.NgayNhan";
                string ngayTraDuKienExpr = thueCoDatPhongLienKet
                    ? "ISNULL((SELECT TOP 1 DP0." + ngayTraDatColumn + " FROM dbo." + bangDatPhong + " DP0 WHERE DP0.MaDatPhong = PT.MaDatPhong), PT.NgayTraDuKien)"
                    : "PT.NgayTraDuKien";
                string ngayTraThueHienTaiExpr = "PT.NgayTraDuKien";
                string tienPhongExpr = PricingHelper.TienPhongSql(ngayNhanDuKienExpr, ngayTraThueHienTaiExpr, ngayTraThueHienTaiExpr);
                string tienPhongCheckInDonExpr = PricingHelper.TienPhongSql(ngayNhanDuKienExpr, ngayTraDuKienExpr, ngayTraDuKienExpr);
                string phuPhiDonThueExpr = PricingHelper.PhuThuNhanSomSql("PT.NgayNhan", ngayNhanDuKienExpr);
                string ngayTraThucTeExpr = ColumnExists("PHIEUTHUE", "NgayTraPhong") ? "PT.NgayTraPhong" : "CAST(NULL AS datetime)";
                string giaNgayDonThueExpr = "ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0))";
                string giaGioDonThueExpr = "ISNULL(NULLIF(LP.DonGiaGio, 0), " + giaNgayDonThueExpr + " / 24.0)";
                bool thueCoChiTietDoan = TableExists("CHITIETDATPHONG") && ColumnExists("PHIEUTHUE", "MaDatPhong");
                string tenPhongP2Expr = TenPhongSql("P2");
                string tongGiaNgayThueExpr = @"(SELECT ISNULL(SUM(ISNULL(NULLIF(LP2.DonGiaDem, 0), ISNULL(LP2.DonGiaGio, 0) * 24.0)), 0)
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
                string dieuKienNhomDatCu = @"DPG.MaKH = DP0.MaKH
                              AND CONVERT(date, DPG." + ngayNhanDatColumn + @") = CONVERT(date, DP0." + ngayNhanDatColumn + @")
                              AND CONVERT(date, DPG." + ngayTraDatColumn + @") = CONVERT(date, DP0." + ngayTraDatColumn + @")";
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
                    ? @"(SELECT ISNULL(SUM(ISNULL(NULLIF(LP2.DonGiaDem, 0), ISNULL(LP2.DonGiaGio, 0) * 24.0)), 0)
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
                string tienPhongNhomDatCuExpr = PricingHelper.TienPhongSql(ngayNhanDuKienExpr, ngayTraThueHienTaiExpr, ngayTraThueHienTaiExpr, tongGiaNgayNhomDatCuExpr, tongGiaGioNhomDatCuExpr);
                string tienPhongDoanThueExpr = PricingHelper.TienPhongSql(ngayNhanDuKienExpr, ngayTraThueHienTaiExpr, ngayTraThueHienTaiExpr, tongGiaNgayThueExpr, tongGiaGioThueExpr);
                string tienPhongCheckInNhomDatCuExpr = PricingHelper.TienPhongSql(ngayNhanDuKienExpr, ngayTraDuKienExpr, ngayTraDuKienExpr, tongGiaNgayNhomDatCuExpr, tongGiaGioNhomDatCuExpr);
                string tienPhongCheckInDoanThueExpr = PricingHelper.TienPhongSql(ngayNhanDuKienExpr, ngayTraDuKienExpr, ngayTraDuKienExpr, tongGiaNgayThueExpr, tongGiaGioThueExpr);
                string phuPhiNhomDatCuExpr = PricingHelper.PhuThuNhanSomSql("PT.NgayNhan", ngayNhanDuKienExpr, tongGiaNgayNhomDatCuExpr);
                string phuPhiDoanThueExpr = PricingHelper.PhuThuNhanSomSql("PT.NgayNhan", ngayNhanDuKienExpr, tongGiaNgayThueExpr);
                string tienPhongThueExpr = thueCoChiTietDoan || thueCoNhomDatCu
                    ? @"CASE WHEN PT.MaDatPhong IS NOT NULL AND " + demNhomDatCuExpr + @" > 1 THEN
                            " + tienPhongNhomDatCuExpr + @"
                           WHEN PT.MaDatPhong IS NOT NULL AND " + tonTaiChiTietThueExpr + @" THEN
                            " + tienPhongDoanThueExpr + @"
                           ELSE " + tienPhongExpr + " END"
                    : tienPhongExpr;
                string tienPhongCheckInFallbackExpr = thueCoChiTietDoan || thueCoNhomDatCu
                    ? @"CASE WHEN PT.MaDatPhong IS NOT NULL AND " + demNhomDatCuExpr + @" > 1 THEN
                            " + tienPhongCheckInNhomDatCuExpr + @"
                           WHEN PT.MaDatPhong IS NOT NULL AND " + tonTaiChiTietThueExpr + @" THEN
                            " + tienPhongCheckInDoanThueExpr + @"
                           ELSE " + tienPhongCheckInDonExpr + " END"
                    : tienPhongCheckInDonExpr;
                string phuPhiThueExpr = thueCoChiTietDoan || thueCoNhomDatCu
                    ? @"CASE WHEN PT.MaDatPhong IS NOT NULL AND " + demNhomDatCuExpr + @" > 1 THEN
                            " + phuPhiNhomDatCuExpr + @"
                           WHEN PT.MaDatPhong IS NOT NULL AND " + tonTaiChiTietThueExpr + @" THEN
                            " + phuPhiDoanThueExpr + @"
                           ELSE " + phuPhiDonThueExpr + " END"
                    : phuPhiDonThueExpr;
                string giaNgayThueExpr = thueCoChiTietDoan || thueCoNhomDatCu
                    ? @"CASE WHEN PT.MaDatPhong IS NOT NULL AND " + demNhomDatCuExpr + @" > 1 THEN
                            " + tongGiaNgayNhomDatCuExpr + @"
                           WHEN PT.MaDatPhong IS NOT NULL AND " + tonTaiChiTietThueExpr + @" THEN
                            " + tongGiaNgayThueExpr + @"
                           ELSE " + giaNgayDonThueExpr + " END"
                    : giaNgayDonThueExpr;
                string giaGioThueExpr = thueCoChiTietDoan || thueCoNhomDatCu
                    ? @"CASE WHEN PT.MaDatPhong IS NOT NULL AND " + demNhomDatCuExpr + @" > 1 THEN
                            " + tongGiaGioNhomDatCuExpr + @"
                           WHEN PT.MaDatPhong IS NOT NULL AND " + tonTaiChiTietThueExpr + @" THEN
                            " + tongGiaGioThueExpr + @"
                           ELSE " + giaGioDonThueExpr + " END"
                    : giaGioDonThueExpr;
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
                string loaiPhongThueExpr = thueCoNhomDatCu
                    ? @"CASE WHEN PT.MaDatPhong IS NOT NULL AND " + demNhomDatCuExpr + @" > 1 THEN
                            " + loaiPhongNhomDatCuExpr + @"
                           ELSE " + tenLoaiPhongExpr + " END"
                    : tenLoaiPhongExpr;
                string soLuongPhongThueExpr = thueCoChiTietDoan || thueCoNhomDatCu
                    ? @"CASE WHEN PT.MaDatPhong IS NOT NULL AND " + demNhomDatCuExpr + @" > 1 THEN
                            " + demNhomDatCuExpr + @"
                           WHEN PT.MaDatPhong IS NOT NULL AND " + tonTaiChiTietThueExpr + @" THEN
                            (SELECT COUNT(*) FROM dbo.CHITIETDATPHONG CT2 WHERE CT2.MaDatPhong = PT.MaDatPhong)
                           ELSE 1 END"
                    : "CAST(1 AS int)";
                string maHoaDonThueExpr = thueCoChiTietDoan || thueCoNhomDatCu
                    ? @"CASE WHEN (" + soLuongPhongThueExpr + @") > 1 THEN
                            N'HD-PTTD' + RIGHT('0000' + CAST(PT.MaThue AS nvarchar(20)), 4)
                           ELSE N'HD-PT' + RIGHT('0000' + CAST(PT.MaThue AS nvarchar(20)), 4) END"
                    : "N'HD-PT' + RIGHT('0000' + CAST(PT.MaThue AS nvarchar(20)), 4)";
                string maPhieuThueExpr = thueCoChiTietDoan || thueCoNhomDatCu
                    ? @"CASE WHEN (" + soLuongPhongThueExpr + @") > 1 THEN
                            N'PTTD' + RIGHT('0000' + CAST(PT.MaThue AS nvarchar(20)), 4)
                           ELSE N'PT' + RIGHT('0000' + CAST(PT.MaThue AS nvarchar(20)), 4) END"
                    : "N'PT' + RIGHT('0000' + CAST(PT.MaThue AS nvarchar(20)), 4)";
                string dichVuExpr = DichVuSubQuery("PT.MaThue", "THUE");
                string hoaDonKey = TableExists("HOADON") ? GetFirstExistingColumn("HOADON", "MaHoaDon", "MaHD", "IDHoaDon", "HoaDonID", "IdHoaDon", "ID", "Ma") : string.Empty;
                string tienPhongCheckInExpr = !string.IsNullOrWhiteSpace(hoaDonKey) && ColumnExists("HOADON", "MaThue") && ColumnExists("HOADON", "TongTienPhong")
                    ? "ISNULL((SELECT TOP 1 NULLIF(HD.TongTienPhong, 0) FROM dbo.HOADON HD WHERE HD.MaThue = PT.MaThue ORDER BY HD." + hoaDonKey + " DESC), " + tienPhongCheckInFallbackExpr + ")"
                    : tienPhongCheckInFallbackExpr;
                string diaChiExpr = ColumnExists("KHACHHANG", "DiaChi") ? "KH.DiaChi" : "CAST(NULL AS nvarchar(255))";
                string loaiKhachExpr = ColumnExists("KHACHHANG", "LoaiKhach") ? "KH.LoaiKhach" : "CAST(N'' AS nvarchar(50))";
                string giamGiaThueExpr = "CAST(CASE WHEN " + loaiKhachExpr + " LIKE N'%VIP%' THEN (" + tienPhongThueExpr + " + " + phuPhiThueExpr + ") * 0.1 ELSE 0 END AS decimal(18,2))";
                string thueVatThueExpr = "CAST((" + tienPhongThueExpr + " + " + phuPhiThueExpr + " - " + giamGiaThueExpr + ") * 0.1 AS decimal(18,2))";

                queries.Add(@"
SELECT 'THUE' AS LoaiPhieu,
       PT.MaThue AS MaGoc,
       " + maHoaDonThueExpr + @" AS MaHoaDon,
       " + maPhieuThueExpr + @" AS MaPhieuThue,
       KH.HoTen AS TenKhachHang,
       KH.SDT AS SoDienThoai,
       " + diaChiExpr + @" AS DiaChi,
       " + tenPhongThueExpr + @" AS SoPhong,
       " + loaiPhongThueExpr + @" AS LoaiPhong,
       " + ngayNhanDuKienExpr + @" AS NgayNhanPhong,
       " + ngayTraDuKienExpr + @" AS NgayTraPhong,
       PT.NgayNhan AS NgayNhanThucTe,
       " + ngayLapExpr + @" AS NgayLapHoaDon,
       " + tienPhongThueExpr + @" AS TienPhong,
       " + tienPhongCheckInExpr + @" AS TienPhongCheckIn,
       " + dichVuExpr + @" AS TienDichVu,
       " + phuPhiThueExpr + @" AS PhuPhi,
       " + ngayTraThucTeExpr + @" AS NgayTraThucTe,
       " + giaGioThueExpr + @" AS GiaGioTinhPhi,
       " + giaNgayThueExpr + @" AS GiaNgayTinhPhi,
       " + thueVatThueExpr + @" AS ThueVat,
       " + giamGiaThueExpr + @" AS GiamGia,
       " + (ColumnExists("PHIEUTHUE", "GhiChu") ? "PT.GhiChu" : "CAST(NULL AS nvarchar(1000))") + @" AS GhiChu,
       CASE
           WHEN PT.TrangThai IN (N'Đã trả', N'Da tra', N'Đã trả phòng', N'Da tra phong', N'Đã thanh toán', N'Da thanh toan') THEN N'Đã thanh toán'
           ELSE N'Chưa thanh toán'
       END AS TrangThai
FROM dbo.PHIEUTHUE PT
JOIN dbo.KHACHHANG KH ON PT.MaKH = KH.MaKH
JOIN dbo.PHONG P ON PT.MaPhong = P.MaPhong
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong");
            }

            if (!string.IsNullOrWhiteSpace(bangDatPhong))
            {
                string ngayNhan = ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "DP.NgayNhanDuKien" : "DP.NgayNhanPhong";
                string ngayTra = ColumnExists(bangDatPhong, "NgayTraDuKien") ? "DP.NgayTraDuKien" : "DP.NgayTraPhong";
                string tenPhongExpr = TenPhongSql("P");
                string tenPhongP2Expr = TenPhongSql("P2");
                string tenLoaiPhongExpr = ColumnExists("LOAIPHONG", "TenLoaiPhong") ? "LP.TenLoaiPhong" : "CAST(P.MaLoaiPhong AS nvarchar(50))";
                bool coChiTietDatPhong = TableExists("CHITIETDATPHONG");
                bool coMaPhongDat = ColumnExists(bangDatPhong, "MaPhong");
                string tonTaiChiTietDatExpr = "EXISTS (SELECT 1 FROM dbo.CHITIETDATPHONG CT2 WHERE CT2.MaDatPhong = DP.MaDatPhong)";
                string tongGiaNgayDatExpr = @"(SELECT ISNULL(SUM(ISNULL(NULLIF(LP2.DonGiaDem, 0), ISNULL(LP2.DonGiaGio, 0) * 24.0)), 0)
                                               FROM dbo.CHITIETDATPHONG CT2
                                               JOIN dbo.PHONG P2 ON CT2.MaPhong = P2.MaPhong
                                               LEFT JOIN dbo.LOAIPHONG LP2 ON P2.MaLoaiPhong = LP2.MaLoaiPhong
                                               WHERE CT2.MaDatPhong = DP.MaDatPhong)";
                string tongGiaGioDatExpr = @"(SELECT ISNULL(SUM(ISNULL(LP2.DonGiaGio, 0)), 0)
                                              FROM dbo.CHITIETDATPHONG CT2
                                              JOIN dbo.PHONG P2 ON CT2.MaPhong = P2.MaPhong
                                              LEFT JOIN dbo.LOAIPHONG LP2 ON P2.MaLoaiPhong = LP2.MaLoaiPhong
                                              WHERE CT2.MaDatPhong = DP.MaDatPhong)";
                string tienPhongDonDatExpr = coMaPhongDat ? PricingHelper.TienPhongSql(ngayNhan, ngayTra, ngayTra) : "CAST(0 AS decimal(18,2))";
                string tienPhongDoanDatExpr = PricingHelper.TienPhongSql(ngayNhan, ngayTra, ngayTra, tongGiaNgayDatExpr, tongGiaGioDatExpr);
                string tienPhongExpr = coChiTietDatPhong
                    ? "CASE WHEN " + tonTaiChiTietDatExpr + " THEN " + tienPhongDoanDatExpr + " ELSE " + tienPhongDonDatExpr + " END"
                    : tienPhongDonDatExpr;
                string dichVuExpr = DichVuSubQuery("DP.MaDatPhong", "DAT");
                string diaChiExpr = ColumnExists("KHACHHANG", "DiaChi") ? "KH.DiaChi" : "CAST(NULL AS nvarchar(255))";
                string loaiKhachExpr = ColumnExists("KHACHHANG", "LoaiKhach") ? "KH.LoaiKhach" : "CAST(N'' AS nvarchar(50))";
                string giamGiaDatExpr = "CAST(CASE WHEN " + loaiKhachExpr + " LIKE N'%VIP%' THEN " + tienPhongExpr + " * 0.1 ELSE 0 END AS decimal(18,2))";
                string thueVatDatExpr = "CAST((" + tienPhongExpr + " - " + giamGiaDatExpr + ") * 0.1 AS decimal(18,2))";
                string tenPhongDatExpr = coChiTietDatPhong
                    ? @"CASE WHEN " + tonTaiChiTietDatExpr + @" THEN
                        (SELECT STRING_AGG(CAST(" + tenPhongP2Expr + @" AS nvarchar(max)), N', ')
                         FROM dbo.CHITIETDATPHONG CT2
                         JOIN dbo.PHONG P2 ON CT2.MaPhong = P2.MaPhong
                         WHERE CT2.MaDatPhong = DP.MaDatPhong)
                       ELSE " + (coMaPhongDat ? tenPhongExpr : "CAST(N'' AS nvarchar(max))") + @" END"
                    : tenPhongExpr;
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
                string joinPhong = coMaPhongDat
                    ? "LEFT JOIN dbo.PHONG P ON DP.MaPhong = P.MaPhong"
                    : string.Empty;
                string existsThue = TableExists("PHIEUTHUE") && ColumnExists("PHIEUTHUE", "MaDatPhong")
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
       N'HD-DP' + RIGHT('0000' + CAST(DP.MaDatPhong AS nvarchar(20)), 4) AS MaHoaDon,
       N'PT' + RIGHT('0000' + CAST(DP.MaDatPhong AS nvarchar(20)), 4) AS MaPhieuThue,
       KH.HoTen AS TenKhachHang,
       KH.SDT AS SoDienThoai,
       " + diaChiExpr + @" AS DiaChi,
       " + tenPhongDatExpr + @" AS SoPhong,
       " + loaiPhongDatExpr + @" AS LoaiPhong,
       " + ngayNhan + @" AS NgayNhanPhong,
       " + ngayTra + @" AS NgayTraPhong,
       CAST(NULL AS datetime) AS NgayNhanThucTe,
       " + ngayNhan + @" AS NgayLapHoaDon,
       " + tienPhongExpr + @" AS TienPhong,
       " + tienPhongExpr + @" AS TienPhongCheckIn,
       " + dichVuExpr + @" AS TienDichVu,
       CAST(0 AS decimal(18,2)) AS PhuPhi,
       CAST(NULL AS datetime) AS NgayTraThucTe,
       CAST(0 AS decimal(18,2)) AS GiaGioTinhPhi,
       CAST(0 AS decimal(18,2)) AS GiaNgayTinhPhi,
       " + thueVatDatExpr + @" AS ThueVat,
       " + giamGiaDatExpr + @" AS GiamGia,
       " + (ColumnExists(bangDatPhong, "GhiChu") ? "DP.GhiChu" : "CAST(NULL AS nvarchar(1000))") + @" AS GhiChu,
       N'Chưa thanh toán' AS TrangThai
FROM dbo." + bangDatPhong + @" DP
JOIN dbo.KHACHHANG KH ON DP.MaKH = KH.MaKH
" + joinPhong + @"
" + (coMaPhongDat ? "LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong" : string.Empty) + @"
" + existsThue);
            }

            if (queries.Count == 0)
            {
                return result;
            }

            string sql = "SELECT * FROM (" + string.Join("\nUNION ALL\n", queries) + @") X
WHERE (@TrangThai = N'' OR X.TrangThai = @TrangThai)
  AND (@TuKhoa = N'' OR X.MaHoaDon LIKE N'%' + @TuKhoa + N'%'
       OR X.MaPhieuThue LIKE N'%' + @TuKhoa + N'%'
       OR X.TenKhachHang LIKE N'%' + @TuKhoa + N'%'
       OR X.SoPhong LIKE N'%' + @TuKhoa + N'%')
  AND (@TuNgay IS NULL OR CONVERT(date, X.NgayLapHoaDon) >= @TuNgay)
  AND (@DenNgay IS NULL OR CONVERT(date, X.NgayLapHoaDon) <= @DenNgay)
ORDER BY X.NgayLapHoaDon DESC";

            DataTable data = ConnectDB.GetData(sql,
                new SqlParameter("@TrangThai", status),
                new SqlParameter("@TuKhoa", keyword),
                new SqlParameter("@TuNgay", filter.TuNgay.HasValue ? filter.TuNgay.Value.Date : DBNull.Value),
                new SqlParameter("@DenNgay", filter.DenNgay.HasValue ? filter.DenNgay.Value.Date : DBNull.Value));

            foreach (DataRow row in data.Rows)
            {
                result.Add(HoaDonItem.FromRow(row));
            }

            return GopHoaDonDoanCu(result);
        }

        private List<HoaDonItem> TaoHoaDonTheoGiaiDoan(IEnumerable<HoaDonItem> source)
        {
            List<HoaDonItem> result = new();
            HashSet<string> maHoaDonDaTao = new(StringComparer.OrdinalIgnoreCase);
            foreach (HoaDonItem item in source)
            {
                decimal dichVuCheckIn = item.LoaiPhieu == "THUE"
                    ? Math.Max(0, LayTienDichVuCheckIn(item.MaGoc))
                    : Math.Min(item.TienDichVu, Math.Max(0, item.TienDichVu));
                decimal tienPhongCheckInRaw = item.TienPhongCheckIn;
                if (item.LoaiPhieu == "THUE")
                {
                    decimal tienPhongTuLichSu = LayTienPhongCheckInTuLichSuThanhToan(item.MaGoc, item.PhuPhi, item.GiamGia > 0);
                    if (tienPhongTuLichSu > 0)
                    {
                        tienPhongCheckInRaw = tienPhongTuLichSu;
                    }
                }
                decimal tienPhongCheckIn = Math.Min(item.TienPhong, Math.Max(0, tienPhongCheckInRaw));
                decimal tienGiaHan = Math.Max(0, item.TienPhong - tienPhongCheckIn);
                bool laVip = item.GiamGia > 0;
                decimal giamGiaCheckIn = laVip ? Math.Round((tienPhongCheckIn + item.PhuPhi) * 0.1m, 0) : 0;
                decimal vatCheckIn = Math.Round(Math.Max(0, tienPhongCheckIn + item.PhuPhi - giamGiaCheckIn) * 0.1m, 0);
                string maCheckIn = TaoMaHoaDonGiaiDoan(item, "CI");
                if (maHoaDonDaTao.Add(maCheckIn))
                {
                    result.Add(TaoBanSaoHoaDon(
                    item,
                    maCheckIn,
                    "CHECKIN",
                    tienPhongCheckIn,
                    dichVuCheckIn,
                    item.PhuPhi,
                    vatCheckIn,
                    giamGiaCheckIn,
                    item.LoaiPhieu == "THUE" ? "Đã thanh toán" : "Chưa thanh toán"));
                }

                if (item.LoaiPhieu != "THUE")
                {
                    continue;
                }

                if (!item.DaThanhToan)
                {
                    continue;
                }

                decimal dichVuPhatSinh = item.LoaiPhieu == "THUE"
                    ? LayTienDichVuPhatSinh(item.MaGoc)
                    : Math.Max(0, item.TienDichVu - dichVuCheckIn);
                decimal phuPhiTraMuon = Math.Max(0, item.PhuPhiTraMuon);
                if (dichVuPhatSinh <= 0 && phuPhiTraMuon <= 0 && tienGiaHan <= 0)
                {
                    continue;
                }

                decimal giamGiaPhatSinh = 0;
                decimal vatPhatSinh = 0;
                string maPhatSinh = TaoMaHoaDonGiaiDoan(item, "PS");
                if (maHoaDonDaTao.Add(maPhatSinh))
                {
                    result.Add(TaoBanSaoHoaDon(
                    item,
                    maPhatSinh,
                    "PHATSINH",
                    tienGiaHan,
                    dichVuPhatSinh,
                    phuPhiTraMuon,
                    vatPhatSinh,
                    giamGiaPhatSinh,
                    item.DaThanhToan ? "Đã thanh toán" : "Chưa thanh toán"));
                }
            }

            return result;
        }

        private static string TaoMaHoaDonGiaiDoan(HoaDonItem item, string suffix)
        {
            string maPhieu = string.IsNullOrWhiteSpace(item.MaPhieuThue)
                ? item.LoaiPhieu + item.MaGoc.ToString("0000")
                : item.MaPhieuThue;
            return "HD-" + suffix + "-" + maPhieu.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static HoaDonItem TaoBanSaoHoaDon(
            HoaDonItem source,
            string maHoaDon,
            string loaiThanhToan,
            decimal tienPhong,
            decimal tienDichVu,
            decimal phuPhi,
            decimal thueVat,
            decimal giamGia,
            string trangThai)
        {
            return new HoaDonItem
            {
                LoaiPhieu = source.LoaiPhieu,
                LoaiThanhToan = loaiThanhToan,
                MaGoc = source.MaGoc,
                MaHoaDon = maHoaDon,
                MaPhieuThue = source.MaPhieuThue,
                TenKhachHang = source.TenKhachHang,
                SoDienThoai = source.SoDienThoai,
                DiaChi = source.DiaChi,
                SoPhong = source.SoPhong,
                LoaiPhong = source.LoaiPhong,
                NgayNhanPhong = source.NgayNhanPhong,
                NgayTraPhong = source.NgayTraPhong,
                NgayNhanThucTe = source.NgayNhanThucTe,
                NgayTraThucTe = source.NgayTraThucTe,
                GiaGioTinhPhi = source.GiaGioTinhPhi,
                GiaNgayTinhPhi = source.GiaNgayTinhPhi,
                NgayLapHoaDon = source.NgayLapHoaDon,
                TienPhong = tienPhong,
                TienPhongCheckIn = source.TienPhongCheckIn,
                TienDichVu = tienDichVu,
                PhuPhi = phuPhi,
                ThueVat = thueVat,
                GiamGia = giamGia,
                TrangThai = trangThai
            };
        }

        private static decimal LayTienDichVuCheckIn(int maThue)
        {
            if (!TableExists("CHITIETTHANHTOAN") ||
                !ColumnExists("CHITIETTHANHTOAN", "LoaiThanhToan") ||
                !ColumnExists("CHITIETTHANHTOAN", "MaThue"))
            {
                return 0;
            }

            string amountColumn = ColumnExists("CHITIETTHANHTOAN", "SoTien") ? "SoTien" :
                ColumnExists("CHITIETTHANHTOAN", "TienThanhToan") ? "TienThanhToan" : string.Empty;
            if (string.IsNullOrWhiteSpace(amountColumn))
            {
                return 0;
            }

            object? value = ConnectDB.ExecuteScalar(
                "SELECT ISNULL(SUM(" + amountColumn + "), 0) FROM dbo.CHITIETTHANHTOAN WHERE MaThue = @MaThue AND LoaiThanhToan = N'ServiceCheckIn'",
                new SqlParameter("@MaThue", maThue));
            decimal tienLichSu = value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
            return tienLichSu > 0 ? tienLichSu : LayTienDichVuTheoMarker(maThue, "[DICHVU_CHECKIN]");
        }

        private static decimal LayTienDichVuTheoMarker(int maThue, string marker)
        {
            string table = TableExists("PHATSINHDICHVU") ? "PHATSINHDICHVU" : TableExists("CHITIETPHATSINH") ? "CHITIETPHATSINH" : string.Empty;
            if (string.IsNullOrWhiteSpace(table) ||
                !ColumnExists(table, "MaThue") ||
                !ColumnExists(table, "GhiChu"))
            {
                return 0;
            }

            string thanhTien = ColumnExists(table, "ThanhTien") ? "ThanhTien" :
                ColumnExists(table, "DonGia") && ColumnExists(table, "SoLuong") ? "(SoLuong * DonGia)" : "0";
            object? value = ConnectDB.ExecuteScalar(
                "SELECT ISNULL(SUM(" + thanhTien + "), 0) FROM dbo." + table + " WHERE MaThue = @MaThue AND ISNULL(GhiChu, N'') LIKE @Marker",
                new SqlParameter("@MaThue", maThue),
                new SqlParameter("@Marker", "%" + marker + "%"));
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static decimal LayTienDichVuPhatSinh(int maThue)
        {
            string table = TableExists("PHATSINHDICHVU") ? "PHATSINHDICHVU" : TableExists("CHITIETPHATSINH") ? "CHITIETPHATSINH" : string.Empty;
            if (string.IsNullOrWhiteSpace(table) || !ColumnExists(table, "MaThue"))
            {
                return 0;
            }

            string thanhTien = ColumnExists(table, "ThanhTien") ? "ThanhTien" :
                ColumnExists(table, "DonGia") && ColumnExists(table, "SoLuong") ? "(SoLuong * DonGia)" : "0";
            string filter = ColumnExists(table, "GhiChu")
                ? ViewSchemaHelper.DichVuTheoLoaiHoaDonFilter(string.Empty, "PHATSINH")
                : string.Empty;
            object? value = ConnectDB.ExecuteScalar(
                "SELECT ISNULL(SUM(" + thanhTien + "), 0) FROM dbo." + table + " WHERE MaThue = @MaThue" + filter,
                new SqlParameter("@MaThue", maThue));
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static decimal LayTienPhongCheckInTuLichSuThanhToan(int maThue, decimal phuPhiNhanSom, bool laVip)
        {
            if (!TableExists("CHITIETTHANHTOAN") ||
                !ColumnExists("CHITIETTHANHTOAN", "LoaiThanhToan") ||
                !ColumnExists("CHITIETTHANHTOAN", "MaThue"))
            {
                return 0;
            }

            string amountColumn = ColumnExists("CHITIETTHANHTOAN", "SoTien") ? "SoTien" :
                ColumnExists("CHITIETTHANHTOAN", "TienThanhToan") ? "TienThanhToan" : string.Empty;
            if (string.IsNullOrWhiteSpace(amountColumn))
            {
                return 0;
            }

            object? paymentValue = ConnectDB.ExecuteScalar(
                "SELECT ISNULL(SUM(" + amountColumn + "), 0) FROM dbo.CHITIETTHANHTOAN WHERE MaThue = @MaThue AND LoaiThanhToan = N'RoomCheckIn'",
                new SqlParameter("@MaThue", maThue));
            decimal tienDaThuChoPhong = paymentValue == null || paymentValue == DBNull.Value ? 0 : Convert.ToDecimal(paymentValue);
            if (tienDaThuChoPhong <= 0)
            {
                return 0;
            }

            decimal tienCoc = 0;
            if (TableExists("HOADON") && ColumnExists("HOADON", "MaThue"))
            {
                string tienCocColumn = ColumnExists("HOADON", "TienCoc") ? "TienCoc" :
                    ColumnExists("HOADON", "TienDatCocTruoc") ? "TienDatCocTruoc" : string.Empty;
                string hoaDonKey = GetFirstExistingColumn("HOADON", "MaHoaDon", "MaHD", "IDHoaDon", "HoaDonID", "IdHoaDon", "ID", "Ma");
                if (!string.IsNullOrWhiteSpace(tienCocColumn) && !string.IsNullOrWhiteSpace(hoaDonKey))
                {
                    object? depositValue = ConnectDB.ExecuteScalar(
                        "SELECT TOP 1 ISNULL(" + tienCocColumn + ", 0) FROM dbo.HOADON WHERE MaThue = @MaThue ORDER BY " + hoaDonKey + " DESC",
                        new SqlParameter("@MaThue", maThue));
                    tienCoc = depositValue == null || depositValue == DBNull.Value ? 0 : Convert.ToDecimal(depositValue);
                }
            }

            decimal heSoSauGiamVaThue = laVip ? 0.99m : 1.1m;
            decimal tienPhong = ((tienDaThuChoPhong + tienCoc) / heSoSauGiamVaThue) - phuPhiNhanSom;
            return Math.Round(Math.Max(0, tienPhong), 0);
        }

        private static List<HoaDonItem> GopHoaDonDoanCu(List<HoaDonItem> items)
        {
            List<HoaDonItem> result = new();

            foreach (var group in items.GroupBy(item => TaoKhoaGopHoaDon(item)))
            {
                List<HoaDonItem> rows = group.OrderBy(item => item.MaGoc).ToList();
                if (rows.Count == 1)
                {
                    result.AddRange(rows);
                    continue;
                }

                HoaDonItem first = rows[0];
                HoaDonItem last = rows[^1];
                result.Add(new HoaDonItem
                {
                    LoaiPhieu = first.LoaiPhieu,
                    MaGoc = first.MaGoc,
                    MaHoaDon = first.LoaiPhieu == "THUE" ? "HD-" + TaoMaPhieuThueDoan(first.MaGoc, 4) : TaoMaGop(first.MaHoaDon, last.MaHoaDon),
                    MaPhieuThue = first.LoaiPhieu == "THUE" ? TaoMaPhieuThueDoan(first.MaGoc, 4) : TaoMaGop(first.MaPhieuThue, last.MaPhieuThue),
                    TenKhachHang = first.TenKhachHang,
                    SoDienThoai = first.SoDienThoai,
                    DiaChi = first.DiaChi,
                    SoPhong = NoiGiaTriKhacNhau(rows.Select(item => item.SoPhong)),
                    LoaiPhong = NoiGiaTriKhacNhau(rows.Select(item => item.LoaiPhong)),
                    NgayNhanPhong = first.NgayNhanPhong,
                    NgayTraPhong = first.NgayTraPhong,
                    NgayNhanThucTe = first.NgayNhanThucTe,
                    NgayTraThucTe = first.NgayTraThucTe,
                    GiaGioTinhPhi = rows.Sum(item => item.GiaGioTinhPhi),
                    GiaNgayTinhPhi = rows.Sum(item => item.GiaNgayTinhPhi),
                    NgayLapHoaDon = first.NgayLapHoaDon,
                    TienPhong = rows.Sum(item => item.TienPhong),
                    TienPhongCheckIn = rows.Sum(item => item.TienPhongCheckIn),
                    TienDichVu = rows.Sum(item => item.TienDichVu),
                    PhuPhi = rows.Sum(item => item.PhuPhi),
                    ThueVat = rows.Sum(item => item.ThueVat),
                    GiamGia = rows.Sum(item => item.GiamGia),
                    TrangThai = first.TrangThai
                });
            }

            return result.OrderByDescending(item => item.NgayLapHoaDon).ThenBy(item => item.MaGoc).ToList();
        }

        private static string TaoKhoaGopHoaDon(HoaDonItem item)
        {
            if (item.LoaiPhieu != "DAT" && item.LoaiPhieu != "THUE")
            {
                return item.LoaiPhieu + "|" + item.MaGoc;
            }

            return string.Join("|",
                item.LoaiPhieu,
                item.TenKhachHang.Trim().ToUpperInvariant(),
                item.SoDienThoai.Trim(),
                item.NgayNhanPhong.ToString("yyyyMMdd"),
                item.NgayTraPhong.ToString("yyyyMMdd"),
                item.TrangThai.Trim().ToUpperInvariant());
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

        private static string TaoMaPhieuThueDoan(int maGoc, int digits)
        {
            return "PTTD" + maGoc.ToString(new string('0', digits));
        }

        private void CapNhatThongKe()
        {
            int tong = danhSachHoaDon.Count;
            int daThanhToan = danhSachHoaDon.Count(item => item.DaThanhToan);
            int chuaThanhToan = tong - daThanhToan;
            decimal doanhThu = danhSachHoaDon.Where(item => item.DaThanhToan).Sum(item => item.TongTien);

            TxtTongHoaDon.Text = tong.ToString("N0");
            TxtChuaThanhToan.Text = chuaThanhToan.ToString("N0");
            TxtDaThanhToan.Text = daThanhToan.ToString("N0");
            TxtDoanhThu.Text = doanhThu.ToString("N0") + " VND";
            TxtTongSo.Text = "Tổng số: " + tong.ToString("N0") + " hóa đơn";
            TxtTrang.Text = tong == 0 ? "Không có hóa đơn phù hợp" : "Hiển thị 1 đến " + tong.ToString("N0") + " trong tổng số " + tong.ToString("N0") + " hóa đơn";
        }

        private void ApDungTabHoaDon()
        {
            string loaiThanhToan = LayLoaiThanhToanDangChon();
            danhSachHoaDon.Clear();
            foreach (HoaDonItem item in tatCaHoaDonGiaiDoan.Where(item => item.LoaiThanhToan == loaiThanhToan))
            {
                danhSachHoaDon.Add(item);
            }

            DgHoaDon.ItemsSource = danhSachHoaDon;
            DgHoaDon.SelectedIndex = danhSachHoaDon.Count > 0 ? 0 : -1;
            CapNhatThongKe();
            CapNhatTrangThaiNut();
        }

        private string LayLoaiThanhToanDangChon()
        {
            return TabLoaiHoaDon.SelectedItem is TabItem item && item.Tag?.ToString() == "PHATSINH"
                ? "PHATSINH"
                : "CHECKIN";
        }

        private void TabLoaiHoaDon_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!dangNapDuLieu && IsLoaded)
            {
                ApDungTabHoaDon();
            }
        }

        private string LayTrangThaiLoc()
        {
            return CboTrangThai.SelectedItem is ComboBoxItem item && item.Content?.ToString() != "Tất cả"
                ? item.Content?.ToString() ?? string.Empty
                : string.Empty;
        }

        private HoaDonItem? LayHoaDonDangChon()
        {
            return DgHoaDon.SelectedItem as HoaDonItem;
        }

        private void CapNhatTrangThaiNut()
        {
            HoaDonItem? item = LayHoaDonDangChon();
            bool coChon = item != null;
            BtnChiTiet.IsEnabled = coChon;
            BtnThanhToan.IsEnabled = coChon && item is { DaThanhToan: false, LoaiPhieu: "THUE" };
            BtnInHoaDon.IsEnabled = coChon;
            BtnTachBill.IsEnabled = coChon;
            BtnXoaHoaDon.IsEnabled = coChon;
        }

        private void BtnLoc_Click(object sender, RoutedEventArgs e)
        {
            TaiDuLieu();
        }

        private void BtnLamMoi_Click(object sender, RoutedEventArgs e)
        {
            TxtTimKiem.Clear();
            CboTrangThai.SelectedIndex = 0;
            TabLoaiHoaDon.SelectedIndex = 0;
            DpTuNgay.SelectedDate = DateTime.Today.AddMonths(-1);
            DpDenNgay.SelectedDate = DateTime.Today;
            TaiDuLieu();
        }

        private void TxtTimKiem_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        private IEnumerable<SearchSuggestionItem> TaoGoiYTimKiem()
        {
            IEnumerable<HoaDonItem> source = tatCaHoaDonGiaiDoan.Count > 0
                ? tatCaHoaDonGiaiDoan
                : danhSachHoaDon;

            foreach (HoaDonItem item in source)
            {
                if (!string.IsNullOrWhiteSpace(item.MaHoaDon))
                {
                    yield return new SearchSuggestionItem(item.MaHoaDon, $"{item.MaHoaDon} - {item.TenKhachHang} - {item.SoPhong}");
                }

                if (!string.IsNullOrWhiteSpace(item.MaPhieuThue))
                {
                    yield return new SearchSuggestionItem(item.MaPhieuThue, $"{item.MaPhieuThue} - {item.TenKhachHang}");
                }

                if (!string.IsNullOrWhiteSpace(item.TenKhachHang))
                {
                    yield return new SearchSuggestionItem(item.TenKhachHang, $"{item.TenKhachHang} - {item.SoDienThoai}");
                }

                if (!string.IsNullOrWhiteSpace(item.SoPhong))
                {
                    yield return new SearchSuggestionItem(item.SoPhong, $"Phòng {item.SoPhong} - {item.TenKhachHang}");
                }
            }
        }

        private void BoLoc_Changed(object sender, EventArgs e)
        {
            if (!dangNapDuLieu && IsLoaded)
            {
                TaiDuLieu();
            }
        }

        private void DgHoaDon_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CapNhatTrangThaiNut();
        }

        private void DgHoaDon_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MoChiTiet();
        }

        private void BtnChiTiet_Click(object sender, RoutedEventArgs e)
        {
            MoChiTiet();
        }

        private void MoChiTiet()
        {
            HoaDonItem? item = LayHoaDonDangChon();
            if (item == null)
            {
                MessageBox.Show("Vui lòng chọn hóa đơn cần xem chi tiết.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            HoaDonChiTietWindow window = new(item);
            DialogService.ShowDimmedDialogResult(window, Window.GetWindow(this));
            if (window.DuLieuDaThayDoi)
            {
                TaiDuLieu();
            }
        }

        private void BtnThanhToan_Click(object sender, RoutedEventArgs e)
        {
            ThanhToanHoaDonDangChon();
        }

        private void ThanhToanHoaDonDangChon()
        {
            HoaDonItem? item = LayHoaDonDangChon();
            if (item == null)
            {
                MessageBox.Show("Vui lòng chọn hóa đơn cần thanh toán.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (item.DaThanhToan)
            {
                MessageBox.Show("Hóa đơn này đã thanh toán.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (item.LoaiPhieu != "THUE")
            {
                MessageBox.Show("Hóa đơn đặt phòng chỉ được thanh toán sau khi khách nhận phòng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show("Xác nhận thanh toán hóa đơn " + item.MaHoaDon + "?", "Thanh toán", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                thanhToanBUS.CheckOut(item.MaGoc);
                MessageBox.Show("Đã thanh toán hóa đơn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể thanh toán hóa đơn: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnInHoaDon_Click(object sender, RoutedEventArgs e)
        {
            HoaDonItem? item = LayHoaDonDangChon();
            if (item == null)
            {
                MessageBox.Show("Vui lòng chọn hóa đơn cần in.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            HoaDonPrintWindow window = new(item);
            DialogService.ShowDimmedDialogResult(window, Window.GetWindow(this));
        }

        private void BtnTachBill_Click(object sender, RoutedEventArgs e)
        {
            HoaDonItem? item = LayHoaDonDangChon();
            if (item == null)
            {
                MessageBox.Show("Vui lòng chọn hóa đơn cần tách bill.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TachBillWindow window = new(item);
            DialogService.ShowDimmedDialogResult(window, Window.GetWindow(this));
        }

        private void BtnXoaHoaDon_Click(object sender, RoutedEventArgs e)
        {
            HoaDonItem? item = LayHoaDonDangChon();
            if (item == null)
            {
                MessageBox.Show("Vui lòng chọn hóa đơn cần xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!TableExists("HOADON"))
            {
                MessageBox.Show("Hóa đơn đang được tổng hợp từ phiếu thuê/đặt phòng nên không xóa trực tiếp để tránh mất dữ liệu đặt phòng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox.Show("Bảng HOADON tồn tại nhưng chưa xác định được khóa chính an toàn để xóa. Vui lòng xóa trong màn hình quản trị dữ liệu hóa đơn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static void ThanhToanPhieuThue(int maThue)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();
            try
            {
                string setNgayTra = ColumnExists("PHIEUTHUE", "NgayTraPhong") ? ", NgayTraPhong = ISNULL(NgayTraPhong, @NgayTra)" : string.Empty;
                using (SqlCommand cmd = new("UPDATE dbo.PHIEUTHUE SET TrangThai = @TrangThai" + setNgayTra + " WHERE MaThue = @Ma", conn, tran))
                {
                    cmd.Parameters.AddWithValue("@TrangThai", "Đã trả phòng");
                    cmd.Parameters.AddWithValue("@NgayTra", DateTime.Now);
                    cmd.Parameters.AddWithValue("@Ma", maThue);
                    cmd.ExecuteNonQuery();
                }

                CapNhatTrangThaiPhongTheoNhomThue(conn, tran, maThue, "Chưa dọn dẹp");

                using (SqlCommand cmd = new(
                           @"UPDATE P
                             SET P.TrangThai = @TrangThaiPhong
                             FROM dbo.PHONG P
                             JOIN dbo.PHIEUTHUE PT ON P.MaPhong = PT.MaPhong
                             WHERE PT.MaThue = @Ma",
                           conn,
                           tran))
                {
                    cmd.Parameters.AddWithValue("@TrangThaiPhong", "Chưa dọn dẹp");
                    cmd.Parameters.AddWithValue("@Ma", maThue);
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

        private static string DichVuSubQuery(string keyExpr, string loaiPhieu)
        {
            string table = TableExists("PHATSINHDICHVU") ? "PHATSINHDICHVU" : TableExists("CHITIETPHATSINH") ? "CHITIETPHATSINH" : string.Empty;
            if (string.IsNullOrWhiteSpace(table))
            {
                return "CAST(0 AS decimal(18,2))";
            }

            string keyColumn = loaiPhieu == "THUE" && ColumnExists(table, "MaThue") ? "MaThue" : ColumnExists(table, "MaDatPhong") ? "MaDatPhong" : string.Empty;
            if (string.IsNullOrWhiteSpace(keyColumn))
            {
                return "CAST(0 AS decimal(18,2))";
            }

            string thanhTien = ColumnExists(table, "ThanhTien") ? "ThanhTien" : ColumnExists(table, "DonGia") && ColumnExists(table, "SoLuong") ? "(SoLuong * DonGia)" : "0";
            string markerFilter = loaiPhieu == "THUE" && ColumnExists(table, "GhiChu")
                ? ViewSchemaHelper.DichVuTheoLoaiHoaDonFilter("PS", "PHATSINH")
                : string.Empty;
            return "(SELECT ISNULL(SUM(" + thanhTien + "), 0) FROM dbo." + table + " PS WHERE PS." + keyColumn + " = " + keyExpr + markerFilter + ")";
        }

        private static string TienPhongSql(string startExpr, string plannedEndExpr, string actualEndExpr)
        {
            string giaNgayExpr = "ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0)";
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

        private static string GetFirstExistingColumn(string tableName, params string[] candidates)
        {
            return ViewSchemaHelper.GetFirstExistingColumn(tableName, candidates);
        }

        private static string TenPhongSql(string alias)
        {
            return ViewSchemaHelper.TenPhongSql(alias);
        }

        private sealed record HoaDonFilter(string Keyword, string Status, DateTime? TuNgay, DateTime? DenNgay);
    }

    public class HoaDonItem
    {
        public string LoaiPhieu { get; init; } = string.Empty;
        public string LoaiThanhToan { get; init; } = string.Empty;
        public int MaGoc { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public string MaPhieuThue { get; init; } = string.Empty;
        public string TenKhachHang { get; init; } = string.Empty;
        public string SoDienThoai { get; init; } = string.Empty;
        public string DiaChi { get; init; } = string.Empty;
        public string SoPhong { get; init; } = string.Empty;
        public string LoaiPhong { get; init; } = string.Empty;
        public DateTime NgayNhanPhong { get; init; }
        public DateTime NgayTraPhong { get; init; }
        public DateTime? NgayNhanThucTe { get; init; }
        public DateTime? NgayTraThucTe { get; init; }
        public decimal GiaGioTinhPhi { get; init; }
        public decimal GiaNgayTinhPhi { get; init; }
        public DateTime NgayLapHoaDon { get; init; }
        public decimal TienPhong { get; init; }
        public decimal TienPhongCheckIn { get; init; }
        public decimal TienDichVu { get; init; }
        public decimal PhuPhi { get; init; }
        public decimal PhuPhiTraMuon => NgayTraThucTe.HasValue
            ? PricingHelper.TinhPhuThuTraMuon(NgayNhanPhong, NgayTraPhong, NgayTraThucTe.Value, GiaGioTinhPhi, GiaNgayTinhPhi)
            : 0;
        public decimal ThueVat { get; init; }
        public decimal GiamGia { get; init; }
        public string TrangThai { get; init; } = string.Empty;
        public decimal TongTien => Math.Max(0, TienPhong + TienDichVu + PhuPhi + ThueVat - GiamGia);
        public bool DaThanhToan => TrangThai.Contains("Đã", StringComparison.OrdinalIgnoreCase) || TrangThai.Contains("Da", StringComparison.OrdinalIgnoreCase);
        public string NgayNhanText => NgayNhanPhong.ToString("dd/MM/yyyy");
        public string NgayTraText => NgayTraPhong.ToString("dd/MM/yyyy");
        public string NgayLapText => NgayLapHoaDon.ToString("dd/MM/yyyy");
        public string TienPhongText => TienPhong.ToString("N0");
        public string TienDichVuText => TienDichVu.ToString("N0");
        public string PhuPhiText => PhuPhi.ToString("N0");
        public string ThueVatText => ThueVat.ToString("N0");
        public string GiamGiaText => GiamGia.ToString("N0");
        public string TongTienText => TongTien.ToString("N0");
        public string TrangThaiBackground => DaThanhToan ? "#DCFCE7" : "#FEF3C7";
        public string TrangThaiForeground => DaThanhToan ? "#15803D" : "#B45309";

        public static HoaDonItem FromRow(DataRow row)
        {
            decimal tienPhongTinhLai = GetDecimal(row, "TienPhong");
            decimal tienPhongCheckInTinhLai = GetDecimal(row, "TienPhongCheckIn");
            decimal tienPhongDaChot = DocTienPhongDaChot(GetString(row, "GhiChu"));
            decimal tienPhong = tienPhongDaChot > 0 ? Math.Max(tienPhongDaChot, tienPhongTinhLai) : tienPhongTinhLai;
            decimal phuPhi = GetDecimal(row, "PhuPhi");
            decimal giamGia = GetDecimal(row, "GiamGia");
            decimal giamGiaTheoTienChot = tienPhongDaChot > 0 && giamGia > 0
                ? Math.Round((tienPhong + phuPhi) * 0.1m, 0)
                : giamGia;
            decimal thueVat = tienPhongDaChot > 0
                ? Math.Round(Math.Max(0, tienPhong + phuPhi - giamGiaTheoTienChot) * 0.1m, 0)
                : GetDecimal(row, "ThueVat");

            return new HoaDonItem
            {
                LoaiPhieu = GetString(row, "LoaiPhieu"),
                MaGoc = GetInt(row, "MaGoc"),
                MaHoaDon = GetString(row, "MaHoaDon"),
                MaPhieuThue = GetString(row, "MaPhieuThue"),
                TenKhachHang = GetString(row, "TenKhachHang"),
                SoDienThoai = GetString(row, "SoDienThoai"),
                DiaChi = GetString(row, "DiaChi"),
                SoPhong = GetString(row, "SoPhong"),
                LoaiPhong = GetString(row, "LoaiPhong"),
                NgayNhanPhong = GetDate(row, "NgayNhanPhong"),
                NgayTraPhong = GetDate(row, "NgayTraPhong"),
                NgayNhanThucTe = GetNullableDate(row, "NgayNhanThucTe"),
                NgayTraThucTe = GetNullableDate(row, "NgayTraThucTe"),
                GiaGioTinhPhi = GetDecimal(row, "GiaGioTinhPhi"),
                GiaNgayTinhPhi = GetDecimal(row, "GiaNgayTinhPhi"),
                NgayLapHoaDon = GetDate(row, "NgayLapHoaDon"),
                TienPhong = tienPhong,
                TienPhongCheckIn = tienPhongDaChot > 0 ? tienPhongDaChot : tienPhongCheckInTinhLai,
                TienDichVu = GetDecimal(row, "TienDichVu"),
                PhuPhi = phuPhi,
                ThueVat = thueVat,
                GiamGia = giamGiaTheoTienChot,
                TrangThai = GetString(row, "TrangThai")
            };
        }

        private static decimal DocTienPhongDaChot(string ghiChu)
        {
            return ViewSchemaHelper.DocTienPhongDaChot(ghiChu);
        }

        private static string GetString(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? row[column]?.ToString() ?? string.Empty : string.Empty;
        }

        private static int GetInt(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && int.TryParse(row[column]?.ToString(), out int value) ? value : 0;
        }

        private static decimal GetDecimal(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && decimal.TryParse(row[column]?.ToString(), out decimal value) ? value : 0;
        }

        private static DateTime GetDate(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && DateTime.TryParse(row[column]?.ToString(), out DateTime value) ? value : DateTime.Today;
        }

        private static DateTime? GetNullableDate(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && row[column] != DBNull.Value && DateTime.TryParse(row[column]?.ToString(), out DateTime value)
                ? value
                : null;
        }
    }
}
