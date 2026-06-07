using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DAL;

namespace QLKS_AnPhu.View
{
    public partial class TachBillWindow : Window
    {
        private readonly HoaDonItem hoaDon;
        private readonly ObservableCollection<RoomSplitItem> danhSachPhong = new();

        public TachBillWindow(HoaDonItem hoaDon)
        {
            this.hoaDon = hoaDon;
            InitializeComponent();
            Loaded += TachBillWindow_Loaded;
        }

        private void TachBillWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TxtThongTinHoaDon.Text = $"{hoaDon.MaHoaDon} - {hoaDon.TenKhachHang} - Phòng {hoaDon.SoPhong}";
            TxtTongTien.Text = hoaDon.TongTien.ToString("N0") + " VND";
            TxtDaiDienBill2.Text = string.Empty;
            DgPhong.ItemsSource = danhSachPhong;
            NapDanhSachPhong();
        }

        private void NapDanhSachPhong()
        {
            danhSachPhong.Clear();

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
            else
            {
                rooms = GioiHanTienPhongTheoHoaDon(rooms, hoaDon.TienPhong);
            }

            foreach (RoomPrintGroup room in rooms)
            {
                if (hoaDon.LoaiThanhToan != "PHATSINH" && room.TienPhong > 0)
                {
                    room.Items.Add(new PrintLineItem("Tiền phòng lúc check-in", room.TienPhong, 1, room.TienPhong));
                }
                else if (hoaDon.LoaiThanhToan == "PHATSINH" && room.TienPhong > 0)
                {
                    room.Items.Add(new PrintLineItem("Gia hạn phòng", room.TienPhong, 1, room.TienPhong));
                }
            }

            foreach (DichVuHoaDonItem item in LoadDichVu())
            {
                RoomPrintGroup target = item.MaPhong.HasValue
                    ? rooms.FirstOrDefault(room => room.MaPhong == item.MaPhong) ?? rooms[0]
                    : rooms[0];
                target.Items.Add(new PrintLineItem(item.TenDichVu, item.DonGia, item.SoLuong, item.ThanhTien));
            }

            if (rooms.Count > 0)
            {
                if (hoaDon.PhuPhi > 0)
                {
                    rooms[0].Items.Add(new PrintLineItem(hoaDon.LoaiThanhToan == "PHATSINH" ? "Phụ phí trả muộn" : "Phụ phí nhận sớm", 0, 0, hoaDon.PhuPhi));
                }

                if (hoaDon.ThueVat > 0)
                {
                    rooms[0].Items.Add(new PrintLineItem("Thuế VAT (10%)", 0, 0, hoaDon.ThueVat));
                }

                if (hoaDon.LoaiThanhToan != "PHATSINH" && hoaDon.GiamGia > 0)
                {
                    rooms[0].Items.Add(new PrintLineItem("Giảm giá / cọc", 0, 0, -hoaDon.GiamGia));
                }
            }

            foreach (RoomPrintGroup room in rooms.Where(room => room.Items.Count > 0))
            {
                RoomSplitItem item = new()
                {
                    MaPhong = room.MaPhong,
                    SoPhong = room.SoPhong,
                    GhiChu = room.MaPhong.HasValue ? "Dịch vụ theo phòng " + room.SoPhong : "Khoản chung",
                };
                foreach (PrintLineItem line in room.Items)
                {
                    item.Items.Add(line);
                }
                item.PropertyChanged += Room_PropertyChanged;
                danhSachPhong.Add(item);
            }

            CapNhatTong();
        }

