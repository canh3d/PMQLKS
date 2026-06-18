using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QLKS_AnPhu.Security;
using QLKS_AnPhu.Services;

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
            TxtNgayLap.Text = "Ngày lập: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            TxtMaHoaDon.Text = "Mã hóa đơn: " + bill.MaBill;
            TxtSoPhong.Text = "Số phòng: " + bill.SoPhong;
            TxtNguoiLap.Text = "Người lập: " + LayNhanVienLap();
            TxtKhachHang.Text = "Khách hàng: " + bill.NguoiThanhToan;
            TxtSdt.Text = string.IsNullOrWhiteSpace(bill.SoDienThoai) ? "SĐT: --" : "SĐT: " + bill.SoDienThoai;
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

        private static string LayNhanVienLap()
        {
            if (!string.IsNullOrWhiteSpace(CurrentUser.HoTen))
            {
                return CurrentUser.HoTen;
            }

            return string.IsNullOrWhiteSpace(CurrentUser.TenDangNhap)
                ? "Chưa xác định"
                : CurrentUser.TenDangNhap;
        }

        private void BtnIn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PrintDialog dialog = new();
                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                InvoicePaper.UpdateLayout();
                double paperWidth = Math.Max(1, InvoicePaper.ActualWidth);
                double paperHeight = Math.Max(1, InvoicePaper.ActualHeight);
                Size printableSize = new(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
                Transform? oldTransform = InvoicePaper.LayoutTransform;
                Thickness oldMargin = InvoicePaper.Margin;

                try
                {
                    double scale = Math.Min(printableSize.Width / paperWidth, printableSize.Height / paperHeight);
                    scale = Math.Min(1, scale);
                    InvoicePaper.Margin = new Thickness(0);
                    InvoicePaper.LayoutTransform = new ScaleTransform(scale, scale);
                    InvoicePaper.Measure(printableSize);
                    InvoicePaper.Arrange(new Rect(new Point(0, 0), printableSize));
                    InvoicePaper.UpdateLayout();

                    dialog.PrintVisual(InvoicePaper, "Hóa đơn tách " + bill.MaBill);
                }
                finally
                {
                    InvoicePaper.LayoutTransform = oldTransform;
                    InvoicePaper.Margin = oldMargin;
                    InvoicePaper.UpdateLayout();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không in được hóa đơn tách: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                Title = "Xuất hóa đơn tách",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = SafeFileName(bill.MaBill) + ".xlsx"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                ExcelDocument document = new()
                {
                    Title = $"HÓA ĐƠN TÁCH {bill.MaBill}",
                    Subtitle = $"Hóa đơn gốc: {hoaDon.MaHoaDon} | Ngày lập: {DateTime.Now:dd/MM/yyyy HH:mm}",
                    SheetName = "Hóa đơn tách"
                };

                ExcelSection information = new()
                {
                    Title = "Thông tin thanh toán",
                    Headers = ["Nội dung", "Thông tin"],
                    ColumnWidths = [22, 42]
                };
                information.Rows.Add(["Người đại diện", bill.NguoiThanhToan]);
                information.Rows.Add(["Số điện thoại", bill.SoDienThoai]);
                information.Rows.Add(["Phòng", bill.SoPhong]);
                information.Rows.Add(["Ghi chú", bill.GhiChu]);
                document.Sections.Add(information);

                ExcelSection details = new()
                {
                    Title = "Chi tiết hóa đơn tách",
                    Headers = ["Phòng", "Khoản mục", "Đơn giá", "Số lượng", "Thành tiền"],
                    ColumnWidths = [12, 36, 18, 12, 20],
                    Summary = $"Tổng tiền hóa đơn tách: {bill.SoTien:N0} VND"
                };
                foreach (RoomPrintGroup room in phongChiTiet)
                {
                    details.Rows.AddRange(room.Items.Select(item => (IReadOnlyList<object?>)
                        [room.SoPhong, item.Ten, new ExcelMoney(item.DonGia), item.SoLuong, new ExcelMoney(item.ThanhTien)]));
                }
                document.Sections.Add(details);

                ExcelExportService.Export(dialog.FileName, document);
                MessageBox.Show("Đã xuất file Excel.", "Xuất Excel", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không xuất được Excel: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
