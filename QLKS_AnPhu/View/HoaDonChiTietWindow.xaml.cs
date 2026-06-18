using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DAL;
using QLKS_AnPhu.DTO;
using QLKS_AnPhu.Security;

namespace QLKS_AnPhu.View
{
    public partial class HoaDonChiTietWindow : Window
    {
        private readonly HoaDonItem hoaDon;
        public bool DuLieuDaThayDoi { get; private set; }

        public HoaDonChiTietWindow(HoaDonItem hoaDon)
        {
            this.hoaDon = hoaDon;
            InitializeComponent();
            Loaded += HoaDonChiTietWindow_Loaded;
        }

        private void HoaDonChiTietWindow_Loaded(object sender, RoutedEventArgs e)
        {
            HienThiHoaDon();
        }

        private void HienThiHoaDon()
        {
            bool daThanhToan = HoaDonDaThanhToan();
            bool laHoaDonHuyGiuCoc = LaHoaDonDaHuyGiuCoc();
            TxtTieuDeHoaDon.Text = laHoaDonHuyGiuCoc
                ? "Hóa đơn hủy đặt phòng"
                : hoaDon.LoaiThanhToan == "PHATSINH"
                    ? "Hóa đơn phát sinh"
                    : "Hóa đơn nhận phòng";
            TxtMaHoaDon.Text = "Mã hóa đơn: " + hoaDon.MaHoaDon;
            TxtNgayLap.Text = "Ngày lập: " + hoaDon.NgayLapHoaDon.ToString("dd/MM/yyyy HH:mm");
            TxtTrangThai.Text = "Trạng thái: " + (laHoaDonHuyGiuCoc ? "Hủy - giữ cọc" : hoaDon.TrangThai);
            TxtKhachHang.Text = "Khách hàng: " + hoaDon.TenKhachHang;
            TxtSdt.Text = "SĐT: " + hoaDon.SoDienThoai;
            TxtPhong.Text = "Phòng: " + hoaDon.SoPhong;
            TxtLoaiPhong.Text = "Loại phòng: " + hoaDon.LoaiPhong;
            TxtThoiGianThue.Text = "Thời gian: " + hoaDon.NgayNhanPhong.ToString("dd/MM/yyyy HH:mm") + " - " + hoaDon.NgayTraPhong.ToString("dd/MM/yyyy HH:mm");
            TxtThoiLuong.Text = "Thời lượng: " + ThoiLuongThueHelper.DinhDang(
                hoaDon.NgayNhanPhong,
                hoaDon.NgayTraPhong,
                hoaDon.CheDoDatPhong,
                hoaDon.LoaiThanhToan == "PHATSINH" ? hoaDon.TienPhongCheckIn : hoaDon.TienPhong,
                hoaDon.GiaGioTinhPhi);

            ObservableCollection<ChiTietThanhToanHoaDonItem> chiTiet = TaoChiTietThanhToan();
            DgChiTietThanhToan.ItemsSource = chiTiet;

            decimal tamTinh = chiTiet.Where(item => item.ThanhTien > 0).Sum(item => item.ThanhTien);
            decimal giamGiaCoc = chiTiet.Where(item => item.ThanhTien < 0).Sum(item => item.ThanhTien);
            decimal canThanhToan = Math.Max(0, tamTinh + giamGiaCoc);
            TxtTamTinh.Text = tamTinh.ToString("N0") + " VND";
            TxtGiamGiaCoc.Text = giamGiaCoc == 0 ? "0 VND" : giamGiaCoc.ToString("N0") + " VND";
            if (laHoaDonHuyGiuCoc)
            {
                TxtDaThanhToan.Text = canThanhToan.ToString("N0") + " VND";
                TxtTongTienLabel.Text = "Đã giữ cọc";
                TxtTongTien.Text = canThanhToan.ToString("N0") + " VND";
            }
            else
            {
                TxtDaThanhToan.Text = daThanhToan ? "Đã thanh toán" : "0 VND";
                TxtTongTienLabel.Text = daThanhToan ? "Đã thanh toán" : "Cần thanh toán";
                TxtTongTien.Text = daThanhToan ? "Đã thanh toán" : canThanhToan.ToString("N0") + " VND";
            }
            TxtGhiChuHoaDon.Text = TaoGhiChuHoaDon();
            BtnThanhToan.IsEnabled = !daThanhToan && !laHoaDonHuyGiuCoc && hoaDon.LoaiPhieu == "THUE";
            BtnThanhToan.Visibility = daThanhToan || laHoaDonHuyGiuCoc ? Visibility.Collapsed : Visibility.Visible;
        }

