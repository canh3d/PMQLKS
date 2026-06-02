using System.Windows;
using System.Windows.Input;

namespace QLKS_AnPhu.View
{
    /// <summary>
    /// Interaction logic for DangNhap.xaml
    /// </summary>
    public partial class DangNhap : Window
    {
        public DangNhap()
        {
            InitializeComponent();
            TxtTenDangNhap.Focus();
        }

        private void BtnDangNhap_Click(object sender, RoutedEventArgs e)
        {
            DangNhapHeThong();
        }

        private void BtnThoat_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DangNhapHeThong();
            }
        }

        private void DangNhapHeThong()
        {
            string tenDangNhap = TxtTenDangNhap.Text.Trim();
            string matKhau = TxtMatKhau.Password;

            if (string.IsNullOrWhiteSpace(tenDangNhap))
            {
                TxtThongBao.Text = "Vui lòng nhập tên đăng nhập.";
                TxtTenDangNhap.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(matKhau))
            {
                TxtThongBao.Text = "Vui lòng nhập mật khẩu.";
                TxtMatKhau.Focus();
                return;
            }

            if (tenDangNhap == "admin" && matKhau == "123456")
            {
                MainWindow mainWindow = new()
                {
                    WindowState = WindowState.Maximized
                };
                mainWindow.Show();
                Close();
                return;
            }

            TxtThongBao.Text = "Tên đăng nhập hoặc mật khẩu không đúng.";
            TxtMatKhau.Clear();
            TxtMatKhau.Focus();
        }
    }
}
