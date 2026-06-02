using System.Windows;
using System.Windows.Controls;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.View
{
    /// <summary>
    /// Interaction logic for DichVuVatTuForm.xaml
    /// </summary>
    public partial class DichVuVatTuForm : Window
    {
        public DichVuVatTuDTO DuLieu { get; private set; }

        public DichVuVatTuForm(DichVuVatTuDTO? duLieu = null)
        {
            InitializeComponent();
            DuLieu = duLieu == null ? new DichVuVatTuDTO() : Clone(duLieu);
            NapDuLieu(duLieu != null);
        }

        private void NapDuLieu(bool isEdit)
        {
            TxtTieuDe.Text = isEdit ? "Sửa dịch vụ - vật tư" : "Thêm dịch vụ - vật tư";
            TxtTen.Text = DuLieu.Ten;
            TxtDonViTinh.Text = DuLieu.DonViTinh;
            TxtDonGia.Text = DuLieu.DonGia == 0 ? string.Empty : DuLieu.DonGia.ToString("0");
            TxtSoLuongTon.Text = DuLieu.SoLuongTon.ToString();
            TxtGhiChu.Text = DuLieu.GhiChu;
            SelectComboItem(CboLoai, string.IsNullOrWhiteSpace(DuLieu.Loai) ? "Dịch vụ" : DuLieu.Loai);
            SelectComboItem(CboTrangThai, string.IsNullOrWhiteSpace(DuLieu.TrangThai) ? "Còn hàng" : DuLieu.TrangThai);
            TxtTen.Focus();
        }

        private void BtnLuu_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(TxtDonGia.Text.Trim(), out decimal donGia) || donGia < 0)
            {
                TxtThongBao.Text = "Đơn giá phải là số không âm.";
                TxtDonGia.Focus();
                return;
            }

            if (!int.TryParse(TxtSoLuongTon.Text.Trim(), out int soLuongTon) || soLuongTon < 0)
            {
                TxtThongBao.Text = "Số lượng tồn phải là số nguyên không âm.";
                TxtSoLuongTon.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtTen.Text))
            {
                TxtThongBao.Text = "Vui lòng nhập tên dịch vụ/vật tư.";
                TxtTen.Focus();
                return;
            }

            DuLieu.Ten = TxtTen.Text.Trim();
            DuLieu.Loai = GetComboText(CboLoai);
            DuLieu.DonViTinh = TxtDonViTinh.Text.Trim();
            DuLieu.DonGia = donGia;
            DuLieu.SoLuongTon = soLuongTon;
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

        private static DichVuVatTuDTO Clone(DichVuVatTuDTO item)
        {
            return new DichVuVatTuDTO
            {
                Ma = item.Ma,
                Ten = item.Ten,
                Loai = item.Loai,
                DonViTinh = item.DonViTinh,
                DonGia = item.DonGia,
                SoLuongTon = item.SoLuongTon,
                TrangThai = item.TrangThai,
                GhiChu = item.GhiChu,
                SourceSchema = item.SourceSchema,
                SourceTable = item.SourceTable,
                KeyColumn = item.KeyColumn
            };
        }
    }
}
