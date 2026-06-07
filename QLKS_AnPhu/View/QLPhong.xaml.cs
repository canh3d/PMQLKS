using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DTO;
using QLKS_AnPhu.UserControls;

namespace QLKS_AnPhu.View
{
    /// <summary>
    /// Interaction logic for QLPhong.xaml
    /// </summary>
    public partial class QLPhong : UserControl
    {
        private readonly PhongBUS phongBUS = new();
        private List<PhongDTO> danhSachGoc = new();
        private List<PhongDTO> danhSachHienThi = new();
        private PhongDTO? phongDangChon;
        private bool dangChonGoiY;

        public QLPhong()
        {
            InitializeComponent();
            Loaded += QLPhong_Loaded;
        }

        private void QLPhong_Loaded(object sender, RoutedEventArgs e)
        {
            TaiDuLieu();
        }

        private void TaiDuLieu()
        {
            try
            {
                TxtLoi.Text = string.Empty;
                danhSachGoc = phongBUS.LayDanhSach();
                NapComboTang();
                HienThiDanhSach(danhSachGoc);
            }
            catch (Exception ex)
            {
                danhSachGoc = new List<PhongDTO>();
                HienThiDanhSach(danhSachGoc);
                TxtLoi.Text = "Không tải được dữ liệu phòng từ database: " + ex.Message;
            }
        }

