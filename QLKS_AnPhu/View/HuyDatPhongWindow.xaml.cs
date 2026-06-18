using System.Collections.ObjectModel;
using System.Windows;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.View
{
    public partial class HuyDatPhongWindow : Window
    {
        public HuyDatPhongWindow(IEnumerable<DuToanHuyDatPhongDTO> duToan)
        {
            InitializeComponent();

            List<DuToanHuyDatPhongDTO> items = duToan.ToList();
            decimal tongCoc = items.Sum(item => item.TienCoc);
            decimal tongGiu = items.Sum(item => item.TienCocGiuLai);
            decimal tongHoan = items.Sum(item => item.TienCocHoanTra);

            TxtMoTa.Text = items.Count > 1
                ? "Bạn đang hủy " + items.Count + " phiếu đặt trong đoàn. Vui lòng kiểm tra khoản cọc giữ lại/hoàn trả trước khi xác nhận."
                : "Vui lòng kiểm tra khoản cọc giữ lại/hoàn trả trước khi xác nhận hủy đặt phòng.";
            TxtTienCoc.Text = DinhDangTien(tongCoc);
            TxtGiuLai.Text = DinhDangTien(tongGiu);
            TxtHoanTra.Text = DinhDangTien(tongHoan);
            TxtChinhSach.Text = TaoMoTaChinhSach(items, tongGiu, tongHoan);
            DgChiTiet.ItemsSource = new ObservableCollection<HuyDatPhongPreviewRow>(
                items.Select(item => new HuyDatPhongPreviewRow(item)));
        }

        private static string TaoMoTaChinhSach(List<DuToanHuyDatPhongDTO> items, decimal tongGiu, decimal tongHoan)
        {
            if (items.Count == 0)
            {
                return "Không có phiếu đặt để hủy.";
            }

            string chinhSach = items.Select(item => item.ChinhSach).Distinct().Count() == 1
                ? items[0].ChinhSach
                : "Các phiếu trong đoàn có mốc giờ khác nhau, xem chi tiết từng dòng bên dưới.";

            return chinhSach + " Giữ lại " + DinhDangTien(tongGiu) + ", hoàn khách " + DinhDangTien(tongHoan) + ".";
        }

        private static string DinhDangTien(decimal value)
        {
            return value.ToString("N0") + " VND";
        }

        private void BtnXacNhan_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void BtnDong_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private class HuyDatPhongPreviewRow
        {
            private readonly DuToanHuyDatPhongDTO source;

            public HuyDatPhongPreviewRow(DuToanHuyDatPhongDTO source)
            {
                this.source = source;
            }

            public int MaDatPhong => source.MaDatPhong;
            public string NgayNhanText => source.NgayNhanDuKien.HasValue
                ? source.NgayNhanDuKien.Value.ToString("dd/MM/yyyy HH:mm")
                : "--";
            public string TienCocText => DinhDangTien(source.TienCoc);
            public string GiuLaiText => DinhDangTien(source.TienCocGiuLai);
            public string HoanTraText => DinhDangTien(source.TienCocHoanTra);
        }
    }
}
