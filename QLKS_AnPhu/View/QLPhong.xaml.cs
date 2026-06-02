using System.Collections.ObjectModel;
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
                StackPanel header = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
                header.Children.Add(new TextBlock
                {
                    Text = $"TẦNG {group.Key}",
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.DimGray,
                    FontSize = 12
                });
                header.Children.Add(new TextBlock
                {
                    Text = $"{group.Count()} phòng",
                    Margin = new Thickness(15, 0, 0, 0),
                    Foreground = System.Windows.Media.Brushes.DimGray,
                    FontSize = 12
                });
                PanelPhongTheoTang.Children.Add(header);

                WrapPanel wrapPanel = new() { Margin = new Thickness(0, 0, 0, 15) };

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
            TxtCTGhiChu.Text = phong.GhiChu;
        }

        private void BtnTim_Click(object sender, RoutedEventArgs e)
        {
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
            List<PhongDTO> phongTrong = danhSachHienThi.ToList();

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
                phongBUS.LuuDatPhongDoan(requests);
                bool nhanNgay = requests.Any(item => item.NhanNgay);
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
            if (e.Key == Key.Enter)
            {
                BtnTim_Click(sender, e);
            }
        }

        private static string GetComboText(ComboBox comboBox)
        {
            return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
        }
    }
}
