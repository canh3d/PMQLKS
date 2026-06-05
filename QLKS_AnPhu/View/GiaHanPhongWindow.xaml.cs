using System.Globalization;
using System.Windows;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.View
{
    public partial class GiaHanPhongWindow : Window
    {
        private readonly GiaHanPhongRequestDTO request;
        private readonly PhongThueOperationBUS bus = new();

        public bool DuLieuDaThayDoi { get; private set; }

        public GiaHanPhongWindow(GiaHanPhongRequestDTO request, string soPhong)
        {
            this.request = request;
            InitializeComponent();
            TxtPhong.Text = "Phòng " + soPhong;
            TxtNgayTraCu.Text = request.NgayTraCu.ToString("dd/MM/yyyy HH:mm");
            DpNgayTraMoi.SelectedDate = request.NgayTraCu.Date.AddDays(1);
            TxtGioTraMoi.Text = request.NgayTraCu.ToString("HH:mm");
        }

        private void BtnXacNhan_Click(object sender, RoutedEventArgs e)
        {
            if (!DpNgayTraMoi.SelectedDate.HasValue ||
                !TimeSpan.TryParseExact(TxtGioTraMoi.Text.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan time))
            {
                MessageBox.Show("Vui lòng nhập ngày và giờ trả mới theo định dạng HH:mm.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            request.NgayTraMoi = DpNgayTraMoi.SelectedDate.Value.Date.Add(time);
            if (MessageBox.Show("Xác nhận gia hạn phòng đến " + request.NgayTraMoi.ToString("dd/MM/yyyy HH:mm") + "?", "Gia hạn phòng", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                bus.GiaHan(request);
                DuLieuDaThayDoi = true;
                MessageBox.Show("Đã gia hạn phòng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể gia hạn phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDong_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
