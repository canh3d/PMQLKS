using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QLKS_AnPhu.DAL;
using QLKS_AnPhu.Services;

namespace QLKS_AnPhu.View
{
    public partial class BaoCao : UserControl
    {
        private readonly ObservableCollection<BaoCaoChiTietItem> danhSachBaoCao = new();
        private readonly ObservableCollection<BaoCaoChiTietItem> danhSachHienThi = new();
        private readonly List<DoanhThuThangItem> doanhThuTheoNgayTrongThang = new();
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
                LoadDoanhThuTheoNgayTrongThang(tuNgay);
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
            List<HoaDonItem> hoaDonDongBo = HoaDon.LayHoaDonDongBo(tuNgay, denNgay);
            decimal tongDoanhThu = hoaDonDongBo
                .Where(LaDoanhThuDaThu)
                .Sum(item => item.TongGiaTriHoaDon);

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
                "SELECT COUNT(*) FROM PHONG WHERE TrangThai IN (N'Đang thuê', N'Có khách', N'Đã đặt', N'Dang thue', N'Co khach', N'Da dat')"));
            decimal tyLeLapDay = tongPhong == 0 ? 0 : Math.Round(phongLapDay * 100m / tongPhong, 1);

            TxtTongDoanhThu.Text = tongDoanhThu.ToString("N0", vietnameseCulture) + " đ";
            TxtTongPhongDat.Text = tongPhongDat.ToString("N0", vietnameseCulture);
            TxtTongKhachHang.Text = tongKhachHang.ToString("N0", vietnameseCulture);
            TxtTyLeLapDay.Text = tyLeLapDay.ToString("N1", vietnameseCulture) + "%";
            TxtTyLeLapDayChart.Text = TxtTyLeLapDay.Text;
            PbTyLeLapDay.Value = (double)Math.Min(100, tyLeLapDay);
        }

        private void LoadDoanhThuTheoNgayTrongThang(DateTime ngayTrongThang)
        {
            DateTime dauThang = new(ngayTrongThang.Year, ngayTrongThang.Month, 1);
            DateTime ngay30HoacCuoiThang = dauThang.AddDays(Math.Min(30, DateTime.DaysInMonth(ngayTrongThang.Year, ngayTrongThang.Month)) - 1);
            int soNgay = DateTime.DaysInMonth(ngayTrongThang.Year, ngayTrongThang.Month);

            Dictionary<int, decimal> revenueByDay = HoaDon.LayHoaDonDongBo(dauThang, ngay30HoacCuoiThang)
                .Where(LaDoanhThuDaThu)
                .GroupBy(item => item.NgayLapHoaDon.Day)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.TongGiaTriHoaDon));

            decimal maxRevenue = revenueByDay.Count == 0 ? 0 : revenueByDay.Values.Max();
            doanhThuTheoNgayTrongThang.Clear();

            for (int day = 1; day <= 30; day++)
            {
                decimal revenue = day <= soNgay && revenueByDay.TryGetValue(day, out decimal value) ? value : 0;
                double height = maxRevenue <= 0 ? 8 : Math.Max(8, (double)(revenue / maxRevenue) * 230);

                doanhThuTheoNgayTrongThang.Add(new DoanhThuThangItem
                {
                    Label = day.ToString("00"),
                    Ngay = day,
                    DoanhThu = revenue,
                    BarHeight = height,
                    Color = revenue > 0 ? Brushes.DeepSkyBlue : Brushes.CornflowerBlue,
                    ToolTip = $"Ngày {day:00}/{ngayTrongThang.Month:00}/{ngayTrongThang.Year}: {revenue.ToString("N0", vietnameseCulture)} đ"
                });
            }

            IcDoanhThuThang.ItemsSource = doanhThuTheoNgayTrongThang;
            IcLabelThang.ItemsSource = doanhThuTheoNgayTrongThang;
        }

        private void LoadTrangThaiPhong()
        {
            int coKhach = ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM PHONG WHERE TrangThai IN (N'Đang thuê', N'Có khách', N'Đã đặt', N'Dang thue', N'Co khach', N'Da dat')"));
            string canDonFilter = ColumnExists("PHONG", "GhiChu")
                ? " AND ISNULL(GhiChu, N'') NOT LIKE N'%[CAN_DON_DEP]%'"
                : string.Empty;
            string canDonOrOther = ColumnExists("PHONG", "GhiChu")
                ? " OR ISNULL(GhiChu, N'') LIKE N'%[CAN_DON_DEP]%'"
                : string.Empty;
            int phongTrong = ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM PHONG WHERE TrangThai IN (N'Trống', N'Phòng trống', N'Trong', N'Phong trong')" + canDonFilter));
            int baoTri = ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM PHONG WHERE TrangThai NOT IN (N'Đang thuê', N'Có khách', N'Đã đặt', N'Dang thue', N'Co khach', N'Da dat', N'Trống', N'Phòng trống', N'Trong', N'Phong trong')" + canDonOrOther));

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
            DataTable table = TaoBangBaoCao();
            DateTime denNgay = denNgayExclusive.AddDays(-1);
            IEnumerable<IGrouping<DateTime, HoaDonItem>> groups = HoaDon.LayHoaDonDongBo(tuNgay, denNgay)
                .Where(LaDoanhThuDaThu)
                .GroupBy(item => item.NgayLapHoaDon.Date)
                .OrderByDescending(group => group.Key);

            foreach (IGrouping<DateTime, HoaDonItem> group in groups)
            {
                table.Rows.Add(
                    group.Key.ToString("yyyyMMdd"),
                    group.Key,
                    "Doanh thu theo ngày",
                    "Doanh thu",
                    group.Count(),
                    group.Sum(item => item.TongGiaTriHoaDon),
                    group.Count().ToString(CultureInfo.InvariantCulture));
            }

            return table;
        }

        private static DataTable LoadBaoCaoHoaDon(DateTime tuNgay, DateTime denNgayExclusive)
        {
            DataTable table = TaoBangBaoCao();
            DateTime denNgay = denNgayExclusive.AddDays(-1);
            foreach (HoaDonItem item in HoaDon.LayHoaDonDongBo(tuNgay, denNgay).OrderByDescending(item => item.NgayLapHoaDon))
            {
                table.Rows.Add(
                    item.MaHoaDon,
                    item.NgayLapHoaDon,
                    item.TenKhachHang + " - phòng " + item.SoPhong,
                    item.LoaiThanhToan == "PHATSINH" ? "Phát sinh" : "Nhận phòng",
                    1,
                    LaDoanhThuDaThu(item) ? item.TongGiaTriHoaDon : 0,
                    item.TrangThai);
            }

            return table;
        }

        private static bool LaDoanhThuDaThu(HoaDonItem item)
        {
            return item.DaThanhToan || item.LaPhieuDatDaHuyGiuCoc;
        }

        private static DataTable TaoBangBaoCao()
        {
            DataTable table = new();
            table.Columns.Add("MaBaoCao", typeof(string));
            table.Columns.Add("Ngay", typeof(DateTime));
            table.Columns.Add("NoiDung", typeof(string));
            table.Columns.Add("Loai", typeof(string));
            table.Columns.Add("SoLuong", typeof(int));
            table.Columns.Add("DoanhThu", typeof(decimal));
            table.Columns.Add("GhiChu", typeof(string));
            return table;
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
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"BaoCaoThongKe_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                ExcelDocument document = new()
                {
                    Title = "BÁO CÁO - THỐNG KÊ KHÁCH SẠN",
                    Subtitle = $"Kỳ báo cáo: {DpTuNgay.SelectedDate:dd/MM/yyyy} - {DpDenNgay.SelectedDate:dd/MM/yyyy} | {GetLoaiBaoCaoText()}",
                    SheetName = "Báo cáo"
                };

                ExcelSection overview = new()
                {
                    Title = "Tổng quan",
                    Headers = ["Tổng doanh thu", "Phòng đã đặt", "Tổng khách hàng", "Tỷ lệ lấp đầy"],
                    ColumnWidths = [22, 18, 18, 18]
                };
                overview.Rows.Add([TxtTongDoanhThu.Text, TxtTongPhongDat.Text, TxtTongKhachHang.Text, TxtTyLeLapDay.Text]);
                document.Sections.Add(overview);

                ExcelSection dailyRevenue = new()
                {
                    Title = "Doanh thu theo ngày trong tháng",
                    Headers = ["Ngày", "Doanh thu"],
                    ColumnWidths = [14, 22],
                    Summary = $"Tổng doanh thu theo biểu đồ: {doanhThuTheoNgayTrongThang.Sum(item => item.DoanhThu):N0} VND"
                };
                dailyRevenue.Rows.AddRange(doanhThuTheoNgayTrongThang.Select(item => (IReadOnlyList<object?>)
                    [item.Label, new ExcelMoney(item.DoanhThu)]));
                document.Sections.Add(dailyRevenue);

                ExcelSection details = new()
                {
                    Title = "Chi tiết báo cáo",
                    Headers = ["Mã", "Ngày", "Nội dung", "Loại", "Số lượng", "Doanh thu", "Ghi chú"],
                    ColumnWidths = [16, 14, 36, 18, 12, 20, 28],
                    Summary = $"Tổng dòng: {danhSachHienThi.Count:N0} | Tổng doanh thu: {TongDoanhThuHienThi():N0} VND"
                };
                details.Rows.AddRange(danhSachHienThi.Select(item => (IReadOnlyList<object?>)
                    [item.MaBaoCao, item.Ngay, item.NoiDung, item.Loai, item.SoLuong, new ExcelMoney(item.DoanhThuValue), item.GhiChu]));
                document.Sections.Add(details);

                ExcelExportService.Export(saveFileDialog.FileName, document);
                MessageBox.Show("Xuất Excel thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không xuất được Excel: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnInBaoCao_Click(object sender, RoutedEventArgs e)
        {
            if (danhSachHienThi.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu báo cáo để in.", "In báo cáo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                FlowDocument document = CreatePrintDocument();
                PrintExportService.Print(document, "Báo cáo thống kê khách sạn");
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

            Table kpiTable = new();
            for (int i = 0; i < 4; i++)
            {
                kpiTable.Columns.Add(new TableColumn());
            }

            TableRowGroup kpiGroup = new();
            TableRow kpiHeader = new();
            foreach (string title in new[] { "Tổng doanh thu", "Phòng đã đặt", "Tổng khách hàng", "Tỷ lệ lấp đầy" })
            {
                kpiHeader.Cells.Add(CreatePrintCell(title, true));
            }
            kpiGroup.Rows.Add(kpiHeader);

            TableRow kpiValue = new();
            foreach (string value in new[] { TxtTongDoanhThu.Text, TxtTongPhongDat.Text, TxtTongKhachHang.Text, TxtTyLeLapDay.Text })
            {
                kpiValue.Cells.Add(CreatePrintCell(value, false));
            }
            kpiGroup.Rows.Add(kpiValue);
            kpiTable.RowGroups.Add(kpiGroup);
            document.Blocks.Add(kpiTable);

            document.Blocks.Add(new Paragraph(new Run("Doanh thu theo ngày trong tháng (01-30)"))
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 14, 0, 6)
            });

            Table chartTable = new();
            for (int i = 0; i < 6; i++)
            {
                chartTable.Columns.Add(new TableColumn());
            }

            TableRowGroup chartGroup = new();
            foreach (IEnumerable<DoanhThuThangItem> chunk in doanhThuTheoNgayTrongThang.Chunk(6))
            {
                TableRow dayRow = new();
                TableRow revenueRow = new();
                foreach (DoanhThuThangItem item in chunk)
                {
                    dayRow.Cells.Add(CreatePrintCell("Ngày " + item.Label, true));
                    revenueRow.Cells.Add(CreatePrintCell(item.DoanhThu.ToString("N0", vietnameseCulture) + " đ", false));
                }
                chartGroup.Rows.Add(dayRow);
                chartGroup.Rows.Add(revenueRow);
            }
            chartTable.RowGroups.Add(chartGroup);
            document.Blocks.Add(chartTable);

            document.Blocks.Add(new Paragraph(new Run("Chi tiết báo cáo"))
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 14, 0, 6)
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
                header.Cells.Add(CreatePrintCell(column, true));
            }
            headerGroup.Rows.Add(header);
            table.RowGroups.Add(headerGroup);

            TableRowGroup bodyGroup = new();
            foreach (BaoCaoChiTietItem item in danhSachHienThi)
            {
                TableRow row = new();
                foreach (string value in new[] { item.MaBaoCao, item.Ngay, item.NoiDung, item.Loai, item.SoLuong.ToString("N0", vietnameseCulture), item.DoanhThu })
                {
                    row.Cells.Add(CreatePrintCell(value, false));
                }
                bodyGroup.Rows.Add(row);
            }

            table.RowGroups.Add(bodyGroup);
            document.Blocks.Add(table);

            document.Blocks.Add(new Paragraph(new Run($"Tổng dòng: {danhSachHienThi.Count:N0} | Tổng doanh thu: {TongDoanhThuHienThi().ToString("N0", vietnameseCulture)} đ"))
            {
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            });

            return document;
        }

        private string GetLoaiBaoCaoText()
        {
            return CboLoaiBaoCao.SelectedItem is ComboBoxItem item
                ? item.Content?.ToString() ?? GetLoaiBaoCao()
                : GetLoaiBaoCao();
        }

        private static TableCell CreatePrintCell(string value, bool header)
        {
            return new TableCell(new Paragraph(new Run(value)))
            {
                FontWeight = header ? FontWeights.Bold : FontWeights.Normal,
                Background = header ? Brushes.LightGray : Brushes.Transparent,
                Padding = new Thickness(4),
                BorderBrush = header ? Brushes.Gray : Brushes.LightGray,
                BorderThickness = new Thickness(0.5)
            };
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

        private decimal TongDoanhThuHienThi()
        {
            return danhSachHienThi.Sum(item => item.DoanhThuValue);
        }

        private sealed class DoanhThuThangItem
        {
            public string Label { get; init; } = string.Empty;
            public int Ngay { get; init; }
            public decimal DoanhThu { get; init; }
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
            public decimal DoanhThuValue { get; init; }
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
                    DoanhThuValue = doanhThu,
                    DoanhThu = doanhThu.ToString("N0", culture) + " đ",
                    GhiChu = row["GhiChu"].ToString() ?? string.Empty
                };
            }
        }
    }
}

