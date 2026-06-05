using System.Windows;

namespace QLKS_AnPhu.View
{
    public partial class ThanhToanCheckInWindow : Window
    {
        public ThanhToanCheckInWindow(string moTa, decimal tienPhong, decimal tienDichVu, decimal phuPhi, decimal datCoc, decimal giamGia)
        {
            InitializeComponent();
            decimal thanhTienSauGiam = Math.Max(0, tienPhong + tienDichVu + phuPhi - giamGia);
            decimal giaTriTinhThue = Math.Max(0, tienPhong + phuPhi - giamGia);
            decimal thueVat = Math.Round(giaTriTinhThue * 0.1m, 0);
            decimal canThanhToan = Math.Max(0, thanhTienSauGiam + thueVat - datCoc);
            TxtMoTa.Text = moTa;
            TxtTienPhong.Text = tienPhong.ToString("N0") + " VND";
            TxtTienDichVu.Text = tienDichVu.ToString("N0") + " VND";
            TxtPhuPhi.Text = phuPhi.ToString("N0") + " VND";
            TxtGiamGia.Text = "- " + giamGia.ToString("N0") + " VND";
            TxtThueVat.Text = thueVat.ToString("N0") + " VND";
            TxtDatCoc.Text = "- " + datCoc.ToString("N0") + " VND";
            TxtCanThanhToan.Text = canThanhToan.ToString("N0") + " VND";
        }

        private void BtnThanhToan_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void BtnHuy_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
