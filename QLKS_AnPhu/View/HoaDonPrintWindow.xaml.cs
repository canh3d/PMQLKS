using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QLKS_AnPhu.DAL;

namespace QLKS_AnPhu.View
{
    public partial class HoaDonPrintWindow : Window
    {
        private readonly HoaDonItem hoaDon;
        private readonly ObservableCollection<PrintLineItem> chiTiet = new();

        public HoaDonPrintWindow(HoaDonItem hoaDon)
        {
            this.hoaDon = hoaDon;
            InitializeComponent();
            Loaded += HoaDonPrintWindow_Loaded;
        }

        private void HoaDonPrintWindow_Loaded(object sender, RoutedEventArgs e)
        {
            NapHoaDon();
        }

        private void NapHoaDon()
        {
            TxtNgayLap.Text = "Ngày lập: " + hoaDon.NgayLapHoaDon.ToString("dd/MM/yyyy HH:mm");
            TxtMaHoaDon.Text = "Mã hóa đơn: " + hoaDon.MaHoaDon;
            TxtSoPhong.Text = "Số phòng: " + hoaDon.SoPhong;
            TxtKhachHang.Text = "Khách hàng: " + hoaDon.TenKhachHang;
            TxtSdt.Text = "SĐT: " + hoaDon.SoDienThoai;
            TxtPhongDong.Text = hoaDon.SoPhong;
            TxtTongPhong.Text = hoaDon.TongTien.ToString("N0", CultureInfo.InvariantCulture);
            TxtTongTien.Text = hoaDon.TongTien.ToString("N0", CultureInfo.InvariantCulture) + " VND";

            chiTiet.Clear();
            chiTiet.Add(new PrintLineItem("Thuê phòng", hoaDon.TienPhong, 1, hoaDon.TienPhong));
            foreach (DichVuHoaDonItem item in LoadDichVu())
            {
                chiTiet.Add(new PrintLineItem(item.TenDichVu, item.DonGia, item.SoLuong, item.ThanhTien));
            }

            if (hoaDon.PhuPhi > 0)
            {
                chiTiet.Add(new PrintLineItem("Phụ phí trả muộn", 0, 0, hoaDon.PhuPhi));
            }

            if (hoaDon.GiamGia > 0)
            {
                chiTiet.Add(new PrintLineItem("Giảm giá / cọc", 0, 0, -hoaDon.GiamGia));
            }

            ItemsChiTiet.ItemsSource = chiTiet;
        }

        private ObservableCollection<DichVuHoaDonItem> LoadDichVu()
        {
            ObservableCollection<DichVuHoaDonItem> result = new();
            string table = TableExists("PHATSINHDICHVU") ? "PHATSINHDICHVU" : TableExists("CHITIETPHATSINH") ? "CHITIETPHATSINH" : string.Empty;
            if (string.IsNullOrWhiteSpace(table) || !TableExists("DICHVUVATTU"))
            {
                return result;
            }

            string keyColumn = hoaDon.LoaiPhieu == "THUE" && ColumnExists(table, "MaThue") ? "MaThue" : ColumnExists(table, "MaDatPhong") ? "MaDatPhong" : string.Empty;
            if (string.IsNullOrWhiteSpace(keyColumn))
            {
                return result;
            }

            string tenDichVu = ColumnExists("DICHVUVATTU", "TenDVVT") ? "TenDVVT" : "TenDichVu";
            string maDvPs = ColumnExists(table, "MaDVVT") ? "MaDVVT" : "MaDichVu";
            string maDv = ColumnExists("DICHVUVATTU", "MaDVVT") ? "MaDVVT" : "MaDichVu";
            string soLuong = ColumnExists(table, "SoLuong") ? "PS.SoLuong" : "1";
            string donGia = ColumnExists(table, "DonGia") ? "ISNULL(PS.DonGia, DV.DonGia)" : "DV.DonGia";
            string thanhTien = ColumnExists(table, "ThanhTien") ? "PS.ThanhTien" : "(" + soLuong + " * " + donGia + ")";

            DataTable data = ConnectDB.GetData(
                @"SELECT DV." + tenDichVu + @" AS TenDichVu,
                         " + soLuong + @" AS SoLuong,
                         " + donGia + @" AS DonGia,
                         " + thanhTien + @" AS ThanhTien
                  FROM dbo." + table + @" PS
                  JOIN dbo.DICHVUVATTU DV ON PS." + maDvPs + " = DV." + maDv + @"
                  WHERE PS." + keyColumn + " = @Ma",
                new SqlParameter("@Ma", hoaDon.MaGoc));

            int stt = 1;
            foreach (DataRow row in data.Rows)
            {
                result.Add(new DichVuHoaDonItem
                {
                    Stt = stt++,
                    TenDichVu = row["TenDichVu"]?.ToString() ?? string.Empty,
                    SoLuong = GetDecimal(row, "SoLuong"),
                    DonGia = GetDecimal(row, "DonGia"),
                    ThanhTien = GetDecimal(row, "ThanhTien")
                });
            }

            return result;
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

            dialog.PrintVisual(InvoicePaper, "Hóa đơn " + hoaDon.MaHoaDon);

            InvoicePaper.LayoutTransform = oldTransform;
            InvoicePaper.UpdateLayout();
        }

