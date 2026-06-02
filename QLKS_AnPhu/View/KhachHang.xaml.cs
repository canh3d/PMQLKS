using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DTO;
using QLKS_AnPhu.UserControls;

namespace QLKS_AnPhu.View
{
    public partial class KhachHang : UserControl
    {
        private readonly KhachHangBUS khachHangBUS = new();
        private readonly PhongBUS phongBUS = new();
        private List<KhachHangDTO> danhSachGoc = new();

        public KhachHang()
        {
            InitializeComponent();
            Loaded += KhachHang_Loaded;
        }

        private void KhachHang_Loaded(object sender, RoutedEventArgs e)
        {
            TaiDuLieu();
        }

        private void TaiDuLieu()
        {
            try
            {
                TxtLoi.Text = string.Empty;
                danhSachGoc = khachHangBUS.LayDanhSach();
                HienThiDanhSach(danhSachGoc);
            }
            catch (Exception ex)
            {
                danhSachGoc = new List<KhachHangDTO>();
                HienThiDanhSach(danhSachGoc);
                TxtLoi.Text = "Không tải được dữ liệu khách hàng từ database: " + ex.Message;
            }
        }

        private void HienThiDanhSach(List<KhachHangDTO> danhSach)
        {
            DgKhachHang.ItemsSource = new ObservableCollection<KhachHangDTO>(danhSach);
            TxtTongDong.Text = $"Tổng: {danhSach.Count} dòng";

            if (danhSach.Count > 0)
            {
                DgKhachHang.SelectedIndex = 0;
            }
            else
            {
                DataContext = null;
            }
        }

        private void BtnTimKiem_Click(object sender, RoutedEventArgs e)
        {
            string keyword = TxtTimKiem.Text.Trim();

            if (string.IsNullOrWhiteSpace(keyword))
            {
                HienThiDanhSach(danhSachGoc);
                return;
            }

            HienThiDanhSach(danhSachGoc
                .Where(item =>
                    item.Ma.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.HoTen.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.SDT.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.CCCD.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.LoaiKhach.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.TrangThai.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList());
        }

        private void BtnLamMoi_Click(object sender, RoutedEventArgs e)
        {
            TxtTimKiem.Clear();
            TaiDuLieu();
        }

        private void BtnLocThuong_Click(object sender, RoutedEventArgs e)
        {
            HienThiDanhSach(danhSachGoc.Where(item => item.LoaiKhach.Contains("Thường", StringComparison.OrdinalIgnoreCase)).ToList());
        }

        private void BtnLocVip_Click(object sender, RoutedEventArgs e)
        {
            HienThiDanhSach(danhSachGoc.Where(item => item.LoaiKhach.Contains("VIP", StringComparison.OrdinalIgnoreCase)).ToList());
        }

        private void BtnLocDangHoatDong_Click(object sender, RoutedEventArgs e)
        {
            HienThiDanhSach(danhSachGoc.Where(item =>
                item.TrangThai.Contains("Hoạt", StringComparison.OrdinalIgnoreCase) ||
                item.TrangThai.Contains("Active", StringComparison.OrdinalIgnoreCase)).ToList());
        }

        private void BtnThem_Click(object sender, RoutedEventArgs e)
        {
            KhachHangForm form = new();

            if (!DialogService.ShowDimmedDialog(form, Window.GetWindow(this)))
            {
                return;
            }

            try
            {
                khachHangBUS.Them(form.DuLieu);
                MessageBox.Show("Thêm khách hàng thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thêm được khách hàng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSua_Click(object sender, RoutedEventArgs e)
        {
            if (DgKhachHang.SelectedItem is not KhachHangDTO selectedItem)
            {
                MessageBox.Show("Vui lòng chọn khách hàng cần sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            KhachHangForm form = new(selectedItem);

            if (!DialogService.ShowDimmedDialog(form, Window.GetWindow(this)))
            {
                return;
            }

            try
            {
                khachHangBUS.Sua(form.DuLieu);
                MessageBox.Show("Sửa khách hàng thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không sửa được khách hàng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnXoa_Click(object sender, RoutedEventArgs e)
        {
            if (DgKhachHang.SelectedItem is not KhachHangDTO selectedItem)
            {
                MessageBox.Show("Vui lòng chọn khách hàng cần xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(
                $"Bạn có chắc muốn xóa khách hàng '{selectedItem.HoTen}'?",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                khachHangBUS.Xoa(selectedItem);
                MessageBox.Show("Xóa khách hàng thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không xóa được khách hàng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtTimKiem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnTimKiem_Click(sender, e);
            }
        }

        private void DgKhachHang_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DataContext = DgKhachHang.SelectedItem as KhachHangDTO;
        }

        private void TxtGhiChuTongHop_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DgKhachHang.SelectedItem is not KhachHangDTO selectedItem)
            {
                return;
            }

            string ghiChuMoi = TxtGhiChuTongHop.Text.Trim();

            try
            {
                selectedItem.GhiChu = ghiChuMoi;
                khachHangBUS.Sua(selectedItem);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không lưu được ghi chú khách hàng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLichSu_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedKhachHang(out KhachHangDTO? khachHang))
            {
                return;
            }

            KhachHangDataWindow window = new(khachHang!, KhachHangDataWindow.DataMode.LichSuThue);
            DialogService.ShowDimmedDialogResult(window, Window.GetWindow(this));
        }

        private void BtnHoaDon_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedKhachHang(out KhachHangDTO? khachHang))
            {
                return;
            }

            KhachHangDataWindow window = new(khachHang!, KhachHangDataWindow.DataMode.HoaDon);
            DialogService.ShowDimmedDialogResult(window, Window.GetWindow(this));
        }

        private void BtnDatPhong_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedKhachHang(out KhachHangDTO? khachHang))
            {
                return;
            }

            PhongDTO? phongTrong;

            try
            {
                phongTrong = phongBUS.LayDanhSach()
                    .FirstOrDefault(item =>
                        !item.TrangThai.Contains("thuê", StringComparison.OrdinalIgnoreCase) &&
                        !item.TrangThai.Contains("thu", StringComparison.OrdinalIgnoreCase) &&
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

            UCDatPhongMoi ucDatPhong = new(phongTrong, khachHang!);
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
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không đặt được phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryGetSelectedKhachHang(out KhachHangDTO? khachHang)
        {
            khachHang = DgKhachHang.SelectedItem as KhachHangDTO;
            if (khachHang != null)
            {
                return true;
            }

            MessageBox.Show("Vui lòng chọn khách hàng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }
}
