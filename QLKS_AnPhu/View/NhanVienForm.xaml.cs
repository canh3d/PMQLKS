using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;

namespace QLKS_AnPhu.View
{
    public partial class NhanVienForm : Window
    {
        public NhanVienForm()
        {
            InitializeComponent();
            CboGioiTinh.SelectedIndex = 0;
            CboChucVu.Text = "Nhân viên";
        }

        public NhanVienForm(NhanVien.NhanVienItem nhanVien) : this()
        {
            TxtTieuDe.Text = "SỬA NHÂN VIÊN";
            TxtHoTen.Text = nhanVien.HoTen;
            SelectComboBoxText(CboGioiTinh, nhanVien.GioiTinh);
            DpNgaySinh.SelectedDate = nhanVien.NgaySinh;
            TxtSDT.Text = EmptyIfPlaceholder(nhanVien.SDT);
            TxtDiaChi.Text = EmptyIfPlaceholder(nhanVien.DiaChi);
            CboChucVu.Text = EmptyIfPlaceholder(nhanVien.ChucVu);
            ChkDangLamViec.IsChecked = nhanVien.TrangThai;
        }

        public SqlParameter[] ToSqlParameters()
        {
            return new[]
            {
                new SqlParameter("@HoTen", TxtHoTen.Text.Trim()),
                new SqlParameter("@GioiTinh", NullIfEmpty(GetComboBoxText(CboGioiTinh))),
                new SqlParameter("@NgaySinh", SqlDbType.Date) { Value = DpNgaySinh.SelectedDate.HasValue ? DpNgaySinh.SelectedDate.Value.Date : DBNull.Value },
                new SqlParameter("@SDT", NullIfEmpty(TxtSDT.Text.Trim())),
                new SqlParameter("@DiaChi", NullIfEmpty(TxtDiaChi.Text.Trim())),
                new SqlParameter("@ChucVu", NullIfEmpty(CboChucVu.Text.Trim())),
                new SqlParameter("@TrangThai", ChkDangLamViec.IsChecked == true)
            };
        }

        private void BtnLuu_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtHoTen.Text))
            {
                MessageBox.Show("Vui lòng nhập họ tên nhân viên.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtHoTen.Focus();
                return;
            }

            if (!string.IsNullOrWhiteSpace(TxtSDT.Text) && TxtSDT.Text.Trim().Length > 15)
            {
                MessageBox.Show("Số điện thoại không được vượt quá 15 ký tự.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtSDT.Focus();
                return;
            }

            DialogResult = true;
        }

        private void BtnHuy_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private static object NullIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
        }

        private static string GetComboBoxText(ComboBox comboBox)
        {
            return comboBox.SelectedItem is ComboBoxItem item
                ? item.Content?.ToString() ?? string.Empty
                : comboBox.Text;
        }

        private static void SelectComboBoxText(ComboBox comboBox, string value)
        {
            foreach (object item in comboBox.Items)
            {
                if (item is ComboBoxItem comboBoxItem &&
                    string.Equals(comboBoxItem.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = comboBoxItem;
                    return;
                }
            }

            comboBox.Text = value;
        }

        private static string EmptyIfPlaceholder(string value)
        {
            return value.Contains("cập nhật", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;
        }
    }
}
