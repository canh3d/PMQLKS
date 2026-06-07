using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MahApps.Metro.IconPacks;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QLKS_AnPhu.DAL;

namespace QLKS_AnPhu.View
{
    public partial class TrangChu : UserControl
    {
        private readonly CultureInfo vietnameseCulture = new("vi-VN");
        private readonly ObservableCollection<DashboardNotice> thongBao = new();
        private readonly ObservableCollection<RecentCustomerItem> khachGanDay = new();

        public TrangChu()
        {
            InitializeComponent();
            ItemsThongBao.ItemsSource = thongBao;
            DgKhachHangGanDay.ItemsSource = khachGanDay;
            Loaded += TrangChu_Loaded;
        }

        private void TrangChu_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDashboardData();
        }

        private void LoadDashboardData()
        {
            try
            {
                DateTime today = DateTime.Today;
                DateTime tomorrow = today.AddDays(1);
                DateTime yesterday = today.AddDays(-1);

                decimal doanhThuHomNay = LayDoanhThu(today, tomorrow);
                decimal doanhThuHomQua = LayDoanhThu(yesterday, today);
                int tongPhong = TableExists("PHONG") ? ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM dbo.PHONG")) : 0;
                int phongDangThue = TableExists("PHONG")
                    ? ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM dbo.PHONG WHERE TrangThai IN (N'Có khách', N'Co khach', N'Đang thuê', N'Dang thue', N'Đã đặt', N'Da dat')"))
                    : 0;
                int phongTrong = TableExists("PHONG")
                    ? ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM dbo.PHONG WHERE TrangThai IN (N'Phòng trống', N'Phong trong', N'Trống', N'Trong')"))
                    : 0;
                int khachHang = TableExists("KHACHHANG") ? ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM dbo.KHACHHANG")) : 0;
                int khachMoiHomNay = LayKhachMoiHomNay(today, tomorrow);
                int datPhongMoiHomNay = LayDatPhongMoiHomNay(today, tomorrow);
                decimal tyLeLapDay = tongPhong == 0 ? 0 : Math.Round(phongDangThue * 100m / tongPhong, 1);
                decimal doanhThuDelta = doanhThuHomQua <= 0 ? (doanhThuHomNay > 0 ? 100 : 0) : Math.Round((doanhThuHomNay - doanhThuHomQua) * 100m / doanhThuHomQua, 1);

                TxtWelcome.Text = "Chào mừng trở lại, Admin!";
                TxtDoanhThuHomNay.Text = doanhThuHomNay.ToString("N0", vietnameseCulture) + " đ";
                TxtDoanhThuDelta.Text = FormatDelta(doanhThuDelta, "% so với hôm qua");
                TxtDoanhThuDelta.Foreground = doanhThuDelta >= 0 ? (Brush)FindResource("SuccessBrush") : (Brush)FindResource("DangerBrush");
                TxtPhongDangThue.Text = phongDangThue.ToString("N0", vietnameseCulture);
                TxtPhongDangThueDelta.Text = "+" + datPhongMoiHomNay.ToString("N0", vietnameseCulture) + " đặt phòng mới";
                TxtPhongTrong.Text = phongTrong.ToString("N0", vietnameseCulture);
                TxtPhongTrongDelta.Text = tongPhong > 0 ? (tongPhong - phongTrong).ToString("N0", vietnameseCulture) + " phòng đang bận" : "Chưa có dữ liệu phòng";
                TxtKhachHang.Text = khachHang.ToString("N0", vietnameseCulture);
                TxtKhachHangDelta.Text = "+" + khachMoiHomNay.ToString("N0", vietnameseCulture) + " mới hôm nay";
                TxtTyLeLapDay.Text = tyLeLapDay.ToString("N1", vietnameseCulture) + "%";

                LoadThongBao(datPhongMoiHomNay, phongDangThue);
                LoadKhachGanDay();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không tải được dữ liệu trang chủ.\nChi tiết: " + ex.Message, "Lỗi dữ liệu", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private decimal LayDoanhThu(DateTime tuNgay, DateTime denNgay)
        {
            if (!TableExists("HOADON") || !ColumnExists("HOADON", "NgayLap"))
            {
                return 0;
            }

            string amountColumn = ColumnExists("HOADON", "TongThanhToan") ? "TongThanhToan" :
                ColumnExists("HOADON", "TongTien") ? "TongTien" : string.Empty;
            if (string.IsNullOrWhiteSpace(amountColumn))
            {
                return 0;
            }

            string paymentFilter = string.Empty;
            if (ColumnExists("HOADON", "DaThanhToan"))
            {
                paymentFilter = " AND (DaThanhToan > 0 OR TrangThai IN (N'Đã thanh toán', N'Da thanh toan'))";
            }
            else if (ColumnExists("HOADON", "TrangThai"))
            {
                paymentFilter = " AND TrangThai IN (N'Đã thanh toán', N'Da thanh toan')";
            }

            return ToDecimal(ConnectDB.ExecuteScalar(
                "SELECT ISNULL(SUM(" + amountColumn + @"), 0)
FROM dbo.HOADON
WHERE NgayLap >= @TuNgay AND NgayLap < @DenNgay" + paymentFilter,
                new SqlParameter("@TuNgay", tuNgay),
                new SqlParameter("@DenNgay", denNgay)));
        }

        private int LayKhachMoiHomNay(DateTime tuNgay, DateTime denNgay)
        {
            if (!TableExists("KHACHHANG"))
            {
                return 0;
            }

            string? dateColumn = ColumnExists("KHACHHANG", "NgayTao") ? "NgayTao" :
                ColumnExists("KHACHHANG", "CreatedAt") ? "CreatedAt" : null;
            if (dateColumn == null)
            {
                return 0;
            }

            return ToInt(ConnectDB.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo.KHACHHANG WHERE " + dateColumn + " >= @TuNgay AND " + dateColumn + " < @DenNgay",
                new SqlParameter("@TuNgay", tuNgay),
                new SqlParameter("@DenNgay", denNgay)));
        }

        private int LayDatPhongMoiHomNay(DateTime tuNgay, DateTime denNgay)
        {
            string table = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists("DATPHONG") ? "DATPHONG" : string.Empty;
            if (string.IsNullOrWhiteSpace(table))
            {
                return 0;
            }

            string? dateColumn = ColumnExists(table, "NgayDat") ? "NgayDat" :
                ColumnExists(table, "NgayLap") ? "NgayLap" :
                ColumnExists(table, "NgayNhanPhong") ? "NgayNhanPhong" : null;
            if (dateColumn == null)
            {
                return 0;
            }

            return ToInt(ConnectDB.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo." + table + " WHERE " + dateColumn + " >= @TuNgay AND " + dateColumn + " < @DenNgay",
                new SqlParameter("@TuNgay", tuNgay),
                new SqlParameter("@DenNgay", denNgay)));
        }

