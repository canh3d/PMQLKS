using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QLKS_AnPhu.DAL;

namespace QLKS_AnPhu.View
{
    public partial class BaoCao : UserControl
    {
        private readonly ObservableCollection<BaoCaoChiTietItem> danhSachBaoCao = new();
        private readonly ObservableCollection<BaoCaoChiTietItem> danhSachHienThi = new();
        private readonly CultureInfo vietnameseCulture = new("vi-VN");

        public BaoCao()
        {
            InitializeComponent();
            SearchSuggestionService.Attach(TxtTimKiem, TaoGoiYTimKiem, _ => LocBaoCao());
            DgBaoCao.ItemsSource = danhSachHienThi;
            Loaded += BaoCao_Loaded;
        }

        private void BaoCao_Loaded(object sender, RoutedEventArgs e)
        {
            DateTime today = DateTime.Today;
            DpTuNgay.SelectedDate = new DateTime(today.Year, today.Month, 1);
            DpDenNgay.SelectedDate = today;
            TaiBaoCao();
        }

        private void BtnXemBaoCao_Click(object sender, RoutedEventArgs e)
        {
            TaiBaoCao();
        }

        private void TxtTimKiem_TextChanged(object sender, TextChangedEventArgs e)
        {
            LocBaoCao();
        }

        private IEnumerable<SearchSuggestionItem> TaoGoiYTimKiem()
        {
            foreach (BaoCaoChiTietItem item in danhSachBaoCao)
            {
                if (!string.IsNullOrWhiteSpace(item.MaBaoCao))
                {
                    yield return new SearchSuggestionItem(item.MaBaoCao, $"{item.MaBaoCao} - {item.NoiDung}");
                }

                if (!string.IsNullOrWhiteSpace(item.NoiDung))
                {
                    yield return new SearchSuggestionItem(item.NoiDung, $"{item.NoiDung} - {item.Loai}");
                }

                if (!string.IsNullOrWhiteSpace(item.GhiChu))
                {
                    yield return new SearchSuggestionItem(item.GhiChu, $"{item.GhiChu} - {item.NoiDung}");
                }
            }
        }

