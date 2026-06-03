using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DAL;
using QLKS_AnPhu.DTO;
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
            CboLoaiPhong.Items.Add("Tất cả loại phòng");
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
                TxtTongBanGhi.Text = $"Tổng số: {danhSachPhieu.Rows.Count} bản ghi";
            }
            catch (Exception ex)
            {
                DgPhieuThue.ItemsSource = null;
                TxtTongBanGhi.Text = "Không tải được dữ liệu.";
                MessageBox.Show("Không thể tải danh sách phiếu thuê.\nChi tiết: " + ex.Message, "Lỗi dữ liệu", MessageBoxButton.OK, MessageBoxImage.Error);
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

            List<string> queries = new();

            if (coPhieuThue)
            {
                string ngayTraThucTe = ColumnExists("PHIEUTHUE", "NgayTraPhong") ? "ISNULL(PT.NgayTraPhong, PT.NgayTraDuKien)" : "PT.NgayTraDuKien";
                string tienPhongExpr = TienPhongSql("PT.NgayNhan", "PT.NgayTraDuKien", ngayTraThucTe);
                bool thueCoChiTietDoan = coChiTietDatPhong && ColumnExists("PHIEUTHUE", "MaDatPhong");
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
                string ngayNhanDatColumn = ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong";
                string ngayTraDatColumn = ColumnExists(bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong";
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
                string tienPhongNhomDatCuExpr = TienPhongSql("PT.NgayNhan", "PT.NgayTraDuKien", ngayTraThucTe, tongGiaNgayNhomDatCuExpr, tongGiaGioNhomDatCuExpr);
                string tienPhongDoanThueExpr = TienPhongSql("PT.NgayNhan", "PT.NgayTraDuKien", ngayTraThucTe, tongGiaNgayThueExpr, tongGiaGioThueExpr);
                string tienPhongThueExpr = thueCoChiTietDoan || thueCoNhomDatCu
                    ? @"CASE WHEN PT.MaDatPhong IS NOT NULL AND " + demNhomDatCuExpr + @" > 1 THEN
                            " + tienPhongNhomDatCuExpr + @"
                           WHEN PT.MaDatPhong IS NOT NULL AND " + tonTaiChiTietThueExpr + @" THEN
                            " + tienPhongDoanThueExpr + @"
                           ELSE " + tienPhongExpr + " END"
                    : tienPhongExpr;
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
                string maPhieuThueExpr = thueCoChiTietDoan || thueCoNhomDatCu
                    ? @"CASE WHEN (" + soLuongPhongThueExpr + @") > 1 THEN
                            N'PTTD' + RIGHT('000000' + CAST(PT.MaThue AS nvarchar(20)), 6)
                           ELSE N'PT' + RIGHT('000000' + CAST(PT.MaThue AS nvarchar(20)), 6) END"
                    : "N'PT' + RIGHT('000000' + CAST(PT.MaThue AS nvarchar(20)), 6)";
                string loaiPhongThueExpr = thueCoNhomDatCu
                    ? @"CASE WHEN PT.MaDatPhong IS NOT NULL AND " + demNhomDatCuExpr + @" > 1 THEN
                            " + loaiPhongNhomDatCuExpr + @"
                           ELSE " + tenLoaiPhongExpr + " END"
                    : tenLoaiPhongExpr;
                queries.Add(@"
SELECT 'THUE' AS LoaiPhieu,
       PT.MaThue AS MaGoc,
       " + maPhieuThueExpr + @" AS MaPhieuThue,
       KH.HoTen AS TenKhachHang,
       KH.SDT AS SoDienThoai,
       " + tenPhongThueExpr + @" AS TenPhong,
       " + soLuongPhongThueExpr + @" AS SoLuongPhong,
       " + loaiPhongThueExpr + @" AS LoaiPhong,
       PT.NgayNhan AS NgayNhanPhong,
       PT.NgayTraDuKien AS NgayTraPhong,
       ISNULL(PT.TienCoc, 0) AS DatCoc,
       " + tienPhongThueExpr + @" AS TongTamTinh,
       " + ghiChuThueExpr + @" AS GhiChu,
       CASE
           WHEN PT.TrangThai IN (N'Đã hủy', N'Da huy') THEN N'Đã hủy'
           WHEN PT.TrangThai IN (N'Đã trả', N'Da tra', N'Đã trả phòng', N'Da tra phong') THEN N'Đã trả phòng'
           WHEN PT.TrangThai IN (N'Đang thuê', N'Dang thue') OR P.TrangThai IN (N'Có khách', N'Co khach', N'Đang thuê', N'Dang thue') THEN N'Đang thuê'
           WHEN PT.TrangThai IN (N'Đã đặt', N'Da dat', N'Đã xác nhận', N'Da xac nhan') THEN N'Đã đặt'
           ELSE ISNULL(PT.TrangThai, N'Đang thuê')
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
                string tienPhongDonDatExpr = coMaPhongDat ? TienPhongSql(ngayNhan, ngayTra, ngayTra) : "CAST(0 AS decimal(18,2))";
                string tienPhongDoanDatExpr = TienPhongSql(ngayNhan, ngayTra, ngayTra, tongGiaNgayDatExpr, tongGiaGioDatExpr);
                string tienPhongDat = coChiTietDatPhong
                    ? "CASE WHEN " + tonTaiChiTietDatExpr + " THEN " + tienPhongDoanDatExpr + " ELSE " + tienPhongDonDatExpr + " END"
                    : tienPhongDonDatExpr;
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
                                  AND P2.TrangThai IN (N'Có khách', N'Co khach', N'Đang thuê', N'Dang thue'))"
                    : "P.TrangThai IN (N'Có khách', N'Co khach', N'Đang thuê', N'Dang thue')";
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
       N'DP' + RIGHT('000000' + CAST(DP.MaDatPhong AS nvarchar(20)), 6) AS MaPhieuThue,
       KH.HoTen AS TenKhachHang,
       KH.SDT AS SoDienThoai,
       " + tenPhongDatExpr + @" AS TenPhong,
       " + soLuongPhongExpr + @" AS SoLuongPhong,
       " + loaiPhongDatExpr + @" AS LoaiPhong,
       " + ngayNhan + @" AS NgayNhanPhong,
       " + ngayTra + @" AS NgayTraPhong,
       ISNULL(" + tienCoc + @", 0) AS DatCoc,
       " + tienPhongDat + @" AS TongTamTinh,
       " + ghiChuDatExpr + @" AS GhiChu,
       CASE
           WHEN DP.TrangThai IN (N'Đã hủy', N'Da huy') THEN N'Đã hủy'
           WHEN DP.TrangThai IN (N'Đã trả', N'Da tra', N'Đã trả phòng', N'Da tra phong') THEN N'Đã trả phòng'
           WHEN " + phongDangThueExpr + @" THEN N'Đang thuê'
           ELSE N'Đã đặt'
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
                DatGiaTriNeuCo(result, newRow, "MaPhieuThue", loaiPhieu == "THUE"
                    ? TaoMaPhieuThueDoan(LayInt(first, "MaGoc"))
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
            return CboTrangThai.SelectedItem is ComboBoxItem item && item.Content?.ToString() != "Tất cả"
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

        private void TxtTimKiem_TextChanged(object sender, TextChangedEventArgs e)
        {
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
                    !item.TrangThai.Contains("thuê", StringComparison.OrdinalIgnoreCase) &&
                    !item.TrangThai.Contains("thue", StringComparison.OrdinalIgnoreCase) &&
                    !item.TrangThai.Contains("đặt", StringComparison.OrdinalIgnoreCase) &&
                    !item.TrangThai.Contains("dat", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không tải được danh sách phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (phongTrong == null)
            {
                MessageBox.Show("Không có phòng trống để đặt.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UCDatPhongMoi ucDatPhong = new(phongTrong);
            ucDatPhong.CloseRequested += UcDatPhong_CloseRequested;
            ucDatPhong.DatPhongRequested += UcDatPhong_DatPhongRequested;

            Window dialog = DialogService.CreateContentDialog(ucDatPhong, "Đặt phòng mới", 1100, 650);
            DialogService.ShowDimmedDialogResult(dialog, Window.GetWindow(this));

            ucDatPhong.CloseRequested -= UcDatPhong_CloseRequested;
            ucDatPhong.DatPhongRequested -= UcDatPhong_DatPhongRequested;
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
                if (sender is UCDatPhongMoi ucDatPhong)
                {
                    DatPhongRequestDTO request = ucDatPhong.TaoYeuCauDatPhong();
                    if (ucDatPhong.NhanNgay)
                    {
                        phongBUS.NhanPhong(request);
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

                if (sender is FrameworkElement element)
                {
                    Window.GetWindow(element)?.Close();
                }
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không đặt được phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
    }
}
