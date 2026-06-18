using System.Collections.ObjectModel;
using System.Windows;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.View
{
    public partial class ThanhToanCheckOutWindow : Window
    {
        public bool ThanhToanSau { get; private set; }

        public ThanhToanCheckOutWindow(DuToanCheckOutDTO duToan)
        {
            InitializeComponent();
            TxtMoTa.Text = $"Phiếu thuê #{duToan.MaThue} - chế độ {duToan.CheDoDatPhong}";
            TxtTraDuKien.Text = duToan.NgayTraDuKien.ToString("dd/MM/yyyy HH:mm");
            TxtTraThucTe.Text = duToan.NgayTraThucTe.ToString("dd/MM/yyyy HH:mm");

            int gioMuon = duToan.SoPhutTraMuon / 60;
            int phutMuon = duToan.SoPhutTraMuon % 60;
            int soGioTinhPhi = duToan.SoPhutTinhPhi / 60;
            decimal donGiaGio = TinhDonGiaGioPhuPhi(duToan);
            DgKhoanThanhToan.ItemsSource = new ObservableCollection<KhoanCheckOutItem>
            {
                new("Dịch vụ phát sinh trong thời gian thuê", "Tổng dịch vụ sau khi nhận phòng", duToan.TienDichVuPhatSinh),
                new("Tiền gia hạn phòng", "Các lần gia hạn đã xác nhận", duToan.TienGiaHan),
                new(
                    duToan.ChenhLechDoiPhong < 0 ? "Hoàn chênh lệch đổi phòng" : "Phụ thu chênh lệch đổi phòng",
                    "Giá phòng mới trừ giá phòng cũ theo thời gian còn lại",
                    duToan.ChenhLechDoiPhong),
                new(
                    "Thời gian trả muộn",
                    duToan.SoPhutTraMuon <= 0
                        ? "Không trả muộn"
                        : duToan.SoPhutTinhPhi <= 0
                            ? $"Muộn {gioMuon} giờ {phutMuon} phút - miễn phí trong 30 phút"
                            : $"Muộn {gioMuon} giờ {phutMuon} phút, tính {soGioTinhPhi} giờ x {donGiaGio:N0} VND/giờ",
                    duToan.PhuPhiTraMuon),
                new("Thuế VAT (10%)", "10% x tổng phát sinh trước VAT", duToan.ThueVat)
            };

            if (duToan.CanTraKhach > 0)
            {
                TxtKetLuan.Text = "Cần trả lại khách";
                TxtTongTien.Text = duToan.CanTraKhach.ToString("N0") + " VND";
                TxtTongTien.Foreground = System.Windows.Media.Brushes.ForestGreen;
                TxtGhiChu.Text = "Khoản hoàn do đổi xuống phòng giá thấp hơn, sau khi bù trừ dịch vụ, gia hạn và phụ phí.";
            }
            else
            {
                TxtKetLuan.Text = "Cần thu thêm";
                TxtTongTien.Text = duToan.CanThuThem.ToString("N0") + " VND";
                TxtGhiChu.Text = "Đây là phần phát sinh sau khoản đã thanh toán lúc nhận phòng.";
            }
        }

        private void BtnXacNhan_Click(object sender, RoutedEventArgs e)
        {
            ThanhToanSau = false;
            DialogResult = true;
        }

        private void BtnThanhToanSau_Click(object sender, RoutedEventArgs e)
        {
            ThanhToanSau = true;
            DialogResult = true;
        }

        private void BtnHuy_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private static decimal TinhDonGiaGioPhuPhi(DuToanCheckOutDTO duToan)
        {
            if (duToan.PhuPhiTraMuon > 0 && duToan.SoPhutTinhPhi > 0)
            {
                int soGioTinhPhi = Math.Max(1, duToan.SoPhutTinhPhi / 60);
                return Math.Round(duToan.PhuPhiTraMuon / soGioTinhPhi, 0);
            }

            string mode = BoDau(duToan.CheDoDatPhong ?? string.Empty).ToLowerInvariant();
            if (mode.Contains("gio"))
            {
                return duToan.GiaGio > 0 ? duToan.GiaGio : Math.Round(duToan.GiaNgay / 24m, 0);
            }

            if (mode.Contains("dem"))
            {
                return Math.Round((duToan.GiaDem > 0 ? duToan.GiaDem : duToan.GiaNgay) / 12m, 0);
            }

            return Math.Round(duToan.GiaNgay / 24m, 0);
        }

        private static string BoDau(string value)
        {
            string formD = value.Normalize(System.Text.NormalizationForm.FormD);
            char[] chars = formD
                .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray();
            return new string(chars).Normalize(System.Text.NormalizationForm.FormC);
        }
    }

    public sealed class KhoanCheckOutItem
    {
        public KhoanCheckOutItem(string ten, string cachTinh, decimal soTien)
        {
            Ten = ten;
            CachTinh = cachTinh;
            SoTien = soTien;
        }

        public string Ten { get; }
        public string CachTinh { get; }
        public decimal SoTien { get; }
        public bool LaKhoanHoan => SoTien < 0;
        public string SoTienHienThi => SoTien == 0
            ? "0 VND"
            : (SoTien < 0 ? "- " : string.Empty) + Math.Abs(SoTien).ToString("N0") + " VND";
    }
}