        private ObservableCollection<ChiTietThanhToanHoaDonItem> TaoChiTietThanhToan()
        {
            if (LaHoaDonDaHuyGiuCoc())
            {
                return TaoChiTietHuyGiuCoc();
            }

            if (hoaDon.LoaiThanhToan == "CHECKIN")
            {
                return TaoChiTietThanhToanCheckIn();
            }

            ObservableCollection<ChiTietThanhToanHoaDonItem> result = new();
            int stt = 1;
            ThongTinCheckInBoSung boSung = new();
            decimal tienDichVuHoaDon = hoaDon.TienDichVu;
            decimal phuPhiHoaDon = hoaDon.PhuPhiHienThi;
            decimal giamGiaHoaDon = hoaDon.GiamGiaHienThi;
            decimal thueVatHoaDon = hoaDon.ThueVatHienThi;

            if (hoaDon.TienPhong != 0)
            {
                result.Add(new ChiTietThanhToanHoaDonItem
                {
                    Stt = stt++,
                    Ten = hoaDon.LoaiThanhToan == "PHATSINH"
                        ? hoaDon.TienPhong < 0
                            ? "Hoàn chênh lệch đổi xuống phòng giá thấp hơn"
                            : "Gia hạn / chênh lệch đổi phòng"
                        : "Tiền phòng lúc check-in",
                    SoPhong = hoaDon.SoPhong,
                    DonGia = Math.Abs(hoaDon.TienPhong),
                    SoLuong = 1,
                    ThanhTien = hoaDon.TienPhong
                });
            }

            decimal tongDichVuDaNap = 0;
            ObservableCollection<DichVuHoaDonItem> dichVuChiTiet = hoaDon.LoaiThanhToan == "CHECKIN"
                ? LoadDichVuCheckIn(boSung)
                : LoadDichVu();
            foreach (DichVuHoaDonItem dichVu in dichVuChiTiet)
            {
                result.Add(new ChiTietThanhToanHoaDonItem
                {
                    Stt = stt++,
                    Ten = dichVu.TenDichVu,
                    SoPhong = hoaDon.SoPhong,
                    DonGia = dichVu.DonGia,
                    SoLuong = dichVu.SoLuong,
                    ThanhTien = dichVu.ThanhTien
                });
                tongDichVuDaNap += dichVu.ThanhTien;
            }

            decimal dichVuChuaCoChiTiet = Math.Max(0, tienDichVuHoaDon - tongDichVuDaNap);
            if (dichVuChuaCoChiTiet > 0)
            {
                result.Add(new ChiTietThanhToanHoaDonItem
                {
                    Stt = stt++,
                    Ten = hoaDon.LoaiThanhToan == "PHATSINH" ? "Dịch vụ phát sinh trong thời gian thuê" : "Dịch vụ tại check-in",
                    SoPhong = hoaDon.SoPhong,
                    DonGia = dichVuChuaCoChiTiet,
                    SoLuong = 1,
                    ThanhTien = dichVuChuaCoChiTiet
                });
            }

            if (phuPhiHoaDon > 0)
            {
                string tenPhuPhi = hoaDon.LoaiThanhToan == "PHATSINH"
                    ? "Phụ phí trả muộn - " + TaoNoiDungTraMuon(hoaDon)
                    : boSung.PhuPhiNhanSom > 0 && !string.IsNullOrWhiteSpace(boSung.MoTaPhuPhiNhanSom)
                        ? boSung.MoTaPhuPhiNhanSom
                        : "Phụ phí nhận sớm - " + TaoNoiDungNhanSom(hoaDon.NgayNhanThucTe, hoaDon.NgayNhanPhong, phuPhiHoaDon);
                result.Add(new ChiTietThanhToanHoaDonItem
                {
                    Stt = stt++,
                    Ten = tenPhuPhi,
                    SoPhong = hoaDon.SoPhong,
                    ThanhTien = phuPhiHoaDon
                });
            }

            if (giamGiaHoaDon > 0)
            {
                result.Add(new ChiTietThanhToanHoaDonItem
                {
                    Stt = stt++,
                    Ten = "Giảm giá",
                    SoPhong = hoaDon.SoPhong,
                    ThanhTien = -giamGiaHoaDon
                });
            }

            if (thueVatHoaDon > 0)
            {
                result.Add(new ChiTietThanhToanHoaDonItem
                {
                    Stt = stt++,
                    Ten = "Thuế VAT (10%)",
                    SoPhong = hoaDon.SoPhong,
                    ThanhTien = thueVatHoaDon
                });
            }

            if (boSung.TienCoc > 0)
            {
                result.Add(new ChiTietThanhToanHoaDonItem
                {
                    Stt = stt++,
                    Ten = "Đã đặt cọc",
                    SoPhong = hoaDon.SoPhong,
                    ThanhTien = -boSung.TienCoc
                });
            }
            else if (hoaDon.LoaiThanhToan != "PHATSINH" && hoaDon.TienCoc > 0)
            {
                result.Add(new ChiTietThanhToanHoaDonItem
                {
                    Stt = stt++,
                    Ten = "Đã đặt cọc",
                    SoPhong = hoaDon.SoPhong,
                    ThanhTien = -hoaDon.TienCoc
                });
            }

            return result;
        }

