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
        private readonly ObservableCollection<RoomPrintGroup> phongChiTiet = new();

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
            TxtNgayLap.Text = "Ngay lap: " + hoaDon.NgayLapHoaDon.ToString("dd/MM/yyyy HH:mm");
            TxtMaHoaDon.Text = "Ma hoa don: " + hoaDon.MaHoaDon;
            TxtSoPhong.Text = "So phong: " + hoaDon.SoPhong;
            TxtKhachHang.Text = "Khach hang: " + hoaDon.TenKhachHang;
            TxtSdt.Text = "SDT: " + hoaDon.SoDienThoai;
            TxtTongTien.Text = hoaDon.TongTien.ToString("N0", CultureInfo.InvariantCulture) + " VND";

            chiTiet.Clear();
            phongChiTiet.Clear();

            List<RoomPrintGroup> rooms = LoadPhongHoaDon();
            if (rooms.Count == 0)
            {
                rooms.Add(new RoomPrintGroup
                {
                    MaPhong = null,
                    SoPhong = hoaDon.SoPhong,
                    TienPhong = hoaDon.TienPhong
                });
            }

            foreach (RoomPrintGroup room in rooms)
            {
                room.Items.Add(new PrintLineItem("Thue phong", room.TienPhong, 1, room.TienPhong));
            }

            foreach (DichVuHoaDonItem item in LoadDichVu())
            {
                RoomPrintGroup target = item.MaPhong.HasValue
                    ? rooms.FirstOrDefault(room => room.MaPhong == item.MaPhong) ?? rooms[0]
                    : rooms[0];
                target.Items.Add(new PrintLineItem(item.TenDichVu, item.DonGia, item.SoLuong, item.ThanhTien));
            }

            if (hoaDon.PhuPhi > 0)
            {
                rooms[0].Items.Add(new PrintLineItem("Phu phi tra muon", 0, 0, hoaDon.PhuPhi));
            }

            if (hoaDon.GiamGia > 0)
            {
                rooms[0].Items.Add(new PrintLineItem("Giam gia / coc", 0, 0, -hoaDon.GiamGia));
            }

            foreach (RoomPrintGroup room in rooms)
            {
                foreach (PrintLineItem item in room.Items)
                {
                    chiTiet.Add(item);
                }

                phongChiTiet.Add(room);
            }

            ItemsPhongHoaDon.ItemsSource = phongChiTiet;
        }

        private List<RoomPrintGroup> LoadPhongHoaDon()
        {
            if (hoaDon.LoaiPhieu == "THUE" && TableExists("PHIEUTHUE"))
            {
                string ngayTraExpr = ColumnExists("PHIEUTHUE", "NgayTraPhong") ? "ISNULL(PT.NgayTraPhong, PT.NgayTraDuKien)" : "PT.NgayTraDuKien";
                string tienPhongExpr = TienPhongSql("PT.NgayNhan", "PT.NgayTraDuKien", ngayTraExpr);

                if (ColumnExists("PHIEUTHUE", "MaDatPhong") && TableExists("CHITIETDATPHONG"))
                {
                    DataTable data = ConnectDB.GetData(@"
SELECT P.MaPhong,
       " + TenPhongSql("P") + @" AS SoPhong,
       " + tienPhongExpr + @" AS TienPhong
FROM dbo.PHIEUTHUE PT
JOIN dbo.CHITIETDATPHONG CT ON PT.MaDatPhong = CT.MaDatPhong
JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE PT.MaThue = @Ma
ORDER BY P.MaPhong",
                        new SqlParameter("@Ma", hoaDon.MaGoc));
                    if (data.Rows.Count > 0)
                    {
                        return MapPhongHoaDon(data);
                    }
                }

                DataTable single = ConnectDB.GetData(@"
SELECT P.MaPhong,
       " + TenPhongSql("P") + @" AS SoPhong,
       " + tienPhongExpr + @" AS TienPhong
FROM dbo.PHIEUTHUE PT
JOIN dbo.PHONG P ON PT.MaPhong = P.MaPhong
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE PT.MaThue = @Ma",
                    new SqlParameter("@Ma", hoaDon.MaGoc));
                return MapPhongHoaDon(single);
            }

            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists("DATPHONG") ? "DATPHONG" : string.Empty;
            if (hoaDon.LoaiPhieu == "DAT" && !string.IsNullOrWhiteSpace(bangDatPhong))
            {
                string ngayNhanExpr = ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "DP.NgayNhanDuKien" : "DP.NgayNhanPhong";
                string ngayTraExpr = ColumnExists(bangDatPhong, "NgayTraDuKien") ? "DP.NgayTraDuKien" : "DP.NgayTraPhong";
                string tienPhongExpr = TienPhongSql(ngayNhanExpr, ngayTraExpr, ngayTraExpr);
                string joinPhong = TableExists("CHITIETDATPHONG")
                    ? @"JOIN dbo.CHITIETDATPHONG CT ON DP.MaDatPhong = CT.MaDatPhong
JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong"
                    : "JOIN dbo.PHONG P ON DP.MaPhong = P.MaPhong";

                DataTable data = ConnectDB.GetData(@"
SELECT P.MaPhong,
       " + TenPhongSql("P") + @" AS SoPhong,
       " + tienPhongExpr + @" AS TienPhong
FROM dbo." + bangDatPhong + @" DP
" + joinPhong + @"
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE DP.MaDatPhong = @Ma
ORDER BY P.MaPhong",
                    new SqlParameter("@Ma", hoaDon.MaGoc));
                return MapPhongHoaDon(data);
            }

            return new List<RoomPrintGroup>();
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
            string maPhongExpr = ColumnExists(table, "MaPhong") ? "PS.MaPhong" : "CAST(NULL AS int)";

            DataTable data = ConnectDB.GetData(
                @"SELECT DV." + tenDichVu + @" AS TenDichVu,
                         " + maPhongExpr + @" AS MaPhong,
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
                    MaPhong = GetNullableInt(row, "MaPhong"),
                    TenDichVu = row["TenDichVu"]?.ToString() ?? string.Empty,
                    SoLuong = GetDecimal(row, "SoLuong"),
                    DonGia = GetDecimal(row, "DonGia"),
                    ThanhTien = GetDecimal(row, "ThanhTien")
                });
            }

            return result;
        }

        private static List<RoomPrintGroup> MapPhongHoaDon(DataTable data)
        {
            return data.AsEnumerable()
                .Select(row => new RoomPrintGroup
                {
                    MaPhong = GetNullableInt(row, "MaPhong"),
                    SoPhong = row["SoPhong"]?.ToString() ?? string.Empty,
                    TienPhong = GetDecimal(row, "TienPhong")
                })
                .Where(room => !string.IsNullOrWhiteSpace(room.SoPhong))
                .ToList();
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

            dialog.PrintVisual(InvoicePaper, "Hoa don " + hoaDon.MaHoaDon);

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

            MessageBox.Show("Da xuat anh hoa don.", "Xuat anh", MessageBoxButton.OK, MessageBoxImage.Information);
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
            builder.AppendLine("Khach san An Phu");
            builder.AppendLine("Ma hoa don," + Csv(hoaDon.MaHoaDon));
            builder.AppendLine("Ngay lap," + Csv(hoaDon.NgayLapHoaDon.ToString("dd/MM/yyyy HH:mm")));
            builder.AppendLine("Khach hang," + Csv(hoaDon.TenKhachHang));
            builder.AppendLine("SDT," + Csv(hoaDon.SoDienThoai));
            builder.AppendLine("Phong," + Csv(hoaDon.SoPhong));
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
            builder.AppendLine("Tong tien,,,," + hoaDon.TongTien.ToString(CultureInfo.InvariantCulture));
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

            return string.IsNullOrWhiteSpace(value) ? "HoaDon" : value;
        }

        private static decimal GetDecimal(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && decimal.TryParse(row[column]?.ToString(), out decimal value) ? value : 0;
        }

        private static int? GetNullableInt(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) &&
                   row[column] != DBNull.Value &&
                   int.TryParse(row[column]?.ToString(), out int value)
                ? value
                : null;
        }

        private static string TienPhongSql(string startExpr, string plannedEndExpr, string actualEndExpr)
        {
            string giaNgayExpr = "ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0)";
            return @"CAST(CASE
    WHEN " + plannedEndExpr + @" IS NULL OR DATEDIFF(minute, " + startExpr + @", " + plannedEndExpr + @") <= 0 THEN " + giaNgayExpr + @"
    WHEN CAST(" + startExpr + @" AS date) = CAST(" + plannedEndExpr + @" AS date) THEN CEILING(DATEDIFF(minute, " + startExpr + @", " + plannedEndExpr + @") / 60.0) * ISNULL(LP.DonGiaGio, 0)
    WHEN DATEDIFF(hour, " + startExpr + @", " + plannedEndExpr + @") <= 12 THEN " + giaNgayExpr + @"
    ELSE CASE WHEN DATEDIFF(day, CAST(" + startExpr + @" AS date), CAST(" + plannedEndExpr + @" AS date)) <= 0 THEN 1
              ELSE DATEDIFF(day, CAST(" + startExpr + @" AS date), CAST(" + plannedEndExpr + @" AS date))
         END * " + giaNgayExpr + @"
END AS decimal(18, 2))";
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

        private static string TenPhongSql(string alias)
        {
            if (ColumnExists("PHONG", "TenPhong"))
            {
                return alias + ".TenPhong";
            }

            if (ColumnExists("PHONG", "SoPhong"))
            {
                return alias + ".SoPhong";
            }

            if (ColumnExists("PHONG", "MaSoPhong"))
            {
                return alias + ".MaSoPhong";
            }

            return "N'P' + CAST(" + alias + ".MaPhong AS nvarchar(20))";
        }
    }

    public class RoomPrintGroup
    {
        public int? MaPhong { get; init; }
        public string SoPhong { get; init; } = string.Empty;
        public decimal TienPhong { get; init; }
        public ObservableCollection<PrintLineItem> Items { get; } = new();
        public decimal TongPhong => Items.Sum(item => item.ThanhTien);
        public string TongPhongText => TongPhong.ToString("N0", CultureInfo.InvariantCulture);
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
