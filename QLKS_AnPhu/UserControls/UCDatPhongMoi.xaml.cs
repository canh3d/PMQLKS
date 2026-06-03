using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.UserControls
{
    public partial class UCDatPhongMoi : UserControl
    {
        private enum CheDoDatPhong
        {
            TheoNgay,
            QuaDem,
            TheoGio
        }

        private const string ThanhToanMarker = "[DATPHONG]";

        public event EventHandler? CloseRequested;
        public event EventHandler<PhongDTO>? DatPhongRequested;

        public bool NhanNgay => RbNhanNgay?.IsChecked == true;
        public decimal TienPhong => tienPhong;
        public decimal TienDichVu => tienDichVu;
        public decimal TienDatCoc => tienDatCoc;

        public DatPhongRequestDTO TaoYeuCauDatPhong()
        {
            TinhTongTien();
            CapNhatGhiChuThanhToan();

            return new DatPhongRequestDTO
            {
                Phong = phong,
                KhachHang = new KhachHangDTO
                {
                    HoTen = TxtHoTen.Text.Trim(),
                    SDT = TxtSDT.Text.Trim(),
                    CCCD = TxtCCCD.Text.Trim(),
                    DiaChi = TxtDiaChi.Text.Trim(),
                    GioiTinh = GetComboText(CboGioiTinh),
                    NgaySinh = DpNgaySinh.SelectedDate,
                    LoaiKhach = GetComboText(CboLoaiKhach),
                    TrangThai = "Đang hoạt động"
                },
                NgayNhan = LayNgayNhanLuu(),
                NgayTra = LayNgayTraLuu(),
                SoNguoi = ParsePositiveInt(TxtSoNguoi.Text, 1),
                NhanNgay = NhanNgay,
                CheDoDatPhong = LayCheDoDatPhong(),
                TienCoc = NhanNgay ? 0 : tienDatCoc,
                TienPhong = tienPhong,
                TienDichVu = tienDichVu,
                DichVuDaThem = dichVuDaThem.Select(item => new DichVuDatPhongDTO
                {
                    Ma = item.Ma,
                    Ten = item.Ten,
                    SoLuong = item.SoLuong,
                    DonGia = item.DonGia
                }).ToList(),
                GhiChu = phong.GhiChu == "--" ? string.Empty : phong.GhiChu
            };
        }

        private readonly DichVuVatTuBUS dichVuVatTuBUS = new();
        private readonly PhongBUS phongBUS = new();
        private PhongDTO phong;
        private readonly KhachHangDTO? khachHangMacDinh;
        private List<PhongDTO> danhSachPhong = new();
        private readonly ObservableCollection<DichVuDatPhongItem> dichVuDaThem = new();
        private CheDoDatPhong cheDoHienTai = CheDoDatPhong.TheoNgay;
        private bool dangNapPhong;
        private bool dangCapNhatDatCoc;
        private decimal datCocGoiYTruoc = -1;
        private decimal tienPhong;
        private decimal tienDichVu;
        private decimal tienDatCoc;

        public UCDatPhongMoi(PhongDTO phong)
        {
            this.phong = phong;
            InitializeComponent();
            Loaded += UCDatPhongMoi_Loaded;
        }

        public UCDatPhongMoi(PhongDTO phong, KhachHangDTO khachHang) : this(phong)
        {
            khachHangMacDinh = khachHang;
        }

        private void UCDatPhongMoi_Loaded(object sender, RoutedEventArgs e)
        {
            DgDichVuDaThem.ItemsSource = dichVuDaThem;
            DpNgaySinh.SelectedDate = DateTime.Today;
            DpNgayNhan.SelectedDate = DateTime.Now;
            DpNgayTra.SelectedDate = DateTime.Now.AddDays(1);
            TxtGioNhanNgay.Text = "14:00";
            TxtGioTraNgay.Text = "12:00";
            TxtGioNhan.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            NapThongTinPhongTuDatabase();
            NapDichVu();
            NapThongTinKhachHang();
            ChuyenCheDo(CheDoDatPhong.TheoNgay);
            CapNhatHienThiDatCoc();
        }

        private void NapThongTinKhachHang()
        {
            if (khachHangMacDinh == null)
            {
                return;
            }

            TxtHoTen.Text = khachHangMacDinh.HoTen;
            TxtSDT.Text = khachHangMacDinh.SDT;
            TxtCCCD.Text = khachHangMacDinh.CCCD;
            TxtDiaChi.Text = khachHangMacDinh.DiaChi;
            DpNgaySinh.SelectedDate = khachHangMacDinh.NgaySinh ?? DateTime.Today;
            SelectComboItem(CboGioiTinh, string.IsNullOrWhiteSpace(khachHangMacDinh.GioiTinh) ? "Nam" : khachHangMacDinh.GioiTinh);
            SelectComboItem(CboLoaiKhach, string.IsNullOrWhiteSpace(khachHangMacDinh.LoaiKhach) ? "Thường" : khachHangMacDinh.LoaiKhach);
        }

        private void NapThongTinPhong()
        {
            CboLoaiPhong.Items.Clear();
            CboLoaiPhong.Items.Add(new ComboBoxItem { Content = string.IsNullOrWhiteSpace(phong.LoaiPhong) ? "Loại phòng" : phong.LoaiPhong });
            CboLoaiPhong.SelectedIndex = 0;

            CboPhong.Items.Clear();
            CboPhong.Items.Add(new ComboBoxItem { Content = phong.MaHienThi });
            CboPhong.SelectedIndex = 0;
        }

        private void NapThongTinPhongTuDatabase()
        {
            dangNapPhong = true;

            try
            {
                danhSachPhong = phongBUS.LayDanhSach();
            }
            catch
            {
                danhSachPhong = new List<PhongDTO>();
            }

            if (danhSachPhong.Count == 0)
            {
                danhSachPhong.Add(phong);
            }

            phong = danhSachPhong.FirstOrDefault(item => item.Ma == phong.Ma) ?? phong;

            List<string> loaiPhong = danhSachPhong
                .Select(item => string.IsNullOrWhiteSpace(item.LoaiPhong) ? "Loại phòng" : item.LoaiPhong)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item)
                .ToList();

            CboLoaiPhong.ItemsSource = loaiPhong;
            CboLoaiPhong.SelectedItem = string.IsNullOrWhiteSpace(phong.LoaiPhong) ? loaiPhong.FirstOrDefault() : phong.LoaiPhong;

            dangNapPhong = false;
            NapPhongTheoLoai(CboLoaiPhong.SelectedItem?.ToString(), phong);
        }

        private void NapPhongTheoLoai(string? loaiPhong, PhongDTO? phongCanChon = null)
        {
            dangNapPhong = true;

            List<PhongDTO> danhSachTheoLoai = danhSachPhong
                .Where(item =>
                    string.IsNullOrWhiteSpace(loaiPhong) ||
                    string.Equals(item.LoaiPhong, loaiPhong, StringComparison.OrdinalIgnoreCase) ||
                    (string.IsNullOrWhiteSpace(item.LoaiPhong) && loaiPhong == "Loại phòng"))
                .OrderBy(item => item.Tang)
                .ThenBy(item => item.MaHienThi)
                .ToList();

            CboPhong.ItemsSource = danhSachTheoLoai;
            CboPhong.SelectedItem = phongCanChon != null && danhSachTheoLoai.Any(item => item.Ma == phongCanChon.Ma)
                ? danhSachTheoLoai.First(item => item.Ma == phongCanChon.Ma)
                : danhSachTheoLoai.FirstOrDefault();

            if (CboPhong.SelectedItem is PhongDTO selectedPhong)
            {
                phong = selectedPhong;
            }

            dangNapPhong = false;
            TinhTongTien();
        }

        private void CboLoaiPhong_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dangNapPhong)
            {
                return;
            }

            NapPhongTheoLoai(CboLoaiPhong.SelectedItem?.ToString());
        }

        private void CboPhong_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dangNapPhong || CboPhong.SelectedItem is not PhongDTO selectedPhong)
            {
                return;
            }

            phong = selectedPhong;

            if (!string.Equals(CboLoaiPhong.SelectedItem?.ToString(), phong.LoaiPhong, StringComparison.OrdinalIgnoreCase))
            {
                dangNapPhong = true;
                CboLoaiPhong.SelectedItem = string.IsNullOrWhiteSpace(phong.LoaiPhong) ? "Loại phòng" : phong.LoaiPhong;
                dangNapPhong = false;
            }

            TinhTongTien();
        }

        private void NapDichVu()
        {
            try
            {
                CboDichVu.ItemsSource = dichVuVatTuBUS.LayDanhSach();
                CboDichVu.SelectedIndex = 0;
            }
            catch
            {
                CboDichVu.ItemsSource = null;
            }
        }

        private void BtnDatTheoNgay_Click(object sender, RoutedEventArgs e)
        {
            ChuyenCheDo(CheDoDatPhong.TheoNgay);
        }

        private void BtnDatQuaDem_Click(object sender, RoutedEventArgs e)
        {
            ChuyenCheDo(CheDoDatPhong.QuaDem);
        }

        private void BtnDatTheoGio_Click(object sender, RoutedEventArgs e)
        {
            ChuyenCheDo(CheDoDatPhong.TheoGio);
        }

        private void ChuyenCheDo(CheDoDatPhong cheDo)
        {
            cheDoHienTai = cheDo;
            CapNhatNutCheDo();

            bool theoGio = cheDo == CheDoDatPhong.TheoGio;
            PanelNgayNhan.Visibility = theoGio ? Visibility.Collapsed : Visibility.Visible;
            PanelNgayTra.Visibility = theoGio ? Visibility.Collapsed : Visibility.Visible;
            TxtGioNhan.Visibility = theoGio ? Visibility.Visible : Visibility.Collapsed;
            PanelDatTheoGio.Visibility = theoGio ? Visibility.Visible : Visibility.Collapsed;

            if (theoGio)
            {
                GrpThongTinDatPhong.Header = "THÔNG TIN ĐẶT PHÒNG THEO GIỜ";
                LblNgayNhan.Text = "Giờ nhận";
                LblNgayTra.Text = "Số giờ thuê";
            }
            else if (cheDo == CheDoDatPhong.QuaDem)
            {
                GrpThongTinDatPhong.Header = "THÔNG TIN ĐẶT PHÒNG QUA ĐÊM";
                LblNgayNhan.Text = "Ngày nhận";
                LblNgayTra.Text = "Ngày trả";
            }
            else
            {
                GrpThongTinDatPhong.Header = "THÔNG TIN ĐẶT PHÒNG THEO NGÀY";
                LblNgayNhan.Text = "Ngày nhận";
                LblNgayTra.Text = "Ngày trả";
            }

            TinhTongTien();
        }

        private void CapNhatNutCheDo()
        {
            SetModeButton(BtnDatTheoNgay, cheDoHienTai == CheDoDatPhong.TheoNgay);
            SetModeButton(BtnDatQuaDem, cheDoHienTai == CheDoDatPhong.QuaDem);
            SetModeButton(BtnDatTheoGio, cheDoHienTai == CheDoDatPhong.TheoGio);
        }

        private static void SetModeButton(Button button, bool active)
        {
            button.Background = active ? BrushFromHex("#2563EB") : BrushFromHex("#F2F4F7");
            button.BorderBrush = active ? BrushFromHex("#2563EB") : BrushFromHex("#D0D5DD");
            button.Foreground = active ? Brushes.White : BrushFromHex("#8B95A5");
        }

        private void BtnQuickHour_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Content is string content)
            {
                TxtSoGioThue.Text = content.Replace("h", string.Empty);
                ChuyenCheDo(CheDoDatPhong.TheoGio);
            }
        }

        private void BtnThemDichVu_Click(object sender, RoutedEventArgs e)
        {
            if (CboDichVu.SelectedItem is not DichVuVatTuDTO dichVu)
            {
                MessageBox.Show("Vui lòng chọn dịch vụ cần thêm.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtSoLuongDichVu.Text.Trim(), out int soLuong) || soLuong <= 0)
            {
                MessageBox.Show("Số lượng dịch vụ phải lớn hơn 0.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DichVuDatPhongItem? existing = dichVuDaThem.FirstOrDefault(item => item.Ma == dichVu.Ma);

            if (existing != null)
            {
                existing.SoLuong += soLuong;
                DgDichVuDaThem.Items.Refresh();
            }
            else
            {
                dichVuDaThem.Add(new DichVuDatPhongItem
                {
                    Ma = dichVu.Ma,
                    Ten = dichVu.Ten,
                    SoLuong = soLuong,
                    DonGia = dichVu.DonGia
                });
            }

            TinhTongTien();
        }

        private void TinhTien_Changed(object sender, RoutedEventArgs e)
        {
            if (dangCapNhatDatCoc)
            {
                return;
            }

            TinhTongTien();
        }

        private void LoaiDatPhong_Changed(object sender, RoutedEventArgs e)
        {
            CapNhatHienThiDatCoc();
            TinhTongTien();
        }

        private void CapNhatHienThiDatCoc()
        {
            if (LblDatCoc == null || TxtDatCoc == null || TxtTienDatCoc == null)
            {
                return;
            }

            Visibility visibility = NhanNgay ? Visibility.Collapsed : Visibility.Visible;
            LblDatCoc.Visibility = visibility;
            TxtDatCoc.Visibility = visibility;
            TxtTienDatCoc.Visibility = visibility;

            if (NhanNgay)
            {
                dangCapNhatDatCoc = true;
                TxtDatCoc.Text = "0";
                dangCapNhatDatCoc = false;
                datCocGoiYTruoc = 0;
            }
        }

        private void TinhTongTien()
        {
            if (TxtDatCoc == null || TxtTienPhong == null || TxtTienDichVu == null || TxtTienDatCoc == null || TxtTongThanhToan == null)
            {
                return;
            }

            tienPhong = TinhTienPhong();
            CapNhatGoiYDatCoc();
            tienDichVu = dichVuDaThem.Sum(item => item.ThanhTien);
            tienDatCoc = NhanNgay ? 0 : ParseDecimal(TxtDatCoc.Text);

            decimal tongThanhToan = NhanNgay
                ? tienPhong + tienDichVu
                : tienDatCoc + tienDichVu;

            TxtTienPhong.Text = $"Tiền phòng: {tienPhong:N0} đ";
            TxtTienDichVu.Text = $"Tiền dịch vụ: {tienDichVu:N0} đ";
            TxtTienDatCoc.Text = $"Đặt cọc: {tienDatCoc:N0} đ";
            TxtTongThanhToan.Text = NhanNgay
                ? $"Tổng thanh toán: {tongThanhToan:N0} đ"
                : $"Tổng thanh toán trước: {tongThanhToan:N0} đ";
        }

        private void CapNhatGoiYDatCoc()
        {
            if (NhanNgay)
            {
                dangCapNhatDatCoc = true;
                TxtDatCoc.Text = "0";
                dangCapNhatDatCoc = false;
                datCocGoiYTruoc = 0;
                return;
            }

            if (TxtDatCoc.IsKeyboardFocusWithin)
            {
                return;
            }

            decimal datCocHienTai = ParseDecimal(TxtDatCoc.Text);
            bool coTheCapNhat =
                string.IsNullOrWhiteSpace(TxtDatCoc.Text) ||
                datCocHienTai == 0 ||
                datCocHienTai == datCocGoiYTruoc;

            if (!coTheCapNhat)
            {
                return;
            }

            decimal goiY = LamTronTien(tienPhong * 0.3m);

            dangCapNhatDatCoc = true;
            TxtDatCoc.Text = goiY.ToString("0");
            dangCapNhatDatCoc = false;
            datCocGoiYTruoc = goiY;
        }

        private static decimal LamTronTien(decimal value)
        {
            const decimal step = 50000m;
            return value <= 0 ? 0 : Math.Ceiling(value / step) * step;
        }

        private decimal TinhTienPhong()
        {
            if (cheDoHienTai == CheDoDatPhong.TheoGio)
            {
                int soGio = ParsePositiveInt(TxtSoGioThue.Text, 1);
                decimal giaGio = phong.GiaGio > 0 ? phong.GiaGio : LayGiaMacDinh();
                return giaGio * soGio;
            }

            DateTime ngayNhan = DpNgayNhan.SelectedDate ?? DateTime.Today;
            DateTime ngayTra = DpNgayTra.SelectedDate ?? ngayNhan.AddDays(1);
            int soNgay = Math.Max(1, (ngayTra.Date - ngayNhan.Date).Days);

            if (cheDoHienTai == CheDoDatPhong.QuaDem)
            {
                decimal giaDem = phong.GiaDem > 0 ? phong.GiaDem : LayGiaMacDinh();
                return giaDem * soNgay;
            }

            decimal giaNgay = phong.GiaNgay > 0 ? phong.GiaNgay : LayGiaMacDinh();
            return giaNgay * soNgay;
        }

        private decimal LayGiaMacDinh()
        {
            if (phong.GiaPhong > 0)
            {
                return phong.GiaPhong;
            }

            if (phong.GiaDem > 0)
            {
                return phong.GiaDem;
            }

            if (phong.GiaNgay > 0)
            {
                return phong.GiaNgay;
            }

            return phong.GiaGio;
        }

        private void BtnLuu_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtHoTen.Text))
            {
                MessageBox.Show("Vui lòng nhập họ tên khách hàng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtHoTen.Focus();
                return;
            }

            if (cheDoHienTai == CheDoDatPhong.TheoGio && !DateTime.TryParseExact(TxtGioNhan.Text.Trim(), "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                MessageBox.Show("Giờ nhận phải đúng định dạng dd/MM/yyyy HH:mm.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtGioNhan.Focus();
                return;
            }

            DatPhongRequested?.Invoke(this, phong);
        }

        private void CapNhatGhiChuThanhToan()
        {
            string ghiChu = phong.GhiChu == "--" ? string.Empty : phong.GhiChu;
            int markerIndex = ghiChu.IndexOf(ThanhToanMarker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
            {
                ghiChu = ghiChu[..markerIndex].Trim();
            }

            string thanhToan = $"{ThanhToanMarker} DatCoc={tienDatCoc:0};TienPhong={tienPhong:0};TienDichVu={tienDichVu:0};NhanNgay={NhanNgay}";
            phong.GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? thanhToan : $"{ghiChu} {thanhToan}";
        }

        private void BtnLamMoi_Click(object sender, RoutedEventArgs e)
        {
            TxtHoTen.Clear();
            TxtSDT.Clear();
            TxtCCCD.Clear();
            TxtDiaChi.Clear();
            TxtSoNguoi.Text = "1";
            TxtDatCoc.Text = "0";
            TxtGioNhanNgay.Text = "14:00";
            TxtGioTraNgay.Text = "12:00";
            TxtGioNhan.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            TxtSoGioThue.Text = "2";
            RbDatTruoc.IsChecked = true;
            dichVuDaThem.Clear();
            ChuyenCheDo(CheDoDatPhong.TheoNgay);
        }

        private void BtnDong_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private DateTime LayNgayNhanLuu()
        {
            if (cheDoHienTai == CheDoDatPhong.TheoGio &&
                DateTime.TryParseExact(TxtGioNhan.Text.Trim(), "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime gioNhan))
            {
                return gioNhan;
            }

            DateTime date = DpNgayNhan.SelectedDate ?? DateTime.Now;
            if (cheDoHienTai == CheDoDatPhong.QuaDem)
            {
                return date.Date.Add(LayGio(TxtGioNhanNgay.Text, new TimeSpan(20, 0, 0)));
            }

            TimeSpan gioNhanTheoNgay = LayGio(TxtGioNhanNgay.Text, new TimeSpan(14, 0, 0));
            return date.Date.Add(gioNhanTheoNgay);
        }

        private DateTime LayNgayTraLuu()
        {
            if (cheDoHienTai == CheDoDatPhong.TheoGio)
            {
                return LayNgayNhanLuu().AddHours(ParsePositiveInt(TxtSoGioThue.Text, 1));
            }

            DateTime date = DpNgayTra.SelectedDate ?? LayNgayNhanLuu().AddDays(1);
            if (cheDoHienTai == CheDoDatPhong.QuaDem)
            {
                return date.Date.Add(LayGio(TxtGioTraNgay.Text, new TimeSpan(8, 0, 0)));
            }

            return date.Date.Add(LayGio(TxtGioTraNgay.Text, new TimeSpan(12, 0, 0)));
        }

        private static TimeSpan LayGio(string value, TimeSpan defaultValue)
        {
            if (TimeSpan.TryParseExact(value.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan result) ||
                TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            return defaultValue;
        }

        private string LayCheDoDatPhong()
        {
            return cheDoHienTai switch
            {
                CheDoDatPhong.TheoGio => "Theo giờ",
                CheDoDatPhong.QuaDem => "Qua đêm",
                _ => "Theo ngày"
            };
        }

        private static string GetComboText(ComboBox comboBox)
        {
            return comboBox.SelectedItem is ComboBoxItem item
                ? item.Content?.ToString() ?? string.Empty
                : comboBox.Text;
        }

        private static int ParsePositiveInt(string value, int fallback)
        {
            return int.TryParse(value.Trim(), out int result) && result > 0 ? result : fallback;
        }

        private static decimal ParseDecimal(string value)
        {
            string normalized = value.Trim().Replace(",", string.Empty).Replace(".", string.Empty);
            return decimal.TryParse(normalized, out decimal result) ? result : 0;
        }

        private static Brush BrushFromHex(string hex)
        {
            return (Brush)new BrushConverter().ConvertFromString(hex)!;
        }

        private static void SelectComboItem(ComboBox comboBox, string value)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private class DichVuDatPhongItem
        {
            public int Ma { get; set; }
            public string Ten { get; set; } = string.Empty;
            public int SoLuong { get; set; }
            public decimal DonGia { get; set; }
            public decimal ThanhTien => SoLuong * DonGia;
        }
    }
}