        private void Room_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RoomSplitItem.IsBill2))
            {
                CapNhatTong();
            }
        }

        private void CapNhatTong()
        {
            TachBillItem bill1 = TaoBill(false);
            TachBillItem bill2 = TaoBill(true);
            TxtBill1.Text = bill1.SoTien.ToString("N0") + " VND";
            TxtBill2.Text = bill2.SoTien.ToString("N0") + " VND";
            TxtTrangThaiTong.Text = $"Hóa đơn 1: {bill1.SoPhong} | Hóa đơn 2: {bill2.SoPhong}\nTổng sau tách: {(bill1.SoTien + bill2.SoTien):N0} VND";
        }

        private TachBillItem TaoBill(bool bill2)
        {
            List<RoomSplitItem> rooms = danhSachPhong.Where(item => item.IsBill2 == bill2).ToList();
            TachBillItem bill = new()
            {
                MaBill = hoaDon.MaHoaDon + (bill2 ? "-T02" : "-T01"),
                SoPhong = rooms.Count == 0 ? "--" : string.Join(", ", rooms.Select(item => item.SoPhong)),
                NguoiThanhToan = bill2 ? TxtDaiDienBill2.Text.Trim() : hoaDon.TenKhachHang,
                SoDienThoai = bill2 ? TxtSdtBill2.Text.Trim() : hoaDon.SoDienThoai,
                GhiChu = bill2 ? TxtGhiChuBill2.Text.Trim() : "Hóa đơn 1 - phần còn lại"
            };

            foreach (RoomSplitItem room in rooms)
            {
                foreach (PrintLineItem line in room.Items)
                {
                    bill.Items.Add(new PrintLineItem($"Phòng {room.SoPhong} - {line.Ten}", line.DonGia, line.SoLuong, line.ThanhTien));
                }
            }

            bill.SoTien = bill.Items.Sum(item => item.ThanhTien);
            return bill;
        }

        private bool KiemTraTachBill(bool yeuCauBill2)
        {
            DgPhong.CommitEdit(DataGridEditingUnit.Cell, true);
            DgPhong.CommitEdit(DataGridEditingUnit.Row, true);

            bool coBill2 = danhSachPhong.Any(item => item.IsBill2);
            bool coBill1 = danhSachPhong.Any(item => !item.IsBill2);

            if (yeuCauBill2 && !coBill2)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 phòng chuyển sang hóa đơn 2.", "Tách bill", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!coBill1)
            {
                MessageBox.Show("Hóa đơn 1 phải còn ít nhất 1 phòng. Không thể chuyển toàn bộ phòng sang hóa đơn 2.", "Tách bill", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (coBill2 && string.IsNullOrWhiteSpace(TxtDaiDienBill2.Text))
            {
                MessageBox.Show("Vui lòng nhập người đại diện cho hóa đơn 2.", "Tách bill", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtDaiDienBill2.Focus();
                return false;
            }

            decimal tong = TaoBill(false).SoTien + TaoBill(true).SoTien;
            if (Math.Abs(tong - hoaDon.TongTien) >= 1)
            {
                MessageBox.Show($"Tổng sau tách chưa khớp hóa đơn gốc.\nHóa đơn gốc: {hoaDon.TongTien:N0} VND\nSau tách: {tong:N0} VND", "Tách bill", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void BtnInBill1_Click(object sender, RoutedEventArgs e)
        {
            if (!KiemTraTachBill(false))
            {
                return;
            }

            MoCuaSoIn(TaoBill(false));
        }

        private void BtnInBill2_Click(object sender, RoutedEventArgs e)
        {
            if (!KiemTraTachBill(true))
            {
                return;
            }

            MoCuaSoIn(TaoBill(true));
        }

        private void BtnInTatCa_Click(object sender, RoutedEventArgs e)
        {
            if (!KiemTraTachBill(true))
            {
                return;
            }

            MoCuaSoIn(TaoBill(false));
            MoCuaSoIn(TaoBill(true));
        }

        private void MoCuaSoIn(TachBillItem bill)
        {
            TachBillPrintWindow window = new(hoaDon, bill)
            {
                Owner = this
            };
            window.ShowDialog();
        }

        private void InDanhSachBill(IEnumerable<TachBillItem> items, string description)
        {
            PrintDialog dialog = new();
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            FlowDocument document = TaoTaiLieuIn(items);
            document.PageWidth = dialog.PrintableAreaWidth;
            document.PageHeight = dialog.PrintableAreaHeight;
            document.PagePadding = new Thickness(36);
            document.ColumnWidth = dialog.PrintableAreaWidth;
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, description);
        }

        private FlowDocument TaoTaiLieuIn(IEnumerable<TachBillItem> items)
        {
            FlowDocument document = new()
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = Brushes.Black
            };

            foreach (TachBillItem item in items.Where(item => item.Items.Count > 0))
            {
                Section section = new();
                section.Blocks.Add(new Paragraph(new Run("AN PHÚ HOTEL"))
                {
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 6)
                });
                section.Blocks.Add(new Paragraph(new Run("BILL THANH TOÁN TÁCH"))
                {
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 16)
                });

                section.Blocks.Add(CreateInfoLine("Mã bill:", item.MaBill));
                section.Blocks.Add(CreateInfoLine("Hóa đơn gốc:", hoaDon.MaHoaDon));
                section.Blocks.Add(CreateInfoLine("Người đại diện:", item.NguoiThanhToan));
                section.Blocks.Add(CreateInfoLine("SĐT:", item.SoDienThoai));
                section.Blocks.Add(CreateInfoLine("Phòng:", item.SoPhong));
                section.Blocks.Add(CreateInfoLine("Kỳ thuê:", hoaDon.NgayNhanText + " - " + hoaDon.NgayTraText));
                section.Blocks.Add(CreateInfoLine("Ghi chú:", item.GhiChu));

                Table money = new() { Margin = new Thickness(0, 18, 0, 0) };
                money.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                money.Columns.Add(new TableColumn { Width = new GridLength(180) });
                TableRowGroup moneyGroup = new();
                money.RowGroups.Add(moneyGroup);
                foreach (PrintLineItem line in item.Items)
                {
                    AddMoneyRow(moneyGroup, line.Ten, line.ThanhTien);
                }
                AddMoneyRow(moneyGroup, "Tổng bill này", item.SoTien, true);
                section.Blocks.Add(money);

                section.Blocks.Add(new Paragraph(new Run("Ngày in: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)))
                {
                    Margin = new Thickness(0, 18, 0, 0),
                    Foreground = Brushes.DimGray
                });

                section.BreakPageBefore = document.Blocks.Count > 0;
                document.Blocks.Add(section);
            }

            return document;
        }

        private static Paragraph CreateInfoLine(string label, string value)
        {
            Paragraph paragraph = new()
            {
                Margin = new Thickness(0, 3, 0, 3)
            };
            paragraph.Inlines.Add(new Run(label + " ") { FontWeight = FontWeights.SemiBold });
            paragraph.Inlines.Add(new Run(value ?? string.Empty));
            return paragraph;
        }

        private static void AddMoneyRow(TableRowGroup group, string label, decimal amount, bool highlight = false)
        {
            TableRow row = new();
            row.Cells.Add(new TableCell(new Paragraph(new Run(label))) { FontWeight = highlight ? FontWeights.Bold : FontWeights.Normal, Padding = new Thickness(0, 6, 8, 6) });
            row.Cells.Add(new TableCell(new Paragraph(new Run(amount.ToString("N0") + " VND")))
            {
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Right,
                Padding = new Thickness(0, 6, 0, 6)
            });
            group.Rows.Add(row);
        }

        private void DgPhong_CurrentCellChanged(object sender, EventArgs e)
        {
            CapNhatTong();
        }

        private void PhongBill2_Checked(object sender, RoutedEventArgs e)
        {
            CapNhatTong();
        }

        private void ThongTinBill2_Changed(object sender, TextChangedEventArgs e)
        {
            CapNhatTong();
        }

        private void BtnDong_Click(object sender, RoutedEventArgs e)
        {
            Close();
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
                        List<RoomPrintGroup> mapped = MapPhongHoaDon(data);
                        return mapped.Count > 1 || !CoNhieuPhongTrongChuoi(hoaDon.SoPhong)
                            ? mapped
                            : TaoPhongTuChuoiHienThi();
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
                List<RoomPrintGroup> singleMapped = MapPhongHoaDon(single);
                return singleMapped.Count > 1 || !CoNhieuPhongTrongChuoi(hoaDon.SoPhong)
                    ? singleMapped
                    : TaoPhongTuChuoiHienThi();
            }

            return new List<RoomPrintGroup>();
        }

        private List<RoomPrintGroup> TaoPhongTuChuoiHienThi()
        {
            List<string> soPhong = hoaDon.SoPhong
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (soPhong.Count <= 1)
            {
                return new List<RoomPrintGroup>();
            }

            decimal tongTienPhong = Math.Max(0, hoaDon.TienPhong);
            decimal tienMoiPhong = Math.Floor(tongTienPhong / soPhong.Count);
            decimal conLai = tongTienPhong - tienMoiPhong * soPhong.Count;
            List<RoomPrintGroup> result = new();

            for (int i = 0; i < soPhong.Count; i++)
            {
                (int? maPhong, string tenPhong) = LayThongTinPhongTheoTen(soPhong[i]);
                result.Add(new RoomPrintGroup
                {
                    MaPhong = maPhong,
                    SoPhong = string.IsNullOrWhiteSpace(tenPhong) ? soPhong[i] : tenPhong,
                    TienPhong = tienMoiPhong + (i == soPhong.Count - 1 ? conLai : 0)
                });
            }

            return result;
        }

        private static bool CoNhieuPhongTrongChuoi(string value)
        {
            return value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Count(item => !string.IsNullOrWhiteSpace(item)) > 1;
        }

        private static (int? MaPhong, string SoPhong) LayThongTinPhongTheoTen(string soPhong)
        {
            if (!TableExists("PHONG"))
            {
                return (null, soPhong);
            }

            List<string> columns = new();
            foreach (string column in new[] { "TenPhong", "SoPhong", "MaSoPhong" })
            {
                if (ColumnExists("PHONG", column))
                {
                    columns.Add(column);
                }
            }

            string displayExpr = columns.Count > 0 ? columns[0] : "CAST(MaPhong AS nvarchar(20))";
            string conditions = columns.Count > 0
                ? string.Join(" OR ", columns.Select(column => "CAST(" + column + " AS nvarchar(100)) = @SoPhong"))
                : "CAST(MaPhong AS nvarchar(100)) = @SoPhong";

            DataTable data = ConnectDB.GetData(
                "SELECT TOP 1 MaPhong, CAST(" + displayExpr + " AS nvarchar(100)) AS SoPhong FROM dbo.PHONG WHERE " + conditions,
                new SqlParameter("@SoPhong", soPhong));

            if (data.Rows.Count == 0)
            {
                return (null, soPhong);
            }

            DataRow row = data.Rows[0];
            return (GetNullableInt(row, "MaPhong"), row["SoPhong"]?.ToString() ?? soPhong);
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
            bool coGhiChu = ColumnExists(table, "GhiChu");
            string filterLoaiDichVu = coGhiChu ? ViewSchemaHelper.DichVuTheoLoaiHoaDonFilter("PS", hoaDon.LoaiThanhToan) : string.Empty;

            DataTable data = ConnectDB.GetData(
                @"SELECT DV." + tenDichVu + @" AS TenDichVu,
                         " + maPhongExpr + @" AS MaPhong,
                         " + soLuong + @" AS SoLuong,
                         " + donGia + @" AS DonGia,
                         " + thanhTien + @" AS ThanhTien
                  FROM dbo." + table + @" PS
                  JOIN dbo.DICHVUVATTU DV ON PS." + maDvPs + " = DV." + maDv + @"
                  WHERE PS." + keyColumn + " = @Ma" + filterLoaiDichVu,
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

        private static List<RoomPrintGroup> GioiHanTienPhongTheoHoaDon(IEnumerable<RoomPrintGroup> source, decimal targetTotal)
        {
            List<RoomPrintGroup> result = new();
            decimal remaining = Math.Max(0, targetTotal);
            foreach (RoomPrintGroup room in source)
            {
                decimal amount = Math.Min(room.TienPhong, remaining);
                result.Add(new RoomPrintGroup
                {
                    MaPhong = room.MaPhong,
                    SoPhong = room.SoPhong,
                    TienPhong = amount
                });
                remaining -= amount;
            }

            return result;
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

    public class RoomSplitItem : INotifyPropertyChanged
    {
        private bool isBill2;
        public event PropertyChangedEventHandler? PropertyChanged;
        public int? MaPhong { get; init; }
        public string SoPhong { get; init; } = string.Empty;
        public string GhiChu { get; init; } = string.Empty;
        public ObservableCollection<PrintLineItem> Items { get; } = new();
        public int SoDichVu => Items.Count(item => !item.Ten.StartsWith("Tiền phòng", StringComparison.OrdinalIgnoreCase) && !item.Ten.StartsWith("Gia hạn", StringComparison.OrdinalIgnoreCase));
        public decimal TongPhong => Items.Sum(item => item.ThanhTien);
        public string TongPhongText => TongPhong.ToString("N0");

        public bool IsBill2
        {
            get => isBill2;
            set
            {
                if (isBill2 == value) return;
                isBill2 = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBill2)));
            }
        }
    }

    public class TachBillItem
    {
        public string MaBill { get; init; } = string.Empty;
        public string SoPhong { get; init; } = string.Empty;
        public string NguoiThanhToan { get; init; } = string.Empty;
        public string SoDienThoai { get; init; } = string.Empty;
        public string GhiChu { get; init; } = string.Empty;
        public ObservableCollection<PrintLineItem> Items { get; } = new();
        public decimal SoTien { get; set; }
    }
}
