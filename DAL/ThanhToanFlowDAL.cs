using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.DAL
{
    public class ThanhToanFlowDAL
    {
        public KetQuaCheckInThanhToanDTO CheckInTuDatPhong(int maDatPhong, decimal tienThucThuTaiQuay)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();

            try
            {
                string bangDatPhong = ResolveTable(conn, tran, "PHIEUDATPHONG", "DATPHONG");
                if (string.IsNullOrWhiteSpace(bangDatPhong))
                {
                    throw new InvalidOperationException("Khong tim thay bang dat phong.");
                }

                int? maThue = LayMaThueTheoDatPhong(conn, tran, maDatPhong);
                ThongTinHoaDon thongTin = LayThongTinHoaDonTheoDatPhong(conn, tran, bangDatPhong, maDatPhong, maThue);
                int maHoaDon = TaoHoacCapNhatHoaDon(conn, tran, thongTin, "Open");

                decimal tienDichVuCheckIn = Math.Max(0, thongTin.TongTienDichVu);
                decimal tienPhongCheckIn = Math.Max(0, tienThucThuTaiQuay - tienDichVuCheckIn);
                if (tienPhongCheckIn > 0)
                {
                    ChenChiTietThanhToan(conn, tran, maHoaDon, maDatPhong, maThue, "Tien phong khi check-in", tienPhongCheckIn, "RoomCheckIn");
                }
                if (tienDichVuCheckIn > 0)
                {
                    ChenChiTietThanhToan(conn, tran, maHoaDon, maDatPhong, maThue, "Dich vu thanh toan khi check-in", tienDichVuCheckIn, "ServiceCheckIn");
                }

                CapNhatTrangThaiDatPhong(conn, tran, bangDatPhong, maDatPhong, "Đã check-in", "Da check-in", "Đang thuê", "Dang thue", "Có khách", "Co khach", "Đã xác nhận", "Da xac nhan");
                CapNhatTrangThaiPhongTheoDatPhong(conn, tran, bangDatPhong, maDatPhong, "Đang thuê", "Dang thue");

                tran.Commit();
                return new KetQuaCheckInThanhToanDTO
                {
                    MaHoaDon = maHoaDon,
                    MaThue = maThue,
                    TongTienDuKien = thongTin.TongTienPhong + thongTin.TongTienDichVu + thongTin.PhuPhi,
                    TienDatCocTruoc = thongTin.TienCoc,
                    TienThucThuTaiQuay = tienThucThuTaiQuay
                };
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        public void CongDichVuPhatSinh(int maThue, decimal tongTienDichVuMoi)
        {
            // Dịch vụ phát sinh được lưu ở bảng phát sinh. Hóa đơn phát sinh chỉ được tạo
            // khi xác nhận thanh toán/trả phòng hoặc chọn thanh toán sau ở bước check-out.
            return;
        }

        public KetQuaCheckOutThanhToanDTO CheckOut(int maThue, bool thanhToanNgay = true)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();

            try
            {
                DateTime ngayTraPhong = DateTime.Now;
                int? maDatPhong = LayMaDatPhongTheoThue(conn, tran, maThue);
                int maHoaDon = LayMaHoaDon(conn, tran, maThue, maDatPhong);
                List<int> nhomMaDatPhong = LayNhomDatPhongTheoThue(conn, tran, maThue);
                DuToanCheckOutDTO duToan = TaoDuToanCheckOut(conn, tran, maThue, maHoaDon, maDatPhong, ngayTraPhong);

                CapNhatNgayTraPhong(conn, tran, maThue, nhomMaDatPhong, ngayTraPhong);

                decimal tienDichVuPhatSinh = duToan.TienDichVuPhatSinh;
                decimal tienGiaHanDoiPhong = duToan.TienGiaHan + Math.Max(0, duToan.ChenhLechDoiPhong);
                decimal tienHoanDoiPhong = Math.Max(0, -duToan.ChenhLechDoiPhong);
                decimal phuThuTraMuon = duToan.PhuPhiTraMuon;
                decimal tienThuThem = duToan.CanThuThem;
                bool coHoaDonPhatSinh = duToan.TienDichVuPhatSinh > 0 ||
                                         duToan.TienGiaHan > 0 ||
                                         duToan.ChenhLechDoiPhong != 0 ||
                                         duToan.PhuPhiTraMuon > 0;

                if (coHoaDonPhatSinh)
                {
                    if (maHoaDon <= 0)
                    {
                        maHoaDon = LayHoacTaoHoaDonTheoThue(conn, tran, maThue, maDatPhong);
                    }

                    if (tienDichVuPhatSinh > 0)
                    {
                        ChenChiTietThanhToan(conn, tran, maHoaDon, maDatPhong, maThue, "Dich vu phat sinh khi check-out", tienDichVuPhatSinh, "Service");
                    }
                    if (tienGiaHanDoiPhong > 0)
                    {
                        ChenChiTietThanhToan(conn, tran, maHoaDon, maDatPhong, maThue, "Tien phong phat sinh do gia han/doi phong", tienGiaHanDoiPhong, "RoomExtra");
                    }
                    if (phuThuTraMuon > 0)
                    {
                        ChenChiTietThanhToan(conn, tran, maHoaDon, maDatPhong, maThue, "Phu phi tra muon khi check-out", phuThuTraMuon, "LateFee");
                    }
                    if (tienHoanDoiPhong > 0)
                    {
                        ChenChiTietThanhToan(conn, tran, maHoaDon, maDatPhong, maThue, "Hoan chenh lech doi xuong phong gia thap hon", tienHoanDoiPhong, "RoomRefund");
                    }

                    CapNhatTongHoaDonSauCheckOut(conn, tran, maHoaDon, maThue, maDatPhong, duToan, thanhToanNgay);
                    if (thanhToanNgay)
                    {
                        CapNhatTrangThaiHoaDon(conn, tran, maHoaDon, "Closed", "Da thanh toan", "Da dong");
                    }
                    else
                    {
                        CapNhatTrangThaiHoaDon(conn, tran, maHoaDon, "Open", "Chua thanh toan", "Cho thanh toan");
                    }
                }
                CapNhatTrangThaiPhieuThue(conn, tran, maThue, nhomMaDatPhong, ngayTraPhong, "Đã trả", "Da tra", "Đã trả phòng", "Da tra phong");
                PhongTrangThaiSchema.DamBaoCoTrangThaiChuaDonDep(conn, tran);
                CapNhatTrangThaiPhongTheoThue(conn, tran, maThue, nhomMaDatPhong, PhongTrangThaiSchema.ChuaDonDep, "Chua don dep", "Dirty");

                tran.Commit();
                return new KetQuaCheckOutThanhToanDTO
                {
                    MaHoaDon = maHoaDon,
                    TienThuThem = tienThuThem,
                    TienHoanKhach = duToan.CanTraKhach,
                    DaThanhToan = thanhToanNgay
                };
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        public void ThanhToanHoaDon(int maThue)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();
            try
            {
                int? maDatPhong = LayMaDatPhongTheoThue(conn, tran, maThue);
                int maHoaDon = LayHoacTaoHoaDonTheoThue(conn, tran, maThue, maDatPhong);
                CapNhatTrangThaiHoaDon(conn, tran, maHoaDon, "Closed", "Da thanh toan", "Da dong");
                if (ColumnExists(conn, tran, "HOADON", "DaThanhToan"))
                {
                    string key = LayCotKhoaHoaDon(conn, tran);
                    using SqlCommand cmd = new("UPDATE dbo.HOADON SET DaThanhToan = 1 WHERE " + key + " = @MaHoaDon", conn, tran);
                    cmd.Parameters.AddWithValue("@MaHoaDon", maHoaDon);
                    cmd.ExecuteNonQuery();
                }
                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        public void ThanhToanHoaDonTheoDoan(int maDoan)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();
            try
            {
                if (!TableExists(conn, tran, "PHIEUTHUE") || !ColumnExists(conn, tran, "PHIEUTHUE", "MaDoan"))
                {
                    throw new InvalidOperationException("Database chưa hỗ trợ thanh toán theo đoàn.");
                }

                List<int> danhSachMaThue = new();
                using (SqlCommand load = new("SELECT MaThue FROM dbo.PHIEUTHUE WHERE MaDoan = @MaDoan ORDER BY MaThue", conn, tran))
                {
                    load.Parameters.AddWithValue("@MaDoan", maDoan);
                    using SqlDataReader reader = load.ExecuteReader();
                    while (reader.Read())
                    {
                        danhSachMaThue.Add(Convert.ToInt32(reader["MaThue"]));
                    }
                }

                if (danhSachMaThue.Count == 0)
                {
                    throw new InvalidOperationException("Không tìm thấy phiếu thuê thuộc đoàn cần thanh toán.");
                }

                foreach (int maThue in danhSachMaThue)
                {
                    int? maDatPhong = LayMaDatPhongTheoThue(conn, tran, maThue);
                    int maHoaDon = LayHoacTaoHoaDonTheoThue(conn, tran, maThue, maDatPhong);
                    CapNhatTrangThaiHoaDon(conn, tran, maHoaDon, "Closed", "Da thanh toan", "Da dong");
                    if (ColumnExists(conn, tran, "HOADON", "DaThanhToan"))
                    {
                        string key = LayCotKhoaHoaDon(conn, tran);
                        using SqlCommand update = new("UPDATE dbo.HOADON SET DaThanhToan = 1 WHERE " + key + " = @MaHoaDon", conn, tran);
                        update.Parameters.AddWithValue("@MaHoaDon", maHoaDon);
                        update.ExecuteNonQuery();
                    }
                }

                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        public DuToanCheckOutDTO DuToanCheckOut(int maThue, DateTime ngayTraThucTe)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();
            try
            {
                int? maDatPhong = LayMaDatPhongTheoThue(conn, tran, maThue);
                int maHoaDon = LayMaHoaDon(conn, tran, maThue, maDatPhong);
                DuToanCheckOutDTO result = TaoDuToanCheckOut(conn, tran, maThue, maHoaDon, maDatPhong, ngayTraThucTe);
                tran.Rollback();
                return result;
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        public void NoShow(int maDatPhong)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();

            try
            {
                string bangDatPhong = ResolveTable(conn, tran, "PHIEUDATPHONG", "DATPHONG");
                if (string.IsNullOrWhiteSpace(bangDatPhong))
                {
                    throw new InvalidOperationException("Khong tim thay bang dat phong.");
                }

                int? maThue = LayMaThueTheoDatPhong(conn, tran, maDatPhong);
                ThongTinHoaDon thongTin = LayThongTinHoaDonTheoDatPhong(conn, tran, bangDatPhong, maDatPhong, maThue);
                decimal tienCocGiuLai = TinhTienCocGiuLaiKhiHuy(thongTin.TienCoc, thongTin.NgayNhanDuKien, DateTime.Now);
                thongTin.TongTienPhong = tienCocGiuLai;
                thongTin.TongTienDichVu = 0;
                thongTin.PhuPhi = 0;
                thongTin.TienCoc = 0;
                int maHoaDon = TaoHoacCapNhatHoaDon(conn, tran, thongTin, "Closed");
                if (tienCocGiuLai > 0)
                {
                    ChenChiTietThanhToan(conn, tran, maHoaDon, maDatPhong, maThue, "Giu tien coc do huy/No-Show", tienCocGiuLai, "No-Show");
                }

                CapNhatTrangThaiDatPhong(conn, tran, bangDatPhong, maDatPhong, "Đã hủy", "Da huy", "No-Show", "No Show", "Khach khong den");
                CapNhatTrangThaiPhieuThueTheoDatPhong(conn, tran, maDatPhong, "Đã hủy", "Da huy", "No-Show", "No Show", "Khach khong den");
                CapNhatTrangThaiPhongTheoDatPhong(conn, tran, bangDatPhong, maDatPhong, "Trống", "Phong trong", "Phòng trống");

                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        public DuToanHuyDatPhongDTO DuToanHuyDatPhong(int maDatPhong)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();

            try
            {
                string bangDatPhong = ResolveTable(conn, tran, "PHIEUDATPHONG", "DATPHONG");
                if (string.IsNullOrWhiteSpace(bangDatPhong))
                {
                    throw new InvalidOperationException("Khong tim thay bang dat phong.");
                }

                int? maThue = LayMaThueTheoDatPhong(conn, tran, maDatPhong);
                ThongTinHoaDon thongTin = LayThongTinHoaDonTheoDatPhong(conn, tran, bangDatPhong, maDatPhong, maThue);
                DateTime thoiDiemHuy = DateTime.Now;
                decimal tienCocGiuLai = TinhTienCocGiuLaiKhiHuy(thongTin.TienCoc, thongTin.NgayNhanDuKien, thoiDiemHuy);
                double? soGioTruocNhan = thongTin.NgayNhanDuKien.HasValue
                    ? (thongTin.NgayNhanDuKien.Value - thoiDiemHuy).TotalHours
                    : null;

                tran.Rollback();
                return new DuToanHuyDatPhongDTO
                {
                    MaDatPhong = maDatPhong,
                    NgayNhanDuKien = thongTin.NgayNhanDuKien,
                    ThoiDiemHuy = thoiDiemHuy,
                    SoGioTruocNhan = soGioTruocNhan,
                    TienCoc = thongTin.TienCoc,
                    TienCocGiuLai = tienCocGiuLai,
                    ChinhSach = TaoMoTaChinhSachHuy(thongTin.TienCoc, thongTin.NgayNhanDuKien, thoiDiemHuy)
                };
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        private static int LayHoacTaoHoaDonTheoThue(SqlConnection conn, SqlTransaction tran, int maThue, int? maDatPhong)
        {
            int maHoaDon = LayMaHoaDon(conn, tran, maThue, maDatPhong);
            if (maHoaDon > 0)
            {
                return maHoaDon;
            }

            string bangDatPhong = ResolveTable(conn, tran, "PHIEUDATPHONG", "DATPHONG");
            ThongTinHoaDon thongTin = !string.IsNullOrWhiteSpace(bangDatPhong) && maDatPhong.HasValue
                ? LayThongTinHoaDonTheoDatPhong(conn, tran, bangDatPhong, maDatPhong.Value, maThue)
                : LayThongTinHoaDonTheoThue(conn, tran, maThue);
            return TaoHoacCapNhatHoaDon(conn, tran, thongTin, "Open");
        }

        private static decimal TinhTienCocGiuLaiKhiHuy(decimal tienCoc, DateTime? ngayNhanDuKien, DateTime thoiDiemHuy)
        {
            if (tienCoc <= 0)
            {
                return 0;
            }

            if (!ngayNhanDuKien.HasValue)
            {
                return tienCoc;
            }

            double soGioTruocNhan = (ngayNhanDuKien.Value - thoiDiemHuy).TotalHours;
            if (soGioTruocNhan >= 24)
            {
                return 0;
            }

            if (soGioTruocNhan >= 12)
            {
                return Math.Round(tienCoc * 0.5m, 0);
            }

            return tienCoc;
        }

        private static string TaoMoTaChinhSachHuy(decimal tienCoc, DateTime? ngayNhanDuKien, DateTime thoiDiemHuy)
        {
            if (tienCoc <= 0)
            {
                return "Không có tiền cọc để hoàn/giữ.";
            }

            if (!ngayNhanDuKien.HasValue)
            {
                return "Không xác định giờ nhận phòng: giữ 100% tiền cọc.";
            }

            double soGioTruocNhan = (ngayNhanDuKien.Value - thoiDiemHuy).TotalHours;
            if (soGioTruocNhan >= 24)
            {
                return "Hủy trước 24 tiếng: hoàn 100% tiền cọc.";
            }

            if (soGioTruocNhan >= 12)
            {
                return "Hủy trước 12 tiếng: giữ 50% tiền cọc.";
            }

            return "Hủy sau mốc 12 tiếng: mất cọc.";
        }

        private static int TaoHoacCapNhatHoaDon(SqlConnection conn, SqlTransaction tran, ThongTinHoaDon thongTin, string trangThai)
        {
            if (!TableExists(conn, tran, "HOADON"))
            {
                return 0;
            }

            string trangThaiHoaDon = LayTrangThaiHoaDon(conn, tran, trangThai);
            string hoaDonKey = LayCotKhoaHoaDon(conn, tran);
            int existing = LayMaHoaDon(conn, tran, thongTin.MaThue, thongTin.MaDatPhong);
            if (existing > 0)
            {
                List<string> sets = new();
                AddSetIfExists(conn, tran, sets, "HOADON", "TongTienPhong", "@TongTienPhong");
                AddSetIfExists(conn, tran, sets, "HOADON", "TongTienDuKien", "@TongTienDuKien");
                AddSetIfExists(conn, tran, sets, "HOADON", "TienDatCocTruoc", "@TienCoc");
                AddSetIfExists(conn, tran, sets, "HOADON", "TienCoc", "@TienCoc");
                AddSetIfExists(conn, tran, sets, "HOADON", "TongTienDichVu", "@TongTienDichVu");
                AddSetIfExists(conn, tran, sets, "HOADON", "TongTienDV", "@TongTienDichVu");
                AddSetIfExists(conn, tran, sets, "HOADON", "TongPhuThu", "@TongPhuThu");
                AddSetIfExists(conn, tran, sets, "HOADON", "TongThanhToan", "@TongThanhToan");
                AddSetIfExists(conn, tran, sets, "HOADON", "TongTien", "@TongThanhToan");
                AddSetIfExists(conn, tran, sets, "HOADON", "GiamGia", "@GiamGia");
                AddSetIfExists(conn, tran, sets, "HOADON", "DaThanhToan", "@DaThanhToan");
                AddSetIfExists(conn, tran, sets, "HOADON", "TrangThai", "@TrangThai");

                if (sets.Count > 0)
                {
                    using SqlCommand update = new("UPDATE dbo.HOADON SET " + string.Join(",", sets) + " WHERE " + hoaDonKey + " = @MaHoaDon", conn, tran);
                    GanThamSoHoaDon(update, thongTin, trangThaiHoaDon);
                    update.Parameters.AddWithValue("@MaHoaDon", existing);
                    update.ExecuteNonQuery();
                }

                return existing;
            }

            if (ColumnRequired(conn, tran, "HOADON", "MaThue") && !thongTin.MaThue.HasValue)
            {
                return 0;
            }

            List<string> columns = new();
            List<string> values = new();
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "MaThue", "@MaThue");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "MaDatPhong", "@MaDatPhong");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "MaKH", "@MaKH");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "MaPhong", "@MaPhong");
            AddFirstColumnIfExists(conn, tran, columns, values, "HOADON", "@MaNV", "MaNV", "MANV", "MaNhanVien", "NhanVienID");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "NgayLap", "@NgayLap");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "NgayLapHoaDon", "@NgayLap");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TongTienPhong", "@TongTienPhong");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TongTienDuKien", "@TongTienDuKien");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TienDatCocTruoc", "@TienCoc");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TienCoc", "@TienCoc");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TongTienDichVu", "@TongTienDichVu");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TongTienDV", "@TongTienDichVu");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TongPhuThu", "@TongPhuThu");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TongThanhToan", "@TongThanhToan");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TongTien", "@TongThanhToan");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "GiamGia", "@GiamGia");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "DaThanhToan", "@DaThanhToan");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TrangThai", "@TrangThai");

            if (columns.Count == 0)
            {
                return 0;
            }

            string identitySelect = !string.IsNullOrWhiteSpace(hoaDonKey)
                ? "; SELECT CONVERT(int, SCOPE_IDENTITY());"
                : string.Empty;
            using SqlCommand insert = new("INSERT INTO dbo.HOADON(" + string.Join(",", columns) + ") VALUES(" + string.Join(",", values) + ")" + identitySelect, conn, tran);
            GanThamSoHoaDon(insert, thongTin, trangThaiHoaDon);
            insert.Parameters.AddWithValue("@MaNV", LayMaNhanVienHoaDon(thongTin));
            object? value;
            try
            {
                value = insert.ExecuteScalar();
            }
            catch (SqlException ex) when (ex.Errors.Cast<SqlError>().Any(error => error.Number == 515 && error.Message.Contains("MaNV", StringComparison.OrdinalIgnoreCase)))
            {
                DamBaoCotGiaTri(conn, tran, columns, values, "HOADON", "MaNV", "@MaNV");
                using SqlCommand retry = new("INSERT INTO dbo.HOADON(" + string.Join(",", columns) + ") VALUES(" + string.Join(",", values) + ")" + identitySelect, conn, tran);
                GanThamSoHoaDon(retry, thongTin, trangThaiHoaDon);
                retry.Parameters.AddWithValue("@MaNV", LayMaNhanVienHoaDon(thongTin));
                value = retry.ExecuteScalar();
            }
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static void GanThamSoHoaDon(SqlCommand cmd, ThongTinHoaDon thongTin, string trangThai)
        {
            cmd.Parameters.AddWithValue("@MaThue", thongTin.MaThue.HasValue ? thongTin.MaThue.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@MaDatPhong", thongTin.MaDatPhong.HasValue ? thongTin.MaDatPhong.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@MaKH", thongTin.MaKhachHang.HasValue ? thongTin.MaKhachHang.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@MaPhong", thongTin.MaPhong.HasValue ? thongTin.MaPhong.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@NgayLap", DateTime.Now);
            cmd.Parameters.AddWithValue("@TongTienPhong", thongTin.TongTienPhong);
            cmd.Parameters.AddWithValue("@TongTienDuKien", thongTin.TongTienPhong);
            cmd.Parameters.AddWithValue("@TienCoc", thongTin.TienCoc);
            cmd.Parameters.AddWithValue("@TongTienDichVu", thongTin.TongTienDichVu);
            cmd.Parameters.AddWithValue("@TongPhuThu", thongTin.PhuPhi);
            cmd.Parameters.AddWithValue("@TongThanhToan", Math.Max(0, thongTin.TongTienPhong + thongTin.TongTienDichVu + thongTin.PhuPhi - thongTin.TienCoc));
            cmd.Parameters.AddWithValue("@GiamGia", 0);
            cmd.Parameters.AddWithValue("@DaThanhToan", LaTrangThaiDaThanhToan(trangThai) ? 1 : 0);
            cmd.Parameters.AddWithValue("@TrangThai", trangThai);
        }

        private static int LayMaNhanVienHoaDon(ThongTinHoaDon thongTin)
        {
            if (thongTin.MaNhanVien.HasValue && thongTin.MaNhanVien.Value > 0)
            {
                return thongTin.MaNhanVien.Value;
            }

            throw new InvalidOperationException("Tài khoản/phiếu nghiệp vụ chưa có nhân viên lập hóa đơn hợp lệ.");
        }

        private static string LayTrangThaiHoaDon(SqlConnection conn, SqlTransaction tran, string requested)
        {
            string normalized = BoDau(requested ?? string.Empty);
            return normalized.Contains("closed", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("da thanh toan", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("da dong", StringComparison.OrdinalIgnoreCase)
                ? LayGiaTriHopLeTheoCheck(conn, tran, "HOADON", "TrangThai", "Da thanh toan", "Đã thanh toán", "Closed")
                : LayGiaTriHopLeTheoCheck(conn, tran, "HOADON", "TrangThai", "Chua thanh toan", "Chưa thanh toán", "Open");
        }

        private static bool LaTrangThaiDaThanhToan(string trangThai)
        {
            string normalized = BoDau(trangThai ?? string.Empty);
            return normalized.Contains("da thanh toan", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("closed", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("da dong", StringComparison.OrdinalIgnoreCase);
        }

        private static void ChenChiTietThanhToan(SqlConnection conn, SqlTransaction tran, int maHoaDon, int? maDatPhong, int? maThue, string noiDung, decimal soTien, string loai)
        {
            if (!TableExists(conn, tran, "CHITIETTHANHTOAN") || soTien <= 0)
            {
                return;
            }

            List<string> columns = new();
            List<string> values = new();
            AddFirstColumnIfExists(conn, tran, columns, values, "CHITIETTHANHTOAN", "@MaHoaDon", "MaHoaDon", "MaHD", "IDHoaDon", "HoaDonID");
            AddColumnIfExists(conn, tran, columns, values, "CHITIETTHANHTOAN", "MaDatPhong", "@MaDatPhong");
            AddColumnIfExists(conn, tran, columns, values, "CHITIETTHANHTOAN", "MaThue", "@MaThue");
            AddColumnIfExists(conn, tran, columns, values, "CHITIETTHANHTOAN", "NgayThanhToan", "@NgayThanhToan");
            AddColumnIfExists(conn, tran, columns, values, "CHITIETTHANHTOAN", "SoTien", "@SoTien");
            AddColumnIfExists(conn, tran, columns, values, "CHITIETTHANHTOAN", "TienThanhToan", "@SoTien");
            AddColumnIfExists(conn, tran, columns, values, "CHITIETTHANHTOAN", "LoaiThanhToan", "@Loai");
            AddColumnIfExists(conn, tran, columns, values, "CHITIETTHANHTOAN", "NoiDung", "@NoiDung");
            AddColumnIfExists(conn, tran, columns, values, "CHITIETTHANHTOAN", "GhiChu", "@NoiDung");

            if (columns.Count == 0)
            {
                return;
            }

            try
            {
                using SqlCommand cmd = new("INSERT INTO dbo.CHITIETTHANHTOAN(" + string.Join(",", columns) + ") VALUES(" + string.Join(",", values) + ")", conn, tran);
                cmd.Parameters.AddWithValue("@MaHoaDon", maHoaDon > 0 ? maHoaDon : DBNull.Value);
                cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong.HasValue ? maDatPhong.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@MaThue", maThue.HasValue ? maThue.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@NgayThanhToan", DateTime.Now);
                cmd.Parameters.AddWithValue("@SoTien", soTien);
                cmd.Parameters.AddWithValue("@Loai", loai);
                cmd.Parameters.AddWithValue("@NoiDung", noiDung);
                cmd.ExecuteNonQuery();
            }
            catch (SqlException ex) when (LaLoiSchemaLichSuThanhToan(ex))
            {
                // Schema cu khong dong nhat; khong de loi ghi lich su chan nghiep vu tra phong.
            }
        }

        private static decimal LayTongTienDichVuHoaDon(SqlConnection conn, SqlTransaction tran, int maHoaDon, int maThue, int? maDatPhong)
        {
            return TinhTongTienDichVuTheoMarker(conn, tran, maThue, maDatPhong, "[DICHVU_PHATSINH]");
        }

        private static DuToanCheckOutDTO TaoDuToanCheckOut(
            SqlConnection conn,
            SqlTransaction tran,
            int maThue,
            int maHoaDon,
            int? maDatPhong,
            DateTime ngayTraThucTe)
        {
            string bangDatPhong = ResolveTable(conn, tran, "PHIEUDATPHONG", "DATPHONG");
            bool coDatPhong = !string.IsNullOrWhiteSpace(bangDatPhong) && ColumnExists(conn, tran, "PHIEUTHUE", "MaDatPhong");
            string joinDatPhong = coDatPhong ? "LEFT JOIN dbo." + bangDatPhong + " DP ON PT.MaDatPhong = DP.MaDatPhong" : string.Empty;
            string cheDoColumn = coDatPhong ? GetFirstExistingColumn(conn, tran, bangDatPhong, "LoaiDat", "CheDoDatPhong", "LoaiDatPhong") : string.Empty;
            string cheDoExpr = coDatPhong && !string.IsNullOrWhiteSpace(cheDoColumn) ? "ISNULL(DP." + cheDoColumn + ", N'')" : "CAST(N'' AS nvarchar(100))";
            string ngayNhanDatColumn = coDatPhong ? GetFirstExistingColumn(conn, tran, bangDatPhong, "NgayNhanDuKien", "NgayNhanPhong", "NgayNhan") : string.Empty;
            string ngayNhanExpr = coDatPhong && !string.IsNullOrWhiteSpace(ngayNhanDatColumn) ? "ISNULL(DP." + ngayNhanDatColumn + ", PT.NgayNhan)" : "PT.NgayNhan";

            using SqlCommand cmd = new(@"
SELECT TOP 1 " + ngayNhanExpr + @" AS NgayNhanDuKien,
       PT.NgayTraDuKien,
       " + cheDoExpr + @" AS CheDoDatPhong,
       ISNULL(LP.DonGiaGio, 0) AS GiaGio,
       ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24)) AS GiaNgay,
       ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(LP.DonGiaGio, 0) * 24)) AS GiaDem,
       " + (ColumnExists(conn, tran, "PHIEUTHUE", "GhiChu") ? "ISNULL(PT.GhiChu, N'')" : "N''") + @" AS GhiChu
FROM dbo.PHIEUTHUE PT
" + joinDatPhong + @"
LEFT JOIN dbo.PHONG P ON PT.MaPhong = P.MaPhong
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
WHERE PT.MaThue = @MaThue", conn, tran);
            cmd.Parameters.AddWithValue("@MaThue", maThue);
            using SqlDataReader reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException("Khong tim thay phieu thue de tinh thanh toan.");
            }

            DateTime ngayNhanDuKien = Convert.ToDateTime(reader["NgayNhanDuKien"]);
            DateTime ngayTraDuKien = Convert.ToDateTime(reader["NgayTraDuKien"]);
            string cheDo = reader["CheDoDatPhong"]?.ToString() ?? string.Empty;
            decimal giaGio = Convert.ToDecimal(reader["GiaGio"]);
            decimal giaNgay = Convert.ToDecimal(reader["GiaNgay"]);
            decimal giaDem = Convert.ToDecimal(reader["GiaDem"]);
            string ghiChu = reader["GhiChu"]?.ToString() ?? string.Empty;
            reader.Close();

            int soPhutTraMuon = Math.Max(0, (int)Math.Ceiling((ngayTraThucTe - ngayTraDuKien).TotalMinutes));
            int soPhutTinhPhi = soPhutTraMuon <= 30 ? 0 : Math.Max(60, (int)Math.Ceiling((soPhutTraMuon - 30) / 60.0) * 60);
            decimal phuPhiTraMuon = TinhPhuPhiTraMuonTheoPhut(ngayNhanDuKien, ngayTraDuKien, ngayTraThucTe, giaGio, giaNgay, giaDem);
            decimal tienGiaHan = DocTongTienMarker(ghiChu, "GIAHAN");
            decimal chenhLechDoiPhong = LayChenhLechDoiPhong(conn, tran, maThue);

            return new DuToanCheckOutDTO
            {
                MaThue = maThue,
                MaHoaDon = maHoaDon,
                NgayTraDuKien = ngayTraDuKien,
                NgayTraThucTe = ngayTraThucTe,
                CheDoDatPhong = cheDo,
                GiaGio = giaGio,
                GiaNgay = giaNgay,
                GiaDem = giaDem,
                TienDichVuPhatSinh = LayTongTienDichVuHoaDon(conn, tran, maHoaDon, maThue, maDatPhong),
                TienGiaHan = tienGiaHan,
                ChenhLechDoiPhong = chenhLechDoiPhong,
                PhuPhiTraMuon = phuPhiTraMuon,
                SoPhutTraMuon = soPhutTraMuon,
                SoPhutTinhPhi = soPhutTinhPhi
            };
        }

        private static decimal TinhPhuPhiTraMuonTheoPhut(DateTime plannedStart, DateTime plannedEnd, DateTime actualEnd, decimal giaGio, decimal giaNgay, decimal giaDem)
        {
            int chargeableMinutes = Math.Max(0, (int)Math.Ceiling((actualEnd - plannedEnd).TotalMinutes));
            if (chargeableMinutes <= 30)
            {
                return 0;
            }

            int chargeableHours = Math.Max(1, (int)Math.Ceiling((chargeableMinutes - 30) / 60.0));
            decimal hourlyRate = LayGiaGioPhuPhiTraMuon(plannedStart, plannedEnd, giaGio, giaNgay, giaDem);

            return Math.Round(chargeableHours * hourlyRate, 0);
        }

        private static decimal LayGiaGioPhuPhiTraMuon(DateTime plannedStart, DateTime plannedEnd, decimal giaGio, decimal giaNgay, decimal giaDem)
        {
            if (plannedStart.Date == plannedEnd.Date && plannedEnd > plannedStart)
            {
                return giaGio > 0 ? giaGio : Math.Round(LayGiaNgayPhuPhi(giaGio, giaNgay) / 24m, 0);
            }

            if (plannedEnd.Date == plannedStart.Date.AddDays(1) &&
                plannedStart.TimeOfDay >= TimeSpan.FromHours(21) &&
                plannedEnd.TimeOfDay <= TimeSpan.FromHours(8.5))
            {
                decimal giaDemTinhPhi = giaDem > 0 ? giaDem : LayGiaNgayPhuPhi(giaGio, giaNgay);
                return Math.Round(giaDemTinhPhi / 12m, 0);
            }

            return Math.Round(LayGiaNgayPhuPhi(giaGio, giaNgay) / 24m, 0);
        }

        private static decimal LayGiaNgayPhuPhi(decimal giaGio, decimal giaNgay)
        {
            if (giaNgay > 0) return giaNgay;
            if (giaGio > 0) return giaGio * 24m;
            return 0;
        }

        private static decimal DocTongTienMarker(string ghiChu, string marker)
        {
            decimal total = 0;
            foreach (Match match in Regex.Matches(ghiChu ?? string.Empty, @"\[" + Regex.Escape(marker) + @"\][^\[]*?SoTien\s*=\s*(-?[0-9][0-9.,]*)", RegexOptions.IgnoreCase))
            {
                string raw = match.Groups[1].Value.Replace(",", string.Empty).Replace(".", string.Empty);
                if (decimal.TryParse(raw, out decimal value))
                {
                    total += value;
                }
            }
            return total;
        }

        private static decimal LayChenhLechDoiPhong(SqlConnection conn, SqlTransaction tran, int maThue)
        {
            if (!TableExists(conn, tran, "DOIPHONG") || !ColumnExists(conn, tran, "DOIPHONG", "ChenhLechTien"))
            {
                return 0;
            }

            using SqlCommand cmd = new("SELECT ISNULL(SUM(ChenhLechTien), 0) FROM dbo.DOIPHONG WHERE MaThue = @MaThue", conn, tran);
            cmd.Parameters.AddWithValue("@MaThue", maThue);
            object? value = cmd.ExecuteScalar();
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static decimal TinhTongTienDichVuTheoMarker(SqlConnection conn, SqlTransaction tran, int? maThue, int? maDatPhong, string marker)
        {
            string table = ResolveTable(conn, tran, "PHATSINHDICHVU", "CHITIETPHATSINH");
            if (string.IsNullOrWhiteSpace(table) || !ColumnExists(conn, tran, table, "GhiChu"))
            {
                return 0;
            }

            string amountExpr = ColumnExists(conn, tran, table, "ThanhTien") ? "ThanhTien" :
                ColumnExists(conn, tran, table, "DonGia") && ColumnExists(conn, tran, table, "SoLuong") ? "(DonGia * SoLuong)" : "0";
            List<string> conditions = new() { "CHARINDEX(@Marker, ISNULL(GhiChu, N'')) > 0" };
            List<string> keys = new();
            if (maThue.HasValue && ColumnExists(conn, tran, table, "MaThue")) keys.Add("MaThue = @MaThue");
            if (maDatPhong.HasValue && ColumnExists(conn, tran, table, "MaDatPhong")) keys.Add("MaDatPhong = @MaDatPhong");
            if (keys.Count == 0)
            {
                return 0;
            }

            conditions.Add("(" + string.Join(" OR ", keys) + ")");
            using SqlCommand cmd = new("SELECT ISNULL(SUM(" + amountExpr + "), 0) FROM dbo." + table + " WHERE " + string.Join(" AND ", conditions), conn, tran);
            cmd.Parameters.AddWithValue("@Marker", marker);
            cmd.Parameters.AddWithValue("@MaThue", maThue.HasValue ? maThue.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong.HasValue ? maDatPhong.Value : DBNull.Value);
            object? value = cmd.ExecuteScalar();
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static void CapNhatTongHoaDonSauCheckOut(
            SqlConnection conn,
            SqlTransaction tran,
            int maHoaDon,
            int maThue,
            int? maDatPhong,
            DuToanCheckOutDTO duToan,
            bool daThanhToan)
        {
            if (maHoaDon <= 0 || !TableExists(conn, tran, "HOADON"))
            {
                return;
            }

            string hoaDonKey = LayCotKhoaHoaDon(conn, tran);
            if (string.IsNullOrWhiteSpace(hoaDonKey))
            {
                return;
            }

            ThongTinHoaDon thongTin = LayThongTinHoaDonTheoThue(conn, tran, maThue);
            decimal tongDichVu = TinhTongTienDichVuPhatSinh(conn, tran, maThue, maDatPhong);
            decimal tongPhuThu = TinhPhuThuTheoThue(conn, tran, maThue) + Math.Max(0, duToan.PhuPhiTraMuon);
            decimal tienPhongCheckIn = LayGiaTriHoaDon(conn, tran, maHoaDon, "TongTienPhong");
            decimal tongThanhToanGoc = LayGiaTriHoaDon(conn, tran, maHoaDon, "TongThanhToan");
            if (tongThanhToanGoc <= 0)
            {
                tongThanhToanGoc = Math.Max(0, Math.Max(tienPhongCheckIn, thongTin.TongTienPhong) - thongTin.TienCoc);
            }
            decimal tongThanhToan = duToan.CanThuThem;

            List<string> sets = new();
            if (WritableColumnExists(conn, tran, "HOADON", "TongTienDichVu")) sets.Add("TongTienDichVu = @TongTienDichVu");
            if (WritableColumnExists(conn, tran, "HOADON", "TongTienDV")) sets.Add("TongTienDV = @TongTienDichVu");
            if (WritableColumnExists(conn, tran, "HOADON", "TongPhuThu")) sets.Add("TongPhuThu = @TongPhuThu");
            if (WritableColumnExists(conn, tran, "HOADON", "TienVat")) sets.Add("TienVat = @TienVat");
            if (WritableColumnExists(conn, tran, "HOADON", "TongThanhToan")) sets.Add("TongThanhToan = @TongThanhToan");
            if (WritableColumnExists(conn, tran, "HOADON", "TongTien")) sets.Add("TongTien = @TongThanhToan");
            if (WritableColumnExists(conn, tran, "HOADON", "NgayLap")) sets.Add("NgayLap = @NgayLap");
            if (WritableColumnExists(conn, tran, "HOADON", "NgayLapHoaDon")) sets.Add("NgayLapHoaDon = @NgayLap");
            if (WritableColumnExists(conn, tran, "HOADON", "DaThanhToan")) sets.Add("DaThanhToan = @DaThanhToan");

            if (sets.Count == 0)
            {
                return;
            }

            using SqlCommand cmd = new("UPDATE dbo.HOADON SET " + string.Join(",", sets) + " WHERE " + hoaDonKey + " = @MaHoaDon", conn, tran);
            cmd.Parameters.AddWithValue("@TongTienDichVu", tongDichVu);
            cmd.Parameters.AddWithValue("@TongPhuThu", tongPhuThu);
            cmd.Parameters.AddWithValue("@TienVat", duToan.ThueVat);
            cmd.Parameters.AddWithValue("@TongThanhToan", tongThanhToan);
            cmd.Parameters.AddWithValue("@DaThanhToan", daThanhToan ? 1 : 0);
            cmd.Parameters.AddWithValue("@NgayLap", DateTime.Now);
            cmd.Parameters.AddWithValue("@MaHoaDon", maHoaDon);
            cmd.ExecuteNonQuery();
        }

        private static decimal LayGiaTriHoaDon(SqlConnection conn, SqlTransaction tran, int maHoaDon, string column)
        {
            if (maHoaDon <= 0 || !TableExists(conn, tran, "HOADON") || !ColumnExists(conn, tran, "HOADON", column))
            {
                return 0;
            }

            string hoaDonKey = LayCotKhoaHoaDon(conn, tran);
            if (string.IsNullOrWhiteSpace(hoaDonKey))
            {
                return 0;
            }

            using SqlCommand cmd = new("SELECT ISNULL(" + column + ", 0) FROM dbo.HOADON WHERE " + hoaDonKey + " = @MaHoaDon", conn, tran);
            cmd.Parameters.AddWithValue("@MaHoaDon", maHoaDon);
            object? value = cmd.ExecuteScalar();
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static decimal TinhTongTienDichVuPhatSinh(SqlConnection conn, SqlTransaction tran, int? maThue, int? maDatPhong)
        {
            decimal markedTotal = TinhTongTienDichVuTheoMarker(conn, tran, maThue, maDatPhong, "[DICHVU_PHATSINH]");
            if (markedTotal > 0)
            {
                return markedTotal;
            }

            string table = ResolveTable(conn, tran, "PHATSINHDICHVU", "CHITIETPHATSINH");
            if (string.IsNullOrWhiteSpace(table))
            {
                return 0;
            }

            string amountExpr = ColumnExists(conn, tran, table, "ThanhTien") ? "ThanhTien" : ColumnExists(conn, tran, table, "DonGia") && ColumnExists(conn, tran, table, "SoLuong") ? "(DonGia * SoLuong)" : "0";
            List<string> conditions = new();
            if (maThue.HasValue && ColumnExists(conn, tran, table, "MaThue")) conditions.Add("MaThue = @MaThue");
            else if (maDatPhong.HasValue && ColumnExists(conn, tran, table, "MaDatPhong")) conditions.Add("MaDatPhong = @MaDatPhong");
            if (conditions.Count == 0)
            {
                return 0;
            }

            using SqlCommand cmd = new("SELECT ISNULL(SUM(" + amountExpr + "), 0) FROM dbo." + table + " WHERE " + string.Join(" AND ", conditions), conn, tran);
            cmd.Parameters.AddWithValue("@MaThue", maThue.HasValue ? maThue.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong.HasValue ? maDatPhong.Value : DBNull.Value);
            object? value = cmd.ExecuteScalar();
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static int LayMaHoaDon(SqlConnection conn, SqlTransaction tran, int? maThue, int? maDatPhong)
        {
            if (!TableExists(conn, tran, "HOADON"))
            {
                return 0;
            }

            string hoaDonKey = LayCotKhoaHoaDon(conn, tran);
            if (string.IsNullOrWhiteSpace(hoaDonKey))
            {
                return 0;
            }

            List<string> conditions = new();
            if (maThue.HasValue && ColumnExists(conn, tran, "HOADON", "MaThue")) conditions.Add("MaThue = @MaThue");
            if (maDatPhong.HasValue && ColumnExists(conn, tran, "HOADON", "MaDatPhong")) conditions.Add("MaDatPhong = @MaDatPhong");
            if (conditions.Count == 0)
            {
                return 0;
            }

            string where = maThue.HasValue && ColumnExists(conn, tran, "HOADON", "MaThue")
                ? "MaThue = @MaThue"
                : string.Join(" OR ", conditions);
            using SqlCommand cmd = new("SELECT " + hoaDonKey + " FROM dbo.HOADON WHERE " + where + " AND ISNULL(TrangThai, N'') NOT IN (N'Đã hủy', N'Da huy')", conn, tran);
            cmd.Parameters.AddWithValue("@MaThue", maThue.HasValue ? maThue.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong.HasValue ? maDatPhong.Value : DBNull.Value);
            List<int> ids = new();
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ids.Add(Convert.ToInt32(reader[0]));
            }

            if (ids.Count > 1)
            {
                throw new InvalidOperationException("Phiếu thuê đã có nhiều hóa đơn. Vui lòng xử lý dữ liệu trùng trước khi thanh toán.");
            }

            return ids.Count == 0 ? 0 : ids[0];
        }

        private static ThongTinHoaDon LayThongTinHoaDonTheoDatPhong(SqlConnection conn, SqlTransaction tran, string bangDatPhong, int maDatPhong, int? maThue)
        {
            string tienCocColumn = GetFirstExistingColumn(conn, tran, bangDatPhong, "TienCoc", "DatCoc");
            string maPhongExpr = ColumnExists(conn, tran, bangDatPhong, "MaPhong") ? "DP.MaPhong" : TableExists(conn, tran, "CHITIETDATPHONG") ? "(SELECT TOP 1 MaPhong FROM dbo.CHITIETDATPHONG CT WHERE CT.MaDatPhong = DP.MaDatPhong ORDER BY MaPhong)" : "CAST(NULL AS int)";
            string tienCocExpr = string.IsNullOrWhiteSpace(tienCocColumn) ? "CAST(0 AS decimal(18,2))" : "ISNULL(DP." + tienCocColumn + ", 0)";
            string tongTienExpr = TienPhongDatPhongExpr(conn, tran, bangDatPhong, "DP", maPhongExpr);
            string ghiChuExpr = ColumnExists(conn, tran, bangDatPhong, "GhiChu") ? "DP.GhiChu" : "CAST(NULL AS nvarchar(1000))";
            string maNhanVienExpr = ColumnExists(conn, tran, bangDatPhong, "MaNV") ? "DP.MaNV" : "CAST(NULL AS int)";
            string ngayNhanColumn = GetFirstExistingColumn(conn, tran, bangDatPhong, "NgayNhanDuKien", "NgayNhanPhong", "NgayNhan");
            string ngayNhanExpr = string.IsNullOrWhiteSpace(ngayNhanColumn) ? "CAST(NULL AS datetime)" : "DP." + ngayNhanColumn;

            using SqlCommand cmd = new(@"
SELECT TOP 1 DP.MaDatPhong,
       DP.MaKH,
       " + maNhanVienExpr + @" AS MaNV,
       " + maPhongExpr + @" AS MaPhong,
       " + ngayNhanExpr + @" AS NgayNhanDuKien,
       " + tienCocExpr + @" AS TienCoc,
       " + tongTienExpr + @" AS TongTienPhong,
       " + ghiChuExpr + @" AS GhiChu
FROM dbo." + bangDatPhong + @" DP
WHERE DP.MaDatPhong = @MaDatPhong", conn, tran);
            cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong);

            using SqlDataReader reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException("Khong tim thay phieu dat phong.");
            }

            int? maPhong = reader["MaPhong"] == DBNull.Value ? null : Convert.ToInt32(reader["MaPhong"]);
            int? maKhachHang = reader["MaKH"] == DBNull.Value ? null : Convert.ToInt32(reader["MaKH"]);
            int? maNhanVien = reader["MaNV"] == DBNull.Value ? null : Convert.ToInt32(reader["MaNV"]);
            DateTime? ngayNhanDuKien = reader["NgayNhanDuKien"] == DBNull.Value ? null : Convert.ToDateTime(reader["NgayNhanDuKien"]);
            decimal tienCoc = reader["TienCoc"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TienCoc"]);
            decimal tongTienPhong = reader["TongTienPhong"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TongTienPhong"]);
            decimal tienPhongDaChot = DocTienPhongDaChot(reader["GhiChu"]?.ToString() ?? string.Empty);
            if (tienPhongDaChot > 0)
            {
                tongTienPhong = tienPhongDaChot;
            }

            reader.Close();

            return new ThongTinHoaDon
            {
                MaDatPhong = maDatPhong,
                MaThue = maThue,
                MaNhanVien = maNhanVien,
                MaKhachHang = maKhachHang,
                MaPhong = maPhong,
                NgayNhanDuKien = ngayNhanDuKien,
                TienCoc = tienCoc,
                TongTienPhong = tongTienPhong,
                TongTienDichVu = TinhTongTienDichVuPhatSinh(conn, tran, maThue, maDatPhong),
                PhuPhi = maThue.HasValue ? TinhPhuThuTheoThue(conn, tran, maThue.Value) : 0
            };
        }

        private static ThongTinHoaDon LayThongTinHoaDonTheoThue(SqlConnection conn, SqlTransaction tran, int maThue)
        {
            if (!TableExists(conn, tran, "PHIEUTHUE"))
            {
                throw new InvalidOperationException("Khong tim thay phieu thue.");
            }

            string maDatPhongExpr = ColumnExists(conn, tran, "PHIEUTHUE", "MaDatPhong") ? "PT.MaDatPhong" : "CAST(NULL AS int)";
            string maPhongExpr = ColumnExists(conn, tran, "PHIEUTHUE", "MaPhong") ? "PT.MaPhong" : "CAST(NULL AS int)";
            string tienCocExpr = ColumnExists(conn, tran, "PHIEUTHUE", "TienCoc") ? "ISNULL(PT.TienCoc, 0)" : "CAST(0 AS decimal(18,2))";
            string tongTienExpr = TienPhongPhieuThueExpr(conn, tran);
            string phuPhiExpr = PhuPhiPhieuThueExpr(conn, tran);
            string ghiChuExpr = ColumnExists(conn, tran, "PHIEUTHUE", "GhiChu") ? "PT.GhiChu" : "CAST(NULL AS nvarchar(1000))";
            string maNhanVienExpr = ColumnExists(conn, tran, "PHIEUTHUE", "MaNV") ? "PT.MaNV" : "CAST(NULL AS int)";

            using SqlCommand cmd = new(@"
SELECT TOP 1 " + maDatPhongExpr + @" AS MaDatPhong,
       PT.MaKH,
       " + maNhanVienExpr + @" AS MaNV,
       " + maPhongExpr + @" AS MaPhong,
       " + tienCocExpr + @" AS TienCoc,
       " + tongTienExpr + @" AS TongTienPhong,
       " + phuPhiExpr + @" AS PhuPhi,
       " + ghiChuExpr + @" AS GhiChu
FROM dbo.PHIEUTHUE PT
WHERE PT.MaThue = @MaThue", conn, tran);
            cmd.Parameters.AddWithValue("@MaThue", maThue);

            using SqlDataReader reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException("Khong tim thay phieu thue.");
            }

            int? maDatPhong = reader["MaDatPhong"] == DBNull.Value ? null : Convert.ToInt32(reader["MaDatPhong"]);
            int? maKhachHang = reader["MaKH"] == DBNull.Value ? null : Convert.ToInt32(reader["MaKH"]);
            int? maNhanVien = reader["MaNV"] == DBNull.Value ? null : Convert.ToInt32(reader["MaNV"]);
            int? maPhong = reader["MaPhong"] == DBNull.Value ? null : Convert.ToInt32(reader["MaPhong"]);
            decimal tienCoc = reader["TienCoc"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TienCoc"]);
            decimal tongTienPhong = reader["TongTienPhong"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TongTienPhong"]);
            decimal phuPhi = reader["PhuPhi"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["PhuPhi"]);
            decimal tienPhongDaChot = DocTienPhongDaChot(reader["GhiChu"]?.ToString() ?? string.Empty);
            if (tienPhongDaChot > 0)
            {
                tongTienPhong = tienPhongDaChot;
            }

            reader.Close();

            return new ThongTinHoaDon
            {
                MaThue = maThue,
                MaDatPhong = maDatPhong,
                MaNhanVien = maNhanVien,
                MaKhachHang = maKhachHang,
                MaPhong = maPhong,
                TienCoc = tienCoc,
                TongTienPhong = tongTienPhong,
                TongTienDichVu = TinhTongTienDichVuPhatSinh(conn, tran, maThue, maDatPhong),
                PhuPhi = phuPhi
            };
        }

        private static string TienPhongDatPhongExpr(SqlConnection conn, SqlTransaction tran, string table, string alias, string maPhongExpr)
        {
            string ngayNhan = ColumnExists(conn, tran, table, "NgayNhanDuKien") ? alias + ".NgayNhanDuKien" : ColumnExists(conn, tran, table, "NgayNhanPhong") ? alias + ".NgayNhanPhong" : "GETDATE()";
            string ngayTra = ColumnExists(conn, tran, table, "NgayTraDuKien") ? alias + ".NgayTraDuKien" : ColumnExists(conn, tran, table, "NgayTraPhong") ? alias + ".NgayTraPhong" : "DATEADD(day, 1, GETDATE())";
            string giaNgay = GiaNgayTheoPhongExpr(conn, tran, maPhongExpr);
            return TienPhongSql(ngayNhan, ngayTra, giaNgay);
        }

        private static decimal DocTienPhongDaChot(string ghiChu)
        {
            if (string.IsNullOrWhiteSpace(ghiChu))
            {
                return 0;
            }

            Match match = Regex.Match(ghiChu, @"(?:TongTienPhong|TienPhong)\s*=\s*([0-9][0-9.,]*)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return 0;
            }

            string raw = match.Groups[1].Value.Replace(",", string.Empty).Replace(".", string.Empty);
            return decimal.TryParse(raw, out decimal value) ? value : 0;
        }

        private static string TienPhongPhieuThueExpr(SqlConnection conn, SqlTransaction tran)
        {
            string ngayTra = ColumnExists(conn, tran, "PHIEUTHUE", "NgayTraPhong") ? "ISNULL(PT.NgayTraPhong, PT.NgayTraDuKien)" : "PT.NgayTraDuKien";
            string giaNgay = GiaNgayTheoPhongExpr(conn, tran, "PT.MaPhong");
            return TienPhongSql("PT.NgayNhan", ngayTra, giaNgay);
        }

        private static string PhuPhiPhieuThueExpr(SqlConnection conn, SqlTransaction tran)
        {
            string ngayTra = ColumnExists(conn, tran, "PHIEUTHUE", "NgayTraPhong") ? "ISNULL(PT.NgayTraPhong, GETDATE())" : "GETDATE()";
            string giaNgay = GiaNgayTheoPhongExpr(conn, tran, "PT.MaPhong");
            string giaGio = GiaGioTheoPhongExpr(conn, tran, "PT.MaPhong", giaNgay);
            string giaDem = GiaDemTheoPhongExpr(conn, tran, "PT.MaPhong", giaNgay);
            return PhuThuSql("PT.NgayNhan", "PT.NgayTraDuKien", ngayTra, giaNgay, giaGio, giaDem);
        }

        private static string GiaNgayTheoPhongExpr(SqlConnection conn, SqlTransaction tran, string maPhongExpr)
        {
            if (TableExists(conn, tran, "LOAIPHONG") && TableExists(conn, tran, "PHONG") && ColumnExists(conn, tran, "PHONG", "MaLoaiPhong"))
            {
                return @"(SELECT TOP 1 ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0))
                          FROM dbo.PHONG P
                          JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
                          WHERE P.MaPhong = " + maPhongExpr + ")";
            }

            return ColumnExists(conn, tran, "PHONG", "GiaNgay")
                ? "(SELECT TOP 1 ISNULL(GiaNgay, 0) FROM dbo.PHONG P WHERE P.MaPhong = " + maPhongExpr + ")"
                : "0";
        }

        private static string GiaGioTheoPhongExpr(SqlConnection conn, SqlTransaction tran, string maPhongExpr, string giaNgayExpr)
        {
            if (TableExists(conn, tran, "LOAIPHONG") && TableExists(conn, tran, "PHONG") && ColumnExists(conn, tran, "PHONG", "MaLoaiPhong"))
            {
                return @"(SELECT TOP 1 ISNULL(NULLIF(LP.DonGiaGio, 0), (" + giaNgayExpr + @") / 24.0)
                          FROM dbo.PHONG P
                          JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
                          WHERE P.MaPhong = " + maPhongExpr + ")";
            }

            return ColumnExists(conn, tran, "PHONG", "GiaGio")
                ? "(SELECT TOP 1 ISNULL(NULLIF(GiaGio, 0), (" + giaNgayExpr + ") / 24.0) FROM dbo.PHONG P WHERE P.MaPhong = " + maPhongExpr + ")"
                : "(" + giaNgayExpr + ") / 24.0";
        }

        private static string GiaDemTheoPhongExpr(SqlConnection conn, SqlTransaction tran, string maPhongExpr, string giaNgayExpr)
        {
            if (TableExists(conn, tran, "LOAIPHONG") && TableExists(conn, tran, "PHONG") && ColumnExists(conn, tran, "PHONG", "MaLoaiPhong"))
            {
                return @"(SELECT TOP 1 ISNULL(NULLIF(LP.DonGiaDem, 0), " + giaNgayExpr + @")
                          FROM dbo.PHONG P
                          JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
                          WHERE P.MaPhong = " + maPhongExpr + ")";
            }

            return ColumnExists(conn, tran, "PHONG", "GiaDem")
                ? "(SELECT TOP 1 ISNULL(NULLIF(GiaDem, 0), (" + giaNgayExpr + ")) FROM dbo.PHONG P WHERE P.MaPhong = " + maPhongExpr + ")"
                : giaNgayExpr;
        }

        private static decimal TinhPhuThuTheoThue(SqlConnection conn, SqlTransaction tran, int maThue)
        {
            if (!TableExists(conn, tran, "PHIEUTHUE"))
            {
                return 0;
            }

            string phuPhiExpr = PhuPhiPhieuThueExpr(conn, tran);
            using SqlCommand cmd = new("SELECT " + phuPhiExpr + " FROM dbo.PHIEUTHUE PT WHERE PT.MaThue = @MaThue", conn, tran);
            cmd.Parameters.AddWithValue("@MaThue", maThue);
            object? value = cmd.ExecuteScalar();
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static string TienPhongSql(string startExpr, string endExpr, string giaNgayExpr)
        {
            return "CAST(CASE WHEN " + endExpr + " IS NULL OR DATEDIFF(day, " + startExpr + ", " + endExpr + ") <= 0 THEN " + giaNgayExpr + " ELSE DATEDIFF(day, " + startExpr + ", " + endExpr + ") * " + giaNgayExpr + " END AS decimal(18,2))";
        }

        private static string PhuThuSql(string startExpr, string plannedEndExpr, string actualEndExpr, string giaNgayExpr, string giaGioExpr, string giaDemExpr)
        {
            string laThueTheoGioExpr = "(" + plannedEndExpr + @" IS NOT NULL
        AND CAST(" + startExpr + @" AS date) = CAST(" + plannedEndExpr + @" AS date)
        AND DATEDIFF(minute, " + startExpr + @", " + plannedEndExpr + @") > 0)";
            string laThueQuaDemExpr = "(" + plannedEndExpr + @" IS NOT NULL
        AND CAST(" + plannedEndExpr + @" AS date) = DATEADD(day, 1, CAST(" + startExpr + @" AS date))
        AND CAST(" + startExpr + @" AS time) >= CAST('21:00' AS time)
        AND CAST(" + plannedEndExpr + @" AS time) <= CAST('08:30' AS time))";
            string giaTheoLoaiExpr = @"CASE
        WHEN " + laThueTheoGioExpr + @" THEN " + giaGioExpr + @"
        WHEN " + laThueQuaDemExpr + @" THEN (" + giaDemExpr + @") / 12.0
        ELSE (" + giaNgayExpr + @") / 24.0
    END";

            return @"CAST(CASE
WHEN " + laThueTheoGioExpr + @" THEN
    CASE
        WHEN " + actualEndExpr + @" IS NULL THEN 0
        WHEN " + actualEndExpr + @" <= " + plannedEndExpr + @" THEN 0
        WHEN DATEDIFF(minute, " + plannedEndExpr + @", " + actualEndExpr + @") <= 30 THEN 0
        ELSE CEILING((DATEDIFF(minute, " + plannedEndExpr + @", " + actualEndExpr + @") - 30) / 60.0) * " + giaTheoLoaiExpr + @"
    END
WHEN " + laThueQuaDemExpr + @" THEN
    CASE
        WHEN " + actualEndExpr + @" IS NULL THEN 0
        WHEN " + actualEndExpr + @" <= " + plannedEndExpr + @" THEN 0
        WHEN DATEDIFF(minute, " + plannedEndExpr + @", " + actualEndExpr + @") <= 30 THEN 0
        ELSE CEILING((DATEDIFF(minute, " + plannedEndExpr + @", " + actualEndExpr + @") - 30) / 60.0) * " + giaTheoLoaiExpr + @"
    END
ELSE (
    CASE
        WHEN " + actualEndExpr + @" IS NULL THEN 0
        WHEN " + actualEndExpr + @" <= " + plannedEndExpr + @" THEN 0
        WHEN DATEDIFF(minute, " + plannedEndExpr + @", " + actualEndExpr + @") <= 30 THEN 0
        ELSE CEILING((DATEDIFF(minute, " + plannedEndExpr + @", " + actualEndExpr + @") - 30) / 60.0) * " + giaTheoLoaiExpr + @"
    END
) END AS decimal(18,2))";
        }

        private static int? LayMaThueTheoDatPhong(SqlConnection conn, SqlTransaction tran, int maDatPhong)
        {
            if (!TableExists(conn, tran, "PHIEUTHUE") || !ColumnExists(conn, tran, "PHIEUTHUE", "MaDatPhong"))
            {
                return null;
            }

            using SqlCommand cmd = new("SELECT TOP 1 MaThue FROM dbo.PHIEUTHUE WHERE MaDatPhong = @MaDatPhong ORDER BY MaThue DESC", conn, tran);
            cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong);
            object? value = cmd.ExecuteScalar();
            return value == null || value == DBNull.Value ? null : Convert.ToInt32(value);
        }

        private static int? LayMaDatPhongTheoThue(SqlConnection conn, SqlTransaction tran, int maThue)
        {
            if (!TableExists(conn, tran, "PHIEUTHUE") || !ColumnExists(conn, tran, "PHIEUTHUE", "MaDatPhong"))
            {
                return null;
            }

            using SqlCommand cmd = new("SELECT TOP 1 MaDatPhong FROM dbo.PHIEUTHUE WHERE MaThue = @MaThue", conn, tran);
            cmd.Parameters.AddWithValue("@MaThue", maThue);
            object? value = cmd.ExecuteScalar();
            return value == null || value == DBNull.Value ? null : Convert.ToInt32(value);
        }

        private static void CapNhatTrangThaiHoaDon(SqlConnection conn, SqlTransaction tran, int maHoaDon, params string[] priorities)
        {
            if (maHoaDon <= 0 || !TableExists(conn, tran, "HOADON") || !ColumnExists(conn, tran, "HOADON", "TrangThai"))
            {
                return;
            }

            string hoaDonKey = LayCotKhoaHoaDon(conn, tran);
            if (string.IsNullOrWhiteSpace(hoaDonKey))
            {
                return;
            }

            string trangThai = LayGiaTriHopLeTheoCheck(conn, tran, "HOADON", "TrangThai", priorities);
            string daThanhToanSet = ColumnExists(conn, tran, "HOADON", "DaThanhToan") ? ", DaThanhToan = @DaThanhToan" : string.Empty;
            using SqlCommand cmd = new("UPDATE dbo.HOADON SET TrangThai = @TrangThai" + daThanhToanSet + " WHERE " + hoaDonKey + " = @MaHoaDon", conn, tran);
            cmd.Parameters.AddWithValue("@TrangThai", trangThai);
            if (!string.IsNullOrWhiteSpace(daThanhToanSet))
            {
                cmd.Parameters.AddWithValue("@DaThanhToan", LaTrangThaiDaThanhToan(trangThai) ? 1 : 0);
            }
            cmd.Parameters.AddWithValue("@MaHoaDon", maHoaDon);
            cmd.ExecuteNonQuery();
        }

        private static void CapNhatNgayTraPhong(SqlConnection conn, SqlTransaction tran, int maThue, List<int> nhomMaDatPhong, DateTime ngayTraPhong)
        {
            if (!TableExists(conn, tran, "PHIEUTHUE") || !ColumnExists(conn, tran, "PHIEUTHUE", "NgayTraPhong"))
            {
                return;
            }

            using SqlCommand cmd = new("UPDATE dbo.PHIEUTHUE SET NgayTraPhong = ISNULL(NgayTraPhong, @NgayTra) WHERE MaThue = @MaThue", conn, tran);
            cmd.Parameters.AddWithValue("@NgayTra", ngayTraPhong);
            cmd.Parameters.AddWithValue("@MaThue", maThue);
            cmd.ExecuteNonQuery();

            if (nhomMaDatPhong.Count > 0 && ColumnExists(conn, tran, "PHIEUTHUE", "MaDatPhong"))
            {
                string danhSachThamSo = TaoDanhSachThamSo(nhomMaDatPhong, "MaDatPhong");
                using SqlCommand updateGroup = new("UPDATE dbo.PHIEUTHUE SET NgayTraPhong = ISNULL(NgayTraPhong, @NgayTra) WHERE MaDatPhong IN (" + danhSachThamSo + ")", conn, tran);
                updateGroup.Parameters.AddWithValue("@NgayTra", ngayTraPhong);
                GanDanhSachThamSo(updateGroup, nhomMaDatPhong, "MaDatPhong");
                updateGroup.ExecuteNonQuery();
            }
        }

        private static void CapNhatTrangThaiPhieuThue(SqlConnection conn, SqlTransaction tran, int maThue, List<int> nhomMaDatPhong, DateTime ngayTraPhong, params string[] priorities)
        {
            if (!TableExists(conn, tran, "PHIEUTHUE") || !ColumnExists(conn, tran, "PHIEUTHUE", "TrangThai"))
            {
                return;
            }

            string trangThai = LayGiaTriHopLeTheoCheck(conn, tran, "PHIEUTHUE", "TrangThai", priorities);
            string setNgayTra = ColumnExists(conn, tran, "PHIEUTHUE", "NgayTraPhong") ? ", NgayTraPhong = ISNULL(NgayTraPhong, @NgayTra)" : string.Empty;
            using SqlCommand cmd = new("UPDATE dbo.PHIEUTHUE SET TrangThai = @TrangThai" + setNgayTra + " WHERE MaThue = @MaThue", conn, tran);
            cmd.Parameters.AddWithValue("@TrangThai", trangThai);
            cmd.Parameters.AddWithValue("@NgayTra", ngayTraPhong);
            cmd.Parameters.AddWithValue("@MaThue", maThue);
            cmd.ExecuteNonQuery();

            if (nhomMaDatPhong.Count > 0 && ColumnExists(conn, tran, "PHIEUTHUE", "MaDatPhong"))
            {
                string danhSachThamSo = TaoDanhSachThamSo(nhomMaDatPhong, "MaDatPhong");
                using SqlCommand updateGroup = new("UPDATE dbo.PHIEUTHUE SET TrangThai = @TrangThai" + setNgayTra + " WHERE MaDatPhong IN (" + danhSachThamSo + ")", conn, tran);
                updateGroup.Parameters.AddWithValue("@TrangThai", trangThai);
                updateGroup.Parameters.AddWithValue("@NgayTra", ngayTraPhong);
                GanDanhSachThamSo(updateGroup, nhomMaDatPhong, "MaDatPhong");
                updateGroup.ExecuteNonQuery();
            }
        }

        private static void CapNhatTrangThaiDatPhong(SqlConnection conn, SqlTransaction tran, string table, int maDatPhong, params string[] priorities)
        {
            if (!ColumnExists(conn, tran, table, "TrangThai"))
            {
                return;
            }

            using SqlCommand cmd = new("UPDATE dbo." + table + " SET TrangThai = @TrangThai WHERE MaDatPhong = @MaDatPhong", conn, tran);
            cmd.Parameters.AddWithValue("@TrangThai", LayGiaTriHopLeTheoCheck(conn, tran, table, "TrangThai", priorities));
            cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong);
            cmd.ExecuteNonQuery();
        }

        private static void CapNhatTrangThaiPhieuThueTheoDatPhong(SqlConnection conn, SqlTransaction tran, int maDatPhong, params string[] priorities)
        {
            if (!TableExists(conn, tran, "PHIEUTHUE") ||
                !ColumnExists(conn, tran, "PHIEUTHUE", "MaDatPhong") ||
                !ColumnExists(conn, tran, "PHIEUTHUE", "TrangThai"))
            {
                return;
            }

            using SqlCommand cmd = new("UPDATE dbo.PHIEUTHUE SET TrangThai = @TrangThai WHERE MaDatPhong = @MaDatPhong", conn, tran);
            cmd.Parameters.AddWithValue("@TrangThai", LayGiaTriHopLeTheoCheck(conn, tran, "PHIEUTHUE", "TrangThai", priorities));
            cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong);
            cmd.ExecuteNonQuery();
        }

        private static void CapNhatTrangThaiPhongTheoDatPhong(SqlConnection conn, SqlTransaction tran, string bangDatPhong, int maDatPhong, params string[] priorities)
        {
            string trangThai = LayGiaTriHopLeTheoCheck(conn, tran, "PHONG", "TrangThai", priorities);
            if (TableExists(conn, tran, "CHITIETDATPHONG"))
            {
                using SqlCommand cmd = new(
                    @"UPDATE P
                      SET P.TrangThai = @TrangThai
                      FROM dbo.PHONG P
                      JOIN dbo.CHITIETDATPHONG CT ON P.MaPhong = CT.MaPhong
                      WHERE CT.MaDatPhong = @MaDatPhong",
                    conn,
                    tran);
                cmd.Parameters.AddWithValue("@TrangThai", trangThai);
                cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong);
                cmd.ExecuteNonQuery();
            }

            if (ColumnExists(conn, tran, bangDatPhong, "MaPhong"))
            {
                using SqlCommand cmd = new(
                    @"UPDATE P
                      SET P.TrangThai = @TrangThai
                      FROM dbo.PHONG P
                      JOIN dbo." + bangDatPhong + @" DP ON P.MaPhong = DP.MaPhong
                      WHERE DP.MaDatPhong = @MaDatPhong",
                    conn,
                    tran);
                cmd.Parameters.AddWithValue("@TrangThai", trangThai);
                cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong);
                cmd.ExecuteNonQuery();
            }
        }

        private static void CapNhatTrangThaiPhongTheoThue(SqlConnection conn, SqlTransaction tran, int maThue, List<int> nhomMaDatPhong, params string[] priorities)
        {
            string trangThai = LayGiaTriHopLeTheoCheck(conn, tran, "PHONG", "TrangThai", priorities);
            string bangDatPhong = ResolveTable(conn, tran, "PHIEUDATPHONG", "DATPHONG");
            if (nhomMaDatPhong.Count > 0)
            {
                CapNhatTrangThaiPhongTheoNhomDatPhong(conn, tran, bangDatPhong, nhomMaDatPhong, trangThai);
            }

            if (ColumnExists(conn, tran, "PHIEUTHUE", "MaDatPhong") && !string.IsNullOrWhiteSpace(bangDatPhong) && TableExists(conn, tran, "CHITIETDATPHONG"))
            {
                using SqlCommand cmd = new(
                    @"UPDATE P
                      SET P.TrangThai = @TrangThai
                      FROM dbo.PHIEUTHUE PT
                      JOIN dbo.CHITIETDATPHONG CT ON PT.MaDatPhong = CT.MaDatPhong
                      JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong
                      WHERE PT.MaThue = @MaThue",
                    conn,
                    tran);
                cmd.Parameters.AddWithValue("@TrangThai", trangThai);
                cmd.Parameters.AddWithValue("@MaThue", maThue);
                cmd.ExecuteNonQuery();
            }

            if (ColumnExists(conn, tran, "PHIEUTHUE", "MaPhong"))
            {
                using SqlCommand cmd = new(
                    @"UPDATE P
                      SET P.TrangThai = @TrangThai
                      FROM dbo.PHONG P
                      JOIN dbo.PHIEUTHUE PT ON P.MaPhong = PT.MaPhong
                      WHERE PT.MaThue = @MaThue",
                    conn,
                    tran);
                cmd.Parameters.AddWithValue("@TrangThai", trangThai);
                cmd.Parameters.AddWithValue("@MaThue", maThue);
                cmd.ExecuteNonQuery();
            }
        }

        private static void CapNhatTrangThaiPhongTheoNhomDatPhong(SqlConnection conn, SqlTransaction tran, string bangDatPhong, List<int> nhomMaDatPhong, string trangThai)
        {
            if (string.IsNullOrWhiteSpace(bangDatPhong) || nhomMaDatPhong.Count == 0)
            {
                return;
            }

            string danhSachThamSo = TaoDanhSachThamSo(nhomMaDatPhong, "MaDatPhong");
            if (TableExists(conn, tran, "CHITIETDATPHONG"))
            {
                using SqlCommand cmd = new(
                    @"UPDATE P
                      SET P.TrangThai = @TrangThai
                      FROM dbo.PHONG P
                      JOIN dbo.CHITIETDATPHONG CT ON P.MaPhong = CT.MaPhong
                      WHERE CT.MaDatPhong IN (" + danhSachThamSo + ")",
                    conn,
                    tran);
                cmd.Parameters.AddWithValue("@TrangThai", trangThai);
                GanDanhSachThamSo(cmd, nhomMaDatPhong, "MaDatPhong");
                cmd.ExecuteNonQuery();
            }

            if (ColumnExists(conn, tran, bangDatPhong, "MaPhong"))
            {
                using SqlCommand cmd = new(
                    @"UPDATE P
                      SET P.TrangThai = @TrangThai
                      FROM dbo.PHONG P
                      JOIN dbo." + bangDatPhong + @" DP ON P.MaPhong = DP.MaPhong
                      WHERE DP.MaDatPhong IN (" + danhSachThamSo + ")",
                    conn,
                    tran);
                cmd.Parameters.AddWithValue("@TrangThai", trangThai);
                GanDanhSachThamSo(cmd, nhomMaDatPhong, "MaDatPhong");
                cmd.ExecuteNonQuery();
            }
        }

        private static List<int> LayNhomDatPhongTheoThue(SqlConnection conn, SqlTransaction tran, int maThue)
        {
            string bangDatPhong = ResolveTable(conn, tran, "PHIEUDATPHONG", "DATPHONG");
            if (string.IsNullOrWhiteSpace(bangDatPhong) || !ColumnExists(conn, tran, "PHIEUTHUE", "MaDatPhong"))
            {
                return new List<int>();
            }

            int? maDatPhong = LayMaDatPhongTheoThue(conn, tran, maThue);
            if (!maDatPhong.HasValue)
            {
                return new List<int>();
            }

            return LayNhomDatPhongLienQuan(conn, tran, bangDatPhong, maDatPhong.Value);
        }

        private static List<int> LayNhomDatPhongLienQuan(SqlConnection conn, SqlTransaction tran, string bangDatPhong, int maDatPhong)
        {
            if (ColumnExists(conn, tran, bangDatPhong, "MaDoan"))
            {
                using SqlCommand maDoanCmd = new("SELECT TOP 1 MaDoan FROM dbo." + bangDatPhong + " WHERE MaDatPhong = @MaDatPhong", conn, tran);
                maDoanCmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong);
                object? maDoanValue = maDoanCmd.ExecuteScalar();
                if (maDoanValue != null && maDoanValue != DBNull.Value && Convert.ToInt32(maDoanValue) > 0)
                {
                    string trangThaiDoanFilter = ColumnExists(conn, tran, bangDatPhong, "TrangThai")
                        ? "  AND (TrangThai IS NULL OR TrangThai NOT IN (N'Da huy', N'Da tra', N'Da tra phong', N'No-Show', N'No Show'))"
                        : string.Empty;
                    using SqlCommand byDoan = new(@"
SELECT MaDatPhong
FROM dbo." + bangDatPhong + @"
WHERE MaDoan = @MaDoan
" + trangThaiDoanFilter + @"
ORDER BY MaDatPhong", conn, tran);
                    byDoan.Parameters.AddWithValue("@MaDoan", Convert.ToInt32(maDoanValue));

                    List<int> byDoanResult = new();
                    using SqlDataReader byDoanReader = byDoan.ExecuteReader();
                    while (byDoanReader.Read())
                    {
                        byDoanResult.Add(Convert.ToInt32(byDoanReader["MaDatPhong"]));
                    }

                    if (byDoanResult.Count > 0)
                    {
                        return byDoanResult;
                    }
                }
            }

            string ngayNhanColumn = ColumnExists(conn, tran, bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : ColumnExists(conn, tran, bangDatPhong, "NgayNhanPhong") ? "NgayNhanPhong" : string.Empty;
            string ngayTraColumn = ColumnExists(conn, tran, bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : ColumnExists(conn, tran, bangDatPhong, "NgayTraPhong") ? "NgayTraPhong" : string.Empty;
            if (string.IsNullOrWhiteSpace(ngayNhanColumn) || string.IsNullOrWhiteSpace(ngayTraColumn) || !ColumnExists(conn, tran, bangDatPhong, "MaKH"))
            {
                return new List<int> { maDatPhong };
            }

            using SqlCommand info = new(@"
SELECT TOP 1 MaKH,
       CONVERT(date, " + ngayNhanColumn + @") AS NgayNhan,
       CONVERT(date, " + ngayTraColumn + @") AS NgayTra
FROM dbo." + bangDatPhong + @"
WHERE MaDatPhong = @MaDatPhong", conn, tran);
            info.Parameters.AddWithValue("@MaDatPhong", maDatPhong);

            int maKhachHang;
            DateTime ngayNhan;
            DateTime ngayTra;
            using (SqlDataReader reader = info.ExecuteReader())
            {
                if (!reader.Read())
                {
                    return new List<int> { maDatPhong };
                }

                maKhachHang = Convert.ToInt32(reader["MaKH"]);
                ngayNhan = Convert.ToDateTime(reader["NgayNhan"]);
                ngayTra = Convert.ToDateTime(reader["NgayTra"]);
            }

            string trangThaiFilter = ColumnExists(conn, tran, bangDatPhong, "TrangThai")
                ? "  AND (TrangThai IS NULL OR TrangThai NOT IN (N'Da huy', N'Da tra', N'Da tra phong', N'No-Show', N'No Show'))"
                : string.Empty;

            using SqlCommand cmd = new(@"
SELECT MaDatPhong
FROM dbo." + bangDatPhong + @"
WHERE MaKH = @MaKH
  AND CONVERT(date, " + ngayNhanColumn + @") = @NgayNhan
  AND CONVERT(date, " + ngayTraColumn + @") = @NgayTra
" + trangThaiFilter + @"
ORDER BY MaDatPhong", conn, tran);
            cmd.Parameters.AddWithValue("@MaKH", maKhachHang);
            cmd.Parameters.AddWithValue("@NgayNhan", ngayNhan.Date);
            cmd.Parameters.AddWithValue("@NgayTra", ngayTra.Date);

            List<int> result = new();
            using SqlDataReader groupReader = cmd.ExecuteReader();
            while (groupReader.Read())
            {
                result.Add(Convert.ToInt32(groupReader["MaDatPhong"]));
            }

            return result.Count == 0 ? new List<int> { maDatPhong } : result;
        }

        private static string TaoDanhSachThamSo(List<int> values, string prefix)
        {
            return string.Join(", ", values.Select((_, index) => "@" + prefix + index));
        }

        private static void GanDanhSachThamSo(SqlCommand cmd, List<int> values, string prefix)
        {
            for (int i = 0; i < values.Count; i++)
            {
                cmd.Parameters.AddWithValue("@" + prefix + i, values[i]);
            }
        }

        private static string LayGiaTriHopLeTheoCheck(SqlConnection conn, SqlTransaction tran, string tableName, string columnName, params string[] priorities)
        {
            List<string> allowed = LayGiaTriTrongCheckConstraint(conn, tran, tableName, columnName);
            foreach (string priority in priorities)
            {
                string? exact = allowed.FirstOrDefault(item => string.Equals(item, priority, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(exact)) return exact;

                string priorityKey = BoDau(priority);
                string? normalized = allowed.FirstOrDefault(item => string.Equals(BoDau(item), priorityKey, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(normalized)) return normalized;
            }

            return ChonGiaTriGanDung(allowed, priorities) ?? priorities.FirstOrDefault() ?? string.Empty;
        }

        private static List<string> LayGiaTriTrongCheckConstraint(SqlConnection conn, SqlTransaction tran, string tableName, string columnName)
        {
            List<string> result = new();
            using SqlCommand cmd = new(
                @"SELECT cc.definition
                  FROM sys.check_constraints cc
                  JOIN sys.tables t ON cc.parent_object_id = t.object_id
                  WHERE t.name = @TableName AND cc.definition LIKE @ColumnName",
                conn,
                tran);
            cmd.Parameters.AddWithValue("@TableName", tableName);
            cmd.Parameters.AddWithValue("@ColumnName", "%" + columnName + "%");
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string definition = reader[0]?.ToString() ?? string.Empty;
                foreach (Match match in Regex.Matches(definition, @"N?'((?:''|[^'])*)'"))
                {
                    string value = match.Groups[1].Value.Replace("''", "'");
                    if (!result.Contains(value, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Add(value);
                    }
                }
            }

            return result;
        }

        private static string? ChonGiaTriGanDung(List<string> allowed, params string[] priorities)
        {
            foreach (string priority in priorities)
            {
                string p = BoDau(priority).ToLowerInvariant();
                string? match = allowed.FirstOrDefault(value =>
                {
                    string v = BoDau(value).ToLowerInvariant();
                    return (p.Contains("trong") && v.Contains("trong")) ||
                           ((p.Contains("bao tri") || p.Contains("sua")) && (v.Contains("bao tri") || v.Contains("sua"))) ||
                           ((p.Contains("check-in") || p.Contains("thue") || p.Contains("co khach")) &&
                            (v.Contains("check-in") || v.Contains("thue") || v.Contains("co khach") || v.Contains("xac nhan"))) ||
                           (p.Contains("dat") && v.Contains("dat")) ||
                           (p.Contains("huy") && v.Contains("huy")) ||
                           (p.Contains("tra") && v.Contains("tra")) ||
                           (p.Contains("thanh toan") && v.Contains("thanh toan"));
                });
                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }
            }

            return null;
        }

        private static void AddColumnIfExists(SqlConnection conn, SqlTransaction tran, List<string> columns, List<string> values, string tableName, string columnName, string valueExpression)
        {
            if (WritableColumnExists(conn, tran, tableName, columnName))
            {
                columns.Add(columnName);
                values.Add(valueExpression);
            }
        }

        private static void AddFirstColumnIfExists(SqlConnection conn, SqlTransaction tran, List<string> columns, List<string> values, string tableName, string valueExpression, params string[] candidates)
        {
            string column = GetFirstExistingColumn(conn, tran, tableName, candidates);
            if (!string.IsNullOrWhiteSpace(column) &&
                !columns.Contains(column, StringComparer.OrdinalIgnoreCase) &&
                WritableColumnExists(conn, tran, tableName, column))
            {
                columns.Add(column);
                values.Add(valueExpression);
            }
        }

        private static void DamBaoCotGiaTri(SqlConnection conn, SqlTransaction tran, List<string> columns, List<string> values, string tableName, string columnName, string valueExpression)
        {
            if (!columns.Contains(columnName, StringComparer.OrdinalIgnoreCase) && WritableColumnExists(conn, tran, tableName, columnName))
            {
                columns.Add(columnName);
                values.Add(valueExpression);
            }
        }

        private static void AddSetIfExists(SqlConnection conn, SqlTransaction tran, List<string> sets, string tableName, string columnName, string parameter)
        {
            if (WritableColumnExists(conn, tran, tableName, columnName))
            {
                sets.Add(columnName + " = " + parameter);
            }
        }

        private static bool TableExists(SqlConnection conn, SqlTransaction tran, string tableName)
        {
            using SqlCommand cmd = new("SELECT COUNT(*) FROM sys.tables WHERE name = @Name", conn, tran);
            cmd.Parameters.AddWithValue("@Name", tableName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
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

        private static bool WritableColumnExists(SqlConnection conn, SqlTransaction tran, string tableName, string columnName)
        {
            using SqlCommand cmd = new(
                @"SELECT COUNT(*)
                  FROM sys.tables t
                  JOIN sys.columns c ON t.object_id = c.object_id
                  WHERE t.name = @TableName
                    AND c.name = @ColumnName
                    AND c.is_computed = 0",
                conn,
                tran);
            cmd.Parameters.AddWithValue("@TableName", tableName);
            cmd.Parameters.AddWithValue("@ColumnName", columnName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static bool ColumnRequired(SqlConnection conn, SqlTransaction tran, string tableName, string columnName)
        {
            using SqlCommand cmd = new(
                @"SELECT COUNT(*)
                  FROM sys.tables t
                  JOIN sys.columns c ON t.object_id = c.object_id
                  WHERE t.name = @TableName
                    AND c.name = @ColumnName
                    AND c.is_nullable = 0
                    AND COLUMNPROPERTY(t.object_id, c.name, 'IsIdentity') = 0",
                conn,
                tran);
            cmd.Parameters.AddWithValue("@TableName", tableName);
            cmd.Parameters.AddWithValue("@ColumnName", columnName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static string ResolveTable(SqlConnection conn, SqlTransaction tran, params string[] candidates)
        {
            return candidates.FirstOrDefault(table => TableExists(conn, tran, table)) ?? string.Empty;
        }

        private static string GetFirstExistingColumn(SqlConnection conn, SqlTransaction tran, string table, params string[] candidates)
        {
            return candidates.FirstOrDefault(column => ColumnExists(conn, tran, table, column)) ?? string.Empty;
        }

        private static string LayCotKhoaHoaDon(SqlConnection conn, SqlTransaction tran)
        {
            return GetFirstExistingColumn(conn, tran, "HOADON", "MaHoaDon", "MaHD", "IDHoaDon", "HoaDonID", "IdHoaDon", "ID", "Ma");
        }

        private static bool LaLoiSchemaLichSuThanhToan(SqlException ex)
        {
            foreach (SqlError error in ex.Errors)
            {
                if (error.Number is 207 or 208 or 213 or 515 or 547)
                {
                    return true;
                }
            }

            return false;
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

        private class ThongTinHoaDon
        {
            public int? MaHoaDon { get; set; }
            public int? MaThue { get; set; }
            public int? MaDatPhong { get; set; }
            public int? MaNhanVien { get; set; }
            public int? MaKhachHang { get; set; }
            public int? MaPhong { get; set; }
            public DateTime? NgayNhanDuKien { get; set; }
            public decimal TongTienPhong { get; set; }
            public decimal TongTienDichVu { get; set; }
            public decimal PhuPhi { get; set; }
            public decimal TienCoc { get; set; }
        }
    }
}
