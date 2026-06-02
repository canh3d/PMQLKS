using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.UserControls
{
    /// <summary>
    /// Interaction logic for UCPhong.xaml
    /// </summary>
    public partial class UCPhong : UserControl
    {
        public event EventHandler<PhongDTO>? PhongSelected;
        public event EventHandler<PhongDTO>? PhongDoubleClicked;

        public PhongDTO Phong { get; }

        public UCPhong(PhongDTO phong)
        {
            InitializeComponent();
            Phong = phong;
            HienThi();
        }

        private void HienThi()
        {
            TxtMaPhong.Text = Phong.MaHienThi;
            TxtTrangThai.Text = Phong.TrangThai;
            TxtLoaiPhong.Text = Phong.LoaiPhong;
            TxtGiaPhong.Text = Phong.GiaPhongHienThi;
            TxtDonDep.Text = Phong.TinhTrangDonDep;
            IconStar.Visibility = Phong.TinhTrangDonDep.Contains("Chưa", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;

            ApplyStatusStyle();
        }

        private void ApplyStatusStyle()
        {
            string trangThai = BoDau(Phong.TrangThai);
            string donDep = BoDau(Phong.TinhTrangDonDep);

            if (trangThai.Contains("dat", StringComparison.OrdinalIgnoreCase))
            {
                SetStyle("#EFF6FF", "#60A5FA", "#1D4ED8");
                return;
            }

            if (trangThai.Contains("thue", StringComparison.OrdinalIgnoreCase) ||
                trangThai.Contains("co khach", StringComparison.OrdinalIgnoreCase))
            {
                SetStyle("#FEF3F2", "#E59696", "#C0392B");
                return;
            }

            if (trangThai.Contains("sua", StringComparison.OrdinalIgnoreCase))
            {
                SetStyle("#F8FAFC", "#A7B6C8", "#475467");
                return;
            }

            if (trangThai.Contains("chua don", StringComparison.OrdinalIgnoreCase) ||
                donDep.Contains("chua", StringComparison.OrdinalIgnoreCase))
            {
                SetStyle("#FFF9EB", "#B7791F", "#B7791F");
                return;
            }

            SetStyle("#ECFDF3", "#6EC491", "#0D9E4D");
        }

        private void SetStyle(string background, string border, string foreground)
        {
            RootBorder.Background = BrushFromHex(background);
            RootBorder.BorderBrush = BrushFromHex(border);
            TxtMaPhong.Foreground = BrushFromHex(foreground);
            TxtTrangThai.Foreground = BrushFromHex(foreground);
        }

        private static Brush BrushFromHex(string hex)
        {
            return (Brush)new BrushConverter().ConvertFromString(hex)!;
        }

        private static string BoDau(string value)
        {
            string formD = (value ?? string.Empty).Normalize(System.Text.NormalizationForm.FormD);
            char[] chars = formD
                .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .Select(ch => ch == 'đ' ? 'd' : ch == 'Đ' ? 'D' : ch)
                .ToArray();
            return new string(chars).Normalize(System.Text.NormalizationForm.FormC);
        }

        private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                PhongDoubleClicked?.Invoke(this, Phong);
                e.Handled = true;
                return;
            }

            PhongSelected?.Invoke(this, Phong);
        }
    }
}
