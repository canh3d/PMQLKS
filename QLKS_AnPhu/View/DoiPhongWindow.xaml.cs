using System.Collections.ObjectModel;
using System.Windows;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.View
{
    public partial class DoiPhongWindow : Window
    {
        private readonly DoiPhongRequestDTO request;
        private readonly PhongThueOperationBUS operationBUS = new();
        private readonly PhongBUS phongBUS = new();

        public bool DuLieuDaThayDoi { get; private set; }

        public DoiPhongWindow(DoiPhongRequestDTO request, string soPhongHienTai)
        {
            this.request = request;
            InitializeComponent();
            TxtPhongHienTai.Text = "Phòng hiện tại: " + soPhongHienTai;
            TxtNgayTra.Text = request.NgayTraDuKien.ToString("dd/MM/yyyy HH:mm");
            Loaded += DoiPhongWindow_Loaded;
        }

        private void DoiPhongWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                List<PhongDTO> rooms = phongBUS.LayDanhSach()
                    .Where(item => item.Ma != request.MaPhongCu && LaPhongTrong(item.TrangThai))
                    .OrderBy(item => item.Tang)
                    .ThenBy(item => item.MaHienThi)
                    .ToList();
                DgPhong.ItemsSource = new ObservableCollection<PhongDTO>(rooms);
                DgPhong.SelectedIndex = rooms.Count > 0 ? 0 : -1;
                TxtSoLuongPhong.Text = rooms.Count + " phòng";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không tải được danh sách phòng trống: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnXacNhan_Click(object sender, RoutedEventArgs e)
        {
            if (DgPhong.SelectedItem is not PhongDTO selected)
            {
                MessageBox.Show("Vui lòng chọn phòng muốn chuyển đến.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            request.MaPhongMoi = selected.Ma;
            if (MessageBox.Show("Xác nhận đổi sang phòng " + selected.MaHienThi + "?", "Đổi phòng", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                operationBUS.DoiPhong(request);
                DuLieuDaThayDoi = true;
                MessageBox.Show("Đã đổi sang phòng " + selected.MaHienThi + ".", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể đổi phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                DoiPhongWindow_Loaded(sender, e);
            }
        }

        private void BtnDong_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static bool LaPhongTrong(string value)
        {
            string normalized = value.Normalize(System.Text.NormalizationForm.FormD);
            normalized = new string(normalized.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
            return normalized.Contains("trong", StringComparison.OrdinalIgnoreCase);
        }
    }
}
