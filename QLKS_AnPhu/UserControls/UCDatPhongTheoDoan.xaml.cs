using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.UserControls
{
    public partial class UCDatPhongTheoDoan : UserControl
    {
        private readonly DichVuVatTuBUS dichVuVatTuBUS = new();
        private readonly ObservableCollection<PhongDoanItem> danhSachPhong = new();
        private PhongDoanItem? phongDangChon;
        private bool dangNap;
        private decimal tongTienPhong;
        private decimal tongTienDichVu;
        private decimal cocGoiY;

        public event EventHandler? CloseRequested;
        public event EventHandler<List<DatPhongRequestDTO>>? DatPhongDoanRequested;

        public UCDatPhongTheoDoan(IEnumerable<PhongDTO> danhSachPhongTrong)
        {
            dangNap = true;
            InitializeComponent();
            Loaded += (_, _) => KhoiTao(danhSachPhongTrong);
        }

        private void KhoiTao(IEnumerable<PhongDTO> danhSachPhongTrong)
        {
            dangNap = true;
            DpNgayNhan.SelectedDate = DateTime.Today;
            DpNgayTra.SelectedDate = DateTime.Today.AddDays(1);
            TxtGioNhan.Text = "14:00";
            TxtGioTra.Text = "12:00";
            TxtSoNguoi.Text = "1";
            TxtDatCoc.Text = "0";
            TxtSoLuongDichVu.Text = "1";

            danhSachPhong.Clear();
            foreach (PhongDTO phong in danhSachPhongTrong.Where(LaPhongCoTheDat).OrderBy(item => item.Tang).ThenBy(item => item.MaHienThi))
            {
                danhSachPhong.Add(new PhongDoanItem(phong));
            }

            DgPhong.ItemsSource = danhSachPhong;
            NapDichVu();
            DgPhong.SelectedIndex = danhSachPhong.Count > 0 ? 0 : -1;
            phongDangChon = DgPhong.SelectedItem as PhongDoanItem ?? danhSachPhong.FirstOrDefault();
            CapNhatDichVuPhongDangChon();
            dangNap = false;
            CapNhatTongTien();
        }

        private void BtnChonTatCa_Click(object sender, RoutedEventArgs e)
        {
            foreach (PhongDoanItem item in danhSachPhong)
            {
                item.Chon = true;
            }

            CapNhatTongTien();
        }

        private void BtnBoChon_Click(object sender, RoutedEventArgs e)
        {
            foreach (PhongDoanItem item in danhSachPhong)
            {
                item.Chon = false;
            }

            CapNhatTongTien();
        }

        private void BtnLamMoi_Click(object sender, RoutedEventArgs e)
        {
            TxtTenDaiDien.Clear();
            TxtSDT.Clear();
            TxtCCCD.Clear();
            TxtDiaChi.Clear();
            TxtGhiChu.Clear();
            CboGioiTinh.SelectedIndex = 0;
            CboLoaiKhach.SelectedIndex = 2;
            RbDatTruoc.IsChecked = true;
            DpNgayNhan.SelectedDate = DateTime.Today;
            DpNgayTra.SelectedDate = DateTime.Today.AddDays(1);
            TxtGioNhan.Text = "14:00";
            TxtGioTra.Text = "12:00";
            TxtSoNguoi.Text = "1";
            TxtSoLuongDichVu.Text = "1";
            foreach (PhongDoanItem item in danhSachPhong)
            {
                item.Chon = false;
                item.DichVuDaThem.Clear();
            }

            phongDangChon = DgPhong.SelectedItem as PhongDoanItem ?? danhSachPhong.FirstOrDefault();
            CapNhatDichVuPhongDangChon();
            CapNhatTongTien(true);
        }

        private void NapDichVu()
        {
            try
            {
                CboDichVuDoan.ItemsSource = dichVuVatTuBUS.LayDanhSach();
                CboDichVuDoan.SelectedIndex = 0;
            }
            catch
            {
                CboDichVuDoan.ItemsSource = null;
            }
        }

        private void BtnThemDichVu_Click(object sender, RoutedEventArgs e)
        {
            PhongDoanItem? phongDichVu = LayPhongDangChon();
            if (phongDichVu == null)
            {
                MessageBox.Show("Vui lòng chọn phòng cần thêm dịch vụ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CboDichVuDoan.SelectedItem is not DichVuVatTuDTO dichVu)
            {
                MessageBox.Show("Vui lòng chọn dịch vụ cần thêm.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtSoLuongDichVu.Text.Trim(), out int soLuong) || soLuong <= 0)
            {
                MessageBox.Show("Số lượng dịch vụ phải lớn hơn 0.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtSoLuongDichVu.Focus();
                return;
            }

            phongDichVu.Chon = true;
            DichVuDoanItem? existing = phongDichVu.DichVuDaThem.FirstOrDefault(item => item.Ma == dichVu.Ma);
            if (existing != null)
            {
                existing.SoLuong += soLuong;
                DgDichVuDoan.Items.Refresh();
            }
            else
            {
                phongDichVu.DichVuDaThem.Add(new DichVuDoanItem
                {
                    Ma = dichVu.Ma,
                    Ten = dichVu.Ten,
                    SoLuong = soLuong,
                    DonGia = dichVu.DonGia
                });
            }

            DgPhong.Items.Refresh();
            CapNhatDichVuPhongDangChon();
            CapNhatTongTien();
        }

        private void BtnXoaDichVu_Click(object sender, RoutedEventArgs e)
        {
            PhongDoanItem? phongDichVu = LayPhongDangChon();
            if (phongDichVu == null)
            {
                MessageBox.Show("Vui lòng chọn phòng cần xóa dịch vụ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DgDichVuDoan.SelectedItem is not DichVuDoanItem selected)
            {
                MessageBox.Show("Vui lòng chọn dịch vụ cần xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            phongDichVu.DichVuDaThem.Remove(selected);
            CapNhatDichVuPhongDangChon();
            CapNhatTongTien();
        }

        private void DgDichVuDoan_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            PhongDoanItem? phongDichVu = LayPhongDangChon();
            if (phongDichVu == null || DgDichVuDoan.SelectedItem is not DichVuDoanItem selected)
            {
                return;
            }

            phongDichVu.DichVuDaThem.Remove(selected);
            CapNhatDichVuPhongDangChon();
            CapNhatTongTien();
        }

        private void BtnDong_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BtnLuu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DatPhongDoanRequested?.Invoke(this, TaoDanhSachYeuCau());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ThongTinDatPhong_Changed(object sender, RoutedEventArgs e)
        {
            if (!dangNap && IsLoaded)
            {
                CapNhatTongTien();
            }
        }

        private void PhongSelection_Changed(object sender, RoutedEventArgs e)
        {
            if (!dangNap && IsLoaded)
            {
                CapNhatTongTien();
            }
        }

        private void DgPhong_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgPhong.SelectedItem is PhongDoanItem selected)
            {
                phongDangChon = selected;
            }

            CapNhatDichVuPhongDangChon();
            CapNhatTongTien();
        }

        private PhongDoanItem? LayPhongDangChon()
        {
            phongDangChon = DgPhong.SelectedItem as PhongDoanItem ?? phongDangChon ?? danhSachPhong.FirstOrDefault();
            return phongDangChon;
        }

        private void CapNhatDichVuPhongDangChon()
        {
            PhongDoanItem? selected = LayPhongDangChon();
            DgDichVuDoan.ItemsSource = selected?.DichVuDaThem;
            TxtPhongDangThem.Text = selected == null
                ? "Chọn một phòng để thêm dịch vụ"
                : $"Phòng đang chọn: {selected.Phong.MaHienThi}";
            TxtTongDichVu.Text = $"Dịch vụ phòng: {FormatMoney(selected?.TongDichVu ?? 0)}";
        }

        private List<DatPhongRequestDTO> TaoDanhSachYeuCau()
        {
            string tenDaiDien = TxtTenDaiDien.Text.Trim();
            if (string.IsNullOrWhiteSpace(tenDaiDien))
            {
                throw new InvalidOperationException("Vui lòng nhập tên đoàn hoặc khách đại diện.");
            }

            List<PhongDoanItem> phongDaChon = danhSachPhong.Where(item => item.Chon).ToList();
            if (phongDaChon.Count == 0)
            {
                throw new InvalidOperationException("Vui lòng chọn ít nhất một phòng trống.");
            }

            DateTime ngayNhan = LayNgayNhan();
            DateTime ngayTra = LayNgayTra();
            if (ngayTra <= ngayNhan)
            {
                throw new InvalidOperationException("Ngày trả phải sau ngày nhận.");
            }

            int soNguoi = Math.Max(1, ParseInt(TxtSoNguoi.Text, 1));
            decimal tongDatCoc = RbNhanNgay.IsChecked == true ? 0 : ParseMoney(TxtDatCoc.Text);
            decimal tongCocConLai = tongDatCoc;
            decimal tongDichVu = phongDaChon.Sum(item => item.TongDichVu);
            decimal tongTienConLai = phongDaChon.Sum(item => TinhTienPhong(item.Phong, ngayNhan, ngayTra));

            KhachHangDTO khachHang = new()
            {
                HoTen = tenDaiDien,
                SDT = TxtSDT.Text.Trim(),
                CCCD = TxtCCCD.Text.Trim(),
                DiaChi = TxtDiaChi.Text.Trim(),
                GioiTinh = GetComboText(CboGioiTinh),
                LoaiKhach = GetComboText(CboLoaiKhach),
                TrangThai = "Đang hoạt động",
                GhiChu = TxtGhiChu.Text.Trim()
            };

            List<DatPhongRequestDTO> requests = new();
            for (int i = 0; i < phongDaChon.Count; i++)
            {
                PhongDoanItem phongItem = phongDaChon[i];
                PhongDTO phong = phongItem.Phong;
                decimal tienPhong = TinhTienPhong(phong, ngayNhan, ngayTra);
                decimal tienCoc = 0;
                decimal tienDichVu = phongItem.TongDichVu;

                if (tongDatCoc > 0)
                {
                    tienCoc = i == phongDaChon.Count - 1
                        ? tongCocConLai
                        : Math.Round(tongDatCoc / phongDaChon.Count, 0);
                    tongCocConLai -= tienCoc;
                }

                requests.Add(new DatPhongRequestDTO
                {
                    Phong = phong,
                    KhachHang = khachHang,
                    NgayNhan = ngayNhan,
                    NgayTra = ngayTra,
                    SoNguoi = Math.Max(1, (int)Math.Ceiling((double)soNguoi / phongDaChon.Count)),
                    NhanNgay = RbNhanNgay.IsChecked == true,
                    CheDoDatPhong = "Theo ngày",
                    TienCoc = tienCoc,
                    TienPhong = tienPhong,
                    TienDichVu = tienDichVu,
                    DichVuDaThem = phongItem.DichVuDaThem.Select(item => new DichVuDatPhongDTO
                    {
                        Ma = item.Ma,
                        Ten = item.Ten,
                        SoLuong = item.SoLuong,
                        DonGia = item.DonGia
                    }).ToList(),
                    GhiChu = TaoGhiChuDoan(phongDaChon.Count, tongDatCoc, tongTienConLai, tienDichVu, TaoMoTaDichVu(phongItem), TxtGhiChu.Text.Trim())
                });
            }

            return requests;
        }

        private void CapNhatTongTien(bool datCocTheoGoiY = false)
        {
            DateTime ngayNhan = LayNgayNhan();
            DateTime ngayTra = LayNgayTra();
            List<PhongDoanItem> phongDaChon = danhSachPhong.Where(item => item.Chon).ToList();
            tongTienPhong = phongDaChon.Sum(item => TinhTienPhong(item.Phong, ngayNhan, ngayTra));
            tongTienDichVu = phongDaChon.Sum(item => item.TongDichVu);
            cocGoiY = RbNhanNgay.IsChecked == true ? 0 : LamTronTien((tongTienPhong + tongTienDichVu) * 0.3m);

            TxtSoPhongChon.Text = $"{phongDaChon.Count} phòng";
            TxtTienPhong.Text = FormatMoney(tongTienPhong);
            TxtTienDichVu.Text = FormatMoney(tongTienDichVu);
            TxtTongDichVu.Text = $"Tổng dịch vụ: {FormatMoney(tongTienDichVu)}";
            TxtTongTamTinh.Text = FormatMoney(tongTienPhong + tongTienDichVu);
            TxtCocGoiY.Text = FormatMoney(cocGoiY);
            TxtTrangThai.Text = danhSachPhong.Count == 0 ? "Không có phòng trống phù hợp." : string.Empty;

            if (RbNhanNgay.IsChecked == true)
            {
                TxtDatCoc.Text = "0";
                TxtDatCoc.IsEnabled = false;
            }
            else
            {
                TxtDatCoc.IsEnabled = true;
                if (datCocTheoGoiY || string.IsNullOrWhiteSpace(TxtDatCoc.Text) || ParseMoney(TxtDatCoc.Text) == 0)
                {
                    TxtDatCoc.Text = cocGoiY.ToString("N0", CultureInfo.InvariantCulture);
                }
            }
        }

        private DateTime LayNgayNhan()
        {
            DateTime ngay = DpNgayNhan?.SelectedDate ?? DateTime.Today;
            return RbNhanNgay?.IsChecked == true && ngay.Date == DateTime.Today
                ? DateTime.Now
                : ngay.Date.Add(LayGio(TxtGioNhan?.Text, new TimeSpan(14, 0, 0)));
        }

        private DateTime LayNgayTra()
        {
            DateTime ngay = DpNgayTra?.SelectedDate ?? DateTime.Today.AddDays(1);
            return ngay.Date.Add(LayGio(TxtGioTra?.Text, new TimeSpan(12, 0, 0)));
        }

        private static TimeSpan LayGio(string? value, TimeSpan defaultValue)
        {
            if (TimeSpan.TryParseExact(value?.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan result) ||
                TimeSpan.TryParse(value?.Trim(), CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            return defaultValue;
        }

        private static decimal TinhTienPhong(PhongDTO phong, DateTime ngayNhan, DateTime ngayTra)
        {
            int soNgay = Math.Max(1, (ngayTra.Date - ngayNhan.Date).Days);
            decimal donGia = phong.GiaNgay > 0 ? phong.GiaNgay : phong.GiaPhong;
            if (donGia <= 0)
            {
                donGia = phong.GiaDem > 0 ? phong.GiaDem : phong.GiaGio;
            }

            return donGia * soNgay;
        }

        private static bool LaPhongCoTheDat(PhongDTO phong)
        {
            string trangThai = RemoveDiacritics(phong.TrangThai).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(trangThai))
            {
                return true;
            }

            return trangThai.Contains("trong")
                   || (!trangThai.Contains("thue")
                       && !trangThai.Contains("dang")
                       && !trangThai.Contains("dat")
                       && !trangThai.Contains("sua")
                       && !trangThai.Contains("don")
                       && !trangThai.Contains("ban"));
        }

        private static string TaoMoTaDichVu(PhongDoanItem phongItem)
        {
            return string.Join(", ", phongItem.DichVuDaThem.Select(item => $"{item.Ten} x{item.SoLuong}"));
        }

        private static string TaoGhiChuDoan(int soPhong, decimal tongCoc, decimal tongTienPhong, decimal tongTienDichVu, string dichVu, string ghiChuNguoiDung)
        {
            string marker = $"[DAT_DOAN] SoPhong={soPhong}; TongCoc={tongCoc:N0}; TongTienPhong={tongTienPhong:N0}; TongTienDichVu={tongTienDichVu:N0}";
            if (!string.IsNullOrWhiteSpace(dichVu))
            {
                marker += $"; DichVu={dichVu}";
            }

            return string.IsNullOrWhiteSpace(ghiChuNguoiDung) ? marker : $"{marker} - {ghiChuNguoiDung}";
        }

        private static decimal LamTronTien(decimal value)
        {
            const decimal buoc = 50000m;
            return Math.Ceiling(value / buoc) * buoc;
        }

        private static int ParseInt(string value, int defaultValue)
        {
            return int.TryParse(value?.Trim(), out int result) ? result : defaultValue;
        }

        private static decimal ParseMoney(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            string normalized = Regex.Replace(value, "[^0-9]", string.Empty);
            return decimal.TryParse(normalized, out decimal result) ? result : 0;
        }

        private static string FormatMoney(decimal value)
        {
            return $"{value:N0} VND";
        }

        private static string GetComboText(ComboBox comboBox)
        {
            return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? comboBox.Text;
        }

        private static string RemoveDiacritics(string text)
        {
            string normalized = text.Normalize(NormalizationForm.FormD);
            StringBuilder builder = new();
            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
        }
    }

    public class PhongDoanItem : INotifyPropertyChanged
    {
        private bool chon;

        public PhongDoanItem(PhongDTO phong)
        {
            Phong = phong;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public PhongDTO Phong { get; }
        public ObservableCollection<DichVuDoanItem> DichVuDaThem { get; } = new();
        public decimal TongDichVu => DichVuDaThem.Sum(item => item.ThanhTien);

        public bool Chon
        {
            get => chon;
            set
            {
                if (chon == value)
                {
                    return;
                }

                chon = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chon)));
            }
        }

        public string GiaNgayHienThi => $"{Phong.GiaNgay:N0}";
        public string GiaDemHienThi => $"{Phong.GiaDem:N0}";
        public string GiaGioHienThi => $"{Phong.GiaGio:N0}";
    }

    public class DichVuDoanItem
    {
        public int Ma { get; set; }
        public string Ten { get; set; } = string.Empty;
        public int SoLuong { get; set; }
        public decimal DonGia { get; set; }
        public decimal ThanhTien => SoLuong * DonGia;
        public string DonGiaHienThi => $"{DonGia:N0}";
        public string ThanhTienHienThi => $"{ThanhTien:N0}";
    }
}
