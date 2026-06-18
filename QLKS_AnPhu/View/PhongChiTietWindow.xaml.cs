using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DAL;
using QLKS_AnPhu.DTO;
using QLKS_AnPhu.Security;

namespace QLKS_AnPhu.View
{
    public partial class PhongChiTietWindow : Window
    {
        private readonly PhongDTO phong;
        private readonly PhongBUS phongBUS = new();
        private readonly DichVuVatTuBUS dichVuVatTuBUS = new();
        private readonly ThanhToanFlowBUS thanhToanBUS = new();
        private readonly DispatcherTimer thoiGianLuuTruTimer = new();
        private ObservableCollection<DichVuChiTietItem> dichVuHienTai = new();
        private RoomStayData? stayData;
        private decimal tienPhong;
        private decimal tienDichVu;
        private decimal tienDichVuCheckIn;
        private decimal tienDichVuPhatSinh;
        private decimal tienGiaHan;
        private decimal phuPhi;
        private decimal phuPhiTraMuon;
        private decimal giamGia;
        private decimal datCoc;
        private decimal canThanhToanHienThi;

        public bool DuLieuDaThayDoi { get; private set; }

        public PhongChiTietWindow(PhongDTO phong)
        {
            this.phong = phong;
            InitializeComponent();
            Loaded += PhongChiTietWindow_Loaded;
            Closed += PhongChiTietWindow_Closed;
            thoiGianLuuTruTimer.Interval = TimeSpan.FromMinutes(1);
            thoiGianLuuTruTimer.Tick += (_, _) => CapNhatThoiGianLuuTru();
        }

        private void PhongChiTietWindow_Loaded(object sender, RoutedEventArgs e)
        {
            stayData = LoadRoomStayData();
            dichVuHienTai = LoadDichVu(stayData);

            tienPhong = stayData?.TienPhong ?? 0;
            tienGiaHan = stayData?.TienGiaHan ?? 0;
            phuPhi = stayData?.PhuPhi ?? 0;
            datCoc = stayData?.TienCoc ?? 0;
            if (stayData?.MaThue.HasValue != true && stayData?.MaDatPhong.HasValue == true)
            {
                phuPhi = CheckInPhuPhiHelper.Tinh(phong, stayData.NgayNhanDat, stayData.NgayTraDat, DateTime.Now, tienPhong).SoTien;
            }

            string tenPhong = $"Phòng {phong.MaHienThi}";
            Title = $"Thông tin chi tiết - {tenPhong}";
            TxtTieuDe.Text = $"Chi tiết phòng - {phong.MaHienThi}";
            TxtNgayNhan.Text = stayData?.NgayNhanThucTe?.ToString("dd/MM/yyyy HH:mm") ?? string.Empty;
            TxtNgayNhanSidebar.Text = stayData?.NgayNhanDat?.ToString("dd/MM/yyyy HH:mm") ?? string.Empty;
            TxtNgayTra.Text = stayData?.NgayTraDat?.ToString("dd/MM/yyyy HH:mm") ?? string.Empty;
            TxtSoLuongKhachHeader.Text = stayData?.SoNguoi > 0 ? $"{stayData.SoNguoi} người" : "--";
            TxtHoTen.Text = stayData?.HoTen ?? string.Empty;
            TxtSDT.Text = stayData?.SDT ?? string.Empty;
            TxtSoLuongKhach.Text = TxtSoLuongKhachHeader.Text;
            TxtLoaiPhong.Text = phong.LoaiPhong;
            TxtTrangThaiPhong.Text = phong.TrangThai;
            DgDichVu.ItemsSource = dichVuHienTai;
            NapDanhSachDichVuThem();
            CapNhatHoaDon();
            CapNhatThoiGianLuuTru();
            thoiGianLuuTruTimer.Start();

            CapNhatTrangThaiNut();
        }

        private void PhongChiTietWindow_Closed(object? sender, EventArgs e)
        {
            thoiGianLuuTruTimer.Stop();
        }

        private void CapNhatTrangThaiNut()
        {
            string trangThai = BoDau(phong.TrangThai);
            bool daDat = stayData?.MaDatPhong.HasValue == true || trangThai.Contains("dat", StringComparison.OrdinalIgnoreCase);
            bool dangThue = stayData?.MaThue.HasValue == true || trangThai.Contains("thue", StringComparison.OrdinalIgnoreCase) || trangThai.Contains("co khach", StringComparison.OrdinalIgnoreCase);
            bool canDonDep = trangThai.Contains("chua don", StringComparison.OrdinalIgnoreCase);

            BtnNhanPhong.IsEnabled = daDat && !dangThue;
            BtnHuyDat.IsEnabled = daDat && !dangThue;
            BtnTraPhong.IsEnabled = dangThue;
            BtnGiaHanPhong.IsEnabled = dangThue && stayData?.MaThue.HasValue == true;
            BtnDoiPhong.IsEnabled = stayData?.MaThue.HasValue == true || stayData?.MaDatPhong.HasValue == true;
            BtnDonDep.IsEnabled = canDonDep;
            BtnThanhToan.IsEnabled = dangThue || stayData != null;
            BtnLuu.IsEnabled = stayData != null;
            CboDichVuThem.IsEnabled = stayData?.MaThue.HasValue == true || stayData?.MaDatPhong.HasValue == true;
            BtnThemDichVu.IsEnabled = CboDichVuThem.IsEnabled;
            TxtSoLuongDichVu.IsEnabled = CboDichVuThem.IsEnabled;
            BtnGiamSoLuongDichVu.IsEnabled = CboDichVuThem.IsEnabled;
            BtnTangSoLuongDichVu.IsEnabled = CboDichVuThem.IsEnabled;
        }

        private void NapDanhSachDichVuThem()
        {
            try
            {
                List<DichVuVatTuDTO> danhSach = dichVuVatTuBUS.LayDanhSach()
                    .Where(item => !string.IsNullOrWhiteSpace(item.Ten))
                    .OrderBy(item => item.Ten)
                    .ToList();

                CboDichVuThem.ItemsSource = danhSach;
                CboDichVuThem.SelectedIndex = danhSach.Count > 0 ? 0 : -1;
            }
            catch
            {
                CboDichVuThem.ItemsSource = null;
            }
        }

        private void CapNhatHoaDon()
        {
            bool daNhanPhong = stayData?.MaThue.HasValue == true;
            decimal tienDichVuDaThanhToanCheckIn = daNhanPhong && stayData != null ? TinhTongDichVuDatTruoc(stayData) : 0;
            tienDichVuCheckIn = daNhanPhong ? 0 : dichVuHienTai.Where(item => item.LaDichVuCheckIn).Sum(item => item.ThanhTien);
            tienDichVuPhatSinh = dichVuHienTai.Where(item => !item.LaDichVuCheckIn).Sum(item => item.ThanhTien);
            tienDichVu = tienDichVuCheckIn + tienDichVuPhatSinh;
            tienGiaHan = daNhanPhong ? stayData?.TienGiaHan ?? 0 : 0;
            phuPhi = stayData?.PhuPhi ?? 0;
            DuToanCheckOutDTO? duToanCheckOut = daNhanPhong && stayData?.MaThue.HasValue == true
                ? LayDuToanCheckOutTamTinh(stayData.MaThue.Value)
                : null;
            phuPhiTraMuon = daNhanPhong ? duToanCheckOut?.PhuPhiTraMuon ?? TinhPhuPhiTraMuonTamTinh() : 0;

            string nhanSomText = TaoNhanPhuPhiNhanSom();
            string traMuonText = TaoNhanPhuPhiTraMuonTamTinh();
            if (!daNhanPhong && stayData?.MaDatPhong.HasValue == true)
            {
                CheckInPhuPhiResult phuPhiNhanSom = CheckInPhuPhiHelper.Tinh(phong, stayData.NgayNhanDat, stayData.NgayTraDat, DateTime.Now, tienPhong);
                phuPhi = phuPhiNhanSom.SoTien;
                nhanSomText = phuPhiNhanSom.MoTa;
            }

            giamGia = LaKhachVip(stayData?.LoaiKhach) ? Math.Round((tienPhong + phuPhi) * 0.1m, 0) : 0;
            decimal vat = Math.Round(Math.Max(0, tienPhong + phuPhi - giamGia) * 0.1m, 0);
            decimal tongCheckIn = Math.Max(0, tienPhong + tienDichVuCheckIn + phuPhi + vat - giamGia);
            decimal daThanhToanLucNhanPhong = daNhanPhong
                ? Math.Max(0, tienPhong + tienDichVuDaThanhToanCheckIn + phuPhi + vat - giamGia)
                : datCoc;
            decimal chenhLechDoiPhong = duToanCheckOut?.ChenhLechDoiPhong ?? 0;
            decimal tongPhatSinhTruocVat = tienDichVuPhatSinh + tienGiaHan + Math.Max(0, chenhLechDoiPhong) + phuPhiTraMuon;
            decimal vatPhatSinh = duToanCheckOut?.ThueVat ?? Math.Round(Math.Max(0, tongPhatSinhTruocVat) * 0.1m, 0);
            decimal canThanhToanThem = daNhanPhong
                ? duToanCheckOut?.CanThuThem ?? Math.Max(0, tongPhatSinhTruocVat + vatPhatSinh)
                : Math.Max(0, tongCheckIn - datCoc);
            canThanhToanHienThi = canThanhToanThem;

            TxtSoTienBanDau.Text = stayData == null
                ? "Phòng chưa có phiếu đặt/thuê đang hoạt động"
                : daNhanPhong
                    ? "Các khoản phát sinh khi trả phòng"
                    : $"Đã đặt cọc: {datCoc:N0} VND";

            if (daNhanPhong)
            {
                DgBangThanhToan.ItemsSource = new List<ThanhToanHoaDonItem>
                {
                    new(traMuonText, phuPhiTraMuon),
                    new("Tiền gia hạn phòng", tienGiaHan),
                    new(chenhLechDoiPhong < 0 ? "Hoàn chênh lệch đổi phòng" : "Phụ thu chênh lệch đổi phòng", chenhLechDoiPhong),
                    new("Dịch vụ phát sinh trong thời gian thuê", tienDichVuPhatSinh),
                    new("Thuế VAT (10%)", vatPhatSinh)
                };
            }
            else
            {
                DgBangThanhToan.ItemsSource = new List<ThanhToanHoaDonItem>
                {
                    new("Tiền phòng", tienPhong),
                    new("Dịch vụ tại check-in", tienDichVuCheckIn),
                    new(nhanSomText, phuPhi),
                    new("Giảm giá", -giamGia),
                    new("Thuế VAT (10%)", vat),
                    new("Đã đặt cọc", -datCoc)
                };
            }

            TxtKhaDung.Text = stayData == null ? "--" : $"{canThanhToanThem:N0} VND";
        }

