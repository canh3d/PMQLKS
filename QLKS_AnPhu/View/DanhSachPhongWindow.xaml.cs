using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
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
        private bool dangNapBoLoc;

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
                NapBoLoc();
                ApDungBoLoc();
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

        private void NapBoLoc()
        {
            dangNapBoLoc = true;

            string loaiDangChon = LayComboText(CboLocLoaiPhong);
            string trangThaiDangChon = LayComboText(CboLocTrangThai);

            CboLocLoaiPhong.Items.Clear();
            CboLocLoaiPhong.Items.Add("Tất cả");
            foreach (string loai in danhSach
                         .Select(item => string.IsNullOrWhiteSpace(item.LoaiPhong) ? "Loại phòng" : item.LoaiPhong)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(item => item))
            {
                CboLocLoaiPhong.Items.Add(loai);
            }

            CboLocTrangThai.Items.Clear();
            CboLocTrangThai.Items.Add("Tất cả");
            foreach (string trangThai in danhSach
                         .Select(item => string.IsNullOrWhiteSpace(item.TrangThai) ? "Trống" : item.TrangThai)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(item => item))
            {
                CboLocTrangThai.Items.Add(trangThai);
            }

            CboLocLoaiPhong.SelectedItem = CboLocLoaiPhong.Items.Contains(loaiDangChon) ? loaiDangChon : "Tất cả";
            CboLocTrangThai.SelectedItem = CboLocTrangThai.Items.Contains(trangThaiDangChon) ? trangThaiDangChon : "Tất cả";

            dangNapBoLoc = false;
        }

        private void ApDungBoLoc()
        {
            string keyword = BoDau(TxtTimKiem?.Text ?? string.Empty).ToLowerInvariant();
            string loaiPhong = LayComboText(CboLocLoaiPhong);
            string trangThai = LayComboText(CboLocTrangThai);

            List<PhongDTO> ketQua = danhSach
                .Where(item =>
                    string.IsNullOrWhiteSpace(keyword) ||
                    BoDau(item.MaHienThi).ToLowerInvariant().Contains(keyword) ||
                    BoDau(item.LoaiPhong).ToLowerInvariant().Contains(keyword) ||
                    BoDau(item.TrangThai).ToLowerInvariant().Contains(keyword) ||
                    BoDau(item.GhiChuHienThi).ToLowerInvariant().Contains(keyword))
                .Where(item => loaiPhong == "Tất cả" || string.Equals(item.LoaiPhong, loaiPhong, StringComparison.OrdinalIgnoreCase))
                .Where(item => trangThai == "Tất cả" || string.Equals(item.TrangThai, trangThai, StringComparison.OrdinalIgnoreCase))
                .ToList();

            DgPhong.ItemsSource = new ObservableCollection<PhongDTO>(ketQua);
            DgPhong.SelectedIndex = ketQua.Count > 0 ? 0 : -1;
        }

        private void BoLoc_Changed(object sender, RoutedEventArgs e)
        {
            if (!dangNapBoLoc && DgPhong != null)
            {
                ApDungBoLoc();
            }
        }

        private static string LayComboText(ComboBox comboBox)
        {
            return comboBox?.SelectedItem?.ToString() ?? "Tất cả";
        }

        private static string BoDau(string? value)
        {
            string formD = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
            char[] chars = formD
                .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                .Select(ch => ch == 'đ' ? 'd' : ch == 'Đ' ? 'D' : ch)
                .ToArray();
            return new string(chars).Normalize(NormalizationForm.FormC);
        }
    }
}
