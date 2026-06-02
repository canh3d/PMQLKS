using System.Windows;
using System.Windows.Controls;
using QLKS_AnPhu.View;

namespace QLKS_AnPhu
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool isSidebarExpanded = true;

        public MainWindow()
        {
            InitializeComponent();
            MainContent.Content = new TrangChu();
        }

        private void BtnToggleMenu_Click(object sender, RoutedEventArgs e)
        {
            isSidebarExpanded = !isSidebarExpanded;
            SidebarColumn.Width = new GridLength(isSidebarExpanded ? 244 : 72);

            Visibility labelVisibility = isSidebarExpanded ? Visibility.Visible : Visibility.Collapsed;
            LblTrangChu.Visibility = labelVisibility;
            LblQuanLyPhong.Visibility = labelVisibility;
            LblPhieuThue.Visibility = labelVisibility;
            LblKhachHang.Visibility = labelVisibility;
            LblHoaDon.Visibility = labelVisibility;
            LblDichVu.Visibility = labelVisibility;
            LblNhanVien.Visibility = labelVisibility;
            LblBaoCao.Visibility = labelVisibility;
            LblDangXuat.Visibility = labelVisibility;
        }

        private void BtnTrangChu_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new TrangChu();
        }

        private void BtnQLPhong_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new QLPhong();
        }

        private void BtnPhieuThue_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new PhieuThue();
        }

        private void BtnDichVuVatTu_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new DichVuVatTu();
        }

        private void BtnKhachHang_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new KhachHang();
        }

        private void BtnHoaDon_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new HoaDon();
        }

        private void BtnDangXuat_Click(object sender, RoutedEventArgs e)
        {
            DangNhap dangNhap = new();
            dangNhap.Show();
            Close();
        }
    }
}