        private void LoadThongBao(int datPhongMoiHomNay, int phongDangThue)
        {
            thongBao.Clear();

            string phongCheckout = LayPhongCheckoutHomNay();
            thongBao.Add(new DashboardNotice(
                string.IsNullOrWhiteSpace(phongCheckout) ? "Không có phòng check-out trong hôm nay" : "Phòng " + phongCheckout + " sẽ check-out hôm nay",
                DateTime.Now.ToString("HH:mm"),
                PackIconMaterialKind.Bell,
                "#E0F2FE",
                "#0284C7"));

            thongBao.Add(new DashboardNotice(
                "Có " + datPhongMoiHomNay.ToString("N0", vietnameseCulture) + " đặt phòng mới hôm nay",
                DateTime.Now.AddMinutes(-20).ToString("HH:mm"),
                PackIconMaterialKind.AccountPlus,
                "#DCFCE7",
                "#16A34A"));

            thongBao.Add(new DashboardNotice(
                phongDangThue.ToString("N0", vietnameseCulture) + " phòng đang có khách hoặc đã đặt",
                DateTime.Now.AddMinutes(-45).ToString("HH:mm"),
                PackIconMaterialKind.AlertOutline,
                "#FEF3C7",
                "#D97706"));
        }

        private string LayPhongCheckoutHomNay()
        {
            if (!TableExists("PHIEUTHUE") || !TableExists("PHONG") || !ColumnExists("PHIEUTHUE", "NgayTraDuKien"))
            {
                return string.Empty;
            }

            string tenPhong = TenPhongSql("P");
            DataTable data = ConnectDB.GetData(@"
SELECT TOP 1 " + tenPhong + @" AS SoPhong
FROM dbo.PHIEUTHUE PT
JOIN dbo.PHONG P ON PT.MaPhong = P.MaPhong
WHERE CAST(PT.NgayTraDuKien AS date) = CAST(GETDATE() AS date)
ORDER BY PT.NgayTraDuKien");
            return data.Rows.Count == 0 ? string.Empty : data.Rows[0]["SoPhong"]?.ToString() ?? string.Empty;
        }

        private void LoadKhachGanDay()
        {
            khachGanDay.Clear();

            DataTable data = LayKhachGanDayTuPhieu();
            if (data.Rows.Count == 0)
            {
                data = LayKhachGanDayTuKhachHang();
            }

            foreach (DataRow row in data.Rows)
            {
                khachGanDay.Add(new RecentCustomerItem
                {
                    HoTen = GetString(row, "HoTen"),
                    Phong = GetString(row, "Phong"),
                    TrangThai = GetString(row, "TrangThai"),
                    Nguon = GetString(row, "Nguon")
                });
            }
        }

        private DataTable LayKhachGanDayTuPhieu()
        {
            if (!TableExists("PHIEUTHUE") || !TableExists("KHACHHANG") || !TableExists("PHONG"))
            {
                return new DataTable();
            }

            string tenPhong = TenPhongSql("P");
            string orderColumn = ColumnExists("PHIEUTHUE", "NgayNhan") ? "PT.NgayNhan" : "PT.MaThue";
            return ConnectDB.GetData(@"
SELECT TOP 5
       KH.HoTen AS HoTen,
       " + tenPhong + @" AS Phong,
       ISNULL(PT.TrangThai, N'Đang thuê') AS TrangThai,
       N'Trực tiếp' AS Nguon
FROM dbo.PHIEUTHUE PT
JOIN dbo.KHACHHANG KH ON PT.MaKH = KH.MaKH
JOIN dbo.PHONG P ON PT.MaPhong = P.MaPhong
ORDER BY " + orderColumn + " DESC");
        }

        private DataTable LayKhachGanDayTuKhachHang()
        {
            if (!TableExists("KHACHHANG"))
            {
                return new DataTable();
            }

            string orderColumn = ColumnExists("KHACHHANG", "MaKH") ? "MaKH" : "HoTen";
            return ConnectDB.GetData(@"
SELECT TOP 5
       HoTen AS HoTen,
       N'--' AS Phong,
       ISNULL(TrangThai, N'Hoạt động') AS TrangThai,
       N'Hồ sơ khách' AS Nguon
FROM dbo.KHACHHANG
ORDER BY " + orderColumn + " DESC");
        }

        private void BtnDatPhongMoi_Click(object sender, RoutedEventArgs e)
        {
            HostWindow()?.NavigateToQLPhong();
        }

        private void BtnXemBaoCao_Click(object sender, RoutedEventArgs e)
        {
            HostWindow()?.NavigateToBaoCao();
        }

        private void BtnInDanhSachKhach_Click(object sender, RoutedEventArgs e)
        {
            if (khachGanDay.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu khách hàng để in.", "In danh sách", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            PrintDialog dialog = new();
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            FlowDocument document = new()
            {
                PagePadding = new Thickness(40),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12
            };

            document.Blocks.Add(new Paragraph(new Run("DANH SÁCH KHÁCH HÀNG GẦN ĐÂY"))
            {
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 18)
            });

            Table table = new();
            table.Columns.Add(new TableColumn { Width = new GridLength(220) });
            table.Columns.Add(new TableColumn { Width = new GridLength(100) });
            table.Columns.Add(new TableColumn { Width = new GridLength(140) });
            table.Columns.Add(new TableColumn { Width = new GridLength(120) });
            TableRowGroup group = new();
            table.RowGroups.Add(group);
            group.Rows.Add(CreatePrintRow("Họ tên", "Phòng", "Trạng thái", "Đến từ", true));

            foreach (RecentCustomerItem item in khachGanDay)
            {
                group.Rows.Add(CreatePrintRow(item.HoTen, item.Phong, item.TrangThai, item.Nguon, false));
            }

            document.Blocks.Add(table);
            IDocumentPaginatorSource paginator = document;
            dialog.PrintDocument(paginator.DocumentPaginator, "Danh sách khách hàng gần đây");
        }

        private void BtnXuatBaoCao_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                Filter = "Excel CSV (*.csv)|*.csv",
                FileName = "DashboardKhachSan_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            StringBuilder builder = new();
            builder.AppendLine("Bao cao dashboard khach san");
            builder.AppendLine("Thoi gian," + Csv(DateTime.Now.ToString("dd/MM/yyyy HH:mm")));
            builder.AppendLine("Chi so,Gia tri");
            builder.AppendLine("Doanh thu hom nay," + Csv(TxtDoanhThuHomNay.Text));
            builder.AppendLine("Phong dang thue," + Csv(TxtPhongDangThue.Text));
            builder.AppendLine("Phong trong," + Csv(TxtPhongTrong.Text));
            builder.AppendLine("Khach hang," + Csv(TxtKhachHang.Text));
            builder.AppendLine("Ty le lap day," + Csv(TxtTyLeLapDay.Text));
            builder.AppendLine();
            builder.AppendLine("Khach hang gan day,Phong,Trang thai,Nguon");
            foreach (RecentCustomerItem item in khachGanDay)
            {
                builder.AppendLine(Csv(item.HoTen) + "," + Csv(item.Phong) + "," + Csv(item.TrangThai) + "," + Csv(item.Nguon));
            }

            File.WriteAllText(dialog.FileName, "\uFEFF" + builder, Encoding.UTF8);
            MessageBox.Show("Đã xuất báo cáo dashboard.", "Xuất Excel", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnLamMoi_Click(object sender, RoutedEventArgs e)
        {
            LoadDashboardData();
        }

        private void BtnXemTatCaThongBao_Click(object sender, RoutedEventArgs e)
        {
            HostWindow()?.NavigateToPhieuThue();
        }

        private MainWindow? HostWindow()
        {
            return Window.GetWindow(this) as MainWindow;
        }

        private static TableRow CreatePrintRow(string col1, string col2, string col3, string col4, bool header)
        {
            TableRow row = new();
            row.Cells.Add(CreatePrintCell(col1, header));
            row.Cells.Add(CreatePrintCell(col2, header));
            row.Cells.Add(CreatePrintCell(col3, header));
            row.Cells.Add(CreatePrintCell(col4, header));
            return row;
        }

        private static TableCell CreatePrintCell(string text, bool header)
        {
            return new TableCell(new Paragraph(new Run(text)))
            {
                Padding = new Thickness(6),
                FontWeight = header ? FontWeights.Bold : FontWeights.Normal,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
        }

        private string FormatDelta(decimal value, string suffix)
        {
            string prefix = value >= 0 ? "+" : string.Empty;
            return prefix + value.ToString("N1", vietnameseCulture) + suffix;
        }

        private static string GetString(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) ? row[column]?.ToString() ?? string.Empty : string.Empty;
        }

        private static string Csv(string value)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static decimal ToDecimal(object? value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static int ToInt(object? value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static bool TableExists(string tableName)
        {
            return ViewSchemaHelper.TableExists(tableName);
        }

        private static bool ColumnExists(string tableName, string columnName)
        {
            return ViewSchemaHelper.ColumnExists(tableName, columnName);
        }

        private static string TenPhongSql(string alias)
        {
            return ViewSchemaHelper.TenPhongSql(alias);
        }
    }

    public sealed class DashboardNotice
    {
        public DashboardNotice(string noiDung, string thoiGian, PackIconMaterialKind icon, string iconBackground, string iconBrush)
        {
            NoiDung = noiDung;
            ThoiGian = thoiGian;
            Icon = icon;
            IconBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(iconBackground));
            IconBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(iconBrush));
        }

        public string NoiDung { get; }
        public string ThoiGian { get; }
        public PackIconMaterialKind Icon { get; }
        public Brush IconBackground { get; }
        public Brush IconBrush { get; }
    }

    public sealed class RecentCustomerItem
    {
        public string HoTen { get; init; } = string.Empty;
        public string Phong { get; init; } = string.Empty;
        public string TrangThai { get; init; } = string.Empty;
        public string Nguon { get; init; } = string.Empty;
    }
}