        private ObservableCollection<ChiTietThanhToanHoaDonItem> TaoChiTietThanhToanCheckIn()
        {
            ObservableCollection<ChiTietThanhToanHoaDonItem> result = new();
            ThongTinCheckInBoSung boSung = LoadThongTinCheckInBoSung();

            if (LaHoaDonDaHuyGiuCoc())
            {
                return TaoChiTietHuyGiuCoc(boSung.TienCoc);
            }

            decimal tienPhong = hoaDon.TienPhong;
            decimal tienDichVu = Math.Max(hoaDon.TienDichVu, boSung.TienDichVuCheckIn);
            if (hoaDon.LoaiPhieu == "THUE")
            {
                tienDichVu = Math.Max(tienDichVu, HoaDon.LayTienDichVuCheckIn(hoaDon.MaGoc));
                tienDichVu = Math.Max(tienDichVu, HoaDon.LayTienDichVuDatTruocTheoThue(hoaDon.MaGoc));
            }

            decimal phuPhi = hoaDon.PhuPhiHienThi;
            decimal giamGia = hoaDon.GiamGiaHienThi;
            decimal thueVat = hoaDon.ThueVatHienThi;
            decimal tienCoc = Math.Max(hoaDon.TienCoc, boSung.TienCoc);
            string tenPhuPhi = string.IsNullOrWhiteSpace(boSung.MoTaPhuPhiNhanSom)
                ? "Phụ phí nhận sớm"
                : boSung.MoTaPhuPhiNhanSom;

            int stt = 1;
            result.Add(new ChiTietThanhToanHoaDonItem
            {
                Stt = stt++,
                Ten = "Tiền phòng",
                SoPhong = hoaDon.SoPhong,
                DonGia = tienPhong,
                SoLuong = 1,
                ThanhTien = tienPhong
            });
            result.Add(new ChiTietThanhToanHoaDonItem
            {
                Stt = stt++,
                Ten = "Dịch vụ tại check-in",
                SoPhong = hoaDon.SoPhong,
                ThanhTien = tienDichVu
            });
            result.Add(new ChiTietThanhToanHoaDonItem
            {
                Stt = stt++,
                Ten = tenPhuPhi,
                SoPhong = hoaDon.SoPhong,
                ThanhTien = phuPhi
            });
            result.Add(new ChiTietThanhToanHoaDonItem
            {
                Stt = stt++,
                Ten = "Giảm giá",
                SoPhong = hoaDon.SoPhong,
                ThanhTien = -giamGia
            });
            result.Add(new ChiTietThanhToanHoaDonItem
            {
                Stt = stt++,
                Ten = "Thuế VAT (10%)",
                SoPhong = hoaDon.SoPhong,
                ThanhTien = thueVat
            });
            result.Add(new ChiTietThanhToanHoaDonItem
            {
                Stt = stt,
                Ten = "Đã đặt cọc",
                SoPhong = hoaDon.SoPhong,
                ThanhTien = -tienCoc
            });

            return result;
        }

        private bool LaPhieuDatDaHuyGiuCoc()
        {
            return hoaDon.LaPhieuDatDaHuyGiuCoc;
        }

        private ObservableCollection<ChiTietThanhToanHoaDonItem> TaoChiTietHuyGiuCoc(decimal tienCocBoSung = 0)
        {
            decimal tienCocGiu = Math.Max(hoaDon.TienCoc, tienCocBoSung);
            return new ObservableCollection<ChiTietThanhToanHoaDonItem>
            {
                new()
                {
                    Stt = 1,
                    Ten = "Giữ tiền cọc do hủy đặt phòng",
                    SoPhong = hoaDon.SoPhong,
                    DonGia = tienCocGiu,
                    SoLuong = 1,
                    ThanhTien = tienCocGiu
                }
            };
        }

        private bool LaHoaDonDaHuyGiuCoc()
        {
            if (LaPhieuDatDaHuyGiuCoc())
            {
                return true;
            }

            if (hoaDon.LoaiPhieu == "THUE" && TableExists("PHIEUTHUE") && ColumnExists("PHIEUTHUE", "TrangThai"))
            {
                object? value = ConnectDB.ExecuteScalar(
                    "SELECT TOP 1 TrangThai FROM dbo.PHIEUTHUE WHERE MaThue = @Ma",
                    new SqlParameter("@Ma", hoaDon.MaGoc));
                return LaTrangThaiHuyGiuCoc(value?.ToString() ?? string.Empty);
            }

            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists("DATPHONG") ? "DATPHONG" : string.Empty;
            if (hoaDon.LoaiPhieu == "DAT" && !string.IsNullOrWhiteSpace(bangDatPhong) && ColumnExists(bangDatPhong, "TrangThai"))
            {
                object? value = ConnectDB.ExecuteScalar(
                    "SELECT TOP 1 TrangThai FROM dbo." + bangDatPhong + " WHERE MaDatPhong = @Ma",
                    new SqlParameter("@Ma", hoaDon.MaGoc));
                return LaTrangThaiHuyGiuCoc(value?.ToString() ?? string.Empty);
            }

            return false;
        }

        private static bool LaTrangThaiHuyGiuCoc(string trangThai)
        {
            if (string.IsNullOrWhiteSpace(trangThai))
            {
                return false;
            }

            string normalized = BoDau(trangThai).ToLowerInvariant();
            return normalized.Contains("huy") ||
                   normalized.Contains("giu coc") ||
                   normalized.Contains("no-show") ||
                   normalized.Contains("no show") ||
                   normalized.Contains("khach khong den");
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
                .Normalize(System.Text.NormalizationForm.FormC);
        }

