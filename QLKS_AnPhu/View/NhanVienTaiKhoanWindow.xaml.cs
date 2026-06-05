using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DAL;
using QLKS_AnPhu.Security;

namespace QLKS_AnPhu.View
{
    public partial class NhanVienTaiKhoanWindow : Window
    {
        private readonly NhanVien.NhanVienItem nhanVien;
        private readonly ObservableCollection<TaiKhoanItem> taiKhoanItems = new();

        public NhanVienTaiKhoanWindow(NhanVien.NhanVienItem nhanVien)
        {
            InitializeComponent();
            this.nhanVien = nhanVien;
            PermissionService.EnsurePermissionTable();
            DgTaiKhoan.ItemsSource = taiKhoanItems;
            HienThiThongTinNhanVien();
            TaiDanhSachTaiKhoan();
        }

        private void HienThiThongTinNhanVien()
        {
            TxtSubTitle.Text = $"{nhanVien.MaHienThi} - {nhanVien.HoTen}";
            TxtMaNhanVien.Text = nhanVien.MaHienThi;
            TxtHoTen.Text = nhanVien.HoTen;
            TxtGioiTinh.Text = nhanVien.GioiTinh;
            TxtNgaySinh.Text = nhanVien.NgaySinhHienThi;
            TxtSDT.Text = nhanVien.SDT;
            TxtChucVu.Text = nhanVien.ChucVu;
            TxtCaLam.Text = nhanVien.CaLamViec;
            TxtTrangThai.Text = nhanVien.TrangThaiHienThi;
            TxtDiaChi.Text = nhanVien.DiaChi;
        }

        private void TaiDanhSachTaiKhoan()
        {
            try
            {
                taiKhoanItems.Clear();
                DataTable table = ConnectDB.GetData(
                    @"
SELECT MaTK, TenDangNhap, VaiTro, TrangThai, MaNV
FROM TAIKHOAN
WHERE MaNV = @MaNV
ORDER BY MaTK;",
                    new SqlParameter("@MaNV", nhanVien.MaNV));

                foreach (DataRow row in table.Rows)
                {
                    taiKhoanItems.Add(TaiKhoanItem.FromDataRow(row));
                }

                TxtTongTaiKhoan.Text = $"Tổng: {taiKhoanItems.Count} tài khoản";
                if (taiKhoanItems.Count > 0)
                {
                    DgTaiKhoan.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không tải được tài khoản nhân viên: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnThemTaiKhoan_Click(object sender, RoutedEventArgs e)
        {
            TaiKhoanNhanVienForm form = new()
            {
                Owner = this
            };

            if (form.ShowDialog() != true)
            {
                return;
            }

            try
            {
                const string sql = @"
INSERT INTO TAIKHOAN (TenDangNhap, MatKhau, VaiTro, TrangThai, MaNV)
VALUES (@TenDangNhap, @MatKhau, @VaiTro, @TrangThai, @MaNV);
SELECT CAST(SCOPE_IDENTITY() AS int);";

                List<SqlParameter> parameters = form.ToSqlParameters().ToList();
                parameters.Add(new SqlParameter("@MaNV", nhanVien.MaNV));
                object? result = ConnectDB.ExecuteScalar(sql, parameters.ToArray());
                int maTkMoi = Convert.ToInt32(result);
                PermissionService.SavePermissions(maTkMoi, form.SelectedPermissions);
                MessageBox.Show("Thêm tài khoản thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDanhSachTaiKhoan();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thêm được tài khoản. Tên đăng nhập không được trùng và vai trò chỉ được là 'Quản lý' hoặc 'Nhân viên'.\nChi tiết: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSuaTaiKhoan_Click(object sender, RoutedEventArgs e)
        {
            if (DgTaiKhoan.SelectedItem is not TaiKhoanItem selected)
            {
                MessageBox.Show("Vui lòng chọn tài khoản cần sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TaiKhoanNhanVienForm form = new(selected)
            {
                Owner = this
            };

            if (form.ShowDialog() != true)
            {
                return;
            }

            try
            {
                string sql = string.IsNullOrWhiteSpace(form.MatKhauMoi)
                    ? @"
UPDATE TAIKHOAN
SET TenDangNhap = @TenDangNhap,
    VaiTro = @VaiTro,
    TrangThai = @TrangThai,
    MaNV = @MaNV
WHERE MaTK = @MaTK;"
                    : @"
UPDATE TAIKHOAN
SET TenDangNhap = @TenDangNhap,
    MatKhau = @MatKhau,
    VaiTro = @VaiTro,
    TrangThai = @TrangThai,
    MaNV = @MaNV
WHERE MaTK = @MaTK;";

                List<SqlParameter> parameters = form.ToSqlParameters(includeEmptyPassword: false).ToList();
                parameters.Add(new SqlParameter("@MaNV", nhanVien.MaNV));
                parameters.Add(new SqlParameter("@MaTK", selected.MaTK));
                ConnectDB.ExecuteNonQuery(sql, parameters.ToArray());
                PermissionService.SavePermissions(selected.MaTK, form.SelectedPermissions);
                MessageBox.Show("Sửa tài khoản thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDanhSachTaiKhoan();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không sửa được tài khoản. Tên đăng nhập không được trùng và vai trò chỉ được là 'Quản lý' hoặc 'Nhân viên'.\nChi tiết: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnXoaTaiKhoan_Click(object sender, RoutedEventArgs e)
        {
            if (DgTaiKhoan.SelectedItem is not TaiKhoanItem selected)
            {
                MessageBox.Show("Vui lòng chọn tài khoản cần xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(
                $"Bạn có chắc muốn xóa tài khoản '{selected.TenDangNhap}'?",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                ConnectDB.ExecuteNonQuery("DELETE FROM TAIKHOAN WHERE MaTK = @MaTK", new SqlParameter("@MaTK", selected.MaTK));
                MessageBox.Show("Xóa tài khoản thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDanhSachTaiKhoan();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không xóa được tài khoản: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLamMoi_Click(object sender, RoutedEventArgs e)
        {
            TaiDanhSachTaiKhoan();
        }

        private void BtnDong_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public sealed class TaiKhoanItem
        {
            public int MaTK { get; init; }
            public string TenDangNhap { get; init; } = string.Empty;
            public string VaiTro { get; init; } = string.Empty;
            public bool TrangThai { get; init; }
            public string TrangThaiHienThi => TrangThai ? "Hoạt động" : "Khóa";
            public int? MaNV { get; init; }

            public static TaiKhoanItem FromDataRow(DataRow row)
            {
                return new TaiKhoanItem
                {
                    MaTK = Convert.ToInt32(row["MaTK"]),
                    TenDangNhap = row["TenDangNhap"].ToString() ?? string.Empty,
                    VaiTro = row["VaiTro"].ToString() ?? string.Empty,
                    TrangThai = row["TrangThai"] != DBNull.Value && Convert.ToBoolean(row["TrangThai"]),
                    MaNV = row["MaNV"] == DBNull.Value ? null : Convert.ToInt32(row["MaNV"])
                };
            }
        }
    }
}
