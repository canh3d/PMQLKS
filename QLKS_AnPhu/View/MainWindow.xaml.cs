using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QLKS_AnPhu.Security;
using QLKS_AnPhu.View;

namespace QLKS_AnPhu
{
    public partial class MainWindow : Window
    {
        private bool isSidebarExpanded = true;
        private readonly AppUser currentUser;
        private Button? activeMenuButton;

        public MainWindow() : this(new AppUser
        {
            MaTK = 0,
            TenDangNhap = "admin",
            VaiTro = "Quản lý",
            HoTenNhanVien = "Admin",
            Permissions = PermissionService.AllPermissions.Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase)
        })
        {
        }

        public MainWindow(AppUser user)
        {
            InitializeComponent();
            currentUser = user;
            LblBrandTitle.Foreground = (Brush)FindResource("TextMainBrush");
            LblBrandSubtitle.Foreground = (Brush)FindResource("TextMutedBrush");
            LblDichVu.Text = "Dịch vụ - vật tư";
            BtnMenuVatTu.Visibility = Visibility.Collapsed;
            BtnMenuCaiDat.Visibility = Visibility.Collapsed;
            TxtXinChao.Text = $"Xin chào: {GetDisplayName()} ({currentUser.VaiTro})";
            ApplyPermissions();
            Navigate(PermissionService.TrangChu, () => new TrangChu(), BtnTrangChu, "Dashboard khách sạn");
        }

        private string GetDisplayName()
        {
            return string.IsNullOrWhiteSpace(currentUser.HoTenNhanVien) ? currentUser.TenDangNhap : currentUser.HoTenNhanVien;
        }

        private void ApplyPermissions()
        {
            SetMenuVisibility(BtnTrangChu, PermissionService.TrangChu);
            SetMenuVisibility(BtnMenuQLPhong, PermissionService.QLPhong);
            SetMenuVisibility(BtnMenuPhieuThue, PermissionService.PhieuThue);
            SetMenuVisibility(BtnMenuKhachHang, PermissionService.KhachHang);
            SetMenuVisibility(BtnMenuHoaDon, PermissionService.HoaDon);
            SetMenuVisibility(BtnMenuDichVuVatTu, PermissionService.DichVuVatTu);
            BtnMenuVatTu.Visibility = Visibility.Collapsed;
            BtnMenuCaiDat.Visibility = Visibility.Collapsed;
            SetMenuVisibility(BtnMenuNhanVien, PermissionService.NhanVien);
            SetMenuVisibility(BtnMenuBaoCao, PermissionService.BaoCao);
        }