        private decimal TinhPhuPhiTraMuonTamTinh()
        {
            if (stayData?.NgayNhanDat is not DateTime ngayNhanDat ||
                stayData.NgayTraDat is not DateTime ngayTraDat)
            {
                return 0;
            }

            DateTime now = DateTime.Now;
            if (now <= ngayTraDat)
            {
                return 0;
            }

            return PricingHelper.TinhPhuThuTraMuon(ngayNhanDat, ngayTraDat, now, phong.GiaGio, phong.GiaNgay, phong.GiaDem);
        }

        private DuToanCheckOutDTO? LayDuToanCheckOutTamTinh(int maThue)
        {
            try
            {
                return thanhToanBUS.DuToanCheckOut(maThue);
            }
            catch
            {
                return null;
            }
        }

        private string TaoNhanPhuPhiTraMuonTamTinh()
        {
            if (stayData?.NgayTraDat is not DateTime ngayTraDat)
            {
                return "Thời gian trả muộn";
            }

            DateTime now = DateTime.Now;
            if (now <= ngayTraDat)
            {
                return "Thời gian trả muộn: không trễ giờ";
            }

            int soPhutTre = Math.Max(0, (int)Math.Ceiling((now - ngayTraDat).TotalMinutes));
            if (soPhutTre <= 30)
            {
                return $"Thời gian trả muộn: {soPhutTre} phút - miễn phí 30 phút đầu";
            }

            int soGioTinhPhi = Math.Max(1, (int)Math.Ceiling((soPhutTre - 30) / 60.0));
            int gioTre = soPhutTre / 60;
            int phutTre = soPhutTre % 60;
            return $"Thời gian trả muộn: {gioTre} giờ {phutTre} phút, tính phụ phí {soGioTinhPhi} giờ";
        }

        private string TaoNhanPhuPhiNhanSom()
        {
            if (stayData?.NgayNhanThucTe is not DateTime ngayNhanThucTe ||
                stayData.NgayNhanDat is not DateTime ngayNhanDat ||
                ngayNhanThucTe >= ngayNhanDat)
            {
                return "Phụ phí nhận sớm";
            }

            TimeSpan somHon = ngayNhanDat - ngayNhanThucTe;
            int tongPhut = Math.Max(0, (int)Math.Ceiling(somHon.TotalMinutes));
            int soGioLamTron = Math.Max(1, (int)Math.Ceiling(tongPhut / 60.0));
            string thoiGian = $"{soGioLamTron} giờ";
            return $"Phụ phí nhận sớm ({thoiGian})";
        }

        private static bool LaKhachVip(string? loaiKhach)
        {
            return !string.IsNullOrWhiteSpace(loaiKhach) &&
                   loaiKhach.Contains("VIP", StringComparison.OrdinalIgnoreCase);
        }

        private decimal TinhTongDichVuDatTruoc(RoomStayData data)
        {
            return LoadDichVuDatTruoc(data).Sum(item => item.ThanhTien);
        }

        private RoomStayData? LoadRoomStayData()
        {
            RoomStayData? rental = LoadActiveRental();
            if (rental != null)
            {
                return rental;
            }

            return LoadActiveBooking();
        }

        private RoomStayData? LoadActiveRental()
        {
            if (!TableExists("PHIEUTHUE") || !TableExists("KHACHHANG"))
            {
                return null;
            }

            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists("DATPHONG") ? "DATPHONG" : string.Empty;
            bool coPhieuDatLienKet = !string.IsNullOrWhiteSpace(bangDatPhong) && ColumnExists("PHIEUTHUE", "MaDatPhong");
            string ngayNhanDatColumn = coPhieuDatLienKet && ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong";
            string ngayTraDatColumn = coPhieuDatLienKet && ColumnExists(bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong";
            string ngayNhanDatExpr = coPhieuDatLienKet ? "ISNULL(DP." + ngayNhanDatColumn + ", PT.NgayNhan)" : "PT.NgayNhan";
            string ngayTraDatExpr = coPhieuDatLienKet ? "ISNULL(DP." + ngayTraDatColumn + ", PT.NgayTraDuKien)" : "PT.NgayTraDuKien";
            string cheDoDatPhongColumn = coPhieuDatLienKet ? GetFirstExistingColumn(bangDatPhong, "LoaiDat", "CheDoDatPhong", "LoaiDatPhong") : string.Empty;
            string cheDoDatPhongExpr = coPhieuDatLienKet && !string.IsNullOrWhiteSpace(cheDoDatPhongColumn)
                ? "ISNULL(DP." + cheDoDatPhongColumn + ", N'')"
                : "CAST(N'' AS nvarchar(100))";
            string joinDatPhong = coPhieuDatLienKet ? "LEFT JOIN dbo." + bangDatPhong + " DP ON PT.MaDatPhong = DP.MaDatPhong" : string.Empty;
            string maDatPhongExpr = ColumnExists("PHIEUTHUE", "MaDatPhong") ? "PT.MaDatPhong" : "CAST(NULL AS int)";
            string soNguoiExpr = ColumnExists("PHIEUTHUE", "SoNguoi") ? "PT.SoNguoi" : "1";
            string ghiChuExpr = ColumnExists("PHIEUTHUE", "GhiChu") ? "PT.GhiChu" : "CAST(NULL AS nvarchar(1000))";
            string tienPhongExpr = PricingHelper.TienPhongSql(ngayNhanDatExpr, "PT.NgayTraDuKien", "PT.NgayTraDuKien");
            string giaNgayExpr = "ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0))";
            string giaGioExpr = "ISNULL(NULLIF(LP.DonGiaGio, 0), " + giaNgayExpr + " / 24.0)";
            string giaDemExpr = "ISNULL(NULLIF(LP.DonGiaDem, 0), " + giaNgayExpr + ")";
            string phuPhiExpr = PricingHelper.PhuThuNhanSomSql("PT.NgayNhan", ngayNhanDatExpr, ngayTraDatExpr, giaNgayExpr, giaGioExpr, giaDemExpr);
            bool coChiTietDatPhong = TableExists("CHITIETDATPHONG") && ColumnExists("PHIEUTHUE", "MaDatPhong");
            string joinPhong = coChiTietDatPhong
                ? @"JOIN dbo.CHITIETDATPHONG CT ON PT.MaDatPhong = CT.MaDatPhong
JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong"
                : "LEFT JOIN dbo.PHONG P ON PT.MaPhong = P.MaPhong";
            string wherePhong = coChiTietDatPhong
                ? "(CT.MaPhong = @MaPhong OR PT.MaPhong = @MaPhong)"
                : "PT.MaPhong = @MaPhong";
            string soPhongDoanExpr = coChiTietDatPhong
                ? "(SELECT COUNT(*) FROM dbo.CHITIETDATPHONG CT2 WHERE CT2.MaDatPhong = PT.MaDatPhong)"
                : "1";
            string danhSachPhongDoanExpr = coChiTietDatPhong
                ? @"(SELECT STRING_AGG(CAST(" + TenPhongSql("P2") + @" AS nvarchar(max)), N', ')
                    FROM dbo.CHITIETDATPHONG CT2
                    JOIN dbo.PHONG P2 ON CT2.MaPhong = P2.MaPhong
                    WHERE CT2.MaDatPhong = PT.MaDatPhong)"
                : TenPhongSql("P");

            DataTable data = ConnectDB.GetData(@"
SELECT TOP 1
       PT.MaThue,
       " + maDatPhongExpr + @" AS MaDatPhong,
       KH.HoTen,
       KH.SDT,
       " + (ColumnExists("KHACHHANG", "LoaiKhach") ? "KH.LoaiKhach" : "CAST(N'' AS nvarchar(50))") + @" AS LoaiKhach,
       " + soNguoiExpr + @" AS SoNguoi,
       PT.NgayNhan AS NgayNhanThucTe,
       " + ngayNhanDatExpr + @" AS NgayNhanDat,
       " + ngayTraDatExpr + @" AS NgayTraDat,
       " + cheDoDatPhongExpr + @" AS CheDoDatPhong,
       ISNULL(PT.TienCoc, 0) AS TienCoc,
       " + tienPhongExpr + @" AS TienPhong,
       " + phuPhiExpr + @" AS PhuPhi,
       " + ghiChuExpr + @" AS GhiChu,
       PT.TrangThai,
       " + soPhongDoanExpr + @" AS SoPhongDoan,
       " + danhSachPhongDoanExpr + @" AS DanhSachPhongDoan
FROM dbo.PHIEUTHUE PT
JOIN dbo.KHACHHANG KH ON PT.MaKH = KH.MaKH
" + joinDatPhong + @"
" + joinPhong + @"
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE " + wherePhong + @"
  AND (
        PT.TrangThai IN (N'Đang thuê', N'Dang thue', N'Có khách', N'Co khach')
        OR P.TrangThai IN (N'Đang thuê', N'Dang thue', N'Có khách', N'Co khach')
      )
ORDER BY PT.MaThue DESC",
                new SqlParameter("@MaPhong", phong.Ma));

            return data.Rows.Count == 0 ? null : MapStay(data.Rows[0], laPhieuThue: true);
        }

