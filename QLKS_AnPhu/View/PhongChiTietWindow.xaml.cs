using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DAL;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.View
{
    public partial class PhongChiTietWindow : Window
    {
        private readonly PhongDTO phong;
        private readonly PhongBUS phongBUS = new();
        private readonly DichVuVatTuBUS dichVuVatTuBUS = new();
        private readonly ThanhToanFlowBUS thanhToanBUS = new();
        private readonly DispatcherTimer thoiGianLuuTruTimer = new();
        private ObservableCollection<DichVuChiTietItem> dichVuHienTai = new();
        private RoomStayData? stayData;
        private decimal tienPhong;
        private decimal tienDichVu;
        private decimal datCoc;

        public PhongChiTietWindow(PhongDTO phong)
        {
            this.phong = phong;
            InitializeComponent();
            Loaded += PhongChiTietWindow_Loaded;
            Closed += PhongChiTietWindow_Closed;
            thoiGianLuuTruTimer.Interval = TimeSpan.FromMinutes(1);
            thoiGianLuuTruTimer.Tick += (_, _) => CapNhatThoiGianLuuTru();
        }

        private void PhongChiTietWindow_Loaded(object sender, RoutedEventArgs e)
        {
            stayData = LoadRoomStayData();
            dichVuHienTai = LoadDichVu(stayData);

            tienPhong = stayData?.TienPhong ?? 0;
            datCoc = stayData?.TienCoc ?? 0;

            string tenPhong = $"Phòng {phong.MaHienThi}";
            Title = $"Thông tin chi tiết - {tenPhong}";
            TxtTieuDe.Text = $"Chi tiết phòng - {tenPhong}";
            TxtSoPhongHeader.Text = phong.MaHienThi;
            TxtNgayNhan.Text = stayData?.NgayNhan?.ToString("dd/MM/yyyy HH:mm") ?? string.Empty;
            TxtNgayNhanSidebar.Text = TxtNgayNhan.Text;
            TxtNgayTra.Text = stayData?.NgayTra?.ToString("dd/MM/yyyy HH:mm") ?? string.Empty;
            TxtSoLuongKhachHeader.Text = stayData?.SoNguoi > 0 ? $"{stayData.SoNguoi} người" : "--";
            TxtHoTen.Text = stayData?.HoTen ?? string.Empty;
            TxtSDT.Text = stayData?.SDT ?? string.Empty;
            TxtSoLuongKhach.Text = TxtSoLuongKhachHeader.Text;
            TxtLoaiPhong.Text = phong.LoaiPhong;
            TxtTrangThaiPhong.Text = phong.TrangThai;
            DgDichVu.ItemsSource = dichVuHienTai;
            NapDanhSachDichVuThem();
            CapNhatHoaDon();
            CapNhatThoiGianLuuTru();
            thoiGianLuuTruTimer.Start();

            CapNhatTrangThaiNut();
        }

        private void PhongChiTietWindow_Closed(object? sender, EventArgs e)
        {
            thoiGianLuuTruTimer.Stop();
        }

        private void CapNhatTrangThaiNut()
        {
            string trangThai = BoDau(phong.TrangThai);
            bool daDat = stayData?.MaDatPhong.HasValue == true || trangThai.Contains("dat", StringComparison.OrdinalIgnoreCase);
            bool dangThue = stayData?.MaThue.HasValue == true || trangThai.Contains("thue", StringComparison.OrdinalIgnoreCase) || trangThai.Contains("co khach", StringComparison.OrdinalIgnoreCase);
            bool canDonDep = trangThai.Contains("chua don", StringComparison.OrdinalIgnoreCase);

            BtnNhanPhong.IsEnabled = daDat && !dangThue;
            BtnHuyDat.IsEnabled = daDat && !dangThue;
            BtnTraPhong.IsEnabled = dangThue;
            BtnDonDep.IsEnabled = canDonDep;
            BtnThanhToan.IsEnabled = dangThue || stayData != null;
            BtnLuu.IsEnabled = stayData != null;
            CboDichVuThem.IsEnabled = stayData?.MaThue.HasValue == true && dangThue;
            TxtSoLuongDichVu.IsEnabled = CboDichVuThem.IsEnabled;
        }

        private void NapDanhSachDichVuThem()
        {
            try
            {
                List<DichVuVatTuDTO> danhSach = dichVuVatTuBUS.LayDanhSach()
                    .Where(item => !string.IsNullOrWhiteSpace(item.Ten))
                    .OrderBy(item => item.Ten)
                    .ToList();

                CboDichVuThem.ItemsSource = danhSach;
                CboDichVuThem.SelectedIndex = danhSach.Count > 0 ? 0 : -1;
            }
            catch
            {
                CboDichVuThem.ItemsSource = null;
            }
        }

        private void CapNhatHoaDon()
        {
            tienDichVu = dichVuHienTai.Sum(item => item.ThanhTien);
            decimal vat = Math.Round((tienPhong + tienDichVu) * 0.1m, 0);
            decimal tongThanhToan = tienPhong + tienDichVu + vat;
            decimal daThanhToanLucNhanPhong = datCoc;
            decimal canThanhToanThem = Math.Max(0, tongThanhToan - daThanhToanLucNhanPhong);

            TxtSoTienBanDau.Text = stayData == null
                ? "Phòng chưa có phiếu đặt/thuê đang hoạt động"
                : $"Đã thanh toán lúc nhận phòng: {daThanhToanLucNhanPhong:N0} VND";

            DgBangThanhToan.ItemsSource = new List<ThanhToanHoaDonItem>
            {
                new("Tiền phòng", tienPhong),
                new("Dịch vụ phát sinh trong thời gian thuê", tienDichVu),
                new("Thuế VAT (10%)", vat),
                new("Đã thanh toán lúc nhận phòng", -daThanhToanLucNhanPhong),
                new("Tổng tiền hóa đơn", tongThanhToan)
            };

            TxtKhaDung.Text = stayData == null ? "--" : $"{canThanhToanThem:N0} VND";
        }

        private RoomStayData? LoadRoomStayData()
        {
            RoomStayData? rental = LoadActiveRental();
            if (rental != null)
            {
                return rental;
            }

            return LoadActiveBooking();
        }

        private RoomStayData? LoadActiveRental()
        {
            if (!TableExists("PHIEUTHUE") || !TableExists("KHACHHANG"))
            {
                return null;
            }

            string ngayTraExpr = ColumnExists("PHIEUTHUE", "NgayTraPhong")
                ? "ISNULL(PT.NgayTraPhong, PT.NgayTraDuKien)"
                : "PT.NgayTraDuKien";
            string maDatPhongExpr = ColumnExists("PHIEUTHUE", "MaDatPhong") ? "PT.MaDatPhong" : "CAST(NULL AS int)";
            string soNguoiExpr = ColumnExists("PHIEUTHUE", "SoNguoi") ? "PT.SoNguoi" : "1";
            string ghiChuExpr = ColumnExists("PHIEUTHUE", "GhiChu") ? "PT.GhiChu" : "CAST(NULL AS nvarchar(1000))";
            string tienPhongExpr = TienPhongSql("PT.NgayNhan", "PT.NgayTraDuKien", ngayTraExpr);
            bool coChiTietDatPhong = TableExists("CHITIETDATPHONG") && ColumnExists("PHIEUTHUE", "MaDatPhong");
            string joinPhong = coChiTietDatPhong
                ? @"JOIN dbo.CHITIETDATPHONG CT ON PT.MaDatPhong = CT.MaDatPhong
JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong"
                : "LEFT JOIN dbo.PHONG P ON PT.MaPhong = P.MaPhong";
            string wherePhong = coChiTietDatPhong
                ? "(CT.MaPhong = @MaPhong OR PT.MaPhong = @MaPhong)"
                : "PT.MaPhong = @MaPhong";
            string soPhongDoanExpr = coChiTietDatPhong
                ? "(SELECT COUNT(*) FROM dbo.CHITIETDATPHONG CT2 WHERE CT2.MaDatPhong = PT.MaDatPhong)"
                : "1";
            string danhSachPhongDoanExpr = coChiTietDatPhong
                ? @"(SELECT STRING_AGG(CAST(" + TenPhongSql("P2") + @" AS nvarchar(max)), N', ')
                    FROM dbo.CHITIETDATPHONG CT2
                    JOIN dbo.PHONG P2 ON CT2.MaPhong = P2.MaPhong
                    WHERE CT2.MaDatPhong = PT.MaDatPhong)"
                : TenPhongSql("P");

            DataTable data = ConnectDB.GetData(@"
SELECT TOP 1
       PT.MaThue,
       " + maDatPhongExpr + @" AS MaDatPhong,
       KH.HoTen,
       KH.SDT,
       " + soNguoiExpr + @" AS SoNguoi,
       PT.NgayNhan,
       PT.NgayTraDuKien AS NgayTra,
       ISNULL(PT.TienCoc, 0) AS TienCoc,
       " + tienPhongExpr + @" AS TienPhong,
       " + ghiChuExpr + @" AS GhiChu,
       PT.TrangThai,
       " + soPhongDoanExpr + @" AS SoPhongDoan,
       " + danhSachPhongDoanExpr + @" AS DanhSachPhongDoan
FROM dbo.PHIEUTHUE PT
JOIN dbo.KHACHHANG KH ON PT.MaKH = KH.MaKH
" + joinPhong + @"
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE " + wherePhong + @"
  AND (
        PT.TrangThai IN (N'Đang thuê', N'Dang thue', N'Có khách', N'Co khach')
        OR P.TrangThai IN (N'Đang thuê', N'Dang thue', N'Có khách', N'Co khach')
      )
ORDER BY PT.MaThue DESC",
                new SqlParameter("@MaPhong", phong.Ma));

            return data.Rows.Count == 0 ? null : MapStay(data.Rows[0], laPhieuThue: true);
        }

        private RoomStayData? LoadActiveBooking()
        {
            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists("DATPHONG") ? "DATPHONG" : string.Empty;
            if (string.IsNullOrWhiteSpace(bangDatPhong) || !TableExists("KHACHHANG"))
            {
                return null;
            }

            string ngayNhanExpr = ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "DP.NgayNhanDuKien" : "DP.NgayNhanPhong";
            string ngayTraExpr = ColumnExists(bangDatPhong, "NgayTraDuKien") ? "DP.NgayTraDuKien" : "DP.NgayTraPhong";
            string tienCocExpr = ColumnExists(bangDatPhong, "TienCoc") ? "DP.TienCoc" : "DP.DatCoc";
            string soNguoiExpr = ColumnExists(bangDatPhong, "SoNguoi") ? "DP.SoNguoi" : "1";
            string ghiChuExpr = ColumnExists(bangDatPhong, "GhiChu") ? "DP.GhiChu" : "CAST(NULL AS nvarchar(1000))";
            string tienPhongExpr = TienPhongSql(ngayNhanExpr, ngayTraExpr, ngayTraExpr);
            bool coChiTietDatPhong = TableExists("CHITIETDATPHONG");
            string joinPhong = coChiTietDatPhong
                ? @"JOIN dbo.CHITIETDATPHONG CT ON DP.MaDatPhong = CT.MaDatPhong
JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong"
                : "JOIN dbo.PHONG P ON DP.MaPhong = P.MaPhong";
            string wherePhong = coChiTietDatPhong ? "CT.MaPhong = @MaPhong" : "DP.MaPhong = @MaPhong";
            string soPhongDoanExpr = coChiTietDatPhong
                ? "(SELECT COUNT(*) FROM dbo.CHITIETDATPHONG CT2 WHERE CT2.MaDatPhong = DP.MaDatPhong)"
                : "1";
            string danhSachPhongDoanExpr = coChiTietDatPhong
                ? @"(SELECT STRING_AGG(CAST(" + TenPhongSql("P2") + @" AS nvarchar(max)), N', ')
                    FROM dbo.CHITIETDATPHONG CT2
                    JOIN dbo.PHONG P2 ON CT2.MaPhong = P2.MaPhong
                    WHERE CT2.MaDatPhong = DP.MaDatPhong)"
                : TenPhongSql("P");

            DataTable data = ConnectDB.GetData(@"
SELECT TOP 1
       CAST(NULL AS int) AS MaThue,
       DP.MaDatPhong,
       KH.HoTen,
       KH.SDT,
       " + soNguoiExpr + @" AS SoNguoi,
       " + ngayNhanExpr + @" AS NgayNhan,
       " + ngayTraExpr + @" AS NgayTra,
       ISNULL(" + tienCocExpr + @", 0) AS TienCoc,
       " + tienPhongExpr + @" AS TienPhong,
       " + ghiChuExpr + @" AS GhiChu,
       DP.TrangThai,
       " + soPhongDoanExpr + @" AS SoPhongDoan,
       " + danhSachPhongDoanExpr + @" AS DanhSachPhongDoan
FROM dbo." + bangDatPhong + @" DP
JOIN dbo.KHACHHANG KH ON DP.MaKH = KH.MaKH
" + joinPhong + @"
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE " + wherePhong + @"
  AND DP.TrangThai IN (N'Đã xác nhận', N'Da xac nhan', N'Đã đặt', N'Da dat', N'Đang thuê', N'Dang thue')
ORDER BY DP.MaDatPhong DESC",
                new SqlParameter("@MaPhong", phong.Ma));

            return data.Rows.Count == 0 ? null : MapStay(data.Rows[0], laPhieuThue: false);
        }

        private ObservableCollection<DichVuChiTietItem> LoadDichVu(RoomStayData? data)
        {
            ObservableCollection<DichVuChiTietItem> result = new();
            if (data == null)
            {
                return result;
            }

            string bangPhatSinh = TableExists("PHATSINHDICHVU")
                ? "PHATSINHDICHVU"
                : TableExists("CHITIETPHATSINH")
                    ? "CHITIETPHATSINH"
                    : string.Empty;

            if (string.IsNullOrWhiteSpace(bangPhatSinh) || !TableExists("DICHVUVATTU"))
            {
                return result;
            }

            string keyColumn = data.MaThue.HasValue && ColumnExists(bangPhatSinh, "MaThue")
                ? "MaThue"
                : data.MaDatPhong.HasValue && ColumnExists(bangPhatSinh, "MaDatPhong")
                    ? "MaDatPhong"
                    : ColumnExists(bangPhatSinh, "MaPhong")
                        ? "MaPhong"
                        : string.Empty;

            if (string.IsNullOrWhiteSpace(keyColumn))
            {
                return result;
            }

            object keyValue = keyColumn == "MaThue"
                ? data.MaThue!.Value
                : keyColumn == "MaDatPhong"
                    ? data.MaDatPhong!.Value
                    : phong.Ma;

            string maDvPs = ColumnExists(bangPhatSinh, "MaDVVT") ? "MaDVVT" : "MaDichVu";
            string maDv = ColumnExists("DICHVUVATTU", "MaDVVT") ? "MaDVVT" : "MaDichVu";
            string tenDv = ColumnExists("DICHVUVATTU", "TenDVVT") ? "TenDVVT" : "TenDichVu";
            string psKey = GetFirstExistingColumn(bangPhatSinh, "MaPhatSinh", "MaCTPhatSinh", "MaChiTiet", "ID", "Ma");
            string psKeyExpr = string.IsNullOrWhiteSpace(psKey) ? "CAST(NULL AS int)" : "PS." + psKey;
            string soLuongExpr = ColumnExists(bangPhatSinh, "SoLuong") ? "PS.SoLuong" : "1";
            string donGiaExpr = ColumnExists(bangPhatSinh, "DonGia") ? "ISNULL(PS.DonGia, DV.DonGia)" : "DV.DonGia";
            string thanhTienExpr = ColumnExists(bangPhatSinh, "ThanhTien") ? "PS.ThanhTien" : "(" + soLuongExpr + " * " + donGiaExpr + ")";
            string roomFilter = keyColumn != "MaPhong" && ColumnExists(bangPhatSinh, "MaPhong")
                ? " AND PS.MaPhong = @MaPhong"
                : string.Empty;
            string keyFilter = "PS." + keyColumn + " = @KeyValue";
            if (keyColumn == "MaThue" && data.MaDatPhong.HasValue && ColumnExists(bangPhatSinh, "MaDatPhong"))
            {
                keyFilter = "(" + keyFilter + " OR PS.MaDatPhong = @MaDatPhong)";
            }

            DataTable services = ConnectDB.GetData(@"
SELECT " + psKeyExpr + @" AS MaPhatSinh,
       DV." + tenDv + @" AS Ten,
       " + soLuongExpr + @" AS SoLuong,
       " + donGiaExpr + @" AS DonGia,
       " + thanhTienExpr + @" AS ThanhTien
FROM dbo." + bangPhatSinh + @" PS
JOIN dbo.DICHVUVATTU DV ON PS." + maDvPs + " = DV." + maDv + @"
WHERE " + keyFilter + roomFilter,
                new SqlParameter("@KeyValue", keyValue),
                new SqlParameter("@MaDatPhong", data.MaDatPhong ?? 0),
                new SqlParameter("@MaPhong", phong.Ma));

            foreach (DataRow row in services.Rows)
            {
                result.Add(new DichVuChiTietItem
                {
                    MaPhatSinh = GetNullableInt(row, "MaPhatSinh"),
                    Ten = row["Ten"]?.ToString() ?? string.Empty,
                    SoLuong = Convert.ToInt32(GetDecimal(row, "SoLuong")),
                    DonGia = GetDecimal(row, "DonGia")
                });
            }

            return result;
        }

        private static RoomStayData MapStay(DataRow row, bool laPhieuThue)
        {
            return new RoomStayData
            {
                MaThue = laPhieuThue ? GetNullableInt(row, "MaThue") : null,
                MaDatPhong = GetNullableInt(row, "MaDatPhong"),
                HoTen = row["HoTen"]?.ToString() ?? string.Empty,
                SDT = row["SDT"]?.ToString() ?? string.Empty,
                SoNguoi = Convert.ToInt32(GetDecimal(row, "SoNguoi")),
                NgayNhan = GetNullableDate(row, "NgayNhan"),
                NgayTra = GetNullableDate(row, "NgayTra"),
                TienCoc = GetDecimal(row, "TienCoc"),
                TienPhong = GetDecimal(row, "TienPhong"),
                GhiChu = row["GhiChu"]?.ToString() ?? string.Empty,
                TrangThai = row["TrangThai"]?.ToString() ?? string.Empty,
                SoPhongDoan = Convert.ToInt32(GetDecimal(row, "SoPhongDoan")),
                DanhSachPhongDoan = row["DanhSachPhongDoan"]?.ToString() ?? string.Empty
            };
        }

        private void CapNhatThoiGianLuuTru()
        {
            ThoiGianLuuTruResult result = TinhThoiGianLuuTru(
                stayData?.NgayNhan,
                stayData?.NgayTra,
                phong.TrangThai ?? string.Empty,
                stayData?.TrangThai ?? string.Empty);
            TxtThoiGianLuuTru.Text = result.Text;
            TxtThoiGianLuuTru.Foreground = result.QuaGio ? Brushes.Red : Brushes.Black;
        }

        private static ThoiGianLuuTruResult TinhThoiGianLuuTru(DateTime? ngayNhan, DateTime? ngayTra, params string[] trangThaiValues)
        {
            if (!ngayNhan.HasValue)
            {
                return new ThoiGianLuuTruResult("0 giờ", false);
            }

            bool dangThue = trangThaiValues.Any(value =>
            {
                string normalized = BoDau(value ?? string.Empty);
                return normalized.Contains("thue", StringComparison.OrdinalIgnoreCase) ||
                       normalized.Contains("co khach", StringComparison.OrdinalIgnoreCase);
            });

            DateTime now = DateTime.Now;
            if (dangThue)
            {
                if (!ngayTra.HasValue)
                {
                    return new ThoiGianLuuTruResult("--", false);
                }

                if (now > ngayTra.Value)
                {
                    int quaGio = Math.Max(1, (int)Math.Ceiling((now - ngayTra.Value).TotalHours));
                    return new ThoiGianLuuTruResult($"Qua gio {quaGio} gio", true);
                }

                int soGioConLai = Math.Max(1, (int)Math.Ceiling((ngayTra.Value - now).TotalHours));
                return new ThoiGianLuuTruResult($"Con lai {soGioConLai} gio", false);

            }

            DateTime end = ngayTra ?? now;
            if (end <= ngayNhan.Value)
            {
                return new ThoiGianLuuTruResult("0 giờ", false);
            }

            int soGio = Math.Max(1, (int)Math.Ceiling((end - ngayNhan.Value).TotalHours));
            return new ThoiGianLuuTruResult($"{soGio} giờ", false);
        }

        private void BtnThoat_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnNhanPhong_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (stayData?.MaDatPhong.HasValue == true)
                {
                    phongBUS.NhanPhongTuDatPhong(stayData.MaDatPhong.Value, tienPhong + tienDichVu, datCoc);
                    stayData = LoadRoomStayData();
                    PhongChiTietWindow_Loaded(sender, e);
                    return;
                }

                if (!phong.TrangThai.Contains("thuê", StringComparison.OrdinalIgnoreCase) &&
                    !phong.TrangThai.Contains("thue", StringComparison.OrdinalIgnoreCase))
                {
                    phongBUS.NhanPhong(phong);
                    phong.TrangThai = "Đang thuê";
                }

                decimal tongHoaDon = tienPhong + tienDichVu;
                decimal conThanhToan = Math.Max(0, tongHoaDon - datCoc);
                MessageBox.Show(
                    $"Hóa đơn nhận phòng\n\n" +
                    $"Tiền phòng: {tienPhong:N0} đ\n" +
                    $"Tiền dịch vụ: {tienDichVu:N0} đ\n" +
                    $"Đã đặt cọc: {datCoc:N0} đ\n" +
                    $"Còn thanh toán: {conThanhToan:N0} đ",
                    "Hóa đơn",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không nhận được phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnTraPhong_Click(object sender, RoutedEventArgs e)
        {
            if (stayData?.MaThue.HasValue != true)
            {
                MessageBox.Show("Phòng này chưa có phiếu thuê đang hoạt động để trả phòng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int maThue = stayData.MaThue.GetValueOrDefault();
            if (MessageBox.Show("Xác nhận trả phòng và chuyển phòng sang trạng thái chưa dọn dẹp?", "Trả phòng", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                thanhToanBUS.CheckOut(maThue);
                MessageBox.Show("Đã trả phòng. Phòng chuyển sang trạng thái chưa dọn dẹp.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                PhongChiTietWindow_Loaded(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể trả phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDonDep_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Xác nhận đã dọn dẹp xong và chuyển phòng về trạng thái phòng trống?", "Dọn dẹp", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                CapNhatPhongTrong();
                MessageBox.Show("Phòng đã sẵn sàng cho thuê.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                stayData = null;
                PhongChiTietWindow_Loaded(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể cập nhật dọn dẹp: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnThemDichVu_Click(object sender, RoutedEventArgs e)
        {
            if (stayData == null || stayData.MaThue.HasValue != true)
            {
                MessageBox.Show("Phải nhận phòng trước khi thêm dịch vụ phát sinh.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CboDichVuThem.SelectedItem is not DichVuVatTuDTO dichVu)
            {
                MessageBox.Show("Vui lòng chọn dịch vụ/vật tư cần thêm.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtSoLuongDichVu.Text.Trim(), out int soLuong) || soLuong <= 0)
            {
                MessageBox.Show("Số lượng dịch vụ phải lớn hơn 0.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtSoLuongDichVu.Focus();
                return;
            }

            try
            {
                ThemDichVuPhatSinh(dichVu, soLuong);
                thanhToanBUS.CongDichVuPhatSinh(stayData.MaThue.Value, dichVu.DonGia * soLuong);
                dichVuHienTai = LoadDichVu(stayData);
                DgDichVu.ItemsSource = dichVuHienTai;
                CapNhatHoaDon();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể thêm dịch vụ: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnXoaDichVu_Click(object sender, RoutedEventArgs e)
        {
            if (stayData?.MaThue.HasValue != true)
            {
                MessageBox.Show("Phải nhận phòng trước khi xóa dịch vụ phát sinh.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DgDichVu.SelectedItem is not DichVuChiTietItem item)
            {
                MessageBox.Show("Vui lòng chọn dòng dịch vụ cần xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (item.MaPhatSinh.HasValue)
                {
                    XoaDichVuPhatSinh(item.MaPhatSinh.Value);
                    dichVuHienTai = LoadDichVu(stayData);
                    DgDichVu.ItemsSource = dichVuHienTai;
                }
                else
                {
                    dichVuHienTai.Remove(item);
                }

                CapNhatHoaDon();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể xóa dịch vụ: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TraPhong(int maThue)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();

            try
            {
                string trangThaiPhong = LayGiaTriHopLeTheoCheck(conn, tran, "PHONG", "TrangThai", "Chưa dọn dẹp", "Chua don dep");
                using (SqlCommand cmd = new("UPDATE dbo.PHONG SET TrangThai = @TrangThai WHERE MaPhong = @MaPhong", conn, tran))
                {
                    cmd.Parameters.AddWithValue("@TrangThai", trangThaiPhong);
                    cmd.Parameters.AddWithValue("@MaPhong", phong.Ma);
                    cmd.ExecuteNonQuery();
                }

                if (TableExists(conn, tran, "PHIEUTHUE") && ColumnExists(conn, tran, "PHIEUTHUE", "TrangThai"))
                {
                    string setNgayTra = ColumnExists(conn, tran, "PHIEUTHUE", "NgayTraPhong") ? ", NgayTraPhong = @NgayTra" : string.Empty;
                    using SqlCommand cmd = new("UPDATE dbo.PHIEUTHUE SET TrangThai = @TrangThai" + setNgayTra + " WHERE MaThue = @MaThue", conn, tran);
                    cmd.Parameters.AddWithValue("@TrangThai", LayGiaTriHopLeTheoCheck(conn, tran, "PHIEUTHUE", "TrangThai", "Đã trả phòng", "Da tra phong", "Đã trả", "Da tra"));
                    cmd.Parameters.AddWithValue("@NgayTra", DateTime.Now);
                    cmd.Parameters.AddWithValue("@MaThue", maThue);
                    cmd.ExecuteNonQuery();
                }

                tran.Commit();
                phong.TrangThai = "Chưa dọn dẹp";
                phong.KhachHienTai = "--";
                phong.GioNhanPhong = "--";
                phong.GioTraDuKien = "--";
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        private void CapNhatPhongTrong()
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();

            try
            {
                string trangThaiPhong = LayGiaTriHopLeTheoCheck(conn, tran, "PHONG", "TrangThai", "Phòng trống", "Phong trong");
                using (SqlCommand cmd = new("UPDATE dbo.PHONG SET TrangThai = @TrangThai WHERE MaPhong = @MaPhong", conn, tran))
                {
                    cmd.Parameters.AddWithValue("@TrangThai", trangThaiPhong);
                    cmd.Parameters.AddWithValue("@MaPhong", phong.Ma);
                    cmd.ExecuteNonQuery();
                }

                tran.Commit();
                phong.TrangThai = "Phòng trống";
                phong.KhachHienTai = "--";
                phong.GioNhanPhong = "--";
                phong.GioTraDuKien = "--";
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        private void ThemDichVuPhatSinh(DichVuVatTuDTO dichVu, int soLuong)
        {
            string bangPhatSinh = TableExists("PHATSINHDICHVU")
                ? "PHATSINHDICHVU"
                : TableExists("CHITIETPHATSINH")
                    ? "CHITIETPHATSINH"
                    : string.Empty;

            if (string.IsNullOrWhiteSpace(bangPhatSinh))
            {
                throw new InvalidOperationException("Không tìm thấy bảng phát sinh dịch vụ trong database.");
            }

            Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
            bool requiresMaThue = ColumnRequired(bangPhatSinh, "MaThue");
            if (requiresMaThue && stayData?.MaThue.HasValue != true)
            {
                throw new InvalidOperationException("Phải nhận phòng trước khi thêm dịch vụ phát sinh.");
            }

            if (stayData?.MaThue.HasValue == true && ColumnExists(bangPhatSinh, "MaThue"))
            {
                values["MaThue"] = stayData.MaThue.Value;
            }
            else if (!requiresMaThue && stayData?.MaDatPhong.HasValue == true && ColumnExists(bangPhatSinh, "MaDatPhong"))
            {
                values["MaDatPhong"] = stayData.MaDatPhong.Value;
            }

            if (ColumnExists(bangPhatSinh, "MaPhong"))
            {
                values["MaPhong"] = phong.Ma;
            }

            string maDvPs = ColumnExists(bangPhatSinh, "MaDVVT") ? "MaDVVT" : ColumnExists(bangPhatSinh, "MaDichVu") ? "MaDichVu" : string.Empty;
            if (string.IsNullOrWhiteSpace(maDvPs))
            {
                throw new InvalidOperationException("Bảng phát sinh dịch vụ thiếu cột mã dịch vụ.");
            }

            values[maDvPs] = dichVu.Ma;
            if (ColumnExists(bangPhatSinh, "SoLuong"))
            {
                values["SoLuong"] = soLuong;
            }

            if (ColumnExists(bangPhatSinh, "DonGia"))
            {
                values["DonGia"] = dichVu.DonGia;
            }

            if (ColumnExists(bangPhatSinh, "ThanhTien"))
            {
                values["ThanhTien"] = soLuong * dichVu.DonGia;
            }

            if (ColumnExists(bangPhatSinh, "NgayPhatSinh"))
            {
                values["NgayPhatSinh"] = DateTime.Now;
            }
            else if (ColumnExists(bangPhatSinh, "NgaySuDung"))
            {
                values["NgaySuDung"] = DateTime.Now;
            }

            if (!values.ContainsKey("MaThue") && !values.ContainsKey("MaDatPhong") && !values.ContainsKey("MaPhong"))
            {
                throw new InvalidOperationException("Không xác định được khóa liên kết để thêm dịch vụ cho phòng.");
            }

            string columns = string.Join(", ", values.Keys.Select(column => "[" + column + "]"));
            string parameters = string.Join(", ", values.Keys.Select(column => "@" + column));
            ConnectDB.ExecuteNonQuery(
                $"INSERT INTO dbo.{bangPhatSinh} ({columns}) VALUES ({parameters})",
                values.Select(pair => new SqlParameter("@" + pair.Key, pair.Value ?? DBNull.Value)).ToArray());
        }

        private static void XoaDichVuPhatSinh(int maPhatSinh)
        {
            string bangPhatSinh = TableExists("PHATSINHDICHVU")
                ? "PHATSINHDICHVU"
                : TableExists("CHITIETPHATSINH")
                    ? "CHITIETPHATSINH"
                    : string.Empty;
            string keyColumn = string.IsNullOrWhiteSpace(bangPhatSinh)
                ? string.Empty
                : GetFirstExistingColumn(bangPhatSinh, "MaPhatSinh", "MaCTPhatSinh", "MaChiTiet", "ID", "Ma");

            if (string.IsNullOrWhiteSpace(bangPhatSinh) || string.IsNullOrWhiteSpace(keyColumn))
            {
                throw new InvalidOperationException("Không xác định được dòng dịch vụ phát sinh để xóa.");
            }

            ConnectDB.ExecuteNonQuery(
                $"DELETE FROM dbo.{bangPhatSinh} WHERE [{keyColumn}] = @MaPhatSinh",
                new SqlParameter("@MaPhatSinh", maPhatSinh));
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

        private static bool TableExists(string tableName)
        {
            object? result = ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM sys.tables WHERE name = @Name", new SqlParameter("@Name", tableName));
            return Convert.ToInt32(result) > 0;
        }

        private static bool TableExists(SqlConnection conn, SqlTransaction tran, string tableName)
        {
            using SqlCommand cmd = new("SELECT COUNT(*) FROM sys.tables WHERE name = @Name", conn, tran);
            cmd.Parameters.AddWithValue("@Name", tableName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
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

        private static bool ColumnRequired(string tableName, string columnName)
        {
            object? result = ConnectDB.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM sys.tables t
                  JOIN sys.columns c ON t.object_id = c.object_id
                  WHERE t.name = @TableName
                    AND c.name = @ColumnName
                    AND c.is_nullable = 0
                    AND COLUMNPROPERTY(t.object_id, c.name, 'IsIdentity') = 0",
                new SqlParameter("@TableName", tableName),
                new SqlParameter("@ColumnName", columnName));
            return Convert.ToInt32(result) > 0;
        }

        private static string GetFirstExistingColumn(string tableName, params string[] columnNames)
        {
            foreach (string columnName in columnNames)
            {
                if (ColumnExists(tableName, columnName))
                {
                    return columnName;
                }
            }

            return string.Empty;
        }

        private static bool ColumnExists(SqlConnection conn, SqlTransaction tran, string tableName, string columnName)
        {
            using SqlCommand cmd = new(
                @"SELECT COUNT(*)
                  FROM sys.tables t
                  JOIN sys.columns c ON t.object_id = c.object_id
                  WHERE t.name = @TableName AND c.name = @ColumnName",
                conn,
                tran);
            cmd.Parameters.AddWithValue("@TableName", tableName);
            cmd.Parameters.AddWithValue("@ColumnName", columnName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static string LayGiaTriHopLeTheoCheck(SqlConnection conn, SqlTransaction tran, string tableName, string columnName, params string[] priorities)
        {
            List<string> allowed = new();
            using (SqlCommand cmd = new(
                       @"SELECT cc.definition
                         FROM sys.check_constraints cc
                         JOIN sys.tables t ON cc.parent_object_id = t.object_id
                         WHERE t.name = @TableName AND cc.definition LIKE @ColumnName",
                       conn,
                       tran))
            {
                cmd.Parameters.AddWithValue("@TableName", tableName);
                cmd.Parameters.AddWithValue("@ColumnName", "%" + columnName + "%");
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string definition = reader[0]?.ToString() ?? string.Empty;
                    foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(definition, @"N?'((?:''|[^'])*)'"))
                    {
                        string value = match.Groups[1].Value.Replace("''", "'");
                        if (!allowed.Contains(value, StringComparer.OrdinalIgnoreCase))
                        {
                            allowed.Add(value);
                        }
                    }
                }
            }

            foreach (string priority in priorities)
            {
                string? match = allowed.FirstOrDefault(value => string.Equals(value, priority, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }
            }

            return allowed.Count > 0 ? allowed[0] : priorities[0];
        }

        private static decimal GetDecimal(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) &&
                   row[column] != DBNull.Value &&
                   decimal.TryParse(row[column]?.ToString(), out decimal value)
                ? value
                : 0;
        }

        private static int? GetNullableInt(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) &&
                   row[column] != DBNull.Value &&
                   int.TryParse(row[column]?.ToString(), out int value)
                ? value
                : null;
        }

        private static DateTime? GetNullableDate(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) &&
                   row[column] != DBNull.Value &&
                   DateTime.TryParse(row[column]?.ToString(), out DateTime value)
                ? value
                : null;
        }

        private static string BoDau(string value)
        {
            string formD = (value ?? string.Empty).Normalize(System.Text.NormalizationForm.FormD);
            string withoutMarks = new(formD
                .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray());
            return withoutMarks
                .Replace("đ", "d")
                .Replace("Đ", "D")
                .Replace("đ", "d")
                .Replace("Đ", "D")
                .Normalize(System.Text.NormalizationForm.FormC);
        }

        private record ThoiGianLuuTruResult(string Text, bool QuaGio);

        private record ThanhToanHoaDonItem(string KhoanMuc, decimal SoTien)
        {
            public string SoTienHienThi => SoTien < 0
                ? $"- {Math.Abs(SoTien):N0} VND"
                : $"{SoTien:N0} VND";
        }

        private class RoomStayData
        {
            public int? MaThue { get; set; }
            public int? MaDatPhong { get; set; }
            public string HoTen { get; set; } = string.Empty;
            public string SDT { get; set; } = string.Empty;
            public int SoNguoi { get; set; }
            public DateTime? NgayNhan { get; set; }
            public DateTime? NgayTra { get; set; }
            public decimal TienCoc { get; set; }
            public decimal TienPhong { get; set; }
            public string GhiChu { get; set; } = string.Empty;
            public string TrangThai { get; set; } = string.Empty;
            public int SoPhongDoan { get; set; } = 1;
            public string DanhSachPhongDoan { get; set; } = string.Empty;
        }

        private class DichVuChiTietItem
        {
            public int? MaPhatSinh { get; set; }
            public string Ten { get; set; } = string.Empty;
            public int SoLuong { get; set; }
            public decimal DonGia { get; set; }
            public decimal ThanhTien => SoLuong * DonGia;
        }
    }
}
