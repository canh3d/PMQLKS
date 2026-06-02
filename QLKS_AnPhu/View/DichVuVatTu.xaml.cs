using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.View
{
    /// <summary>
    /// Interaction logic for DichVuVatTu.xaml
    /// </summary>
    public partial class DichVuVatTu : UserControl
    {
        private readonly DichVuVatTuBUS dichVuVatTuBUS = new();
        private List<DichVuVatTuDTO> danhSachGoc = new();

        public DichVuVatTu()
        {
            InitializeComponent();
            Loaded += DichVuVatTu_Loaded;
        }

        private void DichVuVatTu_Loaded(object sender, RoutedEventArgs e)
        {
            TaiDuLieu();
        }

        private void TaiDuLieu()
        {
            try
            {
                TxtLoi.Text = string.Empty;
                danhSachGoc = dichVuVatTuBUS.LayDanhSach();
                HienThiDanhSach(danhSachGoc);
            }
            catch (Exception ex)
            {
                danhSachGoc = new List<DichVuVatTuDTO>();
                HienThiDanhSach(danhSachGoc);
                TxtLoi.Text = "Không tải được dữ liệu dịch vụ - vật tư từ database: " + ex.Message;
            }
        }

        private void HienThiDanhSach(List<DichVuVatTuDTO> danhSach)
        {
            DgDichVuVatTu.ItemsSource = new ObservableCollection<DichVuVatTuDTO>(danhSach);
            TxtTongDong.Text = $"Tổng: {danhSach.Count} dòng";

            if (danhSach.Count > 0)
            {
                DgDichVuVatTu.SelectedIndex = 0;
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

            List<DichVuVatTuDTO> ketQua = danhSachGoc
                .Where(item =>
                    item.Ma.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.Ten.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.Loai.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.TrangThai.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();

            HienThiDanhSach(ketQua);
        }

        private void BtnLamMoi_Click(object sender, RoutedEventArgs e)
        {
            TxtTimKiem.Clear();
            TaiDuLieu();
        }

        private void BtnLocDichVu_Click(object sender, RoutedEventArgs e)
        {
            HienThiDanhSach(danhSachGoc.Where(item => item.Loai.Contains("Dịch vụ", StringComparison.OrdinalIgnoreCase)).ToList());
        }

        private void BtnLocVatTu_Click(object sender, RoutedEventArgs e)
        {
            HienThiDanhSach(danhSachGoc.Where(item => item.Loai.Contains("Vật tư", StringComparison.OrdinalIgnoreCase)).ToList());
        }

        private void BtnLocSapHet_Click(object sender, RoutedEventArgs e)
        {
            HienThiDanhSach(danhSachGoc.Where(item => item.SoLuongTon <= 0 || item.TrangThai.Contains("Sắp hết", StringComparison.OrdinalIgnoreCase)).ToList());
        }

        private void BtnThem_Click(object sender, RoutedEventArgs e)
        {
            DichVuVatTuForm form = new();

            if (!DialogService.ShowDimmedDialog(form, Window.GetWindow(this)))
            {
                return;
            }

            try
            {
                dichVuVatTuBUS.Them(form.DuLieu);
                MessageBox.Show("Thêm dịch vụ/vật tư thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thêm được dịch vụ/vật tư: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSua_Click(object sender, RoutedEventArgs e)
        {
            if (DgDichVuVatTu.SelectedItem is not DichVuVatTuDTO selectedItem)
            {
                MessageBox.Show("Vui lòng chọn dịch vụ/vật tư cần sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DichVuVatTuForm form = new(selectedItem);

            if (!DialogService.ShowDimmedDialog(form, Window.GetWindow(this)))
            {
                return;
            }

            try
            {
                dichVuVatTuBUS.Sua(form.DuLieu);
                MessageBox.Show("Sửa dịch vụ/vật tư thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không sửa được dịch vụ/vật tư: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnXoa_Click(object sender, RoutedEventArgs e)
        {
            if (DgDichVuVatTu.SelectedItem is not DichVuVatTuDTO selectedItem)
            {
                MessageBox.Show("Vui lòng chọn dịch vụ/vật tư cần xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(
                $"Bạn có chắc muốn xóa '{selectedItem.Ten}'?",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                dichVuVatTuBUS.Xoa(selectedItem);
                MessageBox.Show("Xóa dịch vụ/vật tư thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không xóa được dịch vụ/vật tư: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtTimKiem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnTimKiem_Click(sender, e);
            }
        }

        private void DgDichVuVatTu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DataContext = DgDichVuVatTu.SelectedItem as DichVuVatTuDTO;
        }

    }
}
