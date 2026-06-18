using System.Windows;
using System.Windows.Controls;
using QLKS_AnPhu.Security;
using QLKS_AnPhu.ViewModels;

namespace QLKS_AnPhu.View
{
    public partial class TrangChu : UserControl
    {
        private readonly DashboardViewModel viewModel;

        public TrangChu() : this(new AppUser
        {
            MaTK = 0,
            TenDangNhap = CurrentUser.TenDangNhap,
            VaiTro = CurrentUser.VaiTro,
            MaNV = CurrentUser.MaNV == 0 ? null : CurrentUser.MaNV,
            HoTenNhanVien = CurrentUser.HoTen
        })
        {
        }

        public TrangChu(AppUser user)
        {
            InitializeComponent();
            viewModel = new DashboardViewModel(
                user,
                () => HostWindow()?.NavigateToQLPhong(),
                () => HostWindow()?.NavigateToBaoCao(),
                () => HostWindow()?.NavigateToPhieuThue());
            DataContext = viewModel;
            RevenueCard.Visibility = RoleHelper.IsManagerRole(user.VaiTro) ? Visibility.Visible : Visibility.Collapsed;
            Loaded += TrangChu_Loaded;
            Unloaded += TrangChu_Unloaded;
        }

        private async void TrangChu_Loaded(object sender, RoutedEventArgs e)
        {
            await viewModel.LoadDashboardDataAsync();
        }

        private void TrangChu_Unloaded(object sender, RoutedEventArgs e)
        {
            viewModel.Dispose();
        }

        private MainWindow? HostWindow()
        {
            return Window.GetWindow(this) as MainWindow;
        }
    }
}