        private void BtnXuatAnh_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                Filter = "PNG Image (*.png)|*.png",
                FileName = SafeFileName(hoaDon.MaHoaDon) + ".png"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            RenderTargetBitmap bitmap = new((int)InvoicePaper.ActualWidth, (int)InvoicePaper.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(InvoicePaper);

            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream stream = File.Create(dialog.FileName);
            encoder.Save(stream);

            MessageBox.Show("Đã xuất ảnh hóa đơn.", "Xuất ảnh", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnXuatExcel_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                Filter = "Excel CSV (*.csv)|*.csv",
                FileName = SafeFileName(hoaDon.MaHoaDon) + ".csv"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            StringBuilder builder = new();
            builder.AppendLine("Khách sạn An Phú");
            builder.AppendLine("Mã hóa đơn," + Csv(hoaDon.MaHoaDon));
            builder.AppendLine("Ngày lập," + Csv(hoaDon.NgayLapHoaDon.ToString("dd/MM/yyyy HH:mm")));
            builder.AppendLine("Khách hàng," + Csv(hoaDon.TenKhachHang));
            builder.AppendLine("SĐT," + Csv(hoaDon.SoDienThoai));
            builder.AppendLine("Phòng," + Csv(hoaDon.SoPhong));
            builder.AppendLine();
            builder.AppendLine("Dịch vụ / vật tư,Đơn giá,SL,Thành tiền");
            foreach (PrintLineItem item in chiTiet)
            {
                builder.AppendLine(Csv(item.Ten) + "," + item.DonGia.ToString(CultureInfo.InvariantCulture) + "," + item.SoLuong.ToString(CultureInfo.InvariantCulture) + "," + item.ThanhTien.ToString(CultureInfo.InvariantCulture));
            }

            builder.AppendLine();
            builder.AppendLine("Tổng tiền,,," + hoaDon.TongTien.ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(dialog.FileName, "\uFEFF" + builder, Encoding.UTF8);

            MessageBox.Show("Đã xuất file Excel.", "Xuất Excel", MessageBoxButton.OK, MessageBoxImage.Information);
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

            return string.IsNullOrWhiteSpace(value) ? "HoaDon" : value;
        }

        private static decimal GetDecimal(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && decimal.TryParse(row[column]?.ToString(), out decimal value) ? value : 0;
        }

        private static bool TableExists(string tableName)
        {
            object? result = ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM sys.tables WHERE name = @Name", new SqlParameter("@Name", tableName));
            return Convert.ToInt32(result) > 0;
        }

        private static bool ColumnExists(string tableName, string columnName)
        {
            object? result = ConnectDB.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM sys.tables t
                  JOIN sys.columns c ON t.object_id = c.object_id
                  WHERE t.name = @TableName AND c.name = @ColumnName",
                new SqlParameter("@TableName", tableName),
                new SqlParameter("@ColumnName", columnName));
            return Convert.ToInt32(result) > 0;
        }
    }

    public class PrintLineItem
    {
        public PrintLineItem(string ten, decimal donGia, decimal soLuong, decimal thanhTien)
        {
            Ten = ten;
            DonGia = donGia;
            SoLuong = soLuong;
            ThanhTien = thanhTien;
        }

        public string Ten { get; }
        public decimal DonGia { get; }
        public decimal SoLuong { get; }
        public decimal ThanhTien { get; }
        public string DonGiaText => DonGia == 0 ? string.Empty : DonGia.ToString("N0", CultureInfo.InvariantCulture);
        public string SoLuongText => SoLuong == 0 ? string.Empty : SoLuong.ToString("N0", CultureInfo.InvariantCulture);
        public string ThanhTienText => ThanhTien.ToString("N0", CultureInfo.InvariantCulture);
    }
}