        private void ApDungSoTienCheckInDaChot(ThongTinCheckInBoSung boSung)
        {
            if (hoaDon.LoaiThanhToan != "CHECKIN" || !HoaDonDaThanhToan())
            {
                return;
            }

            decimal dichVuDaChot = Math.Max(hoaDon.TienDichVu, boSung.TienDichVuCheckIn);
            decimal tongDaThu = LayTongThanhToanCheckInDaThu(hoaDon.MaGoc, boSung.MaDatPhong);
            if (tongDaThu <= 0)
            {
                return;
            }

            decimal giamGia = hoaDon.GiamGia;
            decimal coSoTinhThue = Math.Max(0, (tongDaThu - dichVuDaChot + boSung.TienCoc + (giamGia * 1.1m)) / 1.1m);
            decimal phuPhiDaChot = Math.Max(0, Math.Round(coSoTinhThue - hoaDon.TienPhong, 0));
            decimal thueVatDaChot = Math.Round(Math.Max(0, hoaDon.TienPhong + phuPhiDaChot - giamGia) * 0.1m, 0);

            boSung.PhuPhiNhanSom = phuPhiDaChot;
            boSung.ThueVatDaChot = thueVatDaChot;
            if (phuPhiDaChot > 0 && string.IsNullOrWhiteSpace(boSung.MoTaPhuPhiNhanSom))
            {
                boSung.MoTaPhuPhiNhanSom = "Phụ phí nhận sớm";
            }
        }

        private ObservableCollection<DichVuHoaDonItem> LoadDichVuCheckIn(ThongTinCheckInBoSung boSung)
        {
            ObservableCollection<DichVuHoaDonItem> result = new();
            foreach (DichVuHoaDonItem item in LoadDichVu())
            {
                result.Add(item);
            }

            if (result.Count > 0)
            {
                return result;
            }

            if (boSung.MaDatPhong.HasValue)
            {
                foreach (DichVuDatPhongMarker item in LoadDichVuDatPhongMarker(boSung.MaDatPhong.Value, boSung.MaPhong))
                {
                    result.Add(new DichVuHoaDonItem
                    {
                        Stt = result.Count + 1,
                        TenDichVu = "Dịch vụ tại check-in",
                        SoLuong = item.SoLuong,
                        DonGia = item.DonGia,
                        ThanhTien = item.SoLuong * item.DonGia
                    });
                }
            }

            return result;
        }

        private string TaoGhiChuHoaDon()
        {
            if (LaHoaDonDaHuyGiuCoc())
            {
                return "Hóa đơn hủy đặt phòng chỉ ghi nhận khoản tiền cọc khách sạn giữ lại theo chính sách hủy. Phần cọc không bị giữ được hoàn cho khách và không tính doanh thu.";
            }

            if (hoaDon.LoaiThanhToan == "PHATSINH")
            {
                return "Hóa đơn này chỉ gồm dịch vụ, đổi/gia hạn phòng và phụ phí phát sinh sau khi khách đã nhận phòng. VAT đã được tính ở hóa đơn nhận phòng nếu có.";
            }

            return "Hóa đơn nhận phòng gồm tiền phòng đã chốt, dịch vụ tại check-in, phụ phí nhận sớm, VAT và cọc/giảm giá. Dịch vụ thêm sau khi nhận phòng sẽ nằm ở hóa đơn phát sinh khi trả phòng.";
        }

