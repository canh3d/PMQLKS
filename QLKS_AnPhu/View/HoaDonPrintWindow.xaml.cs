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
using QLKS_AnPhu.Security;
using QLKS_AnPhu.Services;

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
            TxtNgayLap.Text = "Ngày lập: " + hoaDon.NgayLapHoaDon.ToString("dd/MM/yyyy HH:mm");
            TxtMaHoaDon.Text = "Mã hóa đơn: " + hoaDon.MaHoaDon;
            TxtNhanVienLap.Text = "Nhân viên lập: " + LayNhanVienLap();
            TxtTrangThai.Text = "Trạng thái: " + hoaDon.TrangThai;
            TxtTrangThai.Foreground = hoaDon.DaThanhToan
                ? System.Windows.Media.Brushes.ForestGreen
                : System.Windows.Media.Brushes.DarkOrange;
            TxtTieuDeHoaDon.Text = hoaDon.LoaiThanhToan == "PHATSINH" ? "Hóa đơn phát sinh" : "Hóa đơn nhận phòng";
            TxtSoPhong.Text = "Phòng: " + hoaDon.SoPhong;
            TxtThoiGianThue.Text = "Thời gian: " + hoaDon.NgayNhanPhong.ToString("dd/MM/yyyy HH:mm") + " - " + hoaDon.NgayTraPhong.ToString("dd/MM/yyyy HH:mm");
            TxtLoaiPhong.Text = "Loại phòng: " + hoaDon.LoaiPhong;
            TxtKhachHang.Text = "Khách hàng: " + hoaDon.TenKhachHang;
            TxtSdt.Text = "SĐT: " + hoaDon.SoDienThoai;

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
            else
            {
                rooms = GioiHanTienPhongTheoHoaDon(rooms, hoaDon.TienPhong, hoaDon.LoaiThanhToan == "PHATSINH");
            }

            if (hoaDon.LaPhieuDatDaHuyGiuCoc)
            {
                rooms[0].Items.Add(new PrintLineItem("Giữ tiền cọc do hủy đặt phòng", hoaDon.TienCoc, 1, hoaDon.TienCoc, rooms[0].SoPhong));
            }
            else
            {
                foreach (RoomPrintGroup room in rooms)
                {
                    if (hoaDon.LoaiThanhToan != "PHATSINH" && room.TienPhong > 0)
                    {
                        room.Items.Add(new PrintLineItem("Tiền phòng lúc check-in", room.TienPhong, 1, room.TienPhong, room.SoPhong));
                    }
                    else if (hoaDon.LoaiThanhToan == "PHATSINH" && room.TienPhong > 0)
                    {
                        room.Items.Add(new PrintLineItem("Tiền phòng / gia hạn / đổi phòng", room.TienPhong, 1, room.TienPhong, room.SoPhong));
                    }
                }
            }
            if (!hoaDon.LaPhieuDatDaHuyGiuCoc && hoaDon.LoaiThanhToan == "PHATSINH" && hoaDon.TienPhong < 0)
            {
                rooms[0].Items.Add(new PrintLineItem(
                    "Hoàn chênh lệch đổi xuống phòng giá thấp hơn",
                    Math.Abs(hoaDon.TienPhong),
                    1,
                    hoaDon.TienPhong,
                    rooms[0].SoPhong));
            }

            if (!hoaDon.LaPhieuDatDaHuyGiuCoc)
            {
                decimal tongDichVuDaNap = 0;
                foreach (DichVuHoaDonItem item in LoadDichVu())
                {
                    RoomPrintGroup target = item.MaPhong.HasValue
                        ? rooms.FirstOrDefault(room => room.MaPhong == item.MaPhong) ?? rooms[0]
                        : rooms[0];
                    target.Items.Add(new PrintLineItem(item.TenDichVu, item.DonGia, item.SoLuong, item.ThanhTien, target.SoPhong));
                    tongDichVuDaNap += item.ThanhTien;
                }

                decimal dichVuChuaCoChiTiet = Math.Max(0, hoaDon.TienDichVu - tongDichVuDaNap);
                if (dichVuChuaCoChiTiet > 0)
                {
                    rooms[0].Items.Add(new PrintLineItem(
                        hoaDon.LoaiThanhToan == "PHATSINH" ? "Dịch vụ phát sinh trong thời gian thuê" : "Dịch vụ tại check-in",
                        dichVuChuaCoChiTiet,
                        1,
                        dichVuChuaCoChiTiet,
                        rooms[0].SoPhong));
                }

                if (hoaDon.LoaiThanhToan != "PHATSINH" && hoaDon.PhuPhiHienThi > 0)
                {
                    rooms[0].Items.Add(new PrintLineItem("Phụ phí nhận sớm", 0, 0, hoaDon.PhuPhiHienThi, rooms[0].SoPhong));
                }
                else if (hoaDon.LoaiThanhToan == "PHATSINH" && hoaDon.PhuPhiHienThi > 0)
                {
                    rooms[0].Items.Add(new PrintLineItem("Phụ phí trả muộn", 0, 0, hoaDon.PhuPhiHienThi, rooms[0].SoPhong));
                }

                if (hoaDon.ThueVatHienThi > 0)
                {
                    rooms[0].Items.Add(new PrintLineItem("Thuế VAT (10%)", 0, 0, hoaDon.ThueVatHienThi, rooms[0].SoPhong));
                }

                if (hoaDon.LoaiThanhToan != "PHATSINH" && hoaDon.GiamGiaHienThi > 0)
                {
                    rooms[0].Items.Add(new PrintLineItem("Giảm giá", 0, 0, -hoaDon.GiamGiaHienThi, rooms[0].SoPhong));
                }

                if (hoaDon.LoaiThanhToan != "PHATSINH" && hoaDon.TienCoc > 0)
                {
                    rooms[0].Items.Add(new PrintLineItem("Đã đặt cọc", 0, 0, -hoaDon.TienCoc, rooms[0].SoPhong));
                }
            }

            int stt = 1;
            foreach (RoomPrintGroup room in rooms)
            {
                foreach (PrintLineItem item in room.Items)
                {
                    item.Stt = stt++;
                    chiTiet.Add(item);
                }

                phongChiTiet.Add(room);
            }

            decimal tamTinh = chiTiet.Where(item => item.ThanhTien > 0).Sum(item => item.ThanhTien);
            decimal giamGiaCoc = chiTiet.Where(item => item.ThanhTien < 0).Sum(item => item.ThanhTien);
            decimal tongTrenBill = Math.Max(0, tamTinh + giamGiaCoc);
            TxtTamTinh.Text = tamTinh.ToString("N0", CultureInfo.InvariantCulture) + " VND";
            TxtGiamGiaCoc.Text = giamGiaCoc == 0 ? "0 VND" : giamGiaCoc.ToString("N0", CultureInfo.InvariantCulture) + " VND";
            bool daThanhToan = hoaDon.DaThanhToan;
            TxtDaThanhToan.Text = daThanhToan
                ? tongTrenBill.ToString("N0", CultureInfo.InvariantCulture) + " VND"
                : "0 VND";
            TxtTongTienLabel.Text = daThanhToan ? "Đã thanh toán" : "Cần thanh toán";
            TxtTongTien.Foreground = daThanhToan ? Brushes.ForestGreen : Brushes.Red;
            TxtTongTien.Text = tongTrenBill.ToString("N0", CultureInfo.InvariantCulture) + " VND";
            TxtGhiChuHoaDon.Text = hoaDon.LoaiThanhToan == "PHATSINH"
                ? "Hóa đơn tổng hợp tiền phòng, gia hạn/đổi phòng, dịch vụ và phụ phí phát sinh."
                : "Dịch vụ tại check-in, phụ phí nhận sớm, VAT và cọc/giảm giá được thể hiện rõ để khách kiểm tra.";
            ItemsChiTietHoaDon.ItemsSource = chiTiet;
        }

        private static string LayNhanVienLap()
        {
            if (!string.IsNullOrWhiteSpace(CurrentUser.HoTen))
            {
                return CurrentUser.HoTen;
            }

            if (!string.IsNullOrWhiteSpace(CurrentUser.TenDangNhap))
            {
                return CurrentUser.TenDangNhap;
            }

            return "Chưa xác định";
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
            string ngayPhatSinhColumn = ColumnExists(table, "NgayPhatSinh") ? "NgayPhatSinh" : ColumnExists(table, "NgaySuDung") ? "NgaySuDung" : string.Empty;
            string orderBy = string.IsNullOrWhiteSpace(ngayPhatSinhColumn) ? string.Empty : " ORDER BY PS." + ngayPhatSinhColumn;
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
                  WHERE PS." + keyColumn + " = @Ma" + filterLoaiDichVu + orderBy,
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

            return GioiHanDichVuTheoTongTien(result, hoaDon.TienDichVu, hoaDon.LoaiThanhToan == "PHATSINH");
        }

        private static ObservableCollection<DichVuHoaDonItem> GioiHanDichVuTheoTongTien(IEnumerable<DichVuHoaDonItem> source, decimal targetTotal, bool layTuCuoi)
        {
            ObservableCollection<DichVuHoaDonItem> result = new();
            decimal remaining = Math.Max(0, targetTotal);
            IEnumerable<DichVuHoaDonItem> ordered = layTuCuoi ? source.Reverse() : source;
            foreach (DichVuHoaDonItem item in ordered)
            {
                if (remaining <= 0)
                {
                    break;
                }

                decimal amount = Math.Min(item.ThanhTien, remaining);
                decimal quantity = item.DonGia > 0 ? amount / item.DonGia : item.SoLuong;
                result.Add(new DichVuHoaDonItem
                {
                    Stt = result.Count + 1,
                    MaPhong = item.MaPhong,
                    TenDichVu = item.TenDichVu,
                    SoLuong = quantity,
                    DonGia = item.DonGia,
                    ThanhTien = amount
                });
                remaining -= amount;
            }

            return layTuCuoi
                ? new ObservableCollection<DichVuHoaDonItem>(result.Reverse())
                : result;
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

        private static List<RoomPrintGroup> GioiHanTienPhongTheoHoaDon(IEnumerable<RoomPrintGroup> source, decimal targetTotal, bool dungDungTongHoaDon = false)
        {
            List<RoomPrintGroup> result = new();
            decimal remaining = Math.Max(0, targetTotal);
            List<RoomPrintGroup> rooms = source.ToList();
            for (int index = 0; index < rooms.Count; index++)
            {
                RoomPrintGroup room = rooms[index];
                decimal amount = dungDungTongHoaDon && index == 0
                    ? remaining
                    : Math.Min(room.TienPhong, remaining);
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

                    dialog.PrintVisual(InvoicePaper, "Hóa đơn " + hoaDon.MaHoaDon);
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
                MessageBox.Show("Không in được hóa đơn: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                Title = "Xuất chi tiết hóa đơn",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = SafeFileName(hoaDon.MaHoaDon) + ".xlsx"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                ExcelDocument document = new()
                {
                    Title = $"HÓA ĐƠN {hoaDon.MaHoaDon}",
                    Subtitle = $"{TxtTieuDeHoaDon.Text} | Ngày lập: {hoaDon.NgayLapHoaDon:dd/MM/yyyy HH:mm}",
                    SheetName = "Hóa đơn"
                };

                ExcelSection information = new()
                {
                    Title = "Thông tin hóa đơn",
                    Headers = ["Nội dung", "Thông tin"],
                    ColumnWidths = [22, 42]
                };
                information.Rows.Add(["Khách hàng", hoaDon.TenKhachHang]);
                information.Rows.Add(["Số điện thoại", hoaDon.SoDienThoai]);
                information.Rows.Add(["Phòng", hoaDon.SoPhong]);
                information.Rows.Add(["Thời gian lưu trú", $"{hoaDon.NgayNhanPhong:dd/MM/yyyy HH:mm} - {hoaDon.NgayTraPhong:dd/MM/yyyy HH:mm}"]);
                information.Rows.Add(["Trạng thái", hoaDon.TrangThai]);
                document.Sections.Add(information);

                ExcelSection details = new()
                {
                    Title = "Chi tiết thanh toán",
                    Headers = ["STT", "Phòng", "Khoản mục", "Đơn giá", "Số lượng", "Thành tiền"],
                    ColumnWidths = [8, 12, 34, 18, 12, 20],
                    Summary = $"Tổng giá trị: {hoaDon.TongGiaTriHoaDon:N0} VND | Tiền cọc: {hoaDon.TienCoc:N0} VND | Cần thanh toán: {hoaDon.TongTien:N0} VND"
                };
                details.Rows.AddRange(chiTiet.Select(item => (IReadOnlyList<object?>)
                    [item.Stt, item.SoPhong, item.Ten, new ExcelMoney(item.DonGia), item.SoLuong, new ExcelMoney(item.ThanhTien)]));
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
            string giaNgayExpr = "ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0))";
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
        public PrintLineItem(string ten, decimal donGia, decimal soLuong, decimal thanhTien, string soPhong = "")
        {
            Ten = ten;
            DonGia = donGia;
            SoLuong = soLuong;
            ThanhTien = thanhTien;
            SoPhong = soPhong;
        }

        public int Stt { get; set; }
        public string Ten { get; }
        public string SoPhong { get; }
        public decimal DonGia { get; }
        public decimal SoLuong { get; }
        public decimal ThanhTien { get; }
        public string DonGiaText => DonGia == 0 ? string.Empty : DonGia.ToString("N0", CultureInfo.InvariantCulture);
        public string SoLuongText => SoLuong == 0 ? string.Empty : SoLuong.ToString("N0", CultureInfo.InvariantCulture);
        public string ThanhTienText => ThanhTien.ToString("N0", CultureInfo.InvariantCulture);
    }
}

