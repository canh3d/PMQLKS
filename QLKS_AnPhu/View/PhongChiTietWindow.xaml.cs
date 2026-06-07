using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Input;
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
        private decimal tienDichVuCheckIn;
        private decimal tienDichVuPhatSinh;
        private decimal phuPhi;
        private decimal giamGia;
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
            phuPhi = stayData?.PhuPhi ?? 0;
            datCoc = stayData?.TienCoc ?? 0;

            string tenPhong = $"Phòng {phong.MaHienThi}";
            Title = $"Thông tin chi tiết - {tenPhong}";
            TxtTieuDe.Text = $"Chi tiết phòng - {tenPhong}";
            TxtSoPhongHeader.Text = phong.MaHienThi;
            TxtNgayNhan.Text = stayData?.NgayNhanThucTe?.ToString("dd/MM/yyyy HH:mm") ?? string.Empty;
            TxtNgayNhanSidebar.Text = stayData?.NgayNhanDat?.ToString("dd/MM/yyyy HH:mm") ?? string.Empty;
            TxtNgayTra.Text = stayData?.NgayTraDat?.ToString("dd/MM/yyyy HH:mm") ?? string.Empty;
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
            BtnGiaHanPhong.IsEnabled = dangThue && stayData?.MaThue.HasValue == true;
            BtnDoiPhong.IsEnabled = stayData?.MaThue.HasValue == true || stayData?.MaDatPhong.HasValue == true;
            BtnDonDep.IsEnabled = canDonDep;
            BtnThanhToan.IsEnabled = dangThue || stayData != null;
            BtnLuu.IsEnabled = stayData != null;
            CboDichVuThem.IsEnabled = stayData?.MaThue.HasValue == true || stayData?.MaDatPhong.HasValue == true;
            TxtSoLuongDichVu.IsEnabled = CboDichVuThem.IsEnabled;
            BtnGiamSoLuongDichVu.IsEnabled = CboDichVuThem.IsEnabled;
            BtnTangSoLuongDichVu.IsEnabled = CboDichVuThem.IsEnabled;
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
            bool daNhanPhong = stayData?.MaThue.HasValue == true;
            decimal tienDichVuDaThanhToanCheckIn = daNhanPhong && stayData != null ? TinhTongDichVuDatTruoc(stayData) : 0;
            tienDichVuCheckIn = daNhanPhong ? 0 : dichVuHienTai.Where(item => item.LaDichVuCheckIn).Sum(item => item.ThanhTien);
            tienDichVuPhatSinh = dichVuHienTai.Where(item => !item.LaDichVuCheckIn).Sum(item => item.ThanhTien);
            tienDichVu = tienDichVuCheckIn + tienDichVuPhatSinh;
            phuPhi = stayData?.PhuPhi ?? 0;
            giamGia = LaKhachVip(stayData?.LoaiKhach) ? Math.Round((tienPhong + phuPhi) * 0.1m, 0) : 0;
            decimal vat = Math.Round(Math.Max(0, tienPhong + phuPhi - giamGia) * 0.1m, 0);
            decimal tienPhongHienThi = daNhanPhong ? 0 : tienPhong;
            decimal phuPhiHienThi = daNhanPhong ? 0 : phuPhi;
            decimal giamGiaHienThi = daNhanPhong ? 0 : giamGia;
            decimal vatHienThi = daNhanPhong ? 0 : vat;
            decimal tongThanhToan = tienPhongHienThi + tienDichVuCheckIn + tienDichVuPhatSinh + phuPhiHienThi + vatHienThi - giamGiaHienThi;
            decimal daThanhToanLucNhanPhong = daNhanPhong
                ? Math.Max(0, tienPhong + tienDichVuDaThanhToanCheckIn + phuPhi + vat - giamGia)
                : datCoc;
            decimal canThanhToanThem = daNhanPhong
                ? Math.Max(0, tienDichVuPhatSinh)
                : Math.Max(0, tongThanhToan - daThanhToanLucNhanPhong);
            string nhanSomText = TaoNhanPhuPhiNhanSom();

            TxtSoTienBanDau.Text = stayData == null
                ? "Phòng chưa có phiếu đặt/thuê đang hoạt động"
                : $"Đã thanh toán lúc nhận phòng: {daThanhToanLucNhanPhong:N0} VND";

            DgBangThanhToan.ItemsSource = new List<ThanhToanHoaDonItem>
            {
                new("Tiền phòng", tienPhong),
                new("Dịch vụ đặt trước/check-in", tienDichVuCheckIn),
                new("Dịch vụ phát sinh trong thời gian thuê", tienDichVuPhatSinh),
                new(nhanSomText, phuPhi),
                new("Gi\u1EA3m gi\u00E1", -giamGia),
                new("Thuế VAT (10%)", vat),
                new("Đã thanh toán lúc nhận phòng", -daThanhToanLucNhanPhong),
                new("Tổng tiền hóa đơn", tongThanhToan)
            };

            if (daNhanPhong)
            {
                DgBangThanhToan.ItemsSource = new List<ThanhToanHoaDonItem>
                {
                    new("Ti\u1EC1n ph\u00F2ng", tienPhongHienThi),
                    new("D\u1ECBch v\u1EE5 \u0111\u1EB7t tr\u01B0\u1EDBc/check-in", tienDichVuCheckIn),
                    new("D\u1ECBch v\u1EE5 ph\u00E1t sinh trong th\u1EDDi gian thu\u00EA", tienDichVuPhatSinh),
                    new(nhanSomText, phuPhiHienThi),
                    new("Gi\u1EA3m gi\u00E1", -giamGiaHienThi),
                    new("Thu\u1EBF VAT (10%)", vatHienThi),
                    new("T\u1ED5ng ti\u1EC1n h\u00F3a \u0111\u01A1n", tongThanhToan)
                };
            }

            TxtKhaDung.Text = stayData == null ? "--" : $"{canThanhToanThem:N0} VND";
        }

        private string TaoNhanPhuPhiNhanSom()
        {
            if (stayData?.NgayNhanThucTe is not DateTime ngayNhanThucTe ||
                stayData.NgayNhanDat is not DateTime ngayNhanDat ||
                ngayNhanThucTe >= ngayNhanDat)
            {
                return "Phụ phí nhận sớm";
            }

            TimeSpan somHon = ngayNhanDat - ngayNhanThucTe;
            int tongPhut = Math.Max(0, (int)Math.Round(somHon.TotalMinutes));
            int soGio = tongPhut / 60;
            int soPhut = tongPhut % 60;
            string thoiGian = soGio > 0 && soPhut > 0
                ? $"{soGio} giờ {soPhut} phút"
                : soGio > 0
                    ? $"{soGio} giờ"
                    : $"{soPhut} phút";
            return $"Phụ phí nhận sớm ({thoiGian})";
        }

        private static bool LaKhachVip(string? loaiKhach)
        {
            return !string.IsNullOrWhiteSpace(loaiKhach) &&
                   loaiKhach.Contains("VIP", StringComparison.OrdinalIgnoreCase);
        }

        private decimal TinhTongDichVuDatTruoc(RoomStayData data)
        {
            return LoadDichVuDatTruoc(data).Sum(item => item.ThanhTien);
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

            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists("DATPHONG") ? "DATPHONG" : string.Empty;
            bool coPhieuDatLienKet = !string.IsNullOrWhiteSpace(bangDatPhong) && ColumnExists("PHIEUTHUE", "MaDatPhong");
            string ngayNhanDatColumn = coPhieuDatLienKet && ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong";
            string ngayTraDatColumn = coPhieuDatLienKet && ColumnExists(bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong";
            string ngayNhanDatExpr = coPhieuDatLienKet ? "ISNULL(DP." + ngayNhanDatColumn + ", PT.NgayNhan)" : "PT.NgayNhan";
            string ngayTraDatExpr = coPhieuDatLienKet ? "ISNULL(DP." + ngayTraDatColumn + ", PT.NgayTraDuKien)" : "PT.NgayTraDuKien";
            string joinDatPhong = coPhieuDatLienKet ? "LEFT JOIN dbo." + bangDatPhong + " DP ON PT.MaDatPhong = DP.MaDatPhong" : string.Empty;
            string maDatPhongExpr = ColumnExists("PHIEUTHUE", "MaDatPhong") ? "PT.MaDatPhong" : "CAST(NULL AS int)";
            string soNguoiExpr = ColumnExists("PHIEUTHUE", "SoNguoi") ? "PT.SoNguoi" : "1";
            string ghiChuExpr = ColumnExists("PHIEUTHUE", "GhiChu") ? "PT.GhiChu" : "CAST(NULL AS nvarchar(1000))";
            string tienPhongExpr = PricingHelper.TienPhongSql(ngayNhanDatExpr, "PT.NgayTraDuKien", "PT.NgayTraDuKien");
            string phuPhiExpr = PricingHelper.PhuThuNhanSomSql("PT.NgayNhan", ngayNhanDatExpr);
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
       " + (ColumnExists("KHACHHANG", "LoaiKhach") ? "KH.LoaiKhach" : "CAST(N'' AS nvarchar(50))") + @" AS LoaiKhach,
       " + soNguoiExpr + @" AS SoNguoi,
       PT.NgayNhan AS NgayNhanThucTe,
       " + ngayNhanDatExpr + @" AS NgayNhanDat,
       " + ngayTraDatExpr + @" AS NgayTraDat,
       ISNULL(PT.TienCoc, 0) AS TienCoc,
       " + tienPhongExpr + @" AS TienPhong,
       " + phuPhiExpr + @" AS PhuPhi,
       " + ghiChuExpr + @" AS GhiChu,
       PT.TrangThai,
       " + soPhongDoanExpr + @" AS SoPhongDoan,
       " + danhSachPhongDoanExpr + @" AS DanhSachPhongDoan
FROM dbo.PHIEUTHUE PT
JOIN dbo.KHACHHANG KH ON PT.MaKH = KH.MaKH
" + joinDatPhong + @"
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
            string tienPhongExpr = PricingHelper.TienPhongSql(ngayNhanExpr, ngayTraExpr, ngayTraExpr);
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
       " + (ColumnExists("KHACHHANG", "LoaiKhach") ? "KH.LoaiKhach" : "CAST(N'' AS nvarchar(50))") + @" AS LoaiKhach,
       " + soNguoiExpr + @" AS SoNguoi,
       CAST(NULL AS datetime) AS NgayNhanThucTe,
       " + ngayNhanExpr + @" AS NgayNhanDat,
       " + ngayTraExpr + @" AS NgayTraDat,
       ISNULL(" + tienCocExpr + @", 0) AS TienCoc,
       " + tienPhongExpr + @" AS TienPhong,
       CAST(0 AS decimal(18,2)) AS PhuPhi,
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
            string psKey = GetFirstExistingColumn(bangPhatSinh, "MaPhatSinh", "MaCTPhatSinh", "MaCTPS", "MaChiTiet", "ID", "Ma");
            string psKeyExpr = string.IsNullOrWhiteSpace(psKey) ? "CAST(NULL AS int)" : "PS." + psKey;
            string soLuongExpr = ColumnExists(bangPhatSinh, "SoLuong") ? "PS.SoLuong" : "1";
            string donGiaExpr = ColumnExists(bangPhatSinh, "DonGia") ? "ISNULL(PS.DonGia, DV.DonGia)" : "DV.DonGia";
            string thanhTienExpr = ColumnExists(bangPhatSinh, "ThanhTien") ? "PS.ThanhTien" : "(" + soLuongExpr + " * " + donGiaExpr + ")";
            string ghiChuExpr = ColumnExists(bangPhatSinh, "GhiChu") ? "ISNULL(PS.GhiChu, N'')" : "N''";
            string roomFilter = keyColumn != "MaPhong" && ColumnExists(bangPhatSinh, "MaPhong")
                ? " AND PS.MaPhong = @MaPhong"
                : string.Empty;
            string checkInFilter = data.MaThue.HasValue && ColumnExists(bangPhatSinh, "GhiChu")
                ? ViewSchemaHelper.DichVuTheoLoaiHoaDonFilter("PS", "PHATSINH")
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
       " + thanhTienExpr + @" AS ThanhTien,
       " + ghiChuExpr + @" AS GhiChu
FROM dbo." + bangPhatSinh + @" PS
JOIN dbo.DICHVUVATTU DV ON PS." + maDvPs + " = DV." + maDv + @"
WHERE " + keyFilter + roomFilter + checkInFilter,
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
                    DonGia = GetDecimal(row, "DonGia"),
                    LaDichVuCheckIn = (row["GhiChu"]?.ToString() ?? string.Empty).Contains("[DICHVU_CHECKIN]", StringComparison.OrdinalIgnoreCase)
                });
            }

            if (!data.MaThue.HasValue)
            {
                foreach (DichVuChiTietItem item in LoadDichVuDatTruoc(data))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private ObservableCollection<DichVuChiTietItem> LoadDichVuDatTruoc(RoomStayData data)
        {
            ObservableCollection<DichVuChiTietItem> result = new();
            if (!data.MaDatPhong.HasValue ||
                !TableExists("CHITIETDATPHONG") ||
                !ColumnExists("CHITIETDATPHONG", "GhiChu") ||
                !TableExists("DICHVUVATTU"))
            {
                return result;
            }

            string maDv = ColumnExists("DICHVUVATTU", "MaDVVT") ? "MaDVVT" : "MaDichVu";
            string tenDv = ColumnExists("DICHVUVATTU", "TenDVVT") ? "TenDVVT" : "TenDichVu";
            DataTable chiTiet = ConnectDB.GetData(
                "SELECT GhiChu FROM dbo.CHITIETDATPHONG WHERE MaDatPhong = @MaDatPhong AND GhiChu LIKE @Marker",
                new SqlParameter("@MaDatPhong", data.MaDatPhong.Value),
                new SqlParameter("@Marker", "%[DICHVU_DAT]%"));

            foreach (DataRow row in chiTiet.Rows)
            {
                foreach (DichVuDatPhongDTO dichVu in DocMarkerDichVuDatTruoc(row["GhiChu"]?.ToString() ?? string.Empty))
                {
                    DataTable ten = ConnectDB.GetData(
                        "SELECT TOP 1 " + tenDv + " AS Ten FROM dbo.DICHVUVATTU WHERE " + maDv + " = @Ma",
                        new SqlParameter("@Ma", dichVu.Ma));
                    result.Add(new DichVuChiTietItem
                    {
                        Ten = ten.Rows.Count > 0 ? ten.Rows[0]["Ten"]?.ToString() ?? string.Empty : "Dich vu " + dichVu.Ma,
                        SoLuong = dichVu.SoLuong,
                        DonGia = dichVu.DonGia,
                        LaDichVuCheckIn = true
                    });
                }
            }

            return result;
        }

        private static List<DichVuDatPhongDTO> DocMarkerDichVuDatTruoc(string ghiChu)
        {
            const string marker = "[DICHVU_DAT]";
            int markerIndex = ghiChu.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return new List<DichVuDatPhongDTO>();
            }

            string payload = ghiChu[(markerIndex + marker.Length)..].Trim();
            int stopIndex = payload.IndexOf(" - ", StringComparison.Ordinal);
            if (stopIndex >= 0)
            {
                payload = payload[..stopIndex];
            }

            List<DichVuDatPhongDTO> result = new();
            foreach (string token in payload.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = token.Split('|');
                if (parts.Length >= 3 &&
                    int.TryParse(parts[0], out int ma) &&
                    int.TryParse(parts[1], out int soLuong) &&
                    decimal.TryParse(parts[2], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal donGia) &&
                    soLuong > 0)
                {
                    result.Add(new DichVuDatPhongDTO { Ma = ma, SoLuong = soLuong, DonGia = donGia });
                }
            }

            return result;
        }

        private static RoomStayData MapStay(DataRow row, bool laPhieuThue)
        {
            string ghiChu = row["GhiChu"]?.ToString() ?? string.Empty;
            decimal tienPhongTinhLai = GetDecimal(row, "TienPhong");
            decimal tienPhongDaChot = DocTienPhongDaChot(ghiChu);

            return new RoomStayData
            {
                MaThue = laPhieuThue ? GetNullableInt(row, "MaThue") : null,
                MaDatPhong = GetNullableInt(row, "MaDatPhong"),
                HoTen = row["HoTen"]?.ToString() ?? string.Empty,
                SDT = row["SDT"]?.ToString() ?? string.Empty,
                LoaiKhach = row["LoaiKhach"]?.ToString() ?? string.Empty,
                SoNguoi = Convert.ToInt32(GetDecimal(row, "SoNguoi")),
                NgayNhanThucTe = GetNullableDate(row, "NgayNhanThucTe"),
                NgayNhanDat = GetNullableDate(row, "NgayNhanDat"),
                NgayTraDat = GetNullableDate(row, "NgayTraDat"),
                TienCoc = GetDecimal(row, "TienCoc"),
                TienPhong = tienPhongDaChot > 0 ? Math.Max(tienPhongDaChot, tienPhongTinhLai) : tienPhongTinhLai,
                PhuPhi = GetDecimal(row, "PhuPhi"),
                GhiChu = ghiChu,
                TrangThai = row["TrangThai"]?.ToString() ?? string.Empty,
                SoPhongDoan = Convert.ToInt32(GetDecimal(row, "SoPhongDoan")),
                DanhSachPhongDoan = row["DanhSachPhongDoan"]?.ToString() ?? string.Empty
            };
        }

        private static decimal DocTienPhongDaChot(string ghiChu)
        {
            return ViewSchemaHelper.DocTienPhongDaChot(ghiChu);
        }

        private void CapNhatThoiGianLuuTru()
        {
            ThoiGianLuuTruResult result = TinhThoiGianLuuTru(
                stayData?.NgayNhanThucTe,
                stayData?.NgayTraDat,
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
                    decimal giamGia = stayData.LoaiKhach.Contains("VIP", StringComparison.OrdinalIgnoreCase) ? Math.Round((tienPhong + phuPhi) * 0.1m, 0) : 0;
                    if (!DialogService.XacNhanThanhToanCheckIn(this, "Phòng " + phong.MaHienThi, tienPhong, tienDichVu, phuPhi, datCoc, giamGia))
                    {
                        return;
                    }
                    phongBUS.NhanPhongTuDatPhong(
                        stayData.MaDatPhong.Value,
                        Math.Max(0, tienPhong + tienDichVu + phuPhi - giamGia),
                        datCoc,
                        Math.Max(0, tienPhong + phuPhi - giamGia));
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

                decimal tongHoaDon = tienPhong + tienDichVu + phuPhi;
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

        private void BtnHuyDat_Click(object sender, RoutedEventArgs e)
        {
            if (stayData is not { MaDatPhong: int maDatPhong } || stayData.MaThue.HasValue)
            {
                MessageBox.Show("Phong nay khong co phieu dat dang cho nhan phong de huy.", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Xac nhan huy dat phong nay?", "Huy dat", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                phongBUS.NoShow(maDatPhong);
                MessageBox.Show("Da huy dat phong va chuyen phong ve trang thai trong.", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Information);
                stayData = null;
                PhongChiTietWindow_Loaded(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Khong the huy dat phong: " + ex.Message, "Loi", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void BtnGiaHanPhong_Click(object sender, RoutedEventArgs e)
        {
            if (stayData is not { MaThue: int maThue, NgayTraDat: DateTime ngayTraDat })
            {
                MessageBox.Show("Phòng này chưa có phiếu thuê đang hoạt động để gia hạn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GiaHanPhongWindow window = new(
                new GiaHanPhongRequestDTO
                {
                    MaThue = maThue,
                    MaDatPhong = stayData.MaDatPhong,
                    MaPhong = phong.Ma,
                    NgayTraCu = ngayTraDat,
                    NgayTraMoi = ngayTraDat
                },
                phong.MaHienThi);

            DialogService.ShowDimmedDialogResult(window, this);
            if (window.DuLieuDaThayDoi)
            {
                PhongChiTietWindow_Loaded(sender, e);
            }
        }

        private void BtnDoiPhong_Click(object sender, RoutedEventArgs e)
        {
            if (stayData is not { NgayTraDat: DateTime ngayTraDat } ||
                (!stayData.MaThue.HasValue && !stayData.MaDatPhong.HasValue))
            {
                MessageBox.Show("Phòng này chưa có phiếu đặt hoặc phiếu thuê đang hoạt động để đổi phòng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DoiPhongWindow window = new(
                new DoiPhongRequestDTO
                {
                    MaThue = stayData.MaThue.GetValueOrDefault(),
                    MaDatPhong = stayData.MaDatPhong,
                    MaPhongCu = phong.Ma,
                    NgayBatDau = stayData.MaThue.HasValue ? DateTime.Now : stayData.NgayNhanDat ?? DateTime.Now,
                    NgayTraDuKien = ngayTraDat
                },
                phong.MaHienThi);

            DialogService.ShowDimmedDialogResult(window, this);
            if (window.DuLieuDaThayDoi)
            {
                Close();
            }
        }

        private void BtnThanhToan_Click(object sender, RoutedEventArgs e)
        {
            if (stayData is not { MaThue: int maThue })
            {
                MessageBox.Show("Chi co the thanh toan khi phong dang co phieu thue.", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Xac nhan thanh toan va tra phong?", "Thanh toan", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                KetQuaCheckOutThanhToanDTO result = thanhToanBUS.CheckOut(maThue);
                MessageBox.Show($"Da thanh toan hoa don {result.MaHoaDon}. So tien thu them: {result.TienThuThem:N0} VND.", "Thanh toan", MessageBoxButton.OK, MessageBoxImage.Information);
                PhongChiTietWindow_Loaded(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Khong the thanh toan: " + ex.Message, "Loi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLuu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                stayData = LoadRoomStayData();
                dichVuHienTai = LoadDichVu(stayData);
                DgDichVu.ItemsSource = dichVuHienTai;
                CapNhatHoaDon();
                CapNhatThoiGianLuuTru();
                CapNhatTrangThaiNut();
                MessageBox.Show("Da luu va cap nhat du lieu hien thi.", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Khong the luu du lieu: " + ex.Message, "Loi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnInHoaDon_Click(object sender, RoutedEventArgs e)
        {
            if (stayData == null)
            {
                MessageBox.Show("Phong nay chua co du lieu hoa don de in.", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            HoaDonPrintWindow window = new(TaoHoaDonTam())
            {
                Owner = this
            };
            window.ShowDialog();
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
            if (stayData == null || (!stayData.MaThue.HasValue && !stayData.MaDatPhong.HasValue))
            {
                MessageBox.Show("Phòng chưa có phiếu đặt hoặc phiếu thuê để thêm dịch vụ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                try
                {
                    if (stayData.MaThue.HasValue)
                    {
                        thanhToanBUS.CongDichVuPhatSinh(stayData.MaThue.Value, dichVu.DonGia * soLuong);
                    }
                }
                catch
                {
                    // Hoa don se duoc tinh lai tu dich vu phat sinh khi thanh toan/check-out.
                }

                dichVuHienTai = LoadDichVu(stayData);
                DgDichVu.ItemsSource = dichVuHienTai;
                CapNhatHoaDon();
                TxtSoLuongDichVu.Text = "1";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể thêm dịch vụ: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnGiamSoLuongDichVu_Click(object sender, RoutedEventArgs e)
        {
            TxtSoLuongDichVu.Text = Math.Max(1, LaySoLuongDichVuNhap() - 1).ToString();
            TxtSoLuongDichVu.CaretIndex = TxtSoLuongDichVu.Text.Length;
        }

        private void BtnTangSoLuongDichVu_Click(object sender, RoutedEventArgs e)
        {
            TxtSoLuongDichVu.Text = Math.Min(999, LaySoLuongDichVuNhap() + 1).ToString();
            TxtSoLuongDichVu.CaretIndex = TxtSoLuongDichVu.Text.Length;
        }

        private void TxtSoLuongDichVu_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void TxtSoLuongDichVu_LostFocus(object sender, RoutedEventArgs e)
        {
            TxtSoLuongDichVu.Text = LaySoLuongDichVuNhap().ToString();
        }

        private int LaySoLuongDichVuNhap()
        {
            return int.TryParse(TxtSoLuongDichVu.Text.Trim(), out int soLuong) && soLuong > 0
                ? Math.Min(999, soLuong)
                : 1;
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

            if (item.LaDichVuCheckIn)
            {
                MessageBox.Show("Dịch vụ này thuộc hóa đơn check-in, không xóa ở phần phát sinh.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (!item.MaPhatSinh.HasValue)
                {
                    throw new InvalidOperationException("Không xác định được khóa dịch vụ phát sinh để xóa khỏi database.");
                }

                XoaDichVuPhatSinh(item.MaPhatSinh.Value);
                dichVuHienTai = LoadDichVu(stayData);
                DgDichVu.ItemsSource = dichVuHienTai;
                CapNhatHoaDon();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể xóa dịch vụ: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private HoaDonItem TaoHoaDonTam()
        {
            bool daNhanPhong = stayData?.MaThue.HasValue == true;
            decimal tienPhongHoaDonTam = daNhanPhong ? 0 : tienPhong;
            decimal tienDichVuHoaDonTam = daNhanPhong ? tienDichVuPhatSinh : tienDichVu;
            decimal phuPhiHoaDonTam = daNhanPhong ? 0 : phuPhi;
            decimal giamGiaHoaDonTam = !daNhanPhong && LaKhachVip(stayData?.LoaiKhach) ? Math.Round((tienPhong + phuPhiHoaDonTam) * 0.1m, 0) : 0;
            decimal vat = Math.Round(Math.Max(0, tienPhongHoaDonTam + phuPhiHoaDonTam - giamGiaHoaDonTam) * 0.1m, 0);
            int maGoc = stayData?.MaThue ?? stayData?.MaDatPhong ?? phong.Ma;
            return new HoaDonItem
            {
                LoaiPhieu = stayData?.MaThue.HasValue == true ? "THUE" : "DAT",
                MaGoc = maGoc,
                MaHoaDon = "HD-TAM-" + maGoc,
                MaPhieuThue = stayData?.MaThue.HasValue == true ? "PT-" + maGoc : "DP-" + maGoc,
                TenKhachHang = stayData?.HoTen ?? string.Empty,
                SoDienThoai = stayData?.SDT ?? string.Empty,
                SoPhong = phong.MaHienThi,
                LoaiPhong = phong.LoaiPhong,
                NgayNhanPhong = stayData?.NgayNhanThucTe ?? stayData?.NgayNhanDat ?? DateTime.Now,
                NgayTraPhong = stayData?.NgayTraDat ?? DateTime.Now,
                NgayLapHoaDon = DateTime.Now,
                TienPhong = tienPhongHoaDonTam,
                TienDichVu = tienDichVuHoaDonTam,
                PhuPhi = phuPhiHoaDonTam,
                ThueVat = vat,
                GiamGia = daNhanPhong ? 0 : datCoc + giamGiaHoaDonTam,
                TrangThai = stayData?.MaThue.HasValue == true ? "Chua thanh toan" : "Tam tinh"
            };
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

            if (ColumnExists(bangPhatSinh, "GhiChu"))
            {
                values["GhiChu"] = "[DICHVU_PHATSINH]";
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
                : GetFirstExistingColumn(bangPhatSinh, "MaPhatSinh", "MaCTPhatSinh", "MaCTPS", "MaChiTiet", "ID", "Ma");

            if (string.IsNullOrWhiteSpace(bangPhatSinh) || string.IsNullOrWhiteSpace(keyColumn))
            {
                throw new InvalidOperationException("Không xác định được dòng dịch vụ phát sinh để xóa.");
            }

            int affected = ConnectDB.ExecuteNonQuery(
                $"DELETE FROM dbo.{bangPhatSinh} WHERE [{keyColumn}] = @MaPhatSinh",
                new SqlParameter("@MaPhatSinh", maPhatSinh));
            if (affected <= 0)
            {
                throw new InvalidOperationException("Dịch vụ phát sinh không còn tồn tại hoặc chưa được xóa khỏi database.");
            }
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
            return ViewSchemaHelper.TenPhongSql(alias);
        }

        private static bool TableExists(string tableName)
        {
            return ViewSchemaHelper.TableExists(tableName);
        }

        private static bool TableExists(SqlConnection conn, SqlTransaction tran, string tableName)
        {
            using SqlCommand cmd = new("SELECT COUNT(*) FROM sys.tables WHERE name = @Name", conn, tran);
            cmd.Parameters.AddWithValue("@Name", tableName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static bool ColumnExists(string tableName, string columnName)
        {
            return ViewSchemaHelper.ColumnExists(tableName, columnName);
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
            return ViewSchemaHelper.GetFirstExistingColumn(tableName, columnNames);
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
            public string LoaiKhach { get; set; } = string.Empty;
            public int SoNguoi { get; set; }
            public DateTime? NgayNhanThucTe { get; set; }
            public DateTime? NgayNhanDat { get; set; }
            public DateTime? NgayTraDat { get; set; }
            public decimal TienCoc { get; set; }
            public decimal TienPhong { get; set; }
            public decimal PhuPhi { get; set; }
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
            public bool LaDichVuCheckIn { get; set; }
            public decimal ThanhTien => SoLuong * DonGia;
        }
    }
}