        private void SetMenuVisibility(Button button, string permissionCode)
        {
            button.Visibility = currentUser.CanAccess(permissionCode) ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool EnsurePermission(string permissionCode)
        {
            if (currentUser.CanAccess(permissionCode))
            {
                return true;
            }

            MessageBox.Show("Tài khoản của bạn không có quyền truy cập chức năng này.", "Không có quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private void Navigate(string permissionCode, Func<UserControl> createView, Button? menuButton = null, string? title = null)
        {
            if (!EnsurePermission(permissionCode))
            {
                return;
            }

            MainContent.Content = createView();
            if (menuButton != null)
            {
                SetActiveMenu(menuButton);
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                TxtPageTitle.Text = title;
            }
        }

        private void SetActiveMenu(Button button)
        {
            if (activeMenuButton != null)
            {
                activeMenuButton.Style = (Style)FindResource("SidebarButtonStyle");
            }

            button.Style = (Style)FindResource("SidebarButtonActiveStyle");
            activeMenuButton = button;
        }

        private void BtnToggleMenu_Click(object sender, RoutedEventArgs e)
        {
            isSidebarExpanded = !isSidebarExpanded;
            SidebarColumn.Width = new GridLength(isSidebarExpanded ? 292 : 96);
            SidebarShell.Margin = isSidebarExpanded ? new Thickness(16) : new Thickness(12, 16, 12, 16);
            BrandHeader.Margin = isSidebarExpanded ? new Thickness(18, 20, 18, 16) : new Thickness(8, 18, 8, 14);
            BrandTextPanel.Visibility = isSidebarExpanded ? Visibility.Visible : Visibility.Collapsed;

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
            LblBrandTitle.Visibility = labelVisibility;
            LblBrandSubtitle.Visibility = labelVisibility;

            CapNhatBoCucNutMenu(BtnTrangChu);
            CapNhatBoCucNutMenu(BtnMenuQLPhong);
            CapNhatBoCucNutMenu(BtnMenuPhieuThue);
            CapNhatBoCucNutMenu(BtnMenuKhachHang);
            CapNhatBoCucNutMenu(BtnMenuHoaDon);
            CapNhatBoCucNutMenu(BtnMenuDichVuVatTu);
            CapNhatBoCucNutMenu(BtnMenuNhanVien);
            CapNhatBoCucNutMenu(BtnMenuBaoCao);
            CapNhatBoCucNutMenu(BtnSidebarLogout);
        }

        private void CapNhatBoCucNutMenu(Button button)
        {
            button.HorizontalContentAlignment = isSidebarExpanded ? HorizontalAlignment.Left : HorizontalAlignment.Center;
            button.Padding = isSidebarExpanded ? new Thickness(16, 0, 16, 0) : new Thickness(0);
            button.Margin = isSidebarExpanded ? new Thickness(14, 4, 14, 4) : new Thickness(10, 5, 10, 5);

            if (button.Content is StackPanel panel)
            {
                panel.HorizontalAlignment = isSidebarExpanded ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                if (panel.Children.Count > 0 && panel.Children[0] is FrameworkElement icon)
                {
                    icon.Margin = isSidebarExpanded ? new Thickness(0, 0, 12, 0) : new Thickness(0);
                }
            }
        }

        private void BtnTrangChu_Click(object sender, RoutedEventArgs e)
        {
            Navigate(PermissionService.TrangChu, () => new TrangChu(), BtnTrangChu, "Dashboard khách sạn");
        }

        public void NavigateToTrangChu()
        {
            Navigate(PermissionService.TrangChu, () => new TrangChu(), BtnTrangChu, "Dashboard khách sạn");
        }

        public void NavigateToQLPhong()
        {
            Navigate(PermissionService.QLPhong, () => new QLPhong(), BtnMenuQLPhong, "Quản lý phòng");
        }

        public void NavigateToKhachHang()
        {
            Navigate(PermissionService.KhachHang, () => new KhachHang(), BtnMenuKhachHang, "Khách hàng");
        }

        public void NavigateToHoaDon()
        {
            Navigate(PermissionService.HoaDon, () => new HoaDon(), BtnMenuHoaDon, "Hóa đơn");
        }

        public void NavigateToPhieuThue()
        {
            Navigate(PermissionService.PhieuThue, () => new PhieuThue(), BtnMenuPhieuThue, "Phiếu thuê");
        }

        public void NavigateToBaoCao()
        {
            Navigate(PermissionService.BaoCao, () => new BaoCao(), BtnMenuBaoCao, "Báo cáo - thống kê");
        }

        private void BtnQLPhong_Click(object sender, RoutedEventArgs e)
        {
            Navigate(PermissionService.QLPhong, () => new QLPhong(), BtnMenuQLPhong, "Quản lý phòng");
        }

        private void BtnPhieuThue_Click(object sender, RoutedEventArgs e)
        {
            Navigate(PermissionService.PhieuThue, () => new PhieuThue(), BtnMenuPhieuThue, "Phiếu thuê");
        }

        private void BtnDichVuVatTu_Click(object sender, RoutedEventArgs e)
        {
            Navigate(PermissionService.DichVuVatTu, () => new DichVuVatTu(), BtnMenuDichVuVatTu, "Dịch vụ - vật tư");
        }

        private void BtnVatTu_Click(object sender, RoutedEventArgs e)
        {
            Navigate(PermissionService.DichVuVatTu, () => new DichVuVatTu(), BtnMenuVatTu, "Vật tư");
        }

        private void BtnKhachHang_Click(object sender, RoutedEventArgs e)
        {
            Navigate(PermissionService.KhachHang, () => new KhachHang(), BtnMenuKhachHang, "Khách hàng");
        }

        private void BtnHoaDon_Click(object sender, RoutedEventArgs e)
        {
            Navigate(PermissionService.HoaDon, () => new HoaDon(), BtnMenuHoaDon, "Hóa đơn");
        }

        private void BtnNhanVien_Click(object sender, RoutedEventArgs e)
        {
            Navigate(PermissionService.NhanVien, () => new NhanVien(), BtnMenuNhanVien, "Nhân viên");
        }

        private void BtnBaoCao_Click(object sender, RoutedEventArgs e)
        {
            Navigate(PermissionService.BaoCao, () => new BaoCao(), BtnMenuBaoCao, "Báo cáo - thống kê");
        }

        private void BtnCaiDat_Click(object sender, RoutedEventArgs e)
        {
            SetActiveMenu(BtnMenuCaiDat);
            TxtPageTitle.Text = "Cài đặt";
            MessageBox.Show("Màn hình cài đặt sẽ được bổ sung sau.", "Cài đặt", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDangXuat_Click(object sender, RoutedEventArgs e)
        {
            DangNhap dangNhap = new();
            dangNhap.Show();
            Close();
        }
    }
}