        private void NapComboTang()
        {
            string selected = GetComboText(CboTang);
            CboTang.Items.Clear();
            CboTang.Items.Add(new ComboBoxItem { Content = "Tất cả" });

            foreach (int tang in danhSachGoc.Select(item => item.Tang).Distinct().OrderBy(item => item))
            {
                CboTang.Items.Add(new ComboBoxItem { Content = $"Tầng {tang}" });
            }

            CboTang.SelectedIndex = 0;

            if (!string.IsNullOrWhiteSpace(selected))
            {
                foreach (ComboBoxItem item in CboTang.Items)
                {
                    if (item.Content?.ToString() == selected)
                    {
                        CboTang.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void HienThiDanhSach(List<PhongDTO> danhSach)
        {
            danhSachHienThi = danhSach;
            PanelPhongTheoTang.Children.Clear();

            foreach (IGrouping<int, PhongDTO> group in danhSach.GroupBy(item => item.Tang).OrderBy(item => item.Key))
            {
                StackPanel header = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
                header.Children.Add(new TextBlock
                {
                    Text = $"TẦNG {group.Key}",
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.DimGray,
                    FontSize = 13
                });
                header.Children.Add(new TextBlock
                {
                    Text = $"{group.Count()} phòng",
                    Margin = new Thickness(15, 0, 0, 0),
                    Foreground = System.Windows.Media.Brushes.DimGray,
                    FontSize = 12
                });
                PanelPhongTheoTang.Children.Add(header);

                WrapPanel wrapPanel = new() { Margin = new Thickness(0, 0, 0, 18) };

                foreach (PhongDTO phong in group)
                {
                    UCPhong ucPhong = new(phong);
                    ucPhong.PhongSelected += UcPhong_PhongSelected;
                    ucPhong.PhongDoubleClicked += UcPhong_PhongDoubleClicked;
                    wrapPanel.Children.Add(ucPhong);
                }

                PanelPhongTheoTang.Children.Add(wrapPanel);
            }

            ChonPhong(danhSach.FirstOrDefault());
        }

        private void UcPhong_PhongSelected(object? sender, PhongDTO phong)
        {
            ChonPhong(phong);
        }

        private void UcPhong_PhongDoubleClicked(object? sender, PhongDTO phong)
        {
            ChonPhong(phong);
            PhongChiTietWindow window = new(phong);
            DialogService.ShowDimmedDialogResult(window, Window.GetWindow(this));
            TaiDuLieu();
        }

        private void ChonPhong(PhongDTO? phong)
        {
            phongDangChon = phong;

            if (phong == null)
            {
                TxtCTMaPhong.Text = "--";
                TxtCTTrangThai.Text = "--";
                TxtCTTang.Text = "Tầng: --";
                TxtCTLoaiPhong.Text = "Loại phòng: --";
                TxtCTGiaPhong.Text = "Giá phòng: --";
                TxtCTKhach.Text = "Khách hiện tại: --";
                TxtCTGioNhan.Text = "Giờ nhận phòng: --";
                TxtCTGioTra.Text = "Giờ trả dự kiến: --";
                TxtCTGhiChu.Text = "--";
                return;
            }

            TxtCTMaPhong.Text = phong.MaHienThi;
            TxtCTTrangThai.Text = phong.TrangThai;
            TxtCTTang.Text = $"Tầng: {phong.Tang}";
            TxtCTLoaiPhong.Text = $"Loại phòng: {phong.LoaiPhong}";
            TxtCTGiaPhong.Text = $"Giá phòng: {phong.GiaPhongChiTiet}";
            TxtCTKhach.Text = $"Khách hiện tại: {phong.KhachHienTai}";
            TxtCTGioNhan.Text = $"Giờ nhận phòng: {phong.GioNhanPhong}";
            TxtCTGioTra.Text = $"Giờ trả dự kiến: {phong.GioTraDuKien}";
            TxtCTGhiChu.Text = phong.GhiChuHienThi;
        }

        private void BtnTim_Click(object sender, RoutedEventArgs e)
        {
            PopupGoiYTimKiem.IsOpen = false;
            string trangThai = GetComboText(CboTrangThai);
            string tang = GetComboText(CboTang);
            string tuKhoa = TxtTimKiem.Text.Trim();
            HienThiDanhSach(phongBUS.Loc(danhSachGoc, trangThai, tang, tuKhoa));
        }

        private void BtnLamMoi_Click(object sender, RoutedEventArgs e)
        {
            TxtTimKiem.Clear();
            CboTrangThai.SelectedIndex = 0;
            TaiDuLieu();
        }

        private void BtnDanhSachPhong_Click(object sender, RoutedEventArgs e)
        {
            DanhSachPhongWindow window = new();
            DialogService.ShowDimmedDialogResult(window, Window.GetWindow(this));
            TaiDuLieu();
        }

        private void BtnDatPhong_Click(object sender, RoutedEventArgs e)
        {
            if (phongDangChon == null)
            {
                MessageBox.Show("Vui lòng chọn phòng cần đặt.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!LaPhongCoTheDat(phongDangChon))
            {
                MessageBox.Show("Chi duoc dat phong dang trong, da don san sang su dung.", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UCDatPhongMoi ucDatPhong = new(phongDangChon);
            ucDatPhong.CloseRequested += UcDatPhong_CloseRequested;
            ucDatPhong.DatPhongRequested += UcDatPhong_DatPhongRequested;

            Window dialog = DialogService.CreateContentDialog(ucDatPhong, "Đặt phòng mới", 1100, 650);
            DialogService.ShowDimmedDialogResult(dialog, Window.GetWindow(this));

            ucDatPhong.CloseRequested -= UcDatPhong_CloseRequested;
            ucDatPhong.DatPhongRequested -= UcDatPhong_DatPhongRequested;
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
                bool nhanNgay = sender is UCDatPhongMoi { NhanNgay: true };
                if (sender is UCDatPhongMoi ucDatPhong)
                {
                    DatPhongRequestDTO request = ucDatPhong.TaoYeuCauDatPhong();
                    if (nhanNgay)
                    {
                        decimal giamGia = request.KhachHang.LoaiKhach.Contains("VIP", StringComparison.OrdinalIgnoreCase) ? Math.Round(request.TienPhong * 0.1m, 0) : 0;
                        if (!DialogService.XacNhanThanhToanCheckIn(Window.GetWindow(this), "Phòng " + request.Phong.MaHienThi, request.TienPhong, request.TienDichVu, giamGia: giamGia))
                        {
                            return;
                        }
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

                MessageBox.Show(nhanNgay ? "Nhận phòng thành công." : "Đặt phòng thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void BtnDatPhongTheoDoan_Click(object sender, RoutedEventArgs e)
        {
            List<PhongDTO> phongTrong = danhSachHienThi
                .Where(LaPhongCoTheDat)
                .ToList();

            if (phongTrong.Count == 0)
            {
                MessageBox.Show("Không có phòng trong danh sách hiện tại.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UCDatPhongTheoDoan ucDatPhong = new(phongTrong);
            ucDatPhong.CloseRequested += UcDatPhongTheoDoan_CloseRequested;
            ucDatPhong.DatPhongDoanRequested += UcDatPhongTheoDoan_DatPhongDoanRequested;

            Window dialog = DialogService.CreateContentDialog(ucDatPhong, "Đặt phòng cho đoàn", 1450, 800);
            DialogService.ShowDimmedDialogResult(dialog, Window.GetWindow(this));

            ucDatPhong.CloseRequested -= UcDatPhongTheoDoan_CloseRequested;
            ucDatPhong.DatPhongDoanRequested -= UcDatPhongTheoDoan_DatPhongDoanRequested;
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
                        "Nhận phòng cho đoàn",
                        requests.Sum(item => item.TienPhong),
                        requests.Sum(item => item.TienDichVu),
                        giamGia: requests.Sum(item => item.KhachHang.LoaiKhach.Contains("VIP", StringComparison.OrdinalIgnoreCase) ? Math.Round(item.TienPhong * 0.1m, 0) : 0)))
                {
                    return;
                }
                phongBUS.LuuDatPhongDoan(requests);
                MessageBox.Show(nhanNgay ? "Nhận phòng cho đoàn thành công." : "Đặt phòng cho đoàn thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                if (sender is FrameworkElement element)
                {
                    Window.GetWindow(element)?.Close();
                }

                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không đặt phòng cho đoàn được: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtTimKiem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && PopupGoiYTimKiem.IsOpen && LbGoiYTimKiem.Items.Count > 0)
            {
                LbGoiYTimKiem.Focus();
                LbGoiYTimKiem.SelectedIndex = 0;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                BtnTim_Click(sender, e);
            }
        }

        private void TxtTimKiem_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!dangChonGoiY)
            {
                CapNhatGoiYTimKiem();
            }
        }

        private void TxtTimKiem_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CapNhatGoiYTimKiem();
        }

        private void LbGoiYTimKiem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ChonGoiYTimKiem();
        }

        private void LbGoiYTimKiem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ChonGoiYTimKiem();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                PopupGoiYTimKiem.IsOpen = false;
                TxtTimKiem.Focus();
                e.Handled = true;
            }
        }

        private void CapNhatGoiYTimKiem()
        {
            string tuKhoa = TxtTimKiem.Text.Trim();

            if (tuKhoa.Length == 0 || danhSachGoc.Count == 0)
            {
                PopupGoiYTimKiem.IsOpen = false;
                return;
            }

            List<GoiYTimKiemPhong> goiY = danhSachGoc
                .Where(phong => ChuaTuKhoa(phong.MaHienThi, tuKhoa)
                    || ChuaTuKhoa(phong.LoaiPhong, tuKhoa)
                    || ChuaTuKhoa(phong.TrangThai, tuKhoa)
                    || ChuaTuKhoa(phong.KhachHienTai, tuKhoa))
                .OrderBy(phong => phong.MaHienThi)
                .Take(8)
                .Select(phong => new GoiYTimKiemPhong(
                    phong.MaHienThi,
                    $"{phong.MaHienThi} - {phong.LoaiPhong} - {phong.TrangThai}"))
                .ToList();

            LbGoiYTimKiem.ItemsSource = goiY;
            PopupGoiYTimKiem.IsOpen = goiY.Count > 0;
        }

        private void ChonGoiYTimKiem()
        {
            if (LbGoiYTimKiem.SelectedItem is not GoiYTimKiemPhong goiY)
            {
                return;
            }

            dangChonGoiY = true;
            TxtTimKiem.Text = goiY.Value;
            TxtTimKiem.CaretIndex = TxtTimKiem.Text.Length;
            dangChonGoiY = false;

            PopupGoiYTimKiem.IsOpen = false;
            BtnTim_Click(TxtTimKiem, new RoutedEventArgs());
        }

        private static bool ChuaTuKhoa(string? giaTri, string tuKhoa)
        {
            return !string.IsNullOrWhiteSpace(giaTri)
                && giaTri.Contains(tuKhoa, StringComparison.OrdinalIgnoreCase);
        }

        private static bool LaPhongCoTheDat(PhongDTO phong)
        {
            string trangThai = BoDau(phong.TrangThai).ToLowerInvariant();
            string donDep = BoDau(phong.TinhTrangDonDep).ToLowerInvariant();

            bool laPhongTrong = string.IsNullOrWhiteSpace(trangThai)
                || trangThai.Contains("phong trong")
                || trangThai == "trong"
                || trangThai.Contains("san sang")
                || trangThai.Contains("ready")
                || trangThai.Contains("available");

            bool biChan = trangThai.Contains("thue")
                || trangThai.Contains("co khach")
                || trangThai.Contains("da dat")
                || trangThai.Contains("chua don")
                || trangThai.Contains("sua")
                || trangThai.Contains("bao tri")
                || donDep.Contains("chua")
                || donDep.Contains("sua")
                || donDep.Contains("bao tri");

            return laPhongTrong && !biChan;
        }

        private static string BoDau(string? value)
        {
            string formD = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
            char[] chars = formD
                .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                .ToArray();

            return new string(chars).Normalize(NormalizationForm.FormC);
        }

        private static string GetComboText(ComboBox comboBox)
        {
            return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
        }

        private sealed record GoiYTimKiemPhong(string Value, string Display);
    }
}
