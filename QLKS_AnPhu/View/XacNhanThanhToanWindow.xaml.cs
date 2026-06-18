using System.Windows;

namespace QLKS_AnPhu.View
{
    public partial class XacNhanThanhToanWindow : Window
    {
        public XacNhanThanhToanWindow(string maHoaDon, decimal soTien)
        {
            InitializeComponent();
            TxtNoiDung.Text = $"Xác nhận khách đã thanh toán hóa đơn {maHoaDon} với số tiền {soTien:N0} VND?";
        }

        private void BtnXacNhan_Click(object sender, RoutedEventArgs e) => DialogResult = true;
        private void BtnHuy_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