        private RoomStayData? LoadActiveBooking()
        {
            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists("DATPHONG") ? "DATPHONG" : string.Empty;
            if (string.IsNullOrWhiteSpace(bangDatPhong) || !TableExists("KHACHHANG"))
            {
                return null;
            }

            string ngayNhanExpr = ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "DP.NgayNhanDuKien" : "DP.NgayNhanPhong";
            string ngayTraExpr = ColumnExists(bangDatPhong, "NgayTraDuKien") ? "DP.NgayTraDuKien" : "DP.NgayTraPhong";
            string cheDoDatPhongColumn = GetFirstExistingColumn(bangDatPhong, "LoaiDat", "CheDoDatPhong", "LoaiDatPhong");
            string cheDoDatPhongExpr = !string.IsNullOrWhiteSpace(cheDoDatPhongColumn)
                ? "ISNULL(DP." + cheDoDatPhongColumn + ", N'')"
                : "CAST(N'' AS nvarchar(100))";
            string tienCocExpr = ColumnExists(bangDatPhong, "TienCoc") ? "DP.TienCoc" : "DP.DatCoc";
            string soNguoiExpr = ColumnExists(bangDatPhong, "SoNguoi") ? "DP.SoNguoi" : "1";
            string ghiChuExpr = ColumnExists(bangDatPhong, "GhiChu") ? "DP.GhiChu" : "CAST(NULL AS nvarchar(1000))";
            string tienPhongExpr = PricingHelper.TienPhongSql(ngayNhanExpr, ngayTraExpr, ngayTraExpr);
            bool coChiTietDatPhong = TableExists("CHITIETDATPHONG");
            string joinPhong = coChiTietDatPhong
                ? @"JOIN dbo.CHITIETDATPHONG CT ON DP.MaDatPhong = CT.MaDatPhong
JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong"
                : "JOIN dbo.PHONG P ON DP.MaPhong = P.MaPhong";
            string wherePhong = coChiTietDatPhong ? "CT.MaPhong = @MaPhong" : "DP.MaPhong = @MaPhong";
            string soPhongDoanExpr = coChiTietDatPhong
                ? "(SELECT COUNT(*) FROM dbo.CHITIETDATPHONG CT2 WHERE CT2.MaDatPhong = DP.MaDatPhong)"
                : "1";
            string danhSachPhongDoanExpr = coChiTietDatPhong
                ? @"(SELECT STRING_AGG(CAST(" + TenPhongSql("P2") + @" AS nvarchar(max)), N', ')
                    FROM dbo.CHITIETDATPHONG CT2
                    JOIN dbo.PHONG P2 ON CT2.MaPhong = P2.MaPhong
                    WHERE CT2.MaDatPhong = DP.MaDatPhong)"
                : TenPhongSql("P");

            DataTable data = ConnectDB.GetData(@"
SELECT TOP 1
       CAST(NULL AS int) AS MaThue,
       DP.MaDatPhong,
       KH.HoTen,
       KH.SDT,
       " + (ColumnExists("KHACHHANG", "LoaiKhach") ? "KH.LoaiKhach" : "CAST(N'' AS nvarchar(50))") + @" AS LoaiKhach,
       " + soNguoiExpr + @" AS SoNguoi,
       CAST(NULL AS datetime) AS NgayNhanThucTe,
       " + ngayNhanExpr + @" AS NgayNhanDat,
       " + ngayTraExpr + @" AS NgayTraDat,
       " + cheDoDatPhongExpr + @" AS CheDoDatPhong,
       ISNULL(" + tienCocExpr + @", 0) AS TienCoc,
       " + tienPhongExpr + @" AS TienPhong,
       CAST(0 AS decimal(18,2)) AS PhuPhi,
       " + ghiChuExpr + @" AS GhiChu,
       DP.TrangThai,
       " + soPhongDoanExpr + @" AS SoPhongDoan,
       " + danhSachPhongDoanExpr + @" AS DanhSachPhongDoan
FROM dbo." + bangDatPhong + @" DP
JOIN dbo.KHACHHANG KH ON DP.MaKH = KH.MaKH
" + joinPhong + @"
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE " + wherePhong + @"
  AND DP.TrangThai IN (N'Đã xác nhận', N'Da xac nhan', N'Đã đặt', N'Da dat', N'Đang thuê', N'Dang thue')
ORDER BY DP.MaDatPhong DESC",
                new SqlParameter("@MaPhong", phong.Ma));

            return data.Rows.Count == 0 ? null : MapStay(data.Rows[0], laPhieuThue: false);
        }

