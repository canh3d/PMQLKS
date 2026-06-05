using System.Data;
using System.Windows;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DAL;
using QLKS_AnPhu.Security;

namespace QLKS_AnPhu.View
{
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

            try
            {
                PermissionService.EnsurePermissionTable();

                DataTable table = ConnectDB.GetData(
                    @"
SELECT TOP 1 tk.MaTK, tk.TenDangNhap, tk.VaiTro, tk.TrangThai, tk.MaNV, ISNULL(nv.HoTen, tk.TenDangNhap) AS HoTenNhanVien
FROM TAIKHOAN tk
LEFT JOIN NHANVIEN nv ON nv.MaNV = tk.MaNV
WHERE tk.TenDangNhap = @TenDangNhap AND tk.MatKhau = @MatKhau;",
                    new SqlParameter("@TenDangNhap", tenDangNhap),
                    new SqlParameter("@MatKhau", matKhau));

                if (table.Rows.Count == 0)
                {
                    TxtThongBao.Text = "Tên đăng nhập hoặc mật khẩu không đúng.";
                    TxtMatKhau.Clear();
                    TxtMatKhau.Focus();
                    return;
                }

                DataRow row = table.Rows[0];
                bool trangThai = row["TrangThai"] != DBNull.Value && Convert.ToBoolean(row["TrangThai"]);
                if (!trangThai)
                {
                    TxtThongBao.Text = "Tài khoản đã bị khóa.";
                    return;
                }

                int maTk = Convert.ToInt32(row["MaTK"]);
                string vaiTro = row["VaiTro"].ToString() ?? string.Empty;
                HashSet<string> permissions = PermissionService.GetPermissions(maTk);

                if (permissions.Count == 0)
                {
                    permissions = PermissionService.DefaultPermissionsForRole(vaiTro).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    PermissionService.SavePermissions(maTk, permissions);
                }

                AppUser user = new()
                {
                    MaTK = maTk,
                    TenDangNhap = row["TenDangNhap"].ToString() ?? string.Empty,
                    VaiTro = vaiTro,
                    MaNV = row["MaNV"] == DBNull.Value ? null : Convert.ToInt32(row["MaNV"]),
                    HoTenNhanVien = row["HoTenNhanVien"].ToString() ?? string.Empty,
                    Permissions = permissions
                };

                MainWindow mainWindow = new(user)
                {
                    WindowState = WindowState.Maximized
                };
                mainWindow.Show();
                Close();
            }
            catch (Exception ex)
            {
                TxtThongBao.Text = "Không đăng nhập được: " + ex.Message;
            }
        }
    }
}
