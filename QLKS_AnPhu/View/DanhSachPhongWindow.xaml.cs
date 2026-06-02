using System.Collections.ObjectModel;
using System.Windows;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.View
{
    /// <summary>
    /// Interaction logic for DanhSachPhongWindow.xaml
    /// </summary>
    public partial class DanhSachPhongWindow : Window
    {
        private readonly PhongBUS phongBUS = new();
        private List<PhongDTO> danhSach = new();

        public DanhSachPhongWindow()
        {
            InitializeComponent();
            Loaded += DanhSachPhongWindow_Loaded;
        }

        private void DanhSachPhongWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TaiDuLieu();
        }

        private void TaiDuLieu()
        {
            try
            {
                danhSach = phongBUS.LayDanhSach();
                DgPhong.ItemsSource = new ObservableCollection<PhongDTO>(danhSach);

                if (danhSach.Count > 0)
                {
                    DgPhong.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không tải được danh sách phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnThem_Click(object sender, RoutedEventArgs e)
        {
            PhongForm form = new();

            if (!DialogService.ShowDimmedDialog(form, this))
            {
                return;
            }

            try
            {
                phongBUS.Them(form.DuLieu);
                MessageBox.Show("Thêm phòng thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thêm được phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSua_Click(object sender, RoutedEventArgs e)
        {
            if (DgPhong.SelectedItem is not PhongDTO selected)
            {
                MessageBox.Show("Vui lòng chọn phòng cần sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PhongForm form = new(selected);

            if (!DialogService.ShowDimmedDialog(form, this))
            {
                return;
            }

            try
            {
                phongBUS.Sua(form.DuLieu);
                MessageBox.Show("Sửa phòng thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không sửa được phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnXoa_Click(object sender, RoutedEventArgs e)
        {
            if (DgPhong.SelectedItem is not PhongDTO selected)
            {
                MessageBox.Show("Vui lòng chọn phòng cần xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(
                $"Bạn có chắc muốn xóa phòng {selected.MaHienThi}?",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                phongBUS.Xoa(selected);
                MessageBox.Show("Xóa phòng thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không xóa được phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLamMoi_Click(object sender, RoutedEventArgs e)
        {
            TaiDuLieu();
        }

        private void BtnDong_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
