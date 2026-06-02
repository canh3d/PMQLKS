using System.Windows;
using System.Windows.Controls;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.View
{
    public partial class KhachHangForm : Window
    {
        public KhachHangDTO DuLieu { get; private set; }

        public KhachHangForm(KhachHangDTO? duLieu = null)
        {
            InitializeComponent();
            DuLieu = duLieu == null ? new KhachHangDTO() : Clone(duLieu);
            NapDuLieu(duLieu != null);
        }

        private void NapDuLieu(bool isEdit)
        {
            TxtTieuDe.Text = isEdit ? "Sửa khách hàng" : "Thêm khách hàng";
            TxtHoTen.Text = DuLieu.HoTen;
            TxtSDT.Text = DuLieu.SDT;
            TxtCCCD.Text = DuLieu.CCCD;
            TxtDiaChi.Text = DuLieu.DiaChi;
            TxtGhiChu.Text = DuLieu.GhiChu;
            DpNgaySinh.SelectedDate = DuLieu.NgaySinh;
            SelectComboItem(CboGioiTinh, string.IsNullOrWhiteSpace(DuLieu.GioiTinh) ? "Nam" : DuLieu.GioiTinh);
            SelectComboItem(CboLoaiKhach, string.IsNullOrWhiteSpace(DuLieu.LoaiKhach) ? "Thường" : DuLieu.LoaiKhach);
            SelectComboItem(CboTrangThai, string.IsNullOrWhiteSpace(DuLieu.TrangThai) ? "Đang hoạt động" : DuLieu.TrangThai);
            TxtHoTen.Focus();
        }

        private void BtnLuu_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtHoTen.Text))
            {
                TxtThongBao.Text = "Vui lòng nhập họ tên khách hàng.";
                TxtHoTen.Focus();
                return;
            }

            DuLieu.HoTen = TxtHoTen.Text.Trim();
            DuLieu.SDT = TxtSDT.Text.Trim();
            DuLieu.CCCD = TxtCCCD.Text.Trim();
            DuLieu.GioiTinh = GetComboText(CboGioiTinh);
            DuLieu.NgaySinh = DpNgaySinh.SelectedDate;
            DuLieu.DiaChi = TxtDiaChi.Text.Trim();
            DuLieu.LoaiKhach = GetComboText(CboLoaiKhach);
            DuLieu.TrangThai = GetComboText(CboTrangThai);
            DuLieu.GhiChu = TxtGhiChu.Text.Trim();

            DialogResult = true;
            Close();
        }

        private void BtnHuy_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static string GetComboText(ComboBox comboBox)
        {
            return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
        }

        private static void SelectComboItem(ComboBox comboBox, string value)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private static KhachHangDTO Clone(KhachHangDTO item)
        {
            return new KhachHangDTO
            {
                Ma = item.Ma,
                HoTen = item.HoTen,
                SDT = item.SDT,
                CCCD = item.CCCD,
                GioiTinh = item.GioiTinh,
                NgaySinh = item.NgaySinh,
                DiaChi = item.DiaChi,
                LoaiKhach = item.LoaiKhach,
                TrangThai = item.TrangThai,
                GhiChu = item.GhiChu,
                SourceSchema = item.SourceSchema,
                SourceTable = item.SourceTable,
                KeyColumn = item.KeyColumn
            };
        }
    }
}
