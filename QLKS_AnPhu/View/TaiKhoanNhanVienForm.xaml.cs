using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.Security;

namespace QLKS_AnPhu.View
{
    public partial class TaiKhoanNhanVienForm : Window
    {
        private readonly bool isEditMode;
        private readonly ObservableCollection<PermissionOption> permissionOptions = new();

        public TaiKhoanNhanVienForm()
        {
            InitializeComponent();
            LstQuyen.ItemsSource = permissionOptions;
            CboVaiTro.Text = "Nhân viên";
            LoadPermissionOptions(PermissionService.DefaultPermissionsForRole("Nhân viên").ToHashSet(StringComparer.OrdinalIgnoreCase));
        }

        public TaiKhoanNhanVienForm(NhanVienTaiKhoanWindow.TaiKhoanItem taiKhoan) : this()
        {
            isEditMode = true;
            TxtTieuDe.Text = "SỬA TÀI KHOẢN";
            TxtMatKhauLabel.Text = "Mật khẩu mới (bỏ trống nếu không đổi)";
            TxtTenDangNhap.Text = taiKhoan.TenDangNhap;
            CboVaiTro.Text = taiKhoan.VaiTro;
            ChkHoatDong.IsChecked = taiKhoan.TrangThai;
            LoadPermissionOptions(PermissionService.GetPermissions(taiKhoan.MaTK));
        }

        public string MatKhauMoi => TxtMatKhau.Text.Trim();

        public IReadOnlyList<string> SelectedPermissions => permissionOptions
            .Where(item => item.IsChecked)
            .Select(item => item.Code)
            .ToList();

        public SqlParameter[] ToSqlParameters(bool includeEmptyPassword = true)
        {
            List<SqlParameter> parameters = new()
            {
                new SqlParameter("@TenDangNhap", TxtTenDangNhap.Text.Trim()),
                new SqlParameter("@VaiTro", GetComboBoxText(CboVaiTro)),
                new SqlParameter("@TrangThai", ChkHoatDong.IsChecked == true)
            };

            if (includeEmptyPassword || !string.IsNullOrWhiteSpace(MatKhauMoi))
            {
                parameters.Add(new SqlParameter("@MatKhau", MatKhauMoi));
            }

            return parameters.ToArray();
        }

        private void LoadPermissionOptions(HashSet<string> selectedPermissions)
        {
            permissionOptions.Clear();
            foreach (PermissionDefinition permission in PermissionService.AllPermissions)
            {
                permissionOptions.Add(new PermissionOption
                {
                    Code = permission.Code,
                    Name = permission.Name,
                    IsChecked = selectedPermissions.Contains(permission.Code)
                });
            }
        }

        private void BtnLuu_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtTenDangNhap.Text))
            {
                MessageBox.Show("Vui lòng nhập tên đăng nhập.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtTenDangNhap.Focus();
                return;
            }

            if (!isEditMode && string.IsNullOrWhiteSpace(TxtMatKhau.Text))
            {
                MessageBox.Show("Vui lòng nhập mật khẩu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtMatKhau.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(GetComboBoxText(CboVaiTro)))
            {
                MessageBox.Show("Vui lòng chọn vai trò.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                CboVaiTro.Focus();
                return;
            }

            if (SelectedPermissions.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một form được phép truy cập.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        private void BtnHuy_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private static string GetComboBoxText(ComboBox comboBox)
        {
            return comboBox.SelectedItem is ComboBoxItem item
                ? item.Content?.ToString() ?? string.Empty
                : comboBox.Text.Trim();
        }

        public sealed class PermissionOption
        {
            public string Code { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
            public bool IsChecked { get; set; }
        }
    }
}