        private void TaiBaoCao()
        {
            if (!TryGetDateRange(out DateTime tuNgay, out DateTime denNgay))
            {
                return;
            }

            try
            {
                ConfigureBaoCaoColumns();
                LoadKpi(tuNgay, denNgay);
                LoadDoanhThuTheoThang(tuNgay.Year);
                LoadTrangThaiPhong();
                LoadBaoCaoChiTiet(tuNgay, denNgay, GetLoaiBaoCao());
                LocBaoCao();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không tải được dữ liệu báo cáo: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryGetDateRange(out DateTime tuNgay, out DateTime denNgay)
        {
            tuNgay = DpTuNgay.SelectedDate?.Date ?? DateTime.Today;
            denNgay = DpDenNgay.SelectedDate?.Date ?? DateTime.Today;

            if (denNgay < tuNgay)
            {
                MessageBox.Show("Đến ngày phải lớn hơn hoặc bằng từ ngày.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private string GetLoaiBaoCao()
        {
            return CboLoaiBaoCao.SelectedIndex switch
            {
                1 => "HoaDon",
                2 => "Phong",
                3 => "DichVu",
                _ => "DoanhThuTheoNgay"
            };
        }

        private void CboLoaiBaoCao_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabBaoCao != null && TabBaoCao.SelectedIndex != CboLoaiBaoCao.SelectedIndex)
            {
                TabBaoCao.SelectedIndex = CboLoaiBaoCao.SelectedIndex;
            }

            if (IsLoaded)
            {
                TaiBaoCao();
            }
        }

        private void TabBaoCao_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != TabBaoCao)
            {
                return;
            }

            if (CboLoaiBaoCao != null && CboLoaiBaoCao.SelectedIndex != TabBaoCao.SelectedIndex)
            {
                CboLoaiBaoCao.SelectedIndex = TabBaoCao.SelectedIndex;
            }

            if (IsLoaded)
            {
                TaiBaoCao();
            }
        }

        private void LoadKpi(DateTime tuNgay, DateTime denNgay)
        {
            DateTime denNgayExclusive = denNgay.AddDays(1);

            decimal tongDoanhThu = ToDecimal(ConnectDB.ExecuteScalar(
                @"
SELECT ISNULL(SUM(TongThanhToan), 0)
FROM HOADON
WHERE NgayLap >= @TuNgay
  AND NgayLap < @DenNgay
  AND (TrangThai = N'Đã thanh toán' OR DaThanhToan > 0);",
                new SqlParameter("@TuNgay", tuNgay),
                new SqlParameter("@DenNgay", denNgayExclusive)));

            int tongPhongDat = ToInt(ConnectDB.ExecuteScalar(
                @"
SELECT COUNT(*)
FROM DATPHONG
WHERE NgayDat >= @TuNgay
  AND NgayDat < @DenNgay;",
                new SqlParameter("@TuNgay", tuNgay),
                new SqlParameter("@DenNgay", denNgayExclusive)));

            int tongKhachHang = ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM KHACHHANG"));

            int tongPhong = ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM PHONG"));
            int phongLapDay = ToInt(ConnectDB.ExecuteScalar(
                "SELECT COUNT(*) FROM PHONG WHERE TrangThai IN (N'Có khách', N'Đã đặt')"));
            decimal tyLeLapDay = tongPhong == 0 ? 0 : Math.Round(phongLapDay * 100m / tongPhong, 1);

            TxtTongDoanhThu.Text = tongDoanhThu.ToString("N0", vietnameseCulture) + " đ";
            TxtTongPhongDat.Text = tongPhongDat.ToString("N0", vietnameseCulture);
            TxtTongKhachHang.Text = tongKhachHang.ToString("N0", vietnameseCulture);
            TxtTyLeLapDay.Text = tyLeLapDay.ToString("N1", vietnameseCulture) + "%";
            TxtTyLeLapDayChart.Text = TxtTyLeLapDay.Text;
            PbTyLeLapDay.Value = (double)Math.Min(100, tyLeLapDay);

            DateTime previousStart = tuNgay.AddDays(-(denNgay - tuNgay).Days - 1);
            DateTime previousEnd = tuNgay;
            decimal previousRevenue = ToDecimal(ConnectDB.ExecuteScalar(
                @"
SELECT ISNULL(SUM(TongThanhToan), 0)
FROM HOADON
WHERE NgayLap >= @TuNgay
  AND NgayLap < @DenNgay
  AND (TrangThai = N'Đã thanh toán' OR DaThanhToan > 0);",
                new SqlParameter("@TuNgay", previousStart),
                new SqlParameter("@DenNgay", previousEnd)));

            if (previousRevenue <= 0)
            {
                TxtDoanhThuKyTruoc.Text = "Chưa có dữ liệu kỳ trước";
                TxtDoanhThuKyTruoc.Foreground = Brushes.DimGray;
            }
            else
            {
                decimal percent = Math.Round((tongDoanhThu - previousRevenue) * 100m / previousRevenue, 1);
                TxtDoanhThuKyTruoc.Text = (percent >= 0 ? "+" : string.Empty) + percent.ToString("N1", vietnameseCulture) + "% so với kỳ trước";
                TxtDoanhThuKyTruoc.Foreground = percent >= 0 ? Brushes.ForestGreen : Brushes.Firebrick;
            }
        }

        private void LoadDoanhThuTheoThang(int year)
        {
            DataTable table = ConnectDB.GetData(
                @"
SELECT MONTH(NgayLap) AS Thang, ISNULL(SUM(TongThanhToan), 0) AS DoanhThu
FROM HOADON
WHERE YEAR(NgayLap) = @Nam
  AND (TrangThai = N'Đã thanh toán' OR DaThanhToan > 0)
GROUP BY MONTH(NgayLap);",
                new SqlParameter("@Nam", year));

            Dictionary<int, decimal> revenueByMonth = table.Rows
                .Cast<DataRow>()
                .ToDictionary(row => Convert.ToInt32(row["Thang"]), row => Convert.ToDecimal(row["DoanhThu"]));

            decimal maxRevenue = revenueByMonth.Count == 0 ? 0 : revenueByMonth.Values.Max();
            List<DoanhThuThangItem> items = new();

            for (int month = 1; month <= 12; month++)
            {
                decimal revenue = revenueByMonth.TryGetValue(month, out decimal value) ? value : 0;
                double height = maxRevenue <= 0 ? 8 : Math.Max(8, (double)(revenue / maxRevenue) * 230);

                items.Add(new DoanhThuThangItem
                {
                    Label = "T" + month,
                    BarHeight = height,
                    Color = month % 3 == 0 ? Brushes.DeepSkyBlue : month % 2 == 0 ? Brushes.RoyalBlue : Brushes.CornflowerBlue,
                    ToolTip = $"Tháng {month}/{year}: {revenue.ToString("N0", vietnameseCulture)} đ"
                });
            }

            IcDoanhThuThang.ItemsSource = items;
            IcLabelThang.ItemsSource = items;
        }

        private void LoadTrangThaiPhong()
        {
            int coKhach = ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM PHONG WHERE TrangThai IN (N'Có khách', N'Đã đặt')"));
            int phongTrong = ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM PHONG WHERE TrangThai = N'Phòng trống'"));
            int baoTri = ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM PHONG WHERE TrangThai NOT IN (N'Có khách', N'Đã đặt', N'Phòng trống')"));

            TxtPhongCoKhach.Text = coKhach.ToString("N0", vietnameseCulture);
            TxtPhongTrong.Text = phongTrong.ToString("N0", vietnameseCulture);
            TxtPhongBaoTri.Text = baoTri.ToString("N0", vietnameseCulture);
        }

        private void LoadBaoCaoChiTiet(DateTime tuNgay, DateTime denNgay, string loaiBaoCao)
        {
            DateTime denNgayExclusive = denNgay.AddDays(1);
            danhSachBaoCao.Clear();

            DataTable table = loaiBaoCao switch
            {
                "HoaDon" => LoadBaoCaoHoaDon(tuNgay, denNgayExclusive),
                "Phong" => LoadBaoCaoPhong(),
                "DichVu" => LoadBaoCaoDichVu(tuNgay, denNgayExclusive),
                _ => LoadBaoCaoDoanhThuTheoNgay(tuNgay, denNgayExclusive)
            };

            foreach (DataRow row in table.Rows)
            {
                danhSachBaoCao.Add(BaoCaoChiTietItem.FromDataRow(row));
            }
        }

        private static DataTable LoadBaoCaoDoanhThuTheoNgay(DateTime tuNgay, DateTime denNgayExclusive)
        {
            return ConnectDB.GetData(
                @"
SELECT
    CONVERT(nvarchar(10), CAST(hd.NgayLap AS date), 112) AS MaBaoCao,
    CAST(hd.NgayLap AS date) AS Ngay,
    N'Doanh thu theo ngày' AS NoiDung,
    N'Doanh thu' AS Loai,
    COUNT(*) AS SoLuong,
    ISNULL(SUM(hd.TongThanhToan), 0) AS DoanhThu,
    CAST(SUM(CASE WHEN hd.TrangThai = N'Đã thanh toán' OR hd.DaThanhToan > 0 THEN 1 ELSE 0 END) AS nvarchar(20)) AS GhiChu
FROM HOADON hd
WHERE hd.NgayLap >= @TuNgay
  AND hd.NgayLap < @DenNgay
GROUP BY CAST(hd.NgayLap AS date)
ORDER BY CAST(hd.NgayLap AS date) DESC;",
                new SqlParameter("@TuNgay", tuNgay),
                new SqlParameter("@DenNgay", denNgayExclusive));
        }

        private static DataTable LoadBaoCaoHoaDon(DateTime tuNgay, DateTime denNgayExclusive)
        {
            return ConnectDB.GetData(
                @"
SELECT
    CONCAT(N'HD', FORMAT(hd.MaHD, '000')) AS MaBaoCao,
    hd.NgayLap AS Ngay,
    CONCAT(N'Hóa đơn thuê phòng #', hd.MaThue) AS NoiDung,
    N'Doanh thu' AS Loai,
    1 AS SoLuong,
    hd.TongThanhToan AS DoanhThu,
    ISNULL(hd.TrangThai, N'') AS GhiChu
FROM HOADON hd
WHERE hd.NgayLap >= @TuNgay
  AND hd.NgayLap < @DenNgay
ORDER BY hd.NgayLap DESC;",
                new SqlParameter("@TuNgay", tuNgay),
                new SqlParameter("@DenNgay", denNgayExclusive));
        }

        private static DataTable LoadBaoCaoPhong()
        {
            string tenPhongExpr = TenPhongSql("p");
            return ConnectDB.GetData(
                @"
SELECT
    CONCAT(N'P', FORMAT(p.MaPhong, '000')) AS MaBaoCao,
    GETDATE() AS Ngay,
    CONCAT(N'Phòng ', " + tenPhongExpr + @", N' - ', lp.TenLoaiPhong) AS NoiDung,
    N'Trạng thái phòng' AS Loai,
    1 AS SoLuong,
    CAST(0 AS decimal(18, 2)) AS DoanhThu,
    p.TrangThai AS GhiChu
FROM PHONG p
LEFT JOIN LOAIPHONG lp ON lp.MaLoaiPhong = p.MaLoaiPhong
ORDER BY p.MaPhong;");
        }

        private static DataTable LoadBaoCaoKhachHang(DateTime tuNgay, DateTime denNgayExclusive)
        {
            return ConnectDB.GetData(
                @"
SELECT
    CONCAT(N'KH', FORMAT(kh.MaKH, '000')) AS MaBaoCao,
    ISNULL(MAX(dp.NgayDat), GETDATE()) AS Ngay,
    kh.HoTen AS NoiDung,
    kh.LoaiKhach AS Loai,
    COUNT(dp.MaDatPhong) AS SoLuong,
    ISNULL(SUM(hd.TongThanhToan), 0) AS DoanhThu,
    ISNULL(kh.SDT, N'') AS GhiChu
FROM KHACHHANG kh
LEFT JOIN DATPHONG dp
    ON dp.MaKH = kh.MaKH
    AND dp.NgayDat >= @TuNgay
    AND dp.NgayDat < @DenNgay
LEFT JOIN HOADON hd
    ON hd.MaKH = kh.MaKH
    AND hd.NgayLap >= @TuNgay
    AND hd.NgayLap < @DenNgay
GROUP BY kh.MaKH, kh.HoTen, kh.LoaiKhach, kh.SDT
ORDER BY DoanhThu DESC, kh.HoTen;",
                new SqlParameter("@TuNgay", tuNgay),
                new SqlParameter("@DenNgay", denNgayExclusive));
        }

        private static DataTable LoadBaoCaoDichVu(DateTime tuNgay, DateTime denNgayExclusive)
        {
            return ConnectDB.GetData(
                @"
SELECT
    CONCAT(N'DV', FORMAT(dv.MaDVVT, '000')) AS MaBaoCao,
    ISNULL(MAX(ct.ThoiDiemSuDung), GETDATE()) AS Ngay,
    dv.TenDVVT AS NoiDung,
    dv.Loai AS Loai,
    ISNULL(SUM(ct.SoLuong), 0) AS SoLuong,
    ISNULL(SUM(ct.ThanhTien), 0) AS DoanhThu,
    dv.DonViTinh AS GhiChu
FROM DICHVUVATTU dv
LEFT JOIN CHITIETPHATSINH ct
    ON ct.MaDVVT = dv.MaDVVT
    AND ct.ThoiDiemSuDung >= @TuNgay
    AND ct.ThoiDiemSuDung < @DenNgay
GROUP BY dv.MaDVVT, dv.TenDVVT, dv.Loai, dv.DonViTinh
ORDER BY DoanhThu DESC, dv.TenDVVT;",
                new SqlParameter("@TuNgay", tuNgay),
                new SqlParameter("@DenNgay", denNgayExclusive));
        }

        private void LocBaoCao()
        {
            string keyword = TxtTimKiem.Text.Trim();
            danhSachHienThi.Clear();

            IEnumerable<BaoCaoChiTietItem> source = danhSachBaoCao;
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                source = source.Where(item =>
                    item.MaBaoCao.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.NoiDung.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.Loai.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.GhiChu.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            foreach (BaoCaoChiTietItem item in source)
            {
                danhSachHienThi.Add(item);
            }

            UpdateKyBaoCaoText();
        }

        private void UpdateKyBaoCaoText()
        {
            if (TxtKyBaoCao == null)
            {
                return;
            }

            string tuNgay = DpTuNgay.SelectedDate?.ToString("dd/MM/yyyy") ?? "--/--/----";
            string denNgay = DpDenNgay.SelectedDate?.ToString("dd/MM/yyyy") ?? "--/--/----";
            TxtKyBaoCao.Text = $"Kỳ báo cáo: {tuNgay} - {denNgay}  /  {danhSachHienThi.Count:N0} dòng";
        }

        private void ConfigureBaoCaoColumns()
        {
            if (DgBaoCao == null)
            {
                return;
            }

            DgBaoCao.Columns.Clear();
            switch (CboLoaiBaoCao.SelectedIndex)
            {
                case 1:
                    AddColumn("Mã hóa đơn", "MaBaoCao", 120);
                    AddColumn("Ngày lập", "Ngay", 130);
                    AddColumn("Nội dung", "NoiDung", new DataGridLength(1, DataGridLengthUnitType.Star));
                    AddColumn("Trạng thái", "GhiChu", 160);
                    AddColumn("Doanh thu", "DoanhThu", 150);
                    break;
                case 2:
                    AddColumn("Mã phòng", "MaBaoCao", 120);
                    AddColumn("Thông tin phòng", "NoiDung", new DataGridLength(1, DataGridLengthUnitType.Star));
                    AddColumn("Trạng thái", "GhiChu", 180);
                    break;
                case 3:
                    AddColumn("Mã DV", "MaBaoCao", 120);
                    AddColumn("Tên dịch vụ - vật tư", "NoiDung", new DataGridLength(1, DataGridLengthUnitType.Star));
                    AddColumn("Loại", "Loai", 150);
                    AddColumn("Số lượng", "SoLuong", 110);
                    AddColumn("Doanh thu", "DoanhThu", 150);
                    AddColumn("Đơn vị", "GhiChu", 120);
                    break;
                default:
                    AddColumn("Ngày", "Ngay", 180);
                    AddColumn("Số hóa đơn", "SoLuong", 180);
                    AddColumn("Đã thanh toán", "GhiChu", 180);
                    AddColumn("Doanh thu", "DoanhThu", new DataGridLength(1, DataGridLengthUnitType.Star));
                    break;
            }
        }

        private void AddColumn(string header, string bindingPath, double width)
        {
            AddColumn(header, bindingPath, new DataGridLength(width));
        }

        private void AddColumn(string header, string bindingPath, DataGridLength width)
        {
            DgBaoCao.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(bindingPath),
                Width = width
            });
        }

        private void BtnXuatExcel_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new()
            {
                Title = "Xuất báo cáo",
                Filter = "Excel CSV (*.csv)|*.csv",
                FileName = $"BaoCaoThongKe_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                StringBuilder builder = new();
                builder.AppendLine("Ma bao cao,Ngay phat sinh,Noi dung,Loai,So luong,Doanh thu,Ghi chu");
                foreach (BaoCaoChiTietItem item in danhSachHienThi)
                {
                    builder.AppendLine(string.Join(",",
                        Csv(item.MaBaoCao),
                        Csv(item.Ngay),
                        Csv(item.NoiDung),
                        Csv(item.Loai),
                        Csv(item.SoLuong.ToString("N0", vietnameseCulture)),
                        Csv(item.DoanhThu),
                        Csv(item.GhiChu)));
                }

                File.WriteAllText(saveFileDialog.FileName, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                MessageBox.Show("Xuất Excel thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không xuất được Excel: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnInBaoCao_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PrintDialog printDialog = new();
                if (printDialog.ShowDialog() != true)
                {
                    return;
                }

                FlowDocument document = CreatePrintDocument();
                document.PageWidth = printDialog.PrintableAreaWidth;
                document.PageHeight = printDialog.PrintableAreaHeight;
                document.PagePadding = new Thickness(36);
                document.ColumnWidth = printDialog.PrintableAreaWidth;
                printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Báo cáo thống kê khách sạn");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không in được báo cáo: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FlowDocument CreatePrintDocument()
        {
            FlowDocument document = new()
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12
            };

            document.Blocks.Add(new Paragraph(new Run("BÁO CÁO - THỐNG KÊ KHÁCH SẠN"))
            {
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });

            document.Blocks.Add(new Paragraph(new Run($"{DpTuNgay.SelectedDate:dd/MM/yyyy} - {DpDenNgay.SelectedDate:dd/MM/yyyy} | {GetLoaiBaoCao()}"))
            {
                TextAlignment = TextAlignment.Center,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 0, 0, 16)
            });

            Table table = new();
            for (int i = 0; i < 6; i++)
            {
                table.Columns.Add(new TableColumn());
            }

            TableRowGroup headerGroup = new();
            TableRow header = new();
            foreach (string column in new[] { "Mã", "Ngày", "Nội dung", "Loại", "SL", "Doanh thu" })
            {
                header.Cells.Add(new TableCell(new Paragraph(new Run(column)))
                {
                    FontWeight = FontWeights.Bold,
                    Background = Brushes.LightGray,
                    Padding = new Thickness(4),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(0.5)
                });
            }
            headerGroup.Rows.Add(header);
            table.RowGroups.Add(headerGroup);

            TableRowGroup bodyGroup = new();
            foreach (BaoCaoChiTietItem item in danhSachHienThi)
            {
                TableRow row = new();
                foreach (string value in new[] { item.MaBaoCao, item.Ngay, item.NoiDung, item.Loai, item.SoLuong.ToString("N0", vietnameseCulture), item.DoanhThu })
                {
                    row.Cells.Add(new TableCell(new Paragraph(new Run(value)))
                    {
                        Padding = new Thickness(4),
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Thickness(0.5)
                    });
                }
                bodyGroup.Rows.Add(row);
            }

            table.RowGroups.Add(bodyGroup);
            document.Blocks.Add(table);
            return document;
        }

        private static decimal ToDecimal(object? value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static int ToInt(object? value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static string TenPhongSql(string alias)
        {
            return ViewSchemaHelper.TenPhongSql(alias);
        }

        private static bool ColumnExists(string tableName, string columnName)
        {
            return ViewSchemaHelper.ColumnExists(tableName, columnName);
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private sealed class DoanhThuThangItem
        {
            public string Label { get; init; } = string.Empty;
            public double BarHeight { get; init; }
            public Brush Color { get; init; } = Brushes.RoyalBlue;
            public string ToolTip { get; init; } = string.Empty;
        }

        private sealed class BaoCaoChiTietItem
        {
            public string MaBaoCao { get; init; } = string.Empty;
            public string Ngay { get; init; } = string.Empty;
            public string NoiDung { get; init; } = string.Empty;
            public string Loai { get; init; } = string.Empty;
            public int SoLuong { get; init; }
            public string DoanhThu { get; init; } = string.Empty;
            public string GhiChu { get; init; } = string.Empty;

            public static BaoCaoChiTietItem FromDataRow(DataRow row)
            {
                CultureInfo culture = new("vi-VN");
                decimal doanhThu = row["DoanhThu"] == DBNull.Value ? 0 : Convert.ToDecimal(row["DoanhThu"]);
                DateTime ngay = row["Ngay"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(row["Ngay"]);

                return new BaoCaoChiTietItem
                {
                    MaBaoCao = row["MaBaoCao"].ToString() ?? string.Empty,
                    Ngay = ngay.ToString("dd/MM/yyyy"),
                    NoiDung = row["NoiDung"].ToString() ?? string.Empty,
                    Loai = row["Loai"].ToString() ?? string.Empty,
                    SoLuong = row["SoLuong"] == DBNull.Value ? 0 : Convert.ToInt32(row["SoLuong"]),
                    DoanhThu = doanhThu.ToString("N0", culture) + " đ",
                    GhiChu = row["GhiChu"].ToString() ?? string.Empty
                };
            }
        }
    }
}
