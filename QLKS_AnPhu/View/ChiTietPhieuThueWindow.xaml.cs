using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DAL;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.View
{
    public partial class ChiTietPhieuThueWindow : Window
    {
        private readonly string loaiPhieu;
        private readonly int maGoc;
        private readonly PhongBUS phongBUS = new();
        private readonly ThanhToanFlowBUS thanhToanBUS = new();
        private DataRow? currentRow;
        private ThongTinPhongChiTiet? thongTinPhongHienTai;

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
                thongTinPhongHienTai = thongTinPhong;
                string trangThaiHienThi = GetString(currentRow, "TrangThaiHienThi");
                bool phieuDaHuy = LaTrangThaiHuy(trangThaiHienThi);
                decimal tienPhong = thongTinPhong.TienPhong > 0 ? thongTinPhong.TienPhong : GetDecimal(currentRow, "TienPhong");
                decimal phuThu = thongTinPhong.PhuPhi > 0 ? thongTinPhong.PhuPhi : GetDecimal(currentRow, "PhuPhi");
                decimal tienCocGiuLai = phieuDaHuy
                    ? HoaDonItem.LayTienCocGiuLaiTheoHoaDon(loaiPhieu, maGoc) ?? 0
                    : 0;
                decimal tienDichVu = phieuDaHuy ? 0 : LoadDichVuSuDung();
                if (phieuDaHuy)
                {
                    DgDichVu.ItemsSource = null;
                    TxtTongDichVu.Text = "Tổng cộng dịch vụ: 0 VND";
                    tienPhong = 0;
                    phuThu = 0;
                }
                decimal daThanhToanCheckIn = phieuDaHuy ? 0 : LayDaThanhToanLucCheckIn();
                decimal canThanhToanThem = phieuDaHuy ? 0 : LayCanThanhToanThemTrongLucThue();

                TxtTieuDe.Text = "Chi tiết phiếu thuê / đặt phòng";
                TxtMaPhieu.Text = TaoMaPhieuHienThi(GetString(currentRow, "MaPhieu"), thongTinPhong.SoLuongPhong);
                TxtTrangThai.Text = trangThaiHienThi;
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
                TxtLoaiPhieu.Text = TaoLoaiPhieuHienThi(loaiPhieu, thongTinPhong.SoLuongPhong);
                TxtDaThanhToanCheckIn.Text = daThanhToanCheckIn.ToString("N0") + " VND";
                TxtCanThanhToanThem.Text = canThanhToanThem.ToString("N0") + " VND";
                TxtGhiChu.Text = GetString(currentRow, "GhiChu");
                LoadLichSuGiaHan();
                LoadLichSuDoiPhong();

                bool daDat = string.Equals(TxtTrangThai.Text, "Đã đặt", StringComparison.OrdinalIgnoreCase);
                bool dangThue = string.Equals(TxtTrangThai.Text, "Đang thuê", StringComparison.OrdinalIgnoreCase);
                BtnNhanPhong.IsEnabled = loaiPhieu == "DAT" && daDat;
                BtnTraPhong.IsEnabled = loaiPhieu == "THUE" && dangThue;
                BtnGiaHanPhong.IsEnabled = loaiPhieu == "THUE" && dangThue && thongTinPhong.MaPhongDaiDien > 0;
                BtnDoiPhong.IsEnabled = ((loaiPhieu == "THUE" && dangThue) || (loaiPhieu == "DAT" && daDat)) && thongTinPhong.MaPhongDaiDien > 0;
                BtnHuyDat.IsEnabled = loaiPhieu == "DAT" && daDat;
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

        private static bool LaTrangThaiHuy(string trangThai)
        {
            string normalized = BoDau(trangThai).ToLowerInvariant();
            return normalized.Contains("huy") ||
                   normalized.Contains("no-show") ||
                   normalized.Contains("no show") ||
                   normalized.Contains("khach khong den");
        }

        private void LoadLichSuGiaHan()
        {
            DataTable table = TaoBangLichSuGiaHan();
            decimal tongTien = 0;
            int stt = 1;

            if (loaiPhieu == "THUE" && currentRow != null)
            {
                string ghiChu = GetString(currentRow, "GhiChu");
                foreach (Match match in Regex.Matches(ghiChu, @"\[GIAHAN\]\s*Tu=(?<tu>[^;]+);Den=(?<den>[^;]+);SoTien=(?<tien>-?\d+(?:[\.,]\d+)?)", RegexOptions.IgnoreCase))
                {
                    if (!TryParseIsoDate(match.Groups["tu"].Value, out DateTime tuNgay) ||
                        !TryParseIsoDate(match.Groups["den"].Value, out DateTime denNgay))
                    {
                        continue;
                    }

                    decimal soTien = ParseMoney(match.Groups["tien"].Value);
                    tongTien += soTien;
                    table.Rows.Add(
                        stt++,
                        tuNgay.ToString("dd/MM/yyyy HH:mm"),
                        denNgay.ToString("dd/MM/yyyy HH:mm"),
                        TinhThoiLuongGiaHan(tuNgay, denNgay),
                        soTien.ToString("N0") + " VND");
                }
            }

            DgGiaHan.ItemsSource = table.DefaultView;
            TxtTongGiaHan.Text = table.Rows.Count == 0
                ? "Chưa có lịch sử gia hạn."
                : $"Tổng gia hạn: {tongTien:N0} VND";
        }

        private void LoadLichSuDoiPhong()
        {
            DataTable table = TaoBangLichSuDoiPhong();
            decimal tongChenhLech = 0;

            if (loaiPhieu == "THUE" &&
                TableExists("DOIPHONG") &&
                ColumnExists("DOIPHONG", "MaThue") &&
                ColumnExists("DOIPHONG", "MaPhongCu") &&
                ColumnExists("DOIPHONG", "MaPhongMoi"))
            {
                string tenPhongCu = TenPhongSql("PC");
                string tenPhongMoi = TenPhongSql("PM");
                string lyDoExpr = ColumnExists("DOIPHONG", "LyDo") ? "ISNULL(DP.LyDo, N'')" : "CAST(N'' AS nvarchar(255))";
                string chenhLechExpr = ColumnExists("DOIPHONG", "ChenhLechTien") ? "ISNULL(DP.ChenhLechTien, 0)" : "CAST(0 AS decimal(18,2))";
                string thoiDiemExpr = ColumnExists("DOIPHONG", "ThoiDiemDoi") ? "DP.ThoiDiemDoi" : "GETDATE()";

                DataTable data = ConnectDB.GetData(
                    @"SELECT " + thoiDiemExpr + @" AS ThoiDiem,
                             " + tenPhongCu + @" AS PhongCu,
                             " + tenPhongMoi + @" AS PhongMoi,
                             " + lyDoExpr + @" AS LyDo,
                             " + chenhLechExpr + @" AS ChenhLechTien
                      FROM dbo.DOIPHONG DP
                      LEFT JOIN dbo.PHONG PC ON DP.MaPhongCu = PC.MaPhong
                      LEFT JOIN dbo.PHONG PM ON DP.MaPhongMoi = PM.MaPhong
                      WHERE DP.MaThue = @MaThue
                      ORDER BY " + thoiDiemExpr,
                    new SqlParameter("@MaThue", maGoc));

                int stt = 1;
                foreach (DataRow row in data.Rows)
                {
                    DateTime thoiDiem = GetDate(row, "ThoiDiem");
                    decimal chenhLech = GetDecimal(row, "ChenhLechTien");
                    tongChenhLech += chenhLech;
                    table.Rows.Add(
                        stt++,
                        thoiDiem.ToString("dd/MM/yyyy HH:mm"),
                        GetString(row, "PhongCu"),
                        GetString(row, "PhongMoi"),
                        chenhLech.ToString("N0") + " VND",
                        GetString(row, "LyDo"));
                }
            }

            DgDoiPhong.ItemsSource = table.DefaultView;
            TxtTongDoiPhong.Text = table.Rows.Count == 0
                ? "Chưa có lịch sử đổi phòng."
                : $"Tổng chênh lệch đổi phòng: {tongChenhLech:N0} VND";
        }

        private static DataTable TaoBangLichSuGiaHan()
        {
            DataTable table = new();
            table.Columns.Add("Lần", typeof(int));
            table.Columns.Add("Từ ngày", typeof(string));
            table.Columns.Add("Đến ngày", typeof(string));
            table.Columns.Add("Thời lượng", typeof(string));
            table.Columns.Add("Số tiền", typeof(string));
            return table;
        }

        private static DataTable TaoBangLichSuDoiPhong()
        {
            DataTable table = new();
            table.Columns.Add("Lần", typeof(int));
            table.Columns.Add("Thời điểm", typeof(string));
            table.Columns.Add("Phòng cũ", typeof(string));
            table.Columns.Add("Phòng mới", typeof(string));
            table.Columns.Add("Chênh lệch", typeof(string));
            table.Columns.Add("Lý do", typeof(string));
            return table;
        }

        private static bool TryParseIsoDate(string value, out DateTime date)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date) ||
                   DateTime.TryParse(value, out date);
        }

        private static decimal ParseMoney(string value)
        {
            string normalized = (value ?? string.Empty).Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal result)
                ? result
                : 0;
        }

        private static string TinhThoiLuongGiaHan(DateTime start, DateTime end)
        {
            if (end <= start)
            {
                return "0 giờ";
            }

            TimeSpan span = end - start;
            if (span.TotalHours < 24)
            {
                int hours = Math.Max(1, (int)Math.Ceiling(span.TotalHours));
                return hours + " giờ";
            }

            int days = Math.Max(1, (int)Math.Ceiling(span.TotalDays));
            return days + " ngày";
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
            string giaNgayExpr = "ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0))";
            string giaGioExpr = "ISNULL(NULLIF(LP.DonGiaGio, 0), " + giaNgayExpr + " / 24.0)";
            string giaDemExpr = "ISNULL(NULLIF(LP.DonGiaDem, 0), " + giaNgayExpr + ")";
            string phuPhiExpr = PricingHelper.PhuThuNhanSomSql("PT.NgayNhan", ngayNhanDuKienExpr, ngayTraDuKienExpr, giaNgayExpr, giaGioExpr, giaDemExpr);
            string diaChiExpr = ColumnExists("KHACHHANG", "DiaChi") ? "KH.DiaChi" : "CAST(NULL AS nvarchar(255))";
            string ghiChuExpr = ColumnExists("PHIEUTHUE", "GhiChu") ? "PT.GhiChu" : "CAST(NULL AS nvarchar(255))";
            string tenLoaiPhongExpr = ColumnExists("LOAIPHONG", "TenLoaiPhong") ? "LP.TenLoaiPhong" : "CAST(P.MaLoaiPhong AS nvarchar(50))";
            string maDatPhongExpr = coPhieuDatLienKet ? "PT.MaDatPhong" : "CAST(NULL AS int)";
            string maDoanExpr = coPhieuDatLienKet && ColumnExists(bangDatPhong, "MaDoan") ? "ISNULL(DP.MaDoan, 0)" : "CAST(0 AS int)";

            return @"SELECT N'PT' + RIGHT('000000' + CAST(PT.MaThue AS nvarchar(20)), 6) AS MaPhieu,
                            PT.MaThue AS MaThue,
                            PT.MaPhong AS MaPhong,
                            " + maDatPhongExpr + @" AS MaDatPhong,
                            " + maDoanExpr + @" AS MaDoan,
                            CASE
                                WHEN PT.TrangThai IN (N'Đã hủy', N'Da huy', N'Hủy', N'Huy', N'No-Show', N'No Show', N'Khach khong den') THEN N'Đã hủy'
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

        private static string TaoLoaiPhieuHienThi(string loaiPhieu, int soLuongPhong)
        {
            bool laDoan = soLuongPhong > 1;
            return loaiPhieu == "DAT"
                ? laDoan ? "Phiếu đặt phòng đoàn" : "Phiếu đặt phòng"
                : laDoan ? "Phiếu thuê đoàn" : "Phiếu thuê";
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
            string maDoanExpr = ColumnExists(bangDatPhong, "MaDoan") ? "ISNULL(DP.MaDoan, 0)" : "CAST(0 AS int)";
            string joinPhong = TableExists("CHITIETDATPHONG")
                ? @"JOIN dbo.CHITIETDATPHONG CT ON DP.MaDatPhong = CT.MaDatPhong
                     JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong"
                : "JOIN dbo.PHONG P ON DP.MaPhong = P.MaPhong";

            return @"SELECT N'DP' + RIGHT('000000' + CAST(DP.MaDatPhong AS nvarchar(20)), 6) AS MaPhieu,
                            DP.MaDatPhong AS MaDatPhong,
                            P.MaPhong AS MaPhong,
                            " + maDoanExpr + @" AS MaDoan,
                            CASE
                                WHEN DP.TrangThai IN (N'Đã hủy', N'Da huy', N'Hủy', N'Huy', N'No-Show', N'No Show', N'Khach khong den') THEN N'Đã hủy'
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
            string maPhongExpr = ColumnExists(table, "MaPhong")
                ? "PS.MaPhong"
                : keyColumn == "MaThue" && TableExists("PHIEUTHUE") && ColumnExists("PHIEUTHUE", "MaPhong")
                    ? "(SELECT TOP 1 PT0.MaPhong FROM dbo.PHIEUTHUE PT0 WHERE PT0.MaThue = PS.MaThue)"
                    : "CAST(NULL AS int)";
            string soPhongExpr = "ISNULL((SELECT TOP 1 " + TenPhongSql("P0") + " FROM dbo.PHONG P0 WHERE P0.MaPhong = " + maPhongExpr + "), N'--')";
            string whereDichVu = "PS." + keyColumn + " = @Ma";
            int parameterValue = maGoc;
            int maDoan = currentRow == null ? 0 : GetInt(currentRow, "MaDoan");

            if (loaiPhieu == "THUE" &&
                maDoan > 0 &&
                keyColumn == "MaThue" &&
                TableExists("PHIEUTHUE") &&
                ColumnExists("PHIEUTHUE", "MaDoan"))
            {
                whereDichVu = "EXISTS (SELECT 1 FROM dbo.PHIEUTHUE PT0 WHERE PT0.MaThue = PS.MaThue AND PT0.MaDoan = @Ma)";
                parameterValue = maDoan;
            }

            DataTable data = ConnectDB.GetData(
                @"SELECT " + soPhongExpr + @" AS [Phòng],
                         DV." + tenDichVu + @" AS [Tên dịch vụ],
                         " + soLuong + @" AS [SL],
                         " + donGia + @" AS [Đơn giá],
                         " + thanhTien + @" AS [Thành tiền]
                  FROM dbo." + table + @" PS
                  JOIN dbo.DICHVUVATTU DV ON PS." + maDvPs + " = DV." + maDv + @"
                  WHERE " + whereDichVu + @"
                  ORDER BY [Phòng], [Tên dịch vụ]",
                new SqlParameter("@Ma", parameterValue));
            DgDichVu.ItemsSource = data.DefaultView;
            decimal total = data.AsEnumerable().Sum(row => GetDecimal(row, "Thành tiền"));
            TxtTongDichVu.Text = "Tổng cộng dịch vụ: " + total.ToString("N0") + " VND";
            return total;
        }

        private decimal LayTongHoaDonCheckInDaChot(IEnumerable<int> maThueLienQuan)
        {
            if (loaiPhieu != "THUE" ||
                !TableExists("HOADON") ||
                !ColumnExists("HOADON", "MaThue"))
            {
                return 0;
            }

            string tongThanhToanColumn = ViewSchemaHelper.GetFirstExistingColumn(
                "HOADON",
                "TongThanhToan",
                "TongTien",
                "ThanhTien");
            string tienCocColumn = ViewSchemaHelper.GetFirstExistingColumn(
                "HOADON",
                "TienCoc",
                "TienDatCocTruoc",
                "DatCoc");

            if (string.IsNullOrWhiteSpace(tongThanhToanColumn))
            {
                return 0;
            }

            List<int> ids = maThueLienQuan.Where(value => value > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return 0;
            }

            List<SqlParameter> parameters = new();
            List<string> placeholders = new();
            for (int index = 0; index < ids.Count; index++)
            {
                string parameterName = "@MaThueHoaDon" + index;
                placeholders.Add(parameterName);
                parameters.Add(new SqlParameter(parameterName, ids[index]));
            }

            string tienCocExpr = string.IsNullOrWhiteSpace(tienCocColumn)
                ? "CAST(0 AS decimal(18,2))"
                : "ISNULL(" + tienCocColumn + ", 0)";
            object? value = ConnectDB.ExecuteScalar(
                "SELECT ISNULL(SUM(ISNULL(" + tongThanhToanColumn + ", 0) + " + tienCocExpr + "), 0) " +
                "FROM dbo.HOADON WHERE MaThue IN (" + string.Join(", ", placeholders) + ")",
                parameters.ToArray());

            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private decimal LayDaThanhToanLucCheckIn()
        {
            if (loaiPhieu != "THUE")
            {
                return 0;
            }

            List<int> maThueLienQuan = LayDanhSachMaThueLienQuan();
            if (!TableExists("CHITIETTHANHTOAN") ||
                !ColumnExists("CHITIETTHANHTOAN", "MaThue") ||
                !ColumnExists("CHITIETTHANHTOAN", "LoaiThanhToan"))
            {
                return LayTongHoaDonCheckInDaChot(maThueLienQuan);
            }

            string amountColumn = ColumnExists("CHITIETTHANHTOAN", "SoTien")
                ? "SoTien"
                : ColumnExists("CHITIETTHANHTOAN", "TienThanhToan")
                    ? "TienThanhToan"
                    : string.Empty;
            if (string.IsNullOrWhiteSpace(amountColumn))
            {
                return LayTongHoaDonCheckInDaChot(maThueLienQuan);
            }

            List<SqlParameter> parameters = new();
            List<string> placeholders = new();
            for (int index = 0; index < maThueLienQuan.Count; index++)
            {
                string parameterName = "@MaThue" + index;
                placeholders.Add(parameterName);
                parameters.Add(new SqlParameter(parameterName, maThueLienQuan[index]));
            }

            object? value = ConnectDB.ExecuteScalar(
                "SELECT ISNULL(SUM(" + amountColumn + "), 0) " +
                "FROM dbo.CHITIETTHANHTOAN " +
                "WHERE MaThue IN (" + string.Join(", ", placeholders) + ") " +
                "AND LoaiThanhToan IN (N'RoomCheckIn', N'ServiceCheckIn')",
                parameters.ToArray());
            decimal tienThuTaiCheckIn = value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
            if (tienThuTaiCheckIn <= 0)
            {
                return LayTongHoaDonCheckInDaChot(maThueLienQuan);
            }

            decimal tienCocDaThu = maThueLienQuan.Sum(LayTienCocHoaDonCheckIn);
            return tienThuTaiCheckIn + tienCocDaThu;
        }

        private static decimal LayTienCocHoaDonCheckIn(int maThue)
        {
            if (!TableExists("HOADON") || !ColumnExists("HOADON", "MaThue"))
            {
                return 0;
            }

            string keyColumn = ViewSchemaHelper.GetFirstExistingColumn(
                "HOADON",
                "MaHoaDon",
                "MaHD",
                "IDHoaDon",
                "HoaDonID",
                "ID",
                "Ma");
            string depositColumn = ViewSchemaHelper.GetFirstExistingColumn(
                "HOADON",
                "TienCoc",
                "TienDatCocTruoc",
                "DatCoc");
            if (string.IsNullOrWhiteSpace(keyColumn) || string.IsNullOrWhiteSpace(depositColumn))
            {
                return 0;
            }

            object? value = ConnectDB.ExecuteScalar(
                "SELECT TOP 1 ISNULL(" + depositColumn + ", 0) " +
                "FROM dbo.HOADON WHERE MaThue = @MaThue ORDER BY " + keyColumn + " ASC",
                new SqlParameter("@MaThue", maThue));
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private decimal LayCanThanhToanThemTrongLucThue()
        {
            if (loaiPhieu != "THUE" ||
                currentRow == null ||
                !string.Equals(GetString(currentRow, "TrangThaiHienThi"), "Đang thuê", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            decimal total = 0;
            foreach (int maThue in LayDanhSachMaThueLienQuan())
            {
                total += thanhToanBUS.DuToanCheckOut(maThue).CanThuThem;
            }
            return total;
        }

        private List<int> LayDanhSachMaThueLienQuan()
        {
            List<int> result = new() { maGoc };
            if (loaiPhieu != "THUE" || currentRow == null || !TableExists("PHIEUTHUE"))
            {
                return result;
            }

            int maDoan = GetInt(currentRow, "MaDoan");
            if (maDoan <= 0)
            {
                return result;
            }

            DataTable data;
            if (ColumnExists("PHIEUTHUE", "MaDoan"))
            {
                data = ConnectDB.GetData(
                    "SELECT DISTINCT MaThue FROM dbo.PHIEUTHUE WHERE MaDoan = @MaDoan",
                    new SqlParameter("@MaDoan", maDoan));
            }
            else
            {
                string bangDatPhong = TableExists("PHIEUDATPHONG")
                    ? "PHIEUDATPHONG"
                    : TableExists("DATPHONG")
                        ? "DATPHONG"
                        : string.Empty;
                if (string.IsNullOrWhiteSpace(bangDatPhong) ||
                    !ColumnExists("PHIEUTHUE", "MaDatPhong") ||
                    !ColumnExists(bangDatPhong, "MaDoan"))
                {
                    return result;
                }

                data = ConnectDB.GetData(
                    "SELECT DISTINCT PT.MaThue " +
                    "FROM dbo.PHIEUTHUE PT " +
                    "JOIN dbo." + bangDatPhong + " DP ON PT.MaDatPhong = DP.MaDatPhong " +
                    "WHERE DP.MaDoan = @MaDoan",
                    new SqlParameter("@MaDoan", maDoan));
            }

            List<int> ids = data.AsEnumerable()
                .Select(row => GetInt(row, "MaThue"))
                .Where(value => value > 0)
                .Distinct()
                .ToList();
            return ids.Count > 0 ? ids : result;
        }

        private ThongTinPhongChiTiet LayThongTinPhongChiTiet()
        {
            ThongTinPhongChiTiet fallback = new()
            {
                SoPhong = GetString(currentRow!, "SoPhong"),
                TenLoaiPhong = GetString(currentRow!, "TenLoaiPhong"),
                SoLuongPhong = 1,
                TienPhong = GetDecimal(currentRow!, "TienPhong"),
                PhuPhi = GetDecimal(currentRow!, "PhuPhi"),
                MaPhongDaiDien = GetInt(currentRow!, "MaPhong"),
                MaDatPhong = GetNullableInt(currentRow!, "MaDatPhong")
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
                    ? rooms.AsEnumerable().Sum(row => PricingHelper.TinhPhuThuNhanSom(ngayNhanThucTe.Value, ngayNhanDuKien, ngayTraDuKien, GetDecimal(row, "DonGiaGio"), GetDecimal(row, "DonGiaNgay"), GetDecimal(row, "DonGiaDem")))
                    : 0;
                DataRow firstRoom = rooms.Rows[0];
                return new ThongTinPhongChiTiet
                {
                    SoPhong = string.Join(", ", soPhong),
                    TenLoaiPhong = string.Join(", ", loaiPhong),
                    SoLuongPhong = soPhong.Count,
                    TienPhong = tienPhong,
                    PhuPhi = phuPhi,
                    MaPhongDaiDien = GetInt(firstRoom, "MaPhong"),
                    MaDatPhong = GetNullableInt(currentRow!, "MaDatPhong"),
                    GiaGio = GetDecimal(firstRoom, "DonGiaGio"),
                    GiaNgay = GetDecimal(firstRoom, "DonGiaNgay"),
                    GiaDem = GetDecimal(firstRoom, "DonGiaDem")
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
            string giaDemExpr = "ISNULL(NULLIF(LP.DonGiaDem, 0), " + giaNgayExpr + ")";
            bool coMaDatPhong = ColumnExists("PHIEUTHUE", "MaDatPhong");

            if (coMaDatPhong &&
                !string.IsNullOrWhiteSpace(bangDatPhong) &&
                ColumnExists(bangDatPhong, "MaDoan") &&
                (TableExists("CHITIETDATPHONG") || ColumnExists(bangDatPhong, "MaPhong")))
            {
                string joinPhongDoan = TableExists("CHITIETDATPHONG")
                    ? @"JOIN dbo.CHITIETDATPHONG CT ON DPG.MaDatPhong = CT.MaDatPhong
JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong"
                    : "JOIN dbo.PHONG P ON DPG.MaPhong = P.MaPhong";
                DataTable theoDoan = ConnectDB.GetData(@"
SELECT P.MaPhong AS MaPhong,
       " + tenPhongExpr + @" AS SoPhong,
       " + tenLoaiPhongExpr + @" AS TenLoaiPhong,
       ISNULL(LP.DonGiaGio, 0) AS DonGiaGio,
       " + giaNgayExpr + @" AS DonGiaNgay,
       " + giaDemExpr + @" AS DonGiaDem
FROM dbo.PHIEUTHUE PT
JOIN dbo." + bangDatPhong + @" DP0 ON PT.MaDatPhong = DP0.MaDatPhong
JOIN dbo." + bangDatPhong + @" DPG ON DPG.MaDoan = DP0.MaDoan AND ISNULL(DPG.MaDoan, 0) > 0
" + joinPhongDoan + @"
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE PT.MaThue = @Ma
ORDER BY P.MaPhong", new SqlParameter("@Ma", maGoc));
                if (theoDoan.Rows.Count > 0)
                {
                    return theoDoan;
                }
            }

            if (coMaDatPhong && !string.IsNullOrWhiteSpace(bangDatPhong) && ColumnExists(bangDatPhong, "MaPhong"))
            {
                string ngayNhanColumn = ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong";
                string ngayTraColumn = ColumnExists(bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong";
                DataTable nhomCu = ConnectDB.GetData(@"
SELECT P.MaPhong AS MaPhong,
       " + tenPhongExpr + @" AS SoPhong,
       " + tenLoaiPhongExpr + @" AS TenLoaiPhong,
       ISNULL(LP.DonGiaGio, 0) AS DonGiaGio,
       " + giaNgayExpr + @" AS DonGiaNgay,
       " + giaDemExpr + @" AS DonGiaDem
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
SELECT P.MaPhong AS MaPhong,
       " + tenPhongExpr + @" AS SoPhong,
       " + tenLoaiPhongExpr + @" AS TenLoaiPhong,
       ISNULL(LP.DonGiaGio, 0) AS DonGiaGio,
       " + giaNgayExpr + @" AS DonGiaNgay,
       " + giaDemExpr + @" AS DonGiaDem
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
SELECT P.MaPhong AS MaPhong,
       " + tenPhongExpr + @" AS SoPhong,
       " + tenLoaiPhongExpr + @" AS TenLoaiPhong,
       ISNULL(LP.DonGiaGio, 0) AS DonGiaGio,
       " + giaNgayExpr + @" AS DonGiaNgay,
       " + giaDemExpr + @" AS DonGiaDem
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
            string giaDemExpr = "ISNULL(NULLIF(LP.DonGiaDem, 0), " + giaNgayExpr + ")";

            if (ColumnExists(bangDatPhong, "MaDoan") &&
                (TableExists("CHITIETDATPHONG") || ColumnExists(bangDatPhong, "MaPhong")))
            {
                string joinPhongDoan = TableExists("CHITIETDATPHONG")
                    ? @"JOIN dbo.CHITIETDATPHONG CT ON DPG.MaDatPhong = CT.MaDatPhong
JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong"
                    : "JOIN dbo.PHONG P ON DPG.MaPhong = P.MaPhong";
                DataTable theoDoan = ConnectDB.GetData(@"
SELECT P.MaPhong AS MaPhong,
       " + tenPhongExpr + @" AS SoPhong,
       " + tenLoaiPhongExpr + @" AS TenLoaiPhong,
       ISNULL(LP.DonGiaGio, 0) AS DonGiaGio,
       " + giaNgayExpr + @" AS DonGiaNgay,
       " + giaDemExpr + @" AS DonGiaDem
FROM dbo." + bangDatPhong + @" DP0
JOIN dbo." + bangDatPhong + @" DPG ON DPG.MaDoan = DP0.MaDoan AND ISNULL(DPG.MaDoan, 0) > 0
" + joinPhongDoan + @"
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE DP0.MaDatPhong = @Ma
ORDER BY P.MaPhong", new SqlParameter("@Ma", maGoc));
                if (theoDoan.Rows.Count > 0)
                {
                    return theoDoan;
                }
            }

            if (ColumnExists(bangDatPhong, "MaPhong"))
            {
                string ngayNhanColumn = ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong";
                string ngayTraColumn = ColumnExists(bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong";
                DataTable nhomCu = ConnectDB.GetData(@"
SELECT P.MaPhong AS MaPhong,
       " + tenPhongExpr + @" AS SoPhong,
       " + tenLoaiPhongExpr + @" AS TenLoaiPhong,
       ISNULL(LP.DonGiaGio, 0) AS DonGiaGio,
       " + giaNgayExpr + @" AS DonGiaNgay,
       " + giaDemExpr + @" AS DonGiaDem
FROM dbo." + bangDatPhong + @" DP0
JOIN dbo." + bangDatPhong + @" DPG ON DPG.MaKH = DP0.MaKH
    AND CONVERT(date, DPG." + ngayNhanColumn + @") = CONVERT(date, DP0." + ngayNhanColumn + @")
    AND CONVERT(date, DPG." + ngayTraColumn + @") = CONVERT(date, DP0." + ngayTraColumn + @")
JOIN dbo.PHONG P ON DPG.MaPhong = P.MaPhong
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE DP0.MaDatPhong = @Ma
ORDER BY P.MaPhong", new SqlParameter("@Ma", maGoc));
                if (nhomCu.Rows.Count > 1)
                {
                    return nhomCu;
                }
            }

            if (TableExists("CHITIETDATPHONG"))
            {
                return ConnectDB.GetData(@"
SELECT P.MaPhong AS MaPhong,
       " + tenPhongExpr + @" AS SoPhong,
       " + tenLoaiPhongExpr + @" AS TenLoaiPhong,
       ISNULL(LP.DonGiaGio, 0) AS DonGiaGio,
       " + giaNgayExpr + @" AS DonGiaNgay,
       " + giaDemExpr + @" AS DonGiaDem
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

                KetQuaCheckInThanhToanDTO result = phongBUS.NhanPhongTuDatPhong(
                    maGoc,
                    tongTienDuKien,
                    tienDatCocTruoc,
                    Math.Max(0, tienPhongCheckIn + phuThuCheckIn - giamGiaCheckIn));
                HoaDonItem billSauThanhToan = TaoBillCheckInSauThanhToan(
                    result,
                    tienPhongCheckIn,
                    tienDichVuCheckIn,
                    phuThuCheckIn,
                    tienDatCocTruoc,
                    giamGiaCheckIn);
                DuLieuDaThayDoi = true;
                MessageBox.Show("Đã nhận phòng. Phòng đã chuyển sang trạng thái đang thuê.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                HienThiBillSauThanhToan(billSauThanhToan);
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
                DuToanCheckOutDTO duToan = thanhToanBUS.DuToanCheckOut(maGoc);
                ThanhToanCheckOutWindow dialog = new(duToan);
                if (DialogService.ShowDimmedDialogResult(dialog, this) != true)
                {
                    return;
                }

                HoaDonItem? billSauThanhToan = dialog.ThanhToanSau ? null : TaoBillPhatSinhSauThanhToan(duToan);
                KetQuaCheckOutThanhToanDTO result = thanhToanBUS.CheckOut(maGoc, !dialog.ThanhToanSau);
                if (billSauThanhToan != null && result.DaThanhToan)
                {
                    billSauThanhToan = HoaDonItem.TaoCheckOutTam(billSauThanhToan, result.MaHoaDon);
                }
                DuLieuDaThayDoi = true;
                MessageBox.Show("Đã trả phòng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                if (billSauThanhToan != null && result.DaThanhToan)
                {
                    HienThiBillSauThanhToan(billSauThanhToan);
                }
                LoadChiTiet();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể trả phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnHuyDat_Click(object sender, RoutedEventArgs e)
        {
            if (loaiPhieu != "DAT")
            {
                return;
            }

            List<int> maDatPhongCanHuy = LayMaDatPhongLienQuan();
            if (maDatPhongCanHuy.Count == 0)
            {
                MessageBox.Show("Không tìm thấy phiếu đặt đang chờ nhận phòng để hủy.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                List<DuToanHuyDatPhongDTO> duToanHuy = maDatPhongCanHuy
                    .Select(maDatPhong => phongBUS.DuToanHuyDatPhong(maDatPhong))
                    .ToList();
                HuyDatPhongWindow confirm = new(duToanHuy);
                if (DialogService.ShowDimmedDialogResult(confirm, this) != true)
                {
                    return;
                }

                foreach (int maDatPhong in maDatPhongCanHuy)
                {
                    phongBUS.NoShow(maDatPhong);
                }

                DuLieuDaThayDoi = true;
                MessageBox.Show("Đã hủy đặt phòng và chuyển phòng về trạng thái trống.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadChiTiet();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể hủy đặt phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnGiaHanPhong_Click(object sender, RoutedEventArgs e)
        {
            if (loaiPhieu != "THUE" || currentRow == null)
            {
                return;
            }

            ThongTinPhongChiTiet thongTinPhong = thongTinPhongHienTai ?? LayThongTinPhongChiTiet();
            if (thongTinPhong.MaPhongDaiDien <= 0)
            {
                MessageBox.Show("Không xác định được phòng để gia hạn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime ngayTraDuKien = GetDate(currentRow, "NgayTraDuKien");
            GiaHanPhongWindow window = new(
                new GiaHanPhongRequestDTO
                {
                    MaThue = maGoc,
                    MaDatPhong = thongTinPhong.MaDatPhong,
                    MaPhong = thongTinPhong.MaPhongDaiDien,
                    NgayTraCu = ngayTraDuKien,
                    NgayTraMoi = ngayTraDuKien,
                    GiaGio = thongTinPhong.GiaGio,
                    GiaNgay = thongTinPhong.GiaNgay,
                    GiaDem = thongTinPhong.GiaDem,
                    SoPhongGiaHan = Math.Max(1, thongTinPhong.SoLuongPhong)
                },
                LaySoPhongDaiDien(thongTinPhong));

            DialogService.ShowDimmedDialogResult(window, this);
            if (window.DuLieuDaThayDoi)
            {
                DuLieuDaThayDoi = true;
                LoadChiTiet();
            }
        }

        private void BtnDoiPhong_Click(object sender, RoutedEventArgs e)
        {
            if (currentRow == null)
            {
                return;
            }

            ThongTinPhongChiTiet thongTinPhong = thongTinPhongHienTai ?? LayThongTinPhongChiTiet();
            if (thongTinPhong.MaPhongDaiDien <= 0)
            {
                MessageBox.Show("Không xác định được phòng hiện tại để đổi.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime ngayTraDuKien = GetDate(currentRow, "NgayTraDuKien");
            DoiPhongWindow window = new(
                new DoiPhongRequestDTO
                {
                    MaThue = loaiPhieu == "THUE" ? maGoc : 0,
                    MaDatPhong = thongTinPhong.MaDatPhong ?? (loaiPhieu == "DAT" ? maGoc : null),
                    MaPhongCu = thongTinPhong.MaPhongDaiDien,
                    NgayBatDau = loaiPhieu == "THUE" ? DateTime.Now : GetDate(currentRow, "NgayNhanDuKien"),
                    NgayTraDuKien = ngayTraDuKien
                },
                LaySoPhongDaiDien(thongTinPhong));

            DialogService.ShowDimmedDialogResult(window, this);
            if (window.DuLieuDaThayDoi)
            {
                DuLieuDaThayDoi = true;
                LoadChiTiet();
            }
        }

        private void HienThiBillSauThanhToan(HoaDonItem hoaDon)
        {
            HoaDonPrintWindow window = new(hoaDon);
            DialogService.ShowDimmedDialogResult(window, this);
        }

        private HoaDonItem TaoBillCheckInSauThanhToan(
            KetQuaCheckInThanhToanDTO result,
            decimal tienPhong,
            decimal tienDichVu,
            decimal phuPhi,
            decimal tienCoc,
            decimal giamGia)
        {
            ThongTinPhongChiTiet thongTinPhong = LayThongTinPhongChiTiet();
            return HoaDonItem.TaoCheckInTam(
                result.MaHoaDon > 0 ? "HD-" + result.MaHoaDon.ToString("0000") : "HD-TAM-" + maGoc,
                result.MaThue ?? maGoc,
                GetString(currentRow!, "HoTen"),
                GetString(currentRow!, "SDT"),
                GetString(currentRow!, "DiaChi"),
                thongTinPhong.SoPhong,
                thongTinPhong.TenLoaiPhong,
                GetDate(currentRow!, "NgayNhanDuKien"),
                GetDate(currentRow!, "NgayTraDuKien"),
                string.Empty,
                0,
                0,
                0,
                tienPhong,
                tienDichVu,
                phuPhi,
                tienCoc,
                giamGia);
        }

        private HoaDonItem TaoBillPhatSinhSauThanhToan(DuToanCheckOutDTO duToan)
        {
            ThongTinPhongChiTiet thongTinPhong = LayThongTinPhongChiTiet();
            decimal tienPhong = duToan.TienPhongPhatSinh;

            return new HoaDonItem
            {
                LoaiPhieu = "THUE",
                LoaiThanhToan = "PHATSINH",
                MaGoc = maGoc,
                MaHoaDon = duToan.MaHoaDon > 0 ? "HD-" + duToan.MaHoaDon.ToString("0000") : "HD-TAM-" + maGoc,
                MaPhieuThue = "PT-" + maGoc,
                TenKhachHang = GetString(currentRow!, "HoTen"),
                SoDienThoai = GetString(currentRow!, "SDT"),
                DiaChi = GetString(currentRow!, "DiaChi"),
                SoPhong = thongTinPhong.SoPhong,
                LoaiPhong = thongTinPhong.TenLoaiPhong,
                NgayNhanPhong = GetDate(currentRow!, "NgayNhanDuKien"),
                NgayTraPhong = duToan.NgayTraDuKien,
                CheDoDatPhong = duToan.CheDoDatPhong,
                NgayNhanThucTe = GetNullableDate(currentRow!, "NgayNhanThucTe"),
                NgayTraThucTe = duToan.NgayTraThucTe,
                GiaGioTinhPhi = duToan.GiaGio,
                GiaNgayTinhPhi = duToan.GiaNgay,
                GiaDemTinhPhi = duToan.GiaDem,
                NgayLapHoaDon = DateTime.Now,
                TienPhong = tienPhong,
                TienDichVu = duToan.TienDichVuPhatSinh,
                PhuPhi = duToan.PhuPhiTraMuon,
                ThueVat = duToan.ThueVat,
                GiamGia = 0,
                TienCoc = 0,
                TrangThai = "Da thanh toan"
            };
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
                string trangThaiPhong = "Trống";
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

                if (ColumnExists("PHONG", "GhiChu"))
                {
                    using SqlCommand note = new(
                        @"UPDATE P
                          SET P.GhiChu = CONCAT(NULLIF(P.GhiChu, N''), CASE WHEN NULLIF(P.GhiChu, N'') IS NULL THEN N'' ELSE N' - ' END, N'[CAN_DON_DEP] Can don dep sau khi tra phong')
                          FROM dbo.PHONG P
                          JOIN dbo.PHIEUTHUE PT ON P.MaPhong = PT.MaPhong
                          WHERE PT.MaThue = @Ma",
                        conn,
                        tran);
                    note.Parameters.AddWithValue("@Ma", maGoc);
                    note.ExecuteNonQuery();
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

        private List<int> LayMaDatPhongLienQuan()
        {
            if (currentRow == null)
            {
                return new List<int>();
            }

            int maDatPhong = GetInt(currentRow, "MaDatPhong");
            if (maDatPhong <= 0 && loaiPhieu == "DAT")
            {
                maDatPhong = maGoc;
            }

            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists("DATPHONG") ? "DATPHONG" : string.Empty;
            int maDoan = GetInt(currentRow, "MaDoan");
            if (!string.IsNullOrWhiteSpace(bangDatPhong) && maDoan > 0 && ColumnExists(bangDatPhong, "MaDoan"))
            {
                DataTable data = ConnectDB.GetData(
                    "SELECT MaDatPhong FROM dbo." + bangDatPhong + " WHERE MaDoan = @MaDoan ORDER BY MaDatPhong",
                    new SqlParameter("@MaDoan", maDoan));
                List<int> ids = data.AsEnumerable()
                    .Select(row => GetInt(row, "MaDatPhong"))
                    .Where(value => value > 0)
                    .Distinct()
                    .ToList();
                if (ids.Count > 0)
                {
                    return ids;
                }
            }

            return maDatPhong > 0 ? new List<int> { maDatPhong } : new List<int>();
        }

        private static string LaySoPhongDaiDien(ThongTinPhongChiTiet thongTinPhong)
        {
            string[] parts = thongTinPhong.SoPhong
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length > 0 ? parts[0] : thongTinPhong.SoPhong;
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

        private static int GetInt(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) &&
                   row[column] != DBNull.Value &&
                   int.TryParse(row[column]?.ToString(), out int value)
                ? value
                : 0;
        }

        private static int? GetNullableInt(DataRow row, string column)
        {
            int value = GetInt(row, column);
            return value > 0 ? value : null;
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
            public int MaPhongDaiDien { get; set; }
            public int? MaDatPhong { get; set; }
            public decimal GiaGio { get; set; }
            public decimal GiaNgay { get; set; }
            public decimal GiaDem { get; set; }
        }
    }
}
