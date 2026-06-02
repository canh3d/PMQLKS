using System.Windows;
using System.Windows.Controls;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.View
{
    public partial class PhongForm : Window
    {
        private bool dangNapDuLieu;

        public PhongDTO DuLieu { get; private set; }

        public PhongForm(PhongDTO? phong = null)
        {
            InitializeComponent();
            DuLieu = phong == null ? new PhongDTO { TrangThai = "Phòng trống", LoaiPhong = "Phòng đơn" } : Clone(phong);
            NapDuLieu(phong != null);
        }

        private void NapDuLieu(bool isEdit)
        {
            dangNapDuLieu = true;
            TxtTieuDe.Text = isEdit ? "Sửa phòng" : "Thêm phòng";
            TxtSoPhong.Text = DuLieu.SoPhong;
            TxtTang.Text = DuLieu.Tang == 0 ? string.Empty : DuLieu.Tang.ToString();
            TxtLoaiPhong.Text = ChuanHoaLoaiPhong(DuLieu.LoaiPhong, DuLieu.MaLoaiPhong);
            TxtGiaGio.Text = DuLieu.GiaGio == 0 ? string.Empty : DuLieu.GiaGio.ToString("0");
            TxtGiaNgay.Text = DuLieu.GiaNgay == 0 ? string.Empty : DuLieu.GiaNgay.ToString("0");
            TxtGiaDem.Text = DuLieu.GiaDem == 0 ? string.Empty : DuLieu.GiaDem.ToString("0");
            TxtGhiChu.Text = DuLieu.GhiChu == "--" ? string.Empty : DuLieu.GhiChu;
            SelectCombo(CboLoaiPhong, TxtLoaiPhong.Text);
            SelectCombo(CboTrangThai, string.IsNullOrWhiteSpace(DuLieu.TrangThai) ? "Phòng trống" : DuLieu.TrangThai);
            dangNapDuLieu = false;

            if (!isEdit)
            {
                CboLoaiPhong.SelectedIndex = 0;
                ApDungGiaTheoLoaiPhong();
            }
        }

        private void BtnLuu_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtSoPhong.Text))
            {
                TxtThongBao.Text = "Vui lòng nhập số phòng.";
                return;
            }

            if (!int.TryParse(TxtTang.Text.Trim(), out int tang) || tang <= 0)
            {
                TxtThongBao.Text = "Tầng phải là số nguyên lớn hơn 0.";
                return;
            }

            string loaiPhong = GetComboText(CboLoaiPhong);
            int maLoaiPhong = LayMaLoaiPhong(loaiPhong);

            if (!decimal.TryParse(TxtGiaGio.Text.Trim(), out decimal giaGio))
            {
                giaGio = 0;
            }

            if (!decimal.TryParse(TxtGiaNgay.Text.Trim(), out decimal giaNgay))
            {
                giaNgay = 0;
            }

            if (!decimal.TryParse(TxtGiaDem.Text.Trim(), out decimal giaDem))
            {
                giaDem = 0;
            }

            DuLieu.SoPhong = TxtSoPhong.Text.Trim();
            DuLieu.TenPhong = TxtSoPhong.Text.Trim();
            DuLieu.Tang = tang;
            DuLieu.MaLoaiPhong = maLoaiPhong;
            DuLieu.LoaiPhong = loaiPhong;
            DuLieu.GiaGio = giaGio;
            DuLieu.GiaNgay = giaNgay;
            DuLieu.GiaDem = giaDem;
            DuLieu.GiaPhong = giaDem > 0 ? giaDem : giaNgay;
            DuLieu.TrangThai = GetComboText(CboTrangThai);
            DuLieu.GhiChu = TxtGhiChu.Text.Trim();

            DialogResult = true;
            Close();
        }

        private void CboLoaiPhong_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!dangNapDuLieu)
            {
                ApDungGiaTheoLoaiPhong();
            }
        }

        private void ApDungGiaTheoLoaiPhong()
        {
            string loaiPhong = GetComboText(CboLoaiPhong);
            TxtLoaiPhong.Text = loaiPhong;
            (decimal giaGio, decimal giaNgay, decimal giaDem) = LayGiaTheoLoaiPhong(loaiPhong);
            TxtGiaGio.Text = giaGio.ToString("0");
            TxtGiaNgay.Text = giaNgay.ToString("0");
            TxtGiaDem.Text = giaDem.ToString("0");
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

        private static void SelectCombo(ComboBox comboBox, string value)
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

        private static string ChuanHoaLoaiPhong(string loaiPhong, int maLoaiPhong)
        {
            if (loaiPhong.Contains("VIP", StringComparison.OrdinalIgnoreCase) || maLoaiPhong == 3)
            {
                return "Phòng VIP";
            }

            if (loaiPhong.Contains("đôi", StringComparison.OrdinalIgnoreCase) ||
                loaiPhong.Contains("doi", StringComparison.OrdinalIgnoreCase) ||
                maLoaiPhong == 2)
            {
                return "Phòng đôi";
            }

            return "Phòng đơn";
        }

        private static int LayMaLoaiPhong(string loaiPhong)
        {
            if (loaiPhong.Contains("VIP", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            return loaiPhong.Contains("đôi", StringComparison.OrdinalIgnoreCase) ||
                   loaiPhong.Contains("doi", StringComparison.OrdinalIgnoreCase)
                ? 2
                : 1;
        }

        private static (decimal GiaGio, decimal GiaNgay, decimal GiaDem) LayGiaTheoLoaiPhong(string loaiPhong)
        {
            if (loaiPhong.Contains("VIP", StringComparison.OrdinalIgnoreCase))
            {
                return (200000m, 1200000m, 900000m);
            }

            if (loaiPhong.Contains("đôi", StringComparison.OrdinalIgnoreCase) ||
                loaiPhong.Contains("doi", StringComparison.OrdinalIgnoreCase))
            {
                return (120000m, 700000m, 500000m);
            }

            return (80000m, 450000m, 350000m);
        }

        private static PhongDTO Clone(PhongDTO phong)
        {
            return new PhongDTO
            {
                Ma = phong.Ma,
                MaPhong = phong.MaPhong,
                SoPhong = phong.SoPhong,
                TenPhong = phong.TenPhong,
                Tang = phong.Tang,
                MaLoaiPhong = phong.MaLoaiPhong,
                LoaiPhong = phong.LoaiPhong,
                GiaGio = phong.GiaGio,
                GiaNgay = phong.GiaNgay,
                GiaDem = phong.GiaDem,
                GiaPhong = phong.GiaPhong,
                TrangThai = phong.TrangThai,
                TinhTrangDonDep = phong.TinhTrangDonDep,
                KhachHienTai = phong.KhachHienTai,
                GioNhanPhong = phong.GioNhanPhong,
                GioTraDuKien = phong.GioTraDuKien,
                GhiChu = phong.GhiChu,
                SourceSchema = phong.SourceSchema,
                SourceTable = phong.SourceTable,
                KeyColumn = phong.KeyColumn
            };
        }
    }
}
