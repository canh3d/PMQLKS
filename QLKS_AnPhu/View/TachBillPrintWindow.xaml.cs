using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace QLKS_AnPhu.View
{
    public partial class TachBillPrintWindow : Window
    {
        private readonly HoaDonItem hoaDon;
        private readonly TachBillItem bill;
        private readonly ObservableCollection<RoomPrintGroup> phongChiTiet = new();

        public TachBillPrintWindow(HoaDonItem hoaDon, TachBillItem bill)
        {
            this.hoaDon = hoaDon;
            this.bill = bill;
            InitializeComponent();
            Loaded += TachBillPrintWindow_Loaded;
        }

        private void TachBillPrintWindow_Loaded(object sender, RoutedEventArgs e)
        {
            NapHoaDon();
        }

        private void NapHoaDon()
        {
            TxtNgayLap.Text = "Ngay lap: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            TxtMaHoaDon.Text = "Ma hoa don: " + bill.MaBill;
            TxtSoPhong.Text = "So phong: " + bill.SoPhong;
            TxtKhachHang.Text = "Khach hang: " + bill.NguoiThanhToan;
            TxtSdt.Text = string.IsNullOrWhiteSpace(bill.SoDienThoai) ? "SDT: --" : "SDT: " + bill.SoDienThoai;
            TxtTongTien.Text = bill.SoTien.ToString("N0", CultureInfo.InvariantCulture) + " VND";

            phongChiTiet.Clear();
            foreach (PrintLineItem item in bill.Items)
            {
                string soPhong = bill.SoPhong;
                string ten = item.Ten;

                const string prefix = "Phong ";
                int separatorIndex = ten.IndexOf(" - ", StringComparison.Ordinal);
                if (ten.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && separatorIndex > prefix.Length)
                {
                    soPhong = ten.Substring(prefix.Length, separatorIndex - prefix.Length).Trim();
                    ten = ten[(separatorIndex + 3)..].Trim();
                }

                RoomPrintGroup? group = phongChiTiet.FirstOrDefault(room =>
                    string.Equals(room.SoPhong, soPhong, StringComparison.OrdinalIgnoreCase));
                if (group == null)
                {
                    group = new RoomPrintGroup
                    {
                        SoPhong = soPhong
                    };
                    phongChiTiet.Add(group);
                }

                group.Items.Add(new PrintLineItem(ten, item.DonGia, item.SoLuong, item.ThanhTien));
            }

            if (phongChiTiet.Count == 0)
            {
                phongChiTiet.Add(new RoomPrintGroup
                {
                    SoPhong = bill.SoPhong
                });
            }

            ItemsPhongHoaDon.ItemsSource = phongChiTiet;
        }

        private void BtnIn_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog dialog = new();
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            Size printableSize = new(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
            Transform? oldTransform = InvoicePaper.LayoutTransform;
            double scale = Math.Min(printableSize.Width / InvoicePaper.ActualWidth, printableSize.Height / InvoicePaper.ActualHeight);
            InvoicePaper.LayoutTransform = new ScaleTransform(scale, scale);
            InvoicePaper.Measure(printableSize);
            InvoicePaper.Arrange(new Rect(new Point(0, 0), printableSize));
            InvoicePaper.UpdateLayout();

            dialog.PrintVisual(InvoicePaper, "Hoa don tach " + bill.MaBill);

            InvoicePaper.LayoutTransform = oldTransform;
            InvoicePaper.UpdateLayout();
        }

        private void BtnXuatAnh_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                Filter = "PNG Image (*.png)|*.png",
                FileName = SafeFileName(bill.MaBill) + ".png"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            RenderTargetBitmap bitmap = new((int)InvoicePaper.ActualWidth, (int)InvoicePaper.ActualHeight, 144, 144, PixelFormats.Pbgra32);
            bitmap.Render(InvoicePaper);

            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream stream = File.Create(dialog.FileName);
            encoder.Save(stream);

            MessageBox.Show("Da xuat anh hoa don.", "Xuat anh", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnXuatExcel_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                Filter = "Excel CSV (*.csv)|*.csv",
                FileName = SafeFileName(bill.MaBill) + ".csv"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            StringBuilder builder = new();
            builder.AppendLine("Khach san An Phu");
            builder.AppendLine("Bill tach," + Csv(bill.MaBill));
            builder.AppendLine("Hoa don goc," + Csv(hoaDon.MaHoaDon));
            builder.AppendLine("Ngay lap," + Csv(DateTime.Now.ToString("dd/MM/yyyy HH:mm")));
            builder.AppendLine("Nguoi dai dien," + Csv(bill.NguoiThanhToan));
            builder.AppendLine("SDT," + Csv(bill.SoDienThoai));
            builder.AppendLine("Phong," + Csv(bill.SoPhong));
            builder.AppendLine();
            builder.AppendLine("Phong,Dich vu / vat tu,Don gia,SL,Thanh tien,Tong phong");
            foreach (RoomPrintGroup room in phongChiTiet)
            {
                bool first = true;
                foreach (PrintLineItem item in room.Items)
                {
                    builder.AppendLine(
                        Csv(first ? room.SoPhong : string.Empty) + "," +
                        Csv(item.Ten) + "," +
                        item.DonGia.ToString(CultureInfo.InvariantCulture) + "," +
                        item.SoLuong.ToString(CultureInfo.InvariantCulture) + "," +
                        item.ThanhTien.ToString(CultureInfo.InvariantCulture) + "," +
                        (first ? room.TongPhong.ToString(CultureInfo.InvariantCulture) : string.Empty));
                    first = false;
                }
            }

            builder.AppendLine();
            builder.AppendLine("Tong tien,,,," + bill.SoTien.ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(dialog.FileName, "\uFEFF" + builder, Encoding.UTF8);

            MessageBox.Show("Da xuat file Excel.", "Xuat Excel", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDong_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static string Csv(string value)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string SafeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '-');
            }

            return string.IsNullOrWhiteSpace(value) ? "HoaDonTach" : value;
        }
    }
}