        private ObservableCollection<DichVuChiTietItem> LoadDichVu(RoomStayData? data)
        {
            ObservableCollection<DichVuChiTietItem> result = new();
            if (data == null)
            {
                return result;
            }

            string bangPhatSinh = TableExists("PHATSINHDICHVU")
                ? "PHATSINHDICHVU"
                : TableExists("CHITIETPHATSINH")
                    ? "CHITIETPHATSINH"
                    : string.Empty;

            if (string.IsNullOrWhiteSpace(bangPhatSinh) || !TableExists("DICHVUVATTU"))
            {
                return result;
            }

            string keyColumn = data.MaThue.HasValue && ColumnExists(bangPhatSinh, "MaThue")
                ? "MaThue"
                : data.MaDatPhong.HasValue && ColumnExists(bangPhatSinh, "MaDatPhong")
                    ? "MaDatPhong"
                    : ColumnExists(bangPhatSinh, "MaPhong")
                        ? "MaPhong"
                        : string.Empty;

            if (string.IsNullOrWhiteSpace(keyColumn))
            {
                return result;
            }

            object keyValue = keyColumn == "MaThue"
                ? data.MaThue!.Value
                : keyColumn == "MaDatPhong"
                    ? data.MaDatPhong!.Value
                    : phong.Ma;

            string maDvPs = ColumnExists(bangPhatSinh, "MaDVVT") ? "MaDVVT" : "MaDichVu";
            string maDv = ColumnExists("DICHVUVATTU", "MaDVVT") ? "MaDVVT" : "MaDichVu";
            string tenDv = ColumnExists("DICHVUVATTU", "TenDVVT") ? "TenDVVT" : "TenDichVu";
            string psKey = GetFirstExistingColumn(bangPhatSinh, "MaPhatSinh", "MaCTPhatSinh", "MaCTPS", "MaChiTiet", "ID", "Ma");
            string psKeyExpr = string.IsNullOrWhiteSpace(psKey) ? "CAST(NULL AS int)" : "PS." + psKey;
            string soLuongExpr = ColumnExists(bangPhatSinh, "SoLuong") ? "PS.SoLuong" : "1";
            string donGiaExpr = ColumnExists(bangPhatSinh, "DonGia") ? "ISNULL(PS.DonGia, DV.DonGia)" : "DV.DonGia";
            string thanhTienExpr = ColumnExists(bangPhatSinh, "ThanhTien") ? "PS.ThanhTien" : "(" + soLuongExpr + " * " + donGiaExpr + ")";
            string ghiChuExpr = ColumnExists(bangPhatSinh, "GhiChu") ? "ISNULL(PS.GhiChu, N'')" : "N''";
            string roomFilter = keyColumn != "MaPhong" && ColumnExists(bangPhatSinh, "MaPhong")
                ? " AND PS.MaPhong = @MaPhong"
                : string.Empty;
            string checkInFilter = data.MaThue.HasValue && ColumnExists(bangPhatSinh, "GhiChu")
                ? ViewSchemaHelper.DichVuTheoLoaiHoaDonFilter("PS", "PHATSINH")
                : string.Empty;
            string keyFilter = "PS." + keyColumn + " = @KeyValue";
            if (keyColumn == "MaThue" && data.MaDatPhong.HasValue && ColumnExists(bangPhatSinh, "MaDatPhong"))
            {
                keyFilter = "(" + keyFilter + " OR PS.MaDatPhong = @MaDatPhong)";
            }

            DataTable services = ConnectDB.GetData(@"
SELECT " + psKeyExpr + @" AS MaPhatSinh,
       DV." + tenDv + @" AS Ten,
       " + soLuongExpr + @" AS SoLuong,
       " + donGiaExpr + @" AS DonGia,
       " + thanhTienExpr + @" AS ThanhTien,
       " + ghiChuExpr + @" AS GhiChu
FROM dbo." + bangPhatSinh + @" PS
JOIN dbo.DICHVUVATTU DV ON PS." + maDvPs + " = DV." + maDv + @"
WHERE " + keyFilter + roomFilter + checkInFilter,
                new SqlParameter("@KeyValue", keyValue),
                new SqlParameter("@MaDatPhong", data.MaDatPhong ?? 0),
                new SqlParameter("@MaPhong", phong.Ma));

            foreach (DataRow row in services.Rows)
            {
                result.Add(new DichVuChiTietItem
                {
                    MaPhatSinh = GetNullableInt(row, "MaPhatSinh"),
                    Ten = row["Ten"]?.ToString() ?? string.Empty,
                    SoLuong = Convert.ToInt32(GetDecimal(row, "SoLuong")),
                    DonGia = GetDecimal(row, "DonGia"),
                    LaDichVuCheckIn = (row["GhiChu"]?.ToString() ?? string.Empty).Contains("[DICHVU_CHECKIN]", StringComparison.OrdinalIgnoreCase)
                });
            }

            if (!data.MaThue.HasValue)
            {
                foreach (DichVuChiTietItem item in LoadDichVuDatTruoc(data))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private ObservableCollection<DichVuChiTietItem> LoadDichVuDatTruoc(RoomStayData data)
        {
            ObservableCollection<DichVuChiTietItem> result = new();
            if (!data.MaDatPhong.HasValue ||
                !TableExists("CHITIETDATPHONG") ||
                !ColumnExists("CHITIETDATPHONG", "GhiChu") ||
                !TableExists("DICHVUVATTU"))
            {
                return result;
            }

            string maDv = ColumnExists("DICHVUVATTU", "MaDVVT") ? "MaDVVT" : "MaDichVu";
            string tenDv = ColumnExists("DICHVUVATTU", "TenDVVT") ? "TenDVVT" : "TenDichVu";
            string roomFilter = ColumnExists("CHITIETDATPHONG", "MaPhong") ? " AND MaPhong = @MaPhong" : string.Empty;
            List<SqlParameter> parameters = new()
            {
                new SqlParameter("@MaDatPhong", data.MaDatPhong.Value),
                new SqlParameter("@Marker", "[DICHVU_DAT]")
            };
            if (!string.IsNullOrWhiteSpace(roomFilter))
            {
                parameters.Add(new SqlParameter("@MaPhong", phong.Ma));
            }

            DataTable chiTiet = ConnectDB.GetData(
                "SELECT GhiChu FROM dbo.CHITIETDATPHONG WHERE MaDatPhong = @MaDatPhong" + roomFilter + " AND CHARINDEX(@Marker, ISNULL(GhiChu, N'')) > 0",
                parameters.ToArray());

            foreach (DataRow row in chiTiet.Rows)
            {
                foreach (DichVuDatPhongDTO dichVu in DocMarkerDichVuDatTruoc(row["GhiChu"]?.ToString() ?? string.Empty))
                {
                    DataTable ten = ConnectDB.GetData(
                        "SELECT TOP 1 " + tenDv + " AS Ten FROM dbo.DICHVUVATTU WHERE " + maDv + " = @Ma",
                        new SqlParameter("@Ma", dichVu.Ma));
                    result.Add(new DichVuChiTietItem
                    {
                        Ten = ten.Rows.Count > 0 ? ten.Rows[0]["Ten"]?.ToString() ?? string.Empty : "Dich vu " + dichVu.Ma,
                        SoLuong = dichVu.SoLuong,
                        DonGia = dichVu.DonGia,
                        LaDichVuCheckIn = true
                    });
                }
            }

            return result;
        }

        private static List<DichVuDatPhongDTO> DocMarkerDichVuDatTruoc(string ghiChu)
        {
            const string marker = "[DICHVU_DAT]";
            int markerIndex = ghiChu.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return new List<DichVuDatPhongDTO>();
            }

            string payload = ghiChu[(markerIndex + marker.Length)..].Trim();
            int stopIndex = payload.IndexOf(" - ", StringComparison.Ordinal);
            if (stopIndex >= 0)
            {
                payload = payload[..stopIndex];
            }

            List<DichVuDatPhongDTO> result = new();
            foreach (string token in payload.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = token.Split('|');
                if (parts.Length >= 3 &&
                    int.TryParse(parts[0], out int ma) &&
                    int.TryParse(parts[1], out int soLuong) &&
                    decimal.TryParse(parts[2], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal donGia) &&
                    soLuong > 0)
                {
                    result.Add(new DichVuDatPhongDTO { Ma = ma, SoLuong = soLuong, DonGia = donGia });
                }
            }

            return result;
        }

        private static RoomStayData MapStay(DataRow row, bool laPhieuThue)
        {
            string ghiChu = row["GhiChu"]?.ToString() ?? string.Empty;
            decimal tienPhongTinhLai = GetDecimal(row, "TienPhong");
            decimal tienPhongDaChot = DocTienPhongDaChot(ghiChu);
            decimal tienGiaHan = laPhieuThue ? DocTongTienMarker(ghiChu, "GIAHAN") : 0;

            return new RoomStayData
            {
                MaThue = laPhieuThue ? GetNullableInt(row, "MaThue") : null,
                MaDatPhong = GetNullableInt(row, "MaDatPhong"),
                HoTen = row["HoTen"]?.ToString() ?? string.Empty,
                SDT = row["SDT"]?.ToString() ?? string.Empty,
                LoaiKhach = row["LoaiKhach"]?.ToString() ?? string.Empty,
                SoNguoi = Convert.ToInt32(GetDecimal(row, "SoNguoi")),
                NgayNhanThucTe = GetNullableDate(row, "NgayNhanThucTe"),
                NgayNhanDat = GetNullableDate(row, "NgayNhanDat"),
                NgayTraDat = GetNullableDate(row, "NgayTraDat"),
                CheDoDatPhong = row["CheDoDatPhong"]?.ToString() ?? string.Empty,
                TienCoc = GetDecimal(row, "TienCoc"),
                TienPhong = tienPhongDaChot > 0 ? tienPhongDaChot : tienPhongTinhLai,
                TienGiaHan = tienGiaHan,
                PhuPhi = GetDecimal(row, "PhuPhi"),
                GhiChu = ghiChu,
                TrangThai = row["TrangThai"]?.ToString() ?? string.Empty,
                SoPhongDoan = Convert.ToInt32(GetDecimal(row, "SoPhongDoan")),
                DanhSachPhongDoan = row["DanhSachPhongDoan"]?.ToString() ?? string.Empty
            };
        }

        private static decimal DocTienPhongDaChot(string ghiChu)
        {
            return ViewSchemaHelper.DocTienPhongDaChot(ghiChu);
        }

        private static decimal DocTongTienMarker(string ghiChu, string marker)
        {
            decimal total = 0;
            foreach (Match match in Regex.Matches(ghiChu ?? string.Empty, @"\[" + Regex.Escape(marker) + @"\][^\[]*?SoTien\s*=\s*(-?[0-9][0-9.,]*)", RegexOptions.IgnoreCase))
            {
                string raw = match.Groups[1].Value.Replace(",", string.Empty).Replace(".", string.Empty);
                if (decimal.TryParse(raw, out decimal value))
                {
                    total += value;
                }
            }

            return total;
        }

        private void CapNhatThoiGianLuuTru()
        {
            ThoiGianLuuTruResult result = TinhThoiGianLuuTru(
                stayData?.NgayNhanThucTe,
                stayData?.NgayTraDat,
                phong.TrangThai ?? string.Empty,
                stayData?.TrangThai ?? string.Empty);
            TxtThoiGianLuuTru.Text = result.Text;
            TxtThoiGianLuuTru.Foreground = result.QuaGio ? Brushes.Red : Brushes.Black;
        }

        private static ThoiGianLuuTruResult TinhThoiGianLuuTru(DateTime? ngayNhan, DateTime? ngayTra, params string[] trangThaiValues)
        {
            if (!ngayNhan.HasValue)
            {
                return new ThoiGianLuuTruResult("0 giờ", false);
            }

            bool dangThue = trangThaiValues.Any(value =>
            {
                string normalized = BoDau(value ?? string.Empty);
                return normalized.Contains("thue", StringComparison.OrdinalIgnoreCase) ||
                       normalized.Contains("co khach", StringComparison.OrdinalIgnoreCase);
            });

            DateTime now = DateTime.Now;
            if (dangThue)
            {
                if (!ngayTra.HasValue)
                {
                    return new ThoiGianLuuTruResult("--", false);
                }

                if (now > ngayTra.Value)
                {
                    return new ThoiGianLuuTruResult($"Quá giờ {DinhDangThoiLuong(now - ngayTra.Value)}", true);
                }

                return new ThoiGianLuuTruResult($"Còn lại {DinhDangThoiLuong(ngayTra.Value - now)}", false);

            }

            DateTime end = ngayTra ?? now;
            if (end <= ngayNhan.Value)
            {
                return new ThoiGianLuuTruResult("0 giờ", false);
            }

            return new ThoiGianLuuTruResult(DinhDangThoiLuong(end - ngayNhan.Value), false);
        }

        private static string DinhDangThoiLuong(TimeSpan duration)
        {
            int totalMinutes = Math.Max(0, (int)Math.Floor(duration.TotalMinutes));
            if (totalMinutes <= 0)
            {
                return "0 phút";
            }

            int days = totalMinutes / (24 * 60);
            int hours = totalMinutes % (24 * 60) / 60;
            int minutes = totalMinutes % 60;

            List<string> parts = new();
            if (days > 0)
            {
                parts.Add($"{days} ngày");
            }
            if (hours > 0)
            {
                parts.Add($"{hours} giờ");
            }
            if (minutes > 0 && days == 0)
            {
                parts.Add($"{minutes} phút");
            }

            return parts.Count > 0 ? string.Join(" ", parts) : $"{minutes} phút";
        }

        private void BtnThoat_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnNhanPhong_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (stayData?.MaDatPhong.HasValue == true)
                {
                    CheckInPhuPhiResult phuPhiNhanSom = CheckInPhuPhiHelper.Tinh(phong, stayData.NgayNhanDat, stayData.NgayTraDat, DateTime.Now, tienPhong);
                    phuPhi = phuPhiNhanSom.SoTien;
                    decimal giamGia = stayData.LoaiKhach.Contains("VIP", StringComparison.OrdinalIgnoreCase) ? Math.Round((tienPhong + phuPhi) * 0.1m, 0) : 0;
                    if (!DialogService.XacNhanThanhToanCheckIn(this, "Phòng " + phong.MaHienThi, tienPhong, tienDichVu, phuPhi, datCoc, giamGia, phuPhiNhanSom.MoTa))
                    {
                        return;
                    }
                    KetQuaCheckInThanhToanDTO result = phongBUS.NhanPhongTuDatPhong(
                        stayData.MaDatPhong.Value,
                        Math.Max(0, tienPhong + tienDichVu + phuPhi - giamGia),
                        datCoc,
                        Math.Max(0, tienPhong + phuPhi - giamGia));
                    HoaDonItem billSauThanhToan = TaoHoaDonTam().VoiTrangThai(
                        "Da thanh toan",
                        result.MaHoaDon > 0 ? "HD-" + result.MaHoaDon.ToString("0000") : null);
                    DuLieuDaThayDoi = true;
                    phong.TrangThai = "Đang thuê";
                    stayData = LoadRoomStayData();
                    PhongChiTietWindow_Loaded(sender, e);
                    HienThiBillSauThanhToan(billSauThanhToan);
                    return;
                }

                if (!phong.TrangThai.Contains("thuê", StringComparison.OrdinalIgnoreCase) &&
                    !phong.TrangThai.Contains("thue", StringComparison.OrdinalIgnoreCase))
                {
                    phongBUS.NhanPhong(phong);
                    DuLieuDaThayDoi = true;
                    phong.TrangThai = "Đang thuê";
                }

                decimal tongHoaDon = tienPhong + tienDichVu + phuPhi;
                decimal conThanhToan = Math.Max(0, tongHoaDon - datCoc);
                MessageBox.Show(
                    $"Hóa đơn nhận phòng\n\n" +
                    $"Tiền phòng: {tienPhong:N0} đ\n" +
                    $"Tiền dịch vụ: {tienDichVu:N0} đ\n" +
                    $"Đã đặt cọc: {datCoc:N0} đ\n" +
                    $"Còn thanh toán: {conThanhToan:N0} đ",
                    "Hóa đơn",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không nhận được phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnHuyDat_Click(object sender, RoutedEventArgs e)
        {
            if (stayData is not { MaDatPhong: int maDatPhong } || stayData.MaThue.HasValue)
            {
                MessageBox.Show("Phong nay khong co phieu dat dang cho nhan phong de huy.", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                HuyDatPhongWindow confirm = new(new[] { phongBUS.DuToanHuyDatPhong(maDatPhong) });
                if (DialogService.ShowDimmedDialogResult(confirm, this) != true)
                {
                    return;
                }

                phongBUS.NoShow(maDatPhong);
                DuLieuDaThayDoi = true;
                MessageBox.Show("Da huy dat phong va chuyen phong ve trang thai trong.", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Information);
                stayData = null;
                PhongChiTietWindow_Loaded(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Khong the huy dat phong: " + ex.Message, "Loi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnTraPhong_Click(object sender, RoutedEventArgs e)
        {
            if (stayData?.MaThue.HasValue != true)
            {
                MessageBox.Show("Phòng này chưa có phiếu thuê đang hoạt động để trả phòng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int maThue = stayData.MaThue.GetValueOrDefault();
                DuToanCheckOutDTO duToan = thanhToanBUS.DuToanCheckOut(maThue);
                ThanhToanCheckOutWindow dialog = new(duToan);
                if (DialogService.ShowDimmedDialogResult(dialog, this) != true)
                {
                    return;
                }

                HoaDonItem? billSauThanhToan = dialog.ThanhToanSau ? null : TaoHoaDonTam();
                KetQuaCheckOutThanhToanDTO result = thanhToanBUS.CheckOut(maThue, !dialog.ThanhToanSau);
                if (billSauThanhToan != null && result.DaThanhToan)
                {
                    billSauThanhToan = HoaDonItem.TaoCheckOutTam(billSauThanhToan, result.MaHoaDon);
                }
                DuLieuDaThayDoi = true;
                string ketQua = result.TienThuThem <= 0 && result.TienHoanKhach <= 0
                    ? "Đã trả phòng. Không có phát sinh thêm."
                    : !result.DaThanhToan
                    ? $"Đã trả phòng và ghi nhận công nợ {result.TienThuThem:N0} VND. Hóa đơn đang chờ thanh toán."
                    : result.TienHoanKhach > 0
                    ? $"Cần trả lại khách {result.TienHoanKhach:N0} VND."
                    : $"Đã thu thêm {result.TienThuThem:N0} VND.";
                MessageBox.Show(ketQua + " Phòng chuyển sang trạng thái chưa dọn dẹp.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                if (billSauThanhToan != null && result.DaThanhToan)
                {
                    HienThiBillSauThanhToan(billSauThanhToan);
                }
                PhongChiTietWindow_Loaded(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể trả phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnGiaHanPhong_Click(object sender, RoutedEventArgs e)
        {
            if (stayData is not { MaThue: int maThue, NgayTraDat: DateTime ngayTraDat })
            {
                MessageBox.Show("Phòng này chưa có phiếu thuê đang hoạt động để gia hạn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GiaHanPhongWindow window = new(
                new GiaHanPhongRequestDTO
                {
                    MaThue = maThue,
                    MaDatPhong = stayData.MaDatPhong,
                    MaPhong = phong.Ma,
                    NgayTraCu = ngayTraDat,
                    NgayTraMoi = ngayTraDat,
                    GiaGio = phong.GiaGio,
                    GiaNgay = phong.GiaNgay,
                    GiaDem = phong.GiaDem,
                    SoPhongGiaHan = Math.Max(1, stayData.SoPhongDoan)
                },
                stayData.SoPhongDoan > 1 && !string.IsNullOrWhiteSpace(stayData.DanhSachPhongDoan)
                    ? stayData.DanhSachPhongDoan
                    : phong.MaHienThi);

            DialogService.ShowDimmedDialogResult(window, this);
            if (window.DuLieuDaThayDoi)
            {
                DuLieuDaThayDoi = true;
                PhongChiTietWindow_Loaded(sender, e);
            }
        }

        private void BtnDoiPhong_Click(object sender, RoutedEventArgs e)
        {
            if (stayData is not { NgayTraDat: DateTime ngayTraDat } ||
                (!stayData.MaThue.HasValue && !stayData.MaDatPhong.HasValue))
            {
                MessageBox.Show("Phòng này chưa có phiếu đặt hoặc phiếu thuê đang hoạt động để đổi phòng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DoiPhongWindow window = new(
                new DoiPhongRequestDTO
                {
                    MaThue = stayData.MaThue.GetValueOrDefault(),
                    MaDatPhong = stayData.MaDatPhong,
                    MaPhongCu = phong.Ma,
                    NgayBatDau = stayData.MaThue.HasValue ? DateTime.Now : stayData.NgayNhanDat ?? DateTime.Now,
                    NgayTraDuKien = ngayTraDat
                },
                phong.MaHienThi);

            DialogService.ShowDimmedDialogResult(window, this);
            if (window.DuLieuDaThayDoi)
            {
                DuLieuDaThayDoi = true;
                Close();
            }
        }

        private void BtnThanhToan_Click(object sender, RoutedEventArgs e)
        {
            if (stayData is not { MaThue: int maThue })
            {
                MessageBox.Show("Chi co the thanh toan khi phong dang co phieu thue.", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                DuToanCheckOutDTO duToan = thanhToanBUS.DuToanCheckOut(maThue);
                ThanhToanCheckOutWindow dialog = new(duToan);
                if (DialogService.ShowDimmedDialogResult(dialog, this) != true)
                {
                    return;
                }

                HoaDonItem? billSauThanhToan = dialog.ThanhToanSau ? null : TaoHoaDonTam();
                KetQuaCheckOutThanhToanDTO result = thanhToanBUS.CheckOut(maThue, !dialog.ThanhToanSau);
                if (billSauThanhToan != null && result.DaThanhToan)
                {
                    billSauThanhToan = HoaDonItem.TaoCheckOutTam(billSauThanhToan, result.MaHoaDon);
                }
                DuLieuDaThayDoi = true;
                string ketQua = result.TienThuThem <= 0 && result.TienHoanKhach <= 0
                    ? "Không có phát sinh thêm."
                    : !result.DaThanhToan
                    ? $"Đã ghi nhận thanh toán sau {result.TienThuThem:N0} VND."
                    : result.TienHoanKhach > 0
                    ? $"Cần trả lại khách {result.TienHoanKhach:N0} VND."
                    : $"Số tiền thu thêm: {result.TienThuThem:N0} VND.";
                string tieuDeKetQua = result.MaHoaDon > 0
                    ? $"Đã xử lý hóa đơn {result.MaHoaDon}. "
                    : "Đã xử lý trả phòng. ";
                MessageBox.Show(tieuDeKetQua + ketQua, "Thanh toán", MessageBoxButton.OK, MessageBoxImage.Information);
                if (billSauThanhToan != null && result.DaThanhToan)
                {
                    HienThiBillSauThanhToan(billSauThanhToan);
                }
                PhongChiTietWindow_Loaded(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Khong the thanh toan: " + ex.Message, "Loi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLuu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                stayData = LoadRoomStayData();
                dichVuHienTai = LoadDichVu(stayData);
                DgDichVu.ItemsSource = dichVuHienTai;
                CapNhatHoaDon();
                CapNhatThoiGianLuuTru();
                CapNhatTrangThaiNut();
                MessageBox.Show("Da luu va cap nhat du lieu hien thi.", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Khong the luu du lieu: " + ex.Message, "Loi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnInHoaDon_Click(object sender, RoutedEventArgs e)
        {
            if (stayData == null)
            {
                MessageBox.Show("Phong nay chua co du lieu hoa don de in.", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            HoaDonPrintWindow window = new(TaoHoaDonTam())
            {
                Owner = this
            };
            window.ShowDialog();
        }

        private void BtnDonDep_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Xác nhận đã dọn dẹp xong và chuyển phòng về trạng thái phòng trống?", "Dọn dẹp", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                CapNhatPhongTrong();
                DuLieuDaThayDoi = true;
                MessageBox.Show("Phòng đã sẵn sàng cho thuê.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                stayData = null;
                PhongChiTietWindow_Loaded(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể cập nhật dọn dẹp: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnThemDichVu_Click(object sender, RoutedEventArgs e)
        {
            if (stayData == null || (!stayData.MaThue.HasValue && !stayData.MaDatPhong.HasValue))
            {
                MessageBox.Show("Phòng chưa có phiếu đặt hoặc phiếu thuê để thêm dịch vụ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CboDichVuThem.SelectedItem is not DichVuVatTuDTO dichVu)
            {
                MessageBox.Show("Vui lòng chọn dịch vụ/vật tư cần thêm.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtSoLuongDichVu.Text.Trim(), out int soLuong) || soLuong <= 0)
            {
                MessageBox.Show("Số lượng dịch vụ phải lớn hơn 0.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtSoLuongDichVu.Focus();
                return;
            }

            try
            {
                bool laDichVuCheckIn = stayData.MaDatPhong.HasValue && !stayData.MaThue.HasValue;
                if (laDichVuCheckIn)
                {
                    ThemDichVuCheckIn(dichVu, soLuong);
                }
                else
                {
                    ThemDichVuPhatSinh(dichVu, soLuong);
                }

                dichVuHienTai = LoadDichVu(stayData);
                DgDichVu.ItemsSource = dichVuHienTai;
                CapNhatHoaDon();
                DuLieuDaThayDoi = true;
                TxtSoLuongDichVu.Text = "1";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể thêm dịch vụ: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnGiamSoLuongDichVu_Click(object sender, RoutedEventArgs e)
        {
            TxtSoLuongDichVu.Text = Math.Max(1, LaySoLuongDichVuNhap() - 1).ToString();
            TxtSoLuongDichVu.CaretIndex = TxtSoLuongDichVu.Text.Length;
        }

        private void BtnTangSoLuongDichVu_Click(object sender, RoutedEventArgs e)
        {
            TxtSoLuongDichVu.Text = Math.Min(999, LaySoLuongDichVuNhap() + 1).ToString();
            TxtSoLuongDichVu.CaretIndex = TxtSoLuongDichVu.Text.Length;
        }

        private void TxtSoLuongDichVu_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void TxtSoLuongDichVu_LostFocus(object sender, RoutedEventArgs e)
        {
            TxtSoLuongDichVu.Text = LaySoLuongDichVuNhap().ToString();
        }

        private int LaySoLuongDichVuNhap()
        {
            return int.TryParse(TxtSoLuongDichVu.Text.Trim(), out int soLuong) && soLuong > 0
                ? Math.Min(999, soLuong)
                : 1;
        }

        private void BtnXoaDichVu_Click(object sender, RoutedEventArgs e)
        {
            if (stayData?.MaThue.HasValue != true)
            {
                MessageBox.Show("Phải nhận phòng trước khi xóa dịch vụ phát sinh.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DgDichVu.SelectedItem is not DichVuChiTietItem item)
            {
                MessageBox.Show("Vui lòng chọn dòng dịch vụ cần xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (item.LaDichVuCheckIn)
            {
                MessageBox.Show("Dịch vụ này thuộc hóa đơn check-in, không xóa ở phần phát sinh.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (!item.MaPhatSinh.HasValue)
                {
                    throw new InvalidOperationException("Không xác định được khóa dịch vụ phát sinh để xóa khỏi database.");
                }

                XoaDichVuPhatSinh(item.MaPhatSinh.Value);
                dichVuHienTai = LoadDichVu(stayData);
                DgDichVu.ItemsSource = dichVuHienTai;
                CapNhatHoaDon();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể xóa dịch vụ: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HienThiBillSauThanhToan(HoaDonItem hoaDon)
        {
            HoaDonPrintWindow window = new(hoaDon);
            DialogService.ShowDimmedDialogResult(window, this);
        }

        private HoaDonItem TaoHoaDonTam()
        {
            bool daNhanPhong = stayData?.MaThue.HasValue == true;
            DuToanCheckOutDTO? duToanCheckOut = daNhanPhong && stayData?.MaThue.HasValue == true
                ? LayDuToanCheckOutTamTinh(stayData.MaThue.Value)
                : null;
            decimal tienPhongPhatSinh = duToanCheckOut != null
                ? duToanCheckOut.TienPhongPhatSinh
                : tienGiaHan;
            decimal tienPhongHoaDonTam = daNhanPhong ? tienPhongPhatSinh : tienPhong;
            decimal tienDichVuHoaDonTam = daNhanPhong ? duToanCheckOut?.TienDichVuPhatSinh ?? tienDichVuPhatSinh : tienDichVu;
            decimal phuPhiHoaDonTam = daNhanPhong ? duToanCheckOut?.PhuPhiTraMuon ?? phuPhiTraMuon : phuPhi;
            decimal giamGiaHoaDonTam = !daNhanPhong && LaKhachVip(stayData?.LoaiKhach) ? Math.Round((tienPhong + phuPhiHoaDonTam) * 0.1m, 0) : 0;
            decimal vat = daNhanPhong
                ? duToanCheckOut?.ThueVat ?? Math.Round(Math.Max(0, Math.Max(0, tienPhongHoaDonTam) + tienDichVuHoaDonTam + phuPhiHoaDonTam) * 0.1m, 0)
                : Math.Round(Math.Max(0, tienPhongHoaDonTam + phuPhiHoaDonTam - giamGiaHoaDonTam) * 0.1m, 0);
            if (daNhanPhong && duToanCheckOut == null && canThanhToanHienThi > 0)
            {
                tienPhongHoaDonTam = Math.Max(0, canThanhToanHienThi - tienDichVuHoaDonTam - phuPhiHoaDonTam - vat + giamGiaHoaDonTam);
            }
            int maGoc = stayData?.MaThue ?? stayData?.MaDatPhong ?? phong.Ma;
            return new HoaDonItem
            {
                LoaiPhieu = stayData?.MaThue.HasValue == true ? "THUE" : "DAT",
                LoaiThanhToan = daNhanPhong ? "PHATSINH" : "CHECKIN",
                MaGoc = maGoc,
                MaHoaDon = "HD-TAM-" + maGoc,
                MaPhieuThue = stayData?.MaThue.HasValue == true ? "PT-" + maGoc : "DP-" + maGoc,
                TenKhachHang = stayData?.HoTen ?? string.Empty,
                SoDienThoai = stayData?.SDT ?? string.Empty,
                SoPhong = phong.MaHienThi,
                LoaiPhong = phong.LoaiPhong,
                NgayNhanPhong = daNhanPhong
                    ? stayData?.NgayNhanDat ?? stayData?.NgayNhanThucTe ?? DateTime.Now
                    : stayData?.NgayNhanThucTe ?? stayData?.NgayNhanDat ?? DateTime.Now,
                NgayTraPhong = stayData?.NgayTraDat ?? DateTime.Now,
                CheDoDatPhong = stayData?.CheDoDatPhong ?? string.Empty,
                GiaGioTinhPhi = phong.GiaGio,
                GiaNgayTinhPhi = phong.GiaNgay,
                GiaDemTinhPhi = phong.GiaDem,
                NgayLapHoaDon = DateTime.Now,
                TienPhong = tienPhongHoaDonTam,
                TienDichVu = tienDichVuHoaDonTam,
                PhuPhi = phuPhiHoaDonTam,
                ThueVat = vat,
                GiamGia = daNhanPhong ? 0 : giamGiaHoaDonTam,
                TienCoc = daNhanPhong ? 0 : datCoc,
                TrangThai = stayData?.MaThue.HasValue == true ? "Chua thanh toan" : "Tam tinh"
            };
        }

        private void TraPhong(int maThue)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();

            try
            {
                PhongTrangThaiSchema.DamBaoCoTrangThaiChuaDonDep(conn, tran);
                string trangThaiPhong = LayGiaTriHopLeTheoCheck(conn, tran, "PHONG", "TrangThai", PhongTrangThaiSchema.ChuaDonDep, "Chua don dep", "Dirty");
                using (SqlCommand cmd = new("UPDATE dbo.PHONG SET TrangThai = @TrangThai WHERE MaPhong = @MaPhong", conn, tran))
                {
                    cmd.Parameters.AddWithValue("@TrangThai", trangThaiPhong);
                    cmd.Parameters.AddWithValue("@MaPhong", phong.Ma);
                    cmd.ExecuteNonQuery();
                }

                if (TableExists(conn, tran, "PHIEUTHUE") && ColumnExists(conn, tran, "PHIEUTHUE", "TrangThai"))
                {
                    string setNgayTra = ColumnExists(conn, tran, "PHIEUTHUE", "NgayTraPhong") ? ", NgayTraPhong = @NgayTra" : string.Empty;
                    using SqlCommand cmd = new("UPDATE dbo.PHIEUTHUE SET TrangThai = @TrangThai" + setNgayTra + " WHERE MaThue = @MaThue", conn, tran);
                    cmd.Parameters.AddWithValue("@TrangThai", LayGiaTriHopLeTheoCheck(conn, tran, "PHIEUTHUE", "TrangThai", "Đã trả phòng", "Da tra phong", "Đã trả", "Da tra"));
                    cmd.Parameters.AddWithValue("@NgayTra", DateTime.Now);
                    cmd.Parameters.AddWithValue("@MaThue", maThue);
                    cmd.ExecuteNonQuery();
                }

                tran.Commit();
                phong.TrangThai = "Chưa dọn dẹp";
                phong.KhachHienTai = "--";
                phong.GioNhanPhong = "--";
                phong.GioTraDuKien = "--";
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        private void CapNhatPhongTrong()
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();

            try
            {
                string trangThaiPhong = LayGiaTriHopLeTheoCheck(conn, tran, "PHONG", "TrangThai", "Trống", "Phong trong", "Phòng trống");
                string ghiChuSachSet = TaoSetXoaGhiChuTuDong(conn, tran);
                using (SqlCommand cmd = new("UPDATE dbo.PHONG SET TrangThai = @TrangThai" + ghiChuSachSet + " WHERE MaPhong = @MaPhong", conn, tran))
                {
                    cmd.Parameters.AddWithValue("@TrangThai", trangThaiPhong);
                    cmd.Parameters.AddWithValue("@MaPhong", phong.Ma);
                    cmd.ExecuteNonQuery();
                }

                tran.Commit();
                phong.TrangThai = "Trống";
                phong.KhachHienTai = "--";
                phong.GioNhanPhong = "--";
                phong.GioTraDuKien = "--";
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        private static string TaoSetXoaGhiChuTuDong(SqlConnection conn, SqlTransaction tran)
        {
            if (!ColumnExists(conn, tran, "PHONG", "GhiChu"))
            {
                return string.Empty;
            }

            return @",
GhiChu = NULLIF(LTRIM(RTRIM(
    REPLACE(REPLACE(REPLACE(REPLACE(
        CASE
            WHEN CHARINDEX(N'[DA_DON_DEP_DOIPHONG:', ISNULL(GhiChu, N'')) > 0
             AND CHARINDEX(N']', ISNULL(GhiChu, N''), CHARINDEX(N'[DA_DON_DEP_DOIPHONG:', ISNULL(GhiChu, N''))) > 0
            THEN STUFF(
                ISNULL(GhiChu, N''),
                CHARINDEX(N'[DA_DON_DEP_DOIPHONG:', ISNULL(GhiChu, N'')),
                CHARINDEX(N']', ISNULL(GhiChu, N''), CHARINDEX(N'[DA_DON_DEP_DOIPHONG:', ISNULL(GhiChu, N'')))
                    - CHARINDEX(N'[DA_DON_DEP_DOIPHONG:', ISNULL(GhiChu, N'')) + 1,
                N'')
            ELSE ISNULL(GhiChu, N'')
        END,
        N'[CAN_DON_DEP] Can don dep sau khi doi phong', N''),
        N'[CAN_DON_DEP] Can don dep sau khi tra phong', N''),
        N'Can don dep sau khi doi phong', N''),
        N'Can don dep sau khi tra phong', N'')
)), N'')";
        }

        private void ThemDichVuPhatSinh(DichVuVatTuDTO dichVu, int soLuong)
        {
            KiemTraTonKhoDichVu(dichVu, soLuong);
            string bangPhatSinh = TableExists("PHATSINHDICHVU")
                ? "PHATSINHDICHVU"
                : TableExists("CHITIETPHATSINH")
                    ? "CHITIETPHATSINH"
                    : string.Empty;

            if (string.IsNullOrWhiteSpace(bangPhatSinh))
            {
                throw new InvalidOperationException("Không tìm thấy bảng phát sinh dịch vụ trong database.");
            }

            Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
            bool requiresMaThue = ColumnRequired(bangPhatSinh, "MaThue");
            if (requiresMaThue && stayData?.MaThue.HasValue != true)
            {
                throw new InvalidOperationException("Phải nhận phòng trước khi thêm dịch vụ phát sinh.");
            }

            if (stayData?.MaThue.HasValue == true && ColumnExists(bangPhatSinh, "MaThue"))
            {
                values["MaThue"] = stayData.MaThue.Value;
            }
            else if (!requiresMaThue && stayData?.MaDatPhong.HasValue == true && ColumnExists(bangPhatSinh, "MaDatPhong"))
            {
                values["MaDatPhong"] = stayData.MaDatPhong.Value;
            }

            if (ColumnExists(bangPhatSinh, "MaPhong"))
            {
                values["MaPhong"] = phong.Ma;
            }

            string maDvPs = ColumnExists(bangPhatSinh, "MaDVVT") ? "MaDVVT" : ColumnExists(bangPhatSinh, "MaDichVu") ? "MaDichVu" : string.Empty;
            if (string.IsNullOrWhiteSpace(maDvPs))
            {
                throw new InvalidOperationException("Bảng phát sinh dịch vụ thiếu cột mã dịch vụ.");
            }

            values[maDvPs] = dichVu.Ma;
            if (ColumnExists(bangPhatSinh, "SoLuong"))
            {
                values["SoLuong"] = soLuong;
            }

            if (ColumnExists(bangPhatSinh, "DonGia"))
            {
                values["DonGia"] = dichVu.DonGia;
            }

            if (ColumnExists(bangPhatSinh, "NgayPhatSinh"))
            {
                values["NgayPhatSinh"] = DateTime.Now;
            }

            if (ColumnExists(bangPhatSinh, "NgaySuDung"))
            {
                values["NgaySuDung"] = DateTime.Now;
            }

            if (ColumnExists(bangPhatSinh, "ThoiDiemSuDung"))
            {
                values["ThoiDiemSuDung"] = DateTime.Now;
            }

            if (ColumnExists(bangPhatSinh, "TrangThai"))
            {
                values["TrangThai"] = true;
            }

            if (ColumnExists(bangPhatSinh, "MaNV"))
            {
                values["MaNV"] = LayMaNhanVienDangNhap();
            }

            if (ColumnExists(bangPhatSinh, "GhiChu"))
            {
                values["GhiChu"] = "[DICHVU_PHATSINH]";
            }

            if (!values.ContainsKey("MaThue") && !values.ContainsKey("MaDatPhong") && !values.ContainsKey("MaPhong"))
            {
                throw new InvalidOperationException("Không xác định được khóa liên kết để thêm dịch vụ cho phòng.");
            }

            string columns = string.Join(", ", values.Keys.Select(column => "[" + column + "]"));
            string parameters = string.Join(", ", values.Keys.Select(column => "@" + column));
            ConnectDB.ExecuteNonQuery(
                $"INSERT INTO dbo.{bangPhatSinh} ({columns}) VALUES ({parameters})",
                values.Select(pair => new SqlParameter("@" + pair.Key, pair.Value ?? DBNull.Value)).ToArray());
            TruTonKhoDichVu(dichVu.Ma, soLuong);
        }

        private void ThemDichVuCheckIn(DichVuVatTuDTO dichVu, int soLuong)
        {
            KiemTraTonKhoDichVu(dichVu, soLuong);
            if (stayData?.MaDatPhong.HasValue != true)
            {
                throw new InvalidOperationException("Không xác định được phiếu đặt phòng để thêm dịch vụ check-in.");
            }

            int maDatPhong = stayData.MaDatPhong.GetValueOrDefault();
            if (!TableExists("CHITIETDATPHONG") || !ColumnExists("CHITIETDATPHONG", "GhiChu"))
            {
                throw new InvalidOperationException("Bảng chi tiết đặt phòng chưa hỗ trợ lưu dịch vụ check-in.");
            }

            string roomFilter = ColumnExists("CHITIETDATPHONG", "MaPhong") ? " AND MaPhong = @MaPhong" : string.Empty;
            List<SqlParameter> selectParams = new()
            {
                new SqlParameter("@MaDatPhong", maDatPhong)
            };
            if (!string.IsNullOrWhiteSpace(roomFilter))
            {
                selectParams.Add(new SqlParameter("@MaPhong", phong.Ma));
            }

            DataTable rows = ConnectDB.GetData(
                "SELECT TOP 1 GhiChu FROM dbo.CHITIETDATPHONG WHERE MaDatPhong = @MaDatPhong" + roomFilter,
                selectParams.ToArray());

            if (rows.Rows.Count == 0)
            {
                throw new InvalidOperationException("Không tìm thấy chi tiết đặt phòng để thêm dịch vụ check-in.");
            }

            string ghiChuHienTai = rows.Rows[0]["GhiChu"]?.ToString() ?? string.Empty;
            List<DichVuDatPhongDTO> danhSach = DocMarkerDichVuDatTruoc(ghiChuHienTai);
            DichVuDatPhongDTO? existing = danhSach.FirstOrDefault(item => item.Ma == dichVu.Ma);
            if (existing != null)
            {
                existing.SoLuong += soLuong;
                existing.DonGia = dichVu.DonGia;
            }
            else
            {
                danhSach.Add(new DichVuDatPhongDTO
                {
                    Ma = dichVu.Ma,
                    Ten = dichVu.Ten,
                    SoLuong = soLuong,
                    DonGia = dichVu.DonGia
                });
            }

            string ghiChuMoi = TaoGhiChuDichVuCheckIn(ghiChuHienTai, danhSach);
            List<SqlParameter> updateParams = new()
            {
                new SqlParameter("@GhiChu", ghiChuMoi),
                new SqlParameter("@MaDatPhong", maDatPhong)
            };
            if (!string.IsNullOrWhiteSpace(roomFilter))
            {
                updateParams.Add(new SqlParameter("@MaPhong", phong.Ma));
            }

            int affected = ConnectDB.ExecuteNonQuery(
                "UPDATE dbo.CHITIETDATPHONG SET GhiChu = @GhiChu WHERE MaDatPhong = @MaDatPhong" + roomFilter,
                updateParams.ToArray());
            if (affected <= 0)
            {
                throw new InvalidOperationException("Không cập nhật được dịch vụ check-in cho phòng.");
            }
            TruTonKhoDichVu(dichVu.Ma, soLuong);
        }

        private static void KiemTraTonKhoDichVu(DichVuVatTuDTO dichVu, int soLuong)
        {
            if (!LaVatTuTonKho(dichVu))
            {
                return;
            }

            string stockColumn = LayCotTonKhoDichVu();
            string keyColumn = LayCotKhoaDichVu();
            if (string.IsNullOrWhiteSpace(stockColumn) || string.IsNullOrWhiteSpace(keyColumn))
            {
                return;
            }

            object? value = ConnectDB.ExecuteScalar(
                "SELECT ISNULL([" + stockColumn + "], 0) FROM dbo.DICHVUVATTU WHERE [" + keyColumn + "] = @Ma",
                new SqlParameter("@Ma", dichVu.Ma));
            int tonKho = value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            if (tonKho < soLuong)
            {
                throw new InvalidOperationException("Không đủ tồn kho cho '" + dichVu.Ten + "'. Tồn hiện tại: " + tonKho + ".");
            }
        }

        private static void TruTonKhoDichVu(int maDichVu, int soLuong)
        {
            DichVuVatTuDTO? dichVu = new DichVuVatTuBUS().LayDanhSach().FirstOrDefault(item => item.Ma == maDichVu);
            if (dichVu != null && !LaVatTuTonKho(dichVu))
            {
                return;
            }

            string stockColumn = LayCotTonKhoDichVu();
            string keyColumn = LayCotKhoaDichVu();
            if (string.IsNullOrWhiteSpace(stockColumn) || string.IsNullOrWhiteSpace(keyColumn))
            {
                return;
            }

            ConnectDB.ExecuteNonQuery(
                "UPDATE dbo.DICHVUVATTU SET [" + stockColumn + "] = CASE WHEN ISNULL([" + stockColumn + "], 0) >= @SoLuong THEN [" + stockColumn + "] - @SoLuong ELSE [" + stockColumn + "] END WHERE [" + keyColumn + "] = @Ma",
                new SqlParameter("@SoLuong", soLuong),
                new SqlParameter("@Ma", maDichVu));
        }

        private static void HoanTonKhoDichVu(int maDichVu, int soLuong)
        {
            DichVuVatTuDTO? dichVu = new DichVuVatTuBUS().LayDanhSach().FirstOrDefault(item => item.Ma == maDichVu);
            if (dichVu != null && !LaVatTuTonKho(dichVu))
            {
                return;
            }

            string stockColumn = LayCotTonKhoDichVu();
            string keyColumn = LayCotKhoaDichVu();
            if (string.IsNullOrWhiteSpace(stockColumn) || string.IsNullOrWhiteSpace(keyColumn))
            {
                return;
            }

            ConnectDB.ExecuteNonQuery(
                "UPDATE dbo.DICHVUVATTU SET [" + stockColumn + "] = ISNULL([" + stockColumn + "], 0) + @SoLuong WHERE [" + keyColumn + "] = @Ma",
                new SqlParameter("@SoLuong", soLuong),
                new SqlParameter("@Ma", maDichVu));
        }

        private static bool LaVatTuTonKho(DichVuVatTuDTO dichVu)
        {
            string text = BoDau(dichVu.Loai).ToLowerInvariant();
            return text.Contains("vat tu") || text.Contains("material") || text.Contains("inventory");
        }

        private static string LayCotTonKhoDichVu()
        {
            return GetFirstExistingColumn("DICHVUVATTU", "SoLuongTon", "SLTon", "TonKho", "SoLuongCon");
        }

        private static string LayCotKhoaDichVu()
        {
            return GetFirstExistingColumn("DICHVUVATTU", "MaDVVT", "MaDichVu", "MaDV", "ID", "Ma");
        }

        private static string TaoGhiChuDichVuCheckIn(string ghiChuHienTai, List<DichVuDatPhongDTO> danhSach)
        {
            const string marker = "[DICHVU_DAT]";
            string ghiChuKhac = ghiChuHienTai.Trim();
            int markerIndex = ghiChuKhac.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
            {
                string before = ghiChuKhac[..markerIndex].Trim(' ', '-', ';');
                string payload = ghiChuKhac[(markerIndex + marker.Length)..].Trim();
                int stopIndex = payload.IndexOf(" - ", StringComparison.Ordinal);
                string after = stopIndex >= 0 ? payload[(stopIndex + 3)..].Trim() : string.Empty;
                ghiChuKhac = string.Join(" - ", new[] { before, after }.Where(item => !string.IsNullOrWhiteSpace(item)));
            }

            string payloadMoi = string.Join(";",
                danhSach
                    .Where(item => item.SoLuong > 0)
                    .Select(item => string.Join("|",
                        item.Ma,
                        item.SoLuong,
                        item.DonGia.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            string markerMoi = string.IsNullOrWhiteSpace(payloadMoi) ? string.Empty : marker + " " + payloadMoi;
            return string.IsNullOrWhiteSpace(ghiChuKhac)
                ? markerMoi
                : string.Join(" - ", new[] { markerMoi, ghiChuKhac }.Where(item => !string.IsNullOrWhiteSpace(item)));
        }

        private static void XoaDichVuPhatSinh(int maPhatSinh)
        {
            string bangPhatSinh = TableExists("PHATSINHDICHVU")
                ? "PHATSINHDICHVU"
                : TableExists("CHITIETPHATSINH")
                    ? "CHITIETPHATSINH"
                    : string.Empty;
            string keyColumn = string.IsNullOrWhiteSpace(bangPhatSinh)
                ? string.Empty
                : GetFirstExistingColumn(bangPhatSinh, "MaPhatSinh", "MaCTPhatSinh", "MaCTPS", "MaChiTiet", "ID", "Ma");

            if (string.IsNullOrWhiteSpace(bangPhatSinh) || string.IsNullOrWhiteSpace(keyColumn))
            {
                throw new InvalidOperationException("Không xác định được dòng dịch vụ phát sinh để xóa.");
            }

            (int maDichVu, int soLuong) = LayThongTinTonKhoCanHoan(bangPhatSinh, keyColumn, maPhatSinh);
            int affected = ConnectDB.ExecuteNonQuery(
                $"DELETE FROM dbo.{bangPhatSinh} WHERE [{keyColumn}] = @MaPhatSinh",
                new SqlParameter("@MaPhatSinh", maPhatSinh));
            if (affected <= 0)
            {
                throw new InvalidOperationException("Dịch vụ phát sinh không còn tồn tại hoặc chưa được xóa khỏi database.");
            }
            if (maDichVu > 0 && soLuong > 0)
            {
                HoanTonKhoDichVu(maDichVu, soLuong);
            }
        }

        private static (int MaDichVu, int SoLuong) LayThongTinTonKhoCanHoan(string bangPhatSinh, string keyColumn, int maPhatSinh)
        {
            string maDvColumn = GetFirstExistingColumn(bangPhatSinh, "MaDVVT", "MaDichVu", "MaDV");
            if (string.IsNullOrWhiteSpace(maDvColumn))
            {
                return (0, 0);
            }

            string soLuongExpr = ColumnExists(bangPhatSinh, "SoLuong") ? "ISNULL(SoLuong, 1)" : "1";
            DataTable data = ConnectDB.GetData(
                "SELECT TOP 1 [" + maDvColumn + "] AS MaDichVu, " + soLuongExpr + " AS SoLuong FROM dbo." + bangPhatSinh + " WHERE [" + keyColumn + "] = @MaPhatSinh",
                new SqlParameter("@MaPhatSinh", maPhatSinh));
            if (data.Rows.Count == 0)
            {
                return (0, 0);
            }

            return (Convert.ToInt32(data.Rows[0]["MaDichVu"]), Convert.ToInt32(data.Rows[0]["SoLuong"]));
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

        private static string TenPhongSql(string alias)
        {
            return ViewSchemaHelper.TenPhongSql(alias);
        }

        private static bool TableExists(string tableName)
        {
            return ViewSchemaHelper.TableExists(tableName);
        }

        private static bool TableExists(SqlConnection conn, SqlTransaction tran, string tableName)
        {
            using SqlCommand cmd = new("SELECT COUNT(*) FROM sys.tables WHERE name = @Name", conn, tran);
            cmd.Parameters.AddWithValue("@Name", tableName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static bool ColumnExists(string tableName, string columnName)
        {
            return ViewSchemaHelper.ColumnExists(tableName, columnName);
        }

        private static bool ColumnRequired(string tableName, string columnName)
        {
            object? result = ConnectDB.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM sys.tables t
                  JOIN sys.columns c ON t.object_id = c.object_id
                  WHERE t.name = @TableName
                    AND c.name = @ColumnName
                    AND c.is_computed = 0
                    AND c.is_nullable = 0
                    AND COLUMNPROPERTY(t.object_id, c.name, 'IsIdentity') = 0",
                new SqlParameter("@TableName", tableName),
                new SqlParameter("@ColumnName", columnName));
            return Convert.ToInt32(result) > 0;
        }

        private static bool WritableColumnExists(string tableName, string columnName)
        {
            object? result = ConnectDB.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM sys.tables t
                  JOIN sys.columns c ON t.object_id = c.object_id
                  WHERE t.name = @TableName
                    AND c.name = @ColumnName
                    AND c.is_computed = 0",
                new SqlParameter("@TableName", tableName),
                new SqlParameter("@ColumnName", columnName));
            return Convert.ToInt32(result) > 0;
        }

        private static string GetFirstExistingColumn(string tableName, params string[] columnNames)
        {
            return ViewSchemaHelper.GetFirstExistingColumn(tableName, columnNames);
        }

        private static int LayMaNhanVienDangNhap()
        {
            if (CurrentUser.MaNV <= 0)
            {
                throw new InvalidOperationException("Tài khoản đăng nhập chưa liên kết nhân viên. Không thể ghi nhận dịch vụ phát sinh.");
            }

            return CurrentUser.MaNV;
        }

        private static bool ColumnExists(SqlConnection conn, SqlTransaction tran, string tableName, string columnName)
        {
            using SqlCommand cmd = new(
                @"SELECT COUNT(*)
                  FROM sys.tables t
                  JOIN sys.columns c ON t.object_id = c.object_id
                  WHERE t.name = @TableName AND c.name = @ColumnName",
                conn,
                tran);
            cmd.Parameters.AddWithValue("@TableName", tableName);
            cmd.Parameters.AddWithValue("@ColumnName", columnName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static string LayGiaTriHopLeTheoCheck(SqlConnection conn, SqlTransaction tran, string tableName, string columnName, params string[] priorities)
        {
            List<string> allowed = new();
            using (SqlCommand cmd = new(
                       @"SELECT cc.definition
                         FROM sys.check_constraints cc
                         JOIN sys.tables t ON cc.parent_object_id = t.object_id
                         WHERE t.name = @TableName AND cc.definition LIKE @ColumnName",
                       conn,
                       tran))
            {
                cmd.Parameters.AddWithValue("@TableName", tableName);
                cmd.Parameters.AddWithValue("@ColumnName", "%" + columnName + "%");
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string definition = reader[0]?.ToString() ?? string.Empty;
                    foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(definition, @"N?'((?:''|[^'])*)'"))
                    {
                        string value = match.Groups[1].Value.Replace("''", "'");
                        if (!allowed.Contains(value, StringComparer.OrdinalIgnoreCase))
                        {
                            allowed.Add(value);
                        }
                    }
                }
            }

            foreach (string priority in priorities)
            {
                string? match = allowed.FirstOrDefault(value => string.Equals(value, priority, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }
            }

            string? heuristic = ChonTrangThaiGanDung(allowed, priorities);
            if (!string.IsNullOrWhiteSpace(heuristic))
            {
                return heuristic;
            }

            return priorities.FirstOrDefault() ?? string.Empty;
        }

        private static string? LayGiaTriHopLeNeuCo(SqlConnection conn, SqlTransaction tran, string tableName, string columnName, params string[] priorities)
        {
            List<string> allowed = new();
            using (SqlCommand cmd = new(
                       @"SELECT cc.definition
                         FROM sys.check_constraints cc
                         JOIN sys.tables t ON cc.parent_object_id = t.object_id
                         WHERE t.name = @TableName AND cc.definition LIKE @ColumnName",
                       conn,
                       tran))
            {
                cmd.Parameters.AddWithValue("@TableName", tableName);
                cmd.Parameters.AddWithValue("@ColumnName", "%" + columnName + "%");
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string definition = reader[0]?.ToString() ?? string.Empty;
                    foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(definition, @"N?'((?:''|[^'])*)'"))
                    {
                        string value = match.Groups[1].Value.Replace("''", "'");
                        if (!allowed.Contains(value, StringComparer.OrdinalIgnoreCase))
                        {
                            allowed.Add(value);
                        }
                    }
                }
            }

            if (allowed.Count == 0)
            {
                return priorities.FirstOrDefault();
            }

            foreach (string priority in priorities)
            {
                string? match = allowed.FirstOrDefault(value => string.Equals(value, priority, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }

                string normalizedPriority = BoDau(priority);
                match = allowed.FirstOrDefault(value => string.Equals(BoDau(value), normalizedPriority, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }
            }

            return null;
        }

        private static string? ChonTrangThaiGanDung(List<string> allowed, params string[] priorities)
        {
            foreach (string priority in priorities)
            {
                string p = BoDau(priority).ToLowerInvariant();
                string? match = allowed.FirstOrDefault(value =>
                {
                    string v = BoDau(value).ToLowerInvariant();
                    return (p.Contains("trong") && v.Contains("trong")) ||
                           ((p.Contains("bao tri") || p.Contains("sua")) && (v.Contains("bao tri") || v.Contains("sua"))) ||
                           ((p.Contains("thue") || p.Contains("co khach")) && (v.Contains("thue") || v.Contains("co khach"))) ||
                           (p.Contains("dat") && v.Contains("dat")) ||
                           (p.Contains("huy") && v.Contains("huy")) ||
                           (p.Contains("tra") && v.Contains("tra")) ||
                           (p.Contains("thanh toan") && v.Contains("thanh toan"));
                });
                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }
            }

            return null;
        }

        private static decimal GetDecimal(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) &&
                   row[column] != DBNull.Value &&
                   decimal.TryParse(row[column]?.ToString(), out decimal value)
                ? value
                : 0;
        }

        private static int? GetNullableInt(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) &&
                   row[column] != DBNull.Value &&
                   int.TryParse(row[column]?.ToString(), out int value)
                ? value
                : null;
        }

        private static DateTime? GetNullableDate(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) &&
                   row[column] != DBNull.Value &&
                   DateTime.TryParse(row[column]?.ToString(), out DateTime value)
                ? value
                : null;
        }

        private static string BoDau(string value)
        {
            string formD = (value ?? string.Empty).Normalize(System.Text.NormalizationForm.FormD);
            string withoutMarks = new(formD
                .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray());
            return withoutMarks
                .Replace("đ", "d")
                .Replace("Đ", "D")
                .Replace("đ", "d")
                .Replace("Đ", "D")
                .Normalize(System.Text.NormalizationForm.FormC);
        }

        private record ThoiGianLuuTruResult(string Text, bool QuaGio);

        private record ThanhToanHoaDonItem(string KhoanMuc, decimal SoTien)
        {
            public string SoTienHienThi => SoTien < 0
                ? $"- {Math.Abs(SoTien):N0} VND"
                : $"{SoTien:N0} VND";
        }

        private class RoomStayData
        {
            public int? MaThue { get; set; }
            public int? MaDatPhong { get; set; }
            public string HoTen { get; set; } = string.Empty;
            public string SDT { get; set; } = string.Empty;
            public string LoaiKhach { get; set; } = string.Empty;
            public int SoNguoi { get; set; }
            public DateTime? NgayNhanThucTe { get; set; }
            public DateTime? NgayNhanDat { get; set; }
            public DateTime? NgayTraDat { get; set; }
            public string CheDoDatPhong { get; set; } = string.Empty;
            public decimal TienCoc { get; set; }
            public decimal TienPhong { get; set; }
            public decimal TienGiaHan { get; set; }
            public decimal PhuPhi { get; set; }
            public string GhiChu { get; set; } = string.Empty;
            public string TrangThai { get; set; } = string.Empty;
            public int SoPhongDoan { get; set; } = 1;
            public string DanhSachPhongDoan { get; set; } = string.Empty;
        }

        private class DichVuChiTietItem
        {
            public int? MaPhatSinh { get; set; }
            public string Ten { get; set; } = string.Empty;
            public int SoLuong { get; set; }
            public decimal DonGia { get; set; }
            public bool LaDichVuCheckIn { get; set; }
            public decimal ThanhTien => SoLuong * DonGia;
        }
    }
}