        private ThongTinCheckInBoSung LoadThongTinCheckInBoSung()
        {
            ThongTinCheckInBoSung result = new();
            if (hoaDon.LoaiPhieu != "THUE" || !TableExists("PHIEUTHUE"))
            {
                return result;
            }

            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists("DATPHONG") ? "DATPHONG" : string.Empty;
            bool coDatPhong = !string.IsNullOrWhiteSpace(bangDatPhong) && ColumnExists("PHIEUTHUE", "MaDatPhong");
            string joinDatPhong = coDatPhong ? "LEFT JOIN dbo." + bangDatPhong + " DP ON PT.MaDatPhong = DP.MaDatPhong" : string.Empty;
            string ngayNhanDatExpr = coDatPhong
                ? "DP." + (ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong")
                : "CAST(NULL AS datetime)";
            string ngayTraDatExpr = coDatPhong
                ? "DP." + (ColumnExists(bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong")
                : "CAST(NULL AS datetime)";
            string tienCocDatColumn = coDatPhong ? GetFirstExistingColumn(bangDatPhong, "TienCoc", "DatCoc") : string.Empty;
            string tienCocDatExpr = coDatPhong && !string.IsNullOrWhiteSpace(tienCocDatColumn)
                ? "ISNULL(DP." + tienCocDatColumn + ", 0)"
                : "CAST(0 AS decimal(18,2))";
            string cheDoDatPhongColumn = coDatPhong ? GetFirstExistingColumn(bangDatPhong, "LoaiDat", "CheDoDatPhong", "LoaiDatPhong") : string.Empty;
            string cheDoDatPhongExpr = coDatPhong && !string.IsNullOrWhiteSpace(cheDoDatPhongColumn)
                ? "ISNULL(DP." + cheDoDatPhongColumn + ", N'')"
                : "CAST(N'' AS nvarchar(100))";

            string maPhongExpr = ColumnExists("PHIEUTHUE", "MaPhong") ? "PT.MaPhong" : "CAST(NULL AS int)";
            string tienCocThueExpr = ColumnExists("PHIEUTHUE", "TienCoc") ? "ISNULL(PT.TienCoc, 0)" : "CAST(0 AS decimal(18,2))";
            string ngayNhanThucTeExpr = ColumnExists("PHIEUTHUE", "NgayNhan") ? "PT.NgayNhan" : "CAST(NULL AS datetime)";
            string giaGioExpr = ColumnExists("LOAIPHONG", "DonGiaGio") ? "ISNULL(LP.DonGiaGio, 0)" : "CAST(0 AS decimal(18,2))";
            string giaNgayExpr = ColumnExists("LOAIPHONG", "DonGiaNgay") ? "ISNULL(LP.DonGiaNgay, 0)" : "CAST(0 AS decimal(18,2))";
            string giaDemExpr = ColumnExists("LOAIPHONG", "DonGiaDem") ? "ISNULL(LP.DonGiaDem, 0)" : "CAST(0 AS decimal(18,2))";

            DataTable data = ConnectDB.GetData(@"
SELECT TOP 1
       " + (coDatPhong ? "PT.MaDatPhong" : "CAST(NULL AS int)") + @" AS MaDatPhong,
       " + maPhongExpr + @" AS MaPhong,
       " + ngayNhanDatExpr + @" AS NgayNhanDat,
       " + ngayTraDatExpr + @" AS NgayTraDat,
       " + cheDoDatPhongExpr + @" AS CheDoDatPhong,
       " + ngayNhanThucTeExpr + @" AS NgayNhanThucTe,
       " + tienCocThueExpr + @" AS TienCocThue,
       " + tienCocDatExpr + @" AS TienCocDat,
       " + giaGioExpr + @" AS GiaGio,
       " + giaNgayExpr + @" AS GiaNgay,
       " + giaDemExpr + @" AS GiaDem
FROM dbo.PHIEUTHUE PT
" + joinDatPhong + @"
LEFT JOIN dbo.PHONG P ON " + maPhongExpr + @" = P.MaPhong
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE PT.MaThue = @MaThue",
                new SqlParameter("@MaThue", hoaDon.MaGoc));

            if (data.Rows.Count == 0)
            {
                return result;
            }

            DataRow row = data.Rows[0];
            int? maDatPhong = GetNullableInt(row, "MaDatPhong");
            int? maPhong = GetNullableInt(row, "MaPhong");
            result.MaDatPhong = maDatPhong;
            result.MaPhong = maPhong;
            result.CheDoDatPhong = row["CheDoDatPhong"]?.ToString() ?? string.Empty;
            result.TienCoc = Math.Max(GetDecimal(row, "TienCocThue"), GetDecimal(row, "TienCocDat"));
            result.TienDichVuCheckIn = Math.Max(
                LayTongThanhToanTheoLoai("ServiceCheckIn", hoaDon.MaGoc, maDatPhong),
                maDatPhong.HasValue ? TinhTongDichVuDatPhongMarker(maDatPhong.Value, maPhong) : 0);

            DateTime? ngayNhanDat = GetNullableDate(row, "NgayNhanDat");
            DateTime? ngayTraDat = GetNullableDate(row, "NgayTraDat");
            DateTime? ngayNhanThucTe = GetNullableDate(row, "NgayNhanThucTe");
            if (ngayNhanThucTe.HasValue)
            {
                PhongDTO phongTinhPhi = new()
                {
                    Ma = maPhong ?? 0,
                    SoPhong = hoaDon.SoPhong,
                    LoaiPhong = hoaDon.LoaiPhong,
                    GiaGio = GetDecimal(row, "GiaGio"),
                    GiaNgay = GetDecimal(row, "GiaNgay"),
                    GiaDem = GetDecimal(row, "GiaDem"),
                    GiaPhong = hoaDon.TienPhong
                };
                CheckInPhuPhiResult phuPhi = CheckInPhuPhiHelper.Tinh(
                    phongTinhPhi,
                    ngayNhanDat,
                    ngayTraDat,
                    ngayNhanThucTe.Value,
                    hoaDon.TienPhong,
                    result.CheDoDatPhong);
                result.PhuPhiNhanSom = phuPhi.SoTien;
                result.MoTaPhuPhiNhanSom = phuPhi.MoTa;
            }

            return result;
        }

        private static decimal LayTongThanhToanTheoLoai(string loaiThanhToan, int maThue, int? maDatPhong)
        {
            if (!TableExists("CHITIETTHANHTOAN") || !ColumnExists("CHITIETTHANHTOAN", "LoaiThanhToan"))
            {
                return 0;
            }

            string amountColumn = ColumnExists("CHITIETTHANHTOAN", "SoTien") ? "SoTien" :
                ColumnExists("CHITIETTHANHTOAN", "TienThanhToan") ? "TienThanhToan" : string.Empty;
            if (string.IsNullOrWhiteSpace(amountColumn))
            {
                return 0;
            }

            List<string> conditions = new() { "LoaiThanhToan = @LoaiThanhToan" };
            List<SqlParameter> parameters = new() { new SqlParameter("@LoaiThanhToan", loaiThanhToan) };
            if (ColumnExists("CHITIETTHANHTOAN", "MaThue"))
            {
                conditions.Add("MaThue = @MaThue");
                parameters.Add(new SqlParameter("@MaThue", maThue));
            }
            else if (maDatPhong.HasValue && ColumnExists("CHITIETTHANHTOAN", "MaDatPhong"))
            {
                conditions.Add("MaDatPhong = @MaDatPhong");
                parameters.Add(new SqlParameter("@MaDatPhong", maDatPhong.Value));
            }
            else
            {
                return 0;
            }

            object? value = ConnectDB.ExecuteScalar(
                "SELECT ISNULL(SUM(" + amountColumn + "), 0) FROM dbo.CHITIETTHANHTOAN WHERE " + string.Join(" AND ", conditions),
                parameters.ToArray());
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static decimal LayTongThanhToanCheckInDaThu(int maThue, int? maDatPhong)
        {
            if (!TableExists("CHITIETTHANHTOAN") || !ColumnExists("CHITIETTHANHTOAN", "LoaiThanhToan"))
            {
                return 0;
            }

            string amountColumn = ColumnExists("CHITIETTHANHTOAN", "SoTien") ? "SoTien" :
                ColumnExists("CHITIETTHANHTOAN", "TienThanhToan") ? "TienThanhToan" : string.Empty;
            if (string.IsNullOrWhiteSpace(amountColumn))
            {
                return 0;
            }

            List<string> conditions = new()
            {
                "LoaiThanhToan IN (N'RoomCheckIn', N'ServiceCheckIn')"
            };
            List<SqlParameter> parameters = new();
            if (ColumnExists("CHITIETTHANHTOAN", "MaThue"))
            {
                conditions.Add("MaThue = @MaThue");
                parameters.Add(new SqlParameter("@MaThue", maThue));
            }
            else if (maDatPhong.HasValue && ColumnExists("CHITIETTHANHTOAN", "MaDatPhong"))
            {
                conditions.Add("MaDatPhong = @MaDatPhong");
                parameters.Add(new SqlParameter("@MaDatPhong", maDatPhong.Value));
            }
            else
            {
                return 0;
            }

            object? value = ConnectDB.ExecuteScalar(
                "SELECT ISNULL(SUM(" + amountColumn + "), 0) FROM dbo.CHITIETTHANHTOAN WHERE " + string.Join(" AND ", conditions),
                parameters.ToArray());
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static decimal TinhTongDichVuDatPhongMarker(int maDatPhong, int? maPhong)
        {
            return LoadDichVuDatPhongMarker(maDatPhong, maPhong).Sum(item => item.SoLuong * item.DonGia);
        }

        private static List<DichVuDatPhongMarker> LoadDichVuDatPhongMarker(int maDatPhong, int? maPhong)
        {
            List<DichVuDatPhongMarker> result = new();
            if (!TableExists("CHITIETDATPHONG") || !ColumnExists("CHITIETDATPHONG", "GhiChu"))
            {
                return result;
            }

            string roomFilter = maPhong.HasValue && ColumnExists("CHITIETDATPHONG", "MaPhong") ? " AND MaPhong = @MaPhong" : string.Empty;
            List<SqlParameter> parameters = new()
            {
                new SqlParameter("@MaDatPhong", maDatPhong),
                new SqlParameter("@Marker", "[DICHVU_DAT]")
            };
            if (!string.IsNullOrWhiteSpace(roomFilter))
            {
                parameters.Add(new SqlParameter("@MaPhong", maPhong!.Value));
            }

            DataTable data = ConnectDB.GetData(
                "SELECT GhiChu FROM dbo.CHITIETDATPHONG WHERE MaDatPhong = @MaDatPhong" + roomFilter + " AND CHARINDEX(@Marker, ISNULL(GhiChu, N'')) > 0",
                parameters.ToArray());

            foreach (DataRow row in data.Rows)
            {
                result.AddRange(DocMarkerDichVuDatPhong(row["GhiChu"]?.ToString() ?? string.Empty));
            }

            return result;
        }

        private static List<DichVuDatPhongMarker> DocMarkerDichVuDatPhong(string ghiChu)
        {
            const string marker = "[DICHVU_DAT]";
            int markerIndex = ghiChu.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return new List<DichVuDatPhongMarker>();
            }

            string payload = ghiChu[(markerIndex + marker.Length)..].Trim();
            int stopIndex = payload.IndexOf(" - ", StringComparison.Ordinal);
            if (stopIndex >= 0)
            {
                payload = payload[..stopIndex];
            }

            List<DichVuDatPhongMarker> result = new();
            foreach (string token in payload.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = token.Split('|');
                if (parts.Length >= 3 &&
                    int.TryParse(parts[0], out int ma) &&
                    int.TryParse(parts[1], out int soLuong) &&
                    decimal.TryParse(parts[2], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal donGia) &&
                    soLuong > 0)
                {
                    result.Add(new DichVuDatPhongMarker(ma, soLuong, donGia));
                }
            }

            return result;
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
            bool coGhiChu = ColumnExists(table, "GhiChu");
            string filterLoaiDichVu = coGhiChu ? ViewSchemaHelper.DichVuTheoLoaiHoaDonFilter("PS", hoaDon.LoaiThanhToan) : string.Empty;

            DataTable data = ConnectDB.GetData(
                @"SELECT DV." + tenDichVu + @" AS TenDichVu,
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
                    TenDichVu = row["TenDichVu"]?.ToString() ?? string.Empty,
                    SoLuong = GetDecimal(row, "SoLuong"),
                    DonGia = GetDecimal(row, "DonGia"),
                    ThanhTien = GetDecimal(row, "ThanhTien")
                });
            }

            return result;
        }

        private void BtnThanhToan_Click(object sender, RoutedEventArgs e)
        {
            if (hoaDon.DaThanhToan || hoaDon.LoaiPhieu != "THUE")
            {
                return;
            }

            if (DialogService.ShowDimmedDialogResult(
                    new XacNhanThanhToanWindow(hoaDon.MaHoaDon, hoaDon.TongTien),
                    this) != true)
            {
                return;
            }

            try
            {
                new ThanhToanFlowBUS().ThanhToanHoaDon(hoaDon.MaGoc);
                DuLieuDaThayDoi = true;
                MessageBox.Show("Đã thanh toán hóa đơn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                HoaDonPrintWindow window = new(hoaDon.VoiTrangThai("Da thanh toan"));
                DialogService.ShowDimmedDialogResult(window, this);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể thanh toán hóa đơn: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnInHoaDon_Click(object sender, RoutedEventArgs e)
        {
            HoaDonPrintWindow window = new(hoaDon);
            DialogService.ShowDimmedDialogResult(window, this);
        }

        private void BtnDong_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static void ThanhToanPhieuThue(int maThue)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();
            try
            {
                string setNgayTra = ColumnExists("PHIEUTHUE", "NgayTraPhong") ? ", NgayTraPhong = ISNULL(NgayTraPhong, @NgayTra)" : string.Empty;
                using (SqlCommand cmd = new("UPDATE dbo.PHIEUTHUE SET TrangThai = @TrangThai" + setNgayTra + " WHERE MaThue = @Ma", conn, tran))
                {
                    cmd.Parameters.AddWithValue("@TrangThai", "Đã trả phòng");
                    cmd.Parameters.AddWithValue("@NgayTra", DateTime.Now);
                    cmd.Parameters.AddWithValue("@Ma", maThue);
                    cmd.ExecuteNonQuery();
                }

                CapNhatTrangThaiPhongTheoNhomThue(conn, tran, maThue, "Trống");

                using (SqlCommand cmd = new(
                           @"UPDATE P
                             SET P.TrangThai = @TrangThaiPhong
                             FROM dbo.PHONG P
                             JOIN dbo.PHIEUTHUE PT ON P.MaPhong = PT.MaPhong
                             WHERE PT.MaThue = @Ma",
                           conn,
                           tran))
                {
                    cmd.Parameters.AddWithValue("@TrangThaiPhong", "Trống");
                    cmd.Parameters.AddWithValue("@Ma", maThue);
                    cmd.ExecuteNonQuery();
                }

                if (ColumnExists("PHONG", "GhiChu"))
                {
                    using SqlCommand note = new(
                        @"UPDATE P
                          SET P.GhiChu = CONCAT(NULLIF(P.GhiChu, N''), CASE WHEN NULLIF(P.GhiChu, N'') IS NULL THEN N'' ELSE N' - ' END, N'[CAN_DON_DEP] Can don dep sau khi tra phong')
                          FROM dbo.PHONG P
                          JOIN dbo.PHIEUTHUE PT ON P.MaPhong = PT.MaPhong
                          WHERE PT.MaThue = @Ma",
                        conn,
                        tran);
                    note.Parameters.AddWithValue("@Ma", maThue);
                    note.ExecuteNonQuery();
                }

                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        private static void CapNhatTrangThaiPhongTheoNhomThue(SqlConnection conn, SqlTransaction tran, int maThue, string trangThaiPhong)
        {
            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists("DATPHONG") ? "DATPHONG" : string.Empty;
            if (ColumnExists("PHIEUTHUE", "MaDatPhong") && !string.IsNullOrWhiteSpace(bangDatPhong) && ColumnExists(bangDatPhong, "MaPhong"))
            {
                string ngayNhanColumn = ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong";
                string ngayTraColumn = ColumnExists(bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong";
                using SqlCommand cmd = new(
                    @"UPDATE P
                      SET P.TrangThai = @TrangThai
                      FROM dbo.PHIEUTHUE PT
                      JOIN dbo." + bangDatPhong + @" DP0 ON PT.MaDatPhong = DP0.MaDatPhong
                      JOIN dbo." + bangDatPhong + @" DPG ON DPG.MaKH = DP0.MaKH
                         AND CONVERT(date, DPG." + ngayNhanColumn + @") = CONVERT(date, DP0." + ngayNhanColumn + @")
                         AND CONVERT(date, DPG." + ngayTraColumn + @") = CONVERT(date, DP0." + ngayTraColumn + @")
                      JOIN dbo.PHONG P ON DPG.MaPhong = P.MaPhong
                      WHERE PT.MaThue = @Ma",
                    conn,
                    tran);
                cmd.Parameters.AddWithValue("@TrangThai", trangThaiPhong);
                cmd.Parameters.AddWithValue("@Ma", maThue);
                cmd.ExecuteNonQuery();
                return;
            }

            if (ColumnExists("PHIEUTHUE", "MaDatPhong") && TableExists("CHITIETDATPHONG"))
            {
                using SqlCommand cmd = new(
                    @"UPDATE P
                      SET P.TrangThai = @TrangThai
                      FROM dbo.PHIEUTHUE PT
                      JOIN dbo.CHITIETDATPHONG CT ON PT.MaDatPhong = CT.MaDatPhong
                      JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong
                      WHERE PT.MaThue = @Ma",
                    conn,
                    tran);
                cmd.Parameters.AddWithValue("@TrangThai", trangThaiPhong);
                cmd.Parameters.AddWithValue("@Ma", maThue);
                cmd.ExecuteNonQuery();
            }
        }

        private static string TinhThoiLuong(DateTime start, DateTime end)
        {
            if (end <= start)
            {
                return "1 ngày";
            }

            if (start.Date == end.Date)
            {
                return Math.Max(1, (int)Math.Ceiling((end - start).TotalHours)) + " giờ";
            }

            if ((end - start).TotalHours <= 12)
            {
                return "1 ngày";
            }

            return Math.Max(1, (int)Math.Ceiling((end - start).TotalDays)) + " ngày";
        }

        private static string TaoNoiDungNhanSom(DateTime? actualStart, DateTime plannedStart, decimal fee)
        {
            if (!actualStart.HasValue)
            {
                return "Chưa nhận phòng (0 VND)";
            }

            int earlyMinutes = Math.Max(0, (int)Math.Ceiling((plannedStart - actualStart.Value).TotalMinutes));
            if (earlyMinutes == 0)
            {
                return "Nhận đúng hoặc sau giờ đặt (0 VND)";
            }

            int roundedHours = Math.Max(1, (int)Math.Ceiling(earlyMinutes / 60.0));
            string duration = roundedHours + " giờ";
            return "Nhận sớm " + duration + " (" + fee.ToString("N0") + " VND)";
        }

        private static string TaoNoiDungTraMuon(HoaDonItem hoaDon)
        {
            if (!hoaDon.NgayTraThucTe.HasValue || hoaDon.NgayTraThucTe.Value <= hoaDon.NgayTraPhong)
            {
                return "Kh\u00F4ng tr\u1EA3 mu\u1ED9n";
            }

            int lateMinutes = Math.Max(1, (int)Math.Ceiling((hoaDon.NgayTraThucTe.Value - hoaDon.NgayTraPhong).TotalMinutes));
            int hours = lateMinutes / 60;
            int minutes = lateMinutes % 60;
            string duration = hours > 0 && minutes > 0
                ? hours + " gi\u1EDD " + minutes + " ph\u00FAt"
                : hours > 0
                    ? hours + " gi\u1EDD"
                    : minutes + " ph\u00FAt";
            return "Tr\u1EA3 mu\u1ED9n " + duration;
        }

        private static decimal GetDecimal(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && decimal.TryParse(row[column]?.ToString(), out decimal value) ? value : 0;
        }

        private static int? GetNullableInt(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && int.TryParse(row[column]?.ToString(), out int value) ? value : null;
        }

        private static DateTime? GetNullableDate(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && DateTime.TryParse(row[column]?.ToString(), out DateTime value) ? value : null;
        }

        private static bool TableExists(string tableName)
        {
            return ViewSchemaHelper.TableExists(tableName);
        }

        private static bool ColumnExists(string tableName, string columnName)
        {
            return ViewSchemaHelper.ColumnExists(tableName, columnName);
        }

        private static string GetFirstExistingColumn(string tableName, params string[] columnNames)
        {
            return ViewSchemaHelper.GetFirstExistingColumn(tableName, columnNames);
        }

        private bool HoaDonDaThanhToan()
        {
            string trangThai = hoaDon.TrangThai ?? string.Empty;
            return hoaDon.DaThanhToan ||
                trangThai.Contains("\u0110\u00e3", StringComparison.OrdinalIgnoreCase) ||
                trangThai.Contains("Da", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal class ThongTinCheckInBoSung
    {
        public int? MaDatPhong { get; set; }
        public int? MaPhong { get; set; }
        public string CheDoDatPhong { get; set; } = string.Empty;
        public decimal TienCoc { get; set; }
        public decimal TienDichVuCheckIn { get; set; }
        public decimal PhuPhiNhanSom { get; set; }
        public decimal ThueVatDaChot { get; set; }
        public string MoTaPhuPhiNhanSom { get; set; } = string.Empty;
    }

    internal sealed record DichVuDatPhongMarker(int Ma, int SoLuong, decimal DonGia);

    public class ChiTietThanhToanHoaDonItem
    {
        public int Stt { get; init; }
        public string Ten { get; init; } = string.Empty;
        public string SoPhong { get; init; } = string.Empty;
        public decimal DonGia { get; init; }
        public decimal SoLuong { get; init; }
        public decimal ThanhTien { get; init; }
        public string DonGiaText => DonGia == 0 ? string.Empty : DonGia.ToString("N0");
        public string SoLuongText => SoLuong == 0 ? string.Empty : SoLuong.ToString("N0");
        public string ThanhTienText => ThanhTien.ToString("N0") + " VND";
    }

    public class DichVuHoaDonItem
    {
        public int Stt { get; init; }
        public int? MaPhong { get; init; }
        public string TenDichVu { get; init; } = string.Empty;
        public decimal SoLuong { get; init; }
        public decimal DonGia { get; init; }
        public decimal ThanhTien { get; init; }
        public string DonGiaText => DonGia.ToString("N0");
        public string ThanhTienText => ThanhTien.ToString("N0");
    }
}

