using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.DAL
{
    public class DatPhongDAL
    {
        public bool KiemTraPhongRanh(int maPhong, DateTime ngayNhanMoi, DateTime ngayTraMoi, out string lyDo)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable);

            try
            {
                BookingScheduleGuard.EnsureRoomAvailable(conn, tran, maPhong, ngayNhanMoi, ngayTraMoi);
                tran.Commit();
                lyDo = string.Empty;
                return true;
            }
            catch (InvalidOperationException ex)
            {
                tran.Rollback();
                lyDo = ex.Message;
                return false;
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        public int LuuDatPhong(DatPhongRequestDTO request)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable);

            try
            {
                DamBaoNhanVienHopLe(request.MaNhanVien);
                BookingScheduleGuard.EnsureRoomAvailable(conn, tran, request.Phong.Ma, request.NgayNhan, request.NgayTra);

                int maKhachHang = LayHoacThemKhachHang(conn, tran, request.KhachHang);
                int maDatPhong = ThemDatPhong(conn, tran, request, maKhachHang);
                ThemChiTietDatPhong(conn, tran, maDatPhong, request);

                if (request.NhanNgay)
                {
                    int maThue = ThemPhieuThue(conn, tran, maDatPhong, request, maKhachHang);
                    ThemDichVuPhatSinh(conn, tran, maDatPhong, maThue, request);
                    CapNhatTrangThaiDatPhong(conn, tran, maDatPhong, "Đã check-in", "Da check-in", "Đang thuê", "Dang thue", "Có khách", "Co khach", "Đã xác nhận", "Da xac nhan");
                    CapNhatTrangThaiPhong(conn, tran, request.Phong.Ma, "Đang thuê", "Dang thue", "Có khách", "Co khach");
                }
                else
                {
                    CapNhatTrangThaiPhong(conn, tran, request.Phong.Ma, "Đã đặt", "Da dat");
                }

                tran.Commit();
                return maDatPhong;
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        public int LuuDatPhongDoan(IEnumerable<DatPhongRequestDTO> requests)
        {
            List<DatPhongRequestDTO> danhSach = requests.ToList();
            if (danhSach.Count == 0)
            {
                throw new InvalidOperationException("Vui lòng chọn ít nhất một phòng cho đoàn.");
            }

            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable);

            try
            {
                foreach (DatPhongRequestDTO request in danhSach)
                {
                    DamBaoNhanVienHopLe(request.MaNhanVien);
                    BookingScheduleGuard.EnsureRoomAvailable(conn, tran, request.Phong.Ma, request.NgayNhan, request.NgayTra);
                }

                DatPhongRequestDTO daiDien = danhSach[0];
                int maKhachHang = LayHoacThemKhachHang(conn, tran, daiDien.KhachHang);
                int? maDoan = TaoDoanKhach(conn, tran, danhSach);
                DatPhongRequestDTO requestGop = new()
                {
                    Phong = daiDien.Phong,
                    KhachHang = daiDien.KhachHang,
                    MaDoan = maDoan,
                    MaNhanVien = daiDien.MaNhanVien,
                    NgayNhan = daiDien.NgayNhan,
                    NgayTra = daiDien.NgayTra,
                    SoNguoi = danhSach.Sum(item => item.SoNguoi),
                    NhanNgay = daiDien.NhanNgay,
                    CheDoDatPhong = daiDien.CheDoDatPhong,
                    TienCoc = danhSach.Sum(item => item.TienCoc),
                    TienPhong = danhSach.Sum(item => item.TienPhong),
                    TienDichVu = danhSach.Sum(item => item.TienDichVu),
                    GhiChu = TaoGhiChuDoan(danhSach)
                };

                int maDatPhong = ThemDatPhong(conn, tran, requestGop, maKhachHang);
                foreach (DatPhongRequestDTO request in danhSach)
                {
                    ThemChiTietDatPhong(conn, tran, maDatPhong, request);
                }

                if (requestGop.NhanNgay)
                {
                    int maThue = ThemPhieuThue(conn, tran, maDatPhong, requestGop, maKhachHang);
                    foreach (DatPhongRequestDTO request in danhSach)
                    {
                        ThemDichVuPhatSinh(conn, tran, maDatPhong, maThue, request);
                        CapNhatTrangThaiPhong(conn, tran, request.Phong.Ma, "Đang thuê", "Dang thue", "Có khách", "Co khach");
                    }

                    CapNhatTrangThaiDatPhong(conn, tran, maDatPhong, "Đã check-in", "Da check-in", "Đang thuê", "Dang thue", "Có khách", "Co khach", "Đã xác nhận", "Da xac nhan");
                }
                else
                {
                    foreach (DatPhongRequestDTO request in danhSach)
                    {
                        CapNhatTrangThaiPhong(conn, tran, request.Phong.Ma, "Đã đặt", "Da dat");
                    }
                }

                tran.Commit();
                return maDatPhong;
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        public void NhanPhongTuDatPhong(int maDatPhong)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable);

            try
            {
                ThongTinNhanPhong thongTin = LayThongTinNhanPhong(conn, tran, maDatPhong);
                DamBaoNhanVienHopLe(thongTin.MaNhanVien);

                List<int> danhSachPhong = LayDanhSachPhongDat(conn, tran, maDatPhong, thongTin.MaPhong);
                foreach (int maPhong in danhSachPhong)
                {
                    BookingScheduleGuard.EnsureRoomAvailable(
                        conn,
                        tran,
                        maPhong,
                        thongTin.NgayNhanThucTe,
                        thongTin.NgayTraDuKienMoi,
                        null,
                        maDatPhong);
                }

                int maThue = TaoHoacCapNhatPhieuThueKhiNhanPhong(conn, tran, maDatPhong, thongTin);
                ThemDichVuDatTruocKhiNhanPhong(conn, tran, maDatPhong, maThue, thongTin.MaNhanVien);
                CapNhatTrangThaiDatPhong(conn, tran, maDatPhong, "Đã check-in", "Da check-in", "Đang thuê", "Dang thue", "Có khách", "Co khach", "Đã xác nhận", "Da xac nhan");
                foreach (int maPhong in danhSachPhong)
                {
                    CapNhatTrangThaiPhong(conn, tran, maPhong, "Đang thuê", "Dang thue", "Có khách", "Co khach");
                }

                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        private static int ThemDatPhong(SqlConnection conn, SqlTransaction tran, DatPhongRequestDTO request, int maKhachHang)
        {
            bool coMaDoan = ColumnExists(conn, tran, "DATPHONG", "MaDoan");
            bool coLoaiDatPhong = ColumnExists(conn, tran, "DATPHONG", "LoaiDatPhong");
            string loaiDatPhongColumn = coLoaiDatPhong ? ", LoaiDatPhong" : string.Empty;
            string loaiDatPhongValue = coLoaiDatPhong ? ", @LoaiDatPhong" : string.Empty;
            string maDoanColumn = coMaDoan ? ", MaDoan" : string.Empty;
            string maDoanValue = coMaDoan ? ", @MaDoan" : string.Empty;
            string loaiDat = request.NhanNgay
                ? LayGiaTriHopLeTheoCheck(conn, tran, "DATPHONG", "LoaiDat", "Nhận ngay", "Nhan ngay", "Walk-in")
                : LayGiaTriHopLeTheoCheck(conn, tran, "DATPHONG", "LoaiDat", "Đặt trước", "Dat truoc");
            string trangThai = request.NhanNgay
                ? LayGiaTriHopLeTheoCheck(conn, tran, "DATPHONG", "TrangThai", "Đã check-in", "Da check-in", "Đang thuê", "Dang thue", "Có khách", "Co khach", "Đã xác nhận", "Da xac nhan")
                : LayGiaTriHopLeTheoCheck(conn, tran, "DATPHONG", "TrangThai", "Đã đặt", "Da dat", "Đã xác nhận", "Da xac nhan", "Chờ xác nhận", "Cho xac nhan");
            string sql = @"
INSERT INTO dbo.DATPHONG
    (MaKH, MaNV, MaPhong, LoaiDat" + loaiDatPhongColumn + @", NgayDat, NgayNhanDuKien, NgayTraDuKien, TienCoc, TrangThai, SoNguoi, GhiChu" + maDoanColumn + @")
VALUES
    (@MaKH, @MaNV, @MaPhong, @LoaiDat" + loaiDatPhongValue + @", SYSDATETIME(), @NgayNhan, @NgayTra, @TienCoc, @TrangThai, @SoNguoi, @GhiChu" + maDoanValue + @");
SELECT CONVERT(int, SCOPE_IDENTITY());";

            using SqlCommand cmd = new(sql, conn, tran);
            cmd.Parameters.Add("@MaKH", SqlDbType.Int).Value = maKhachHang;
            cmd.Parameters.Add("@MaNV", SqlDbType.Int).Value = request.MaNhanVien;
            cmd.Parameters.Add("@MaPhong", SqlDbType.Int).Value = request.Phong.Ma;
            cmd.Parameters.Add("@LoaiDat", SqlDbType.NVarChar, 30).Value = loaiDat;
            if (coLoaiDatPhong)
            {
                cmd.Parameters.Add("@LoaiDatPhong", SqlDbType.NVarChar, 30).Value = request.CheDoDatPhong;
            }
            cmd.Parameters.Add("@NgayNhan", SqlDbType.DateTime2).Value = request.NgayNhan;
            cmd.Parameters.Add("@NgayTra", SqlDbType.DateTime2).Value = request.NgayTra;
            cmd.Parameters.Add("@TienCoc", SqlDbType.Decimal).Value = request.NhanNgay ? 0 : request.TienCoc;
            cmd.Parameters.Add("@TrangThai", SqlDbType.NVarChar, 20).Value = trangThai;
            cmd.Parameters.Add("@SoNguoi", SqlDbType.Int).Value = Math.Max(1, request.SoNguoi);
            cmd.Parameters.Add("@GhiChu", SqlDbType.NVarChar, 1000).Value = TaoGhiChuDatPhong(request);
            if (coMaDoan)
            {
                cmd.Parameters.Add("@MaDoan", SqlDbType.Int).Value = request.MaDoan.HasValue ? request.MaDoan.Value : DBNull.Value;
            }
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static void ThemChiTietDatPhong(SqlConnection conn, SqlTransaction tran, int maDatPhong, DatPhongRequestDTO request)
        {
            List<string> columns = new() { "MaDatPhong", "MaPhong" };
            List<string> values = new() { "@MaDatPhong", "@MaPhong" };

            string ngayNhanColumn = GetFirstExistingColumn(conn, tran, "CHITIETDATPHONG", "NgayNhanDuKien", "NgayNhanPhong");
            string ngayTraColumn = GetFirstExistingColumn(conn, tran, "CHITIETDATPHONG", "NgayTraDuKien", "NgayTraPhong");
            if (!string.IsNullOrWhiteSpace(ngayNhanColumn))
            {
                columns.Add(ngayNhanColumn);
                values.Add("@NgayNhan");
            }
            if (!string.IsNullOrWhiteSpace(ngayTraColumn))
            {
                columns.Add(ngayTraColumn);
                values.Add("@NgayTra");
            }

            string donGiaColumn = GetFirstExistingColumn(conn, tran, "CHITIETDATPHONG", "DonGia", "DonGiaDuKien");
            if (!string.IsNullOrWhiteSpace(donGiaColumn))
            {
                columns.Add(donGiaColumn);
                values.Add("@DonGia");
            }
            if (ColumnExists(conn, tran, "CHITIETDATPHONG", "GhiChu"))
            {
                columns.Add("GhiChu");
                values.Add("@GhiChu");
            }

            string sql = "INSERT INTO dbo.CHITIETDATPHONG (" + string.Join(", ", columns) + ") VALUES (" + string.Join(", ", values) + ");";
            using SqlCommand cmd = new(sql, conn, tran);
            cmd.Parameters.Add("@MaDatPhong", SqlDbType.Int).Value = maDatPhong;
            cmd.Parameters.Add("@MaPhong", SqlDbType.Int).Value = request.Phong.Ma;
            cmd.Parameters.Add("@NgayNhan", SqlDbType.DateTime2).Value = request.NgayNhan;
            cmd.Parameters.Add("@NgayTra", SqlDbType.DateTime2).Value = request.NgayTra;
            cmd.Parameters.Add("@DonGia", SqlDbType.Decimal).Value = LayDonGiaTheoCheDo(request);
            cmd.Parameters.Add("@GhiChu", SqlDbType.NVarChar, 1000).Value = TaoMarkerDichVuDatTruoc(request.DichVuDaThem);
            cmd.ExecuteNonQuery();
        }

        private static int ThemPhieuThue(SqlConnection conn, SqlTransaction tran, int maDatPhong, DatPhongRequestDTO request, int maKhachHang)
        {
            bool coMaDoan = ColumnExists(conn, tran, "PHIEUTHUE", "MaDoan");
            string maDoanColumn = coMaDoan ? ", MaDoan" : string.Empty;
            string maDoanValue = coMaDoan ? ", @MaDoan" : string.Empty;
            string trangThaiThue = LayGiaTriHopLeTheoCheck(conn, tran, "PHIEUTHUE", "TrangThai", "Đang thuê", "Dang thue");
            string sql = @"
INSERT INTO dbo.PHIEUTHUE
    (MaDatPhong, MaKH, MaNV, MaPhong, NgayNhan, NgayTraDuKien, SoNguoi, TienCoc, PhuPhiNhanSom, TrangThai, GhiChu" + maDoanColumn + @")
VALUES
    (@MaDatPhong, @MaKH, @MaNV, @MaPhong, @NgayNhan, @NgayTra, @SoNguoi, @TienCoc, @PhuPhiNhanSom, @TrangThai, @GhiChu" + maDoanValue + @");
SELECT CONVERT(int, SCOPE_IDENTITY());";

            using SqlCommand cmd = new(sql, conn, tran);
            cmd.Parameters.Add("@MaDatPhong", SqlDbType.Int).Value = maDatPhong;
            cmd.Parameters.Add("@MaKH", SqlDbType.Int).Value = maKhachHang;
            cmd.Parameters.Add("@MaNV", SqlDbType.Int).Value = request.MaNhanVien;
            cmd.Parameters.Add("@MaPhong", SqlDbType.Int).Value = request.Phong.Ma;
            cmd.Parameters.Add("@NgayNhan", SqlDbType.DateTime2).Value = DateTime.Now;
            cmd.Parameters.Add("@NgayTra", SqlDbType.DateTime2).Value = request.NgayTra;
            cmd.Parameters.Add("@SoNguoi", SqlDbType.Int).Value = Math.Max(1, request.SoNguoi);
            cmd.Parameters.Add("@TienCoc", SqlDbType.Decimal).Value = request.TienCoc;
            cmd.Parameters.Add("@PhuPhiNhanSom", SqlDbType.Decimal).Value = request.PhuPhiNhanSom;
            cmd.Parameters.Add("@TrangThai", SqlDbType.NVarChar, 20).Value = trangThaiThue;
            cmd.Parameters.Add("@GhiChu", SqlDbType.NVarChar, 1000).Value = TaoGhiChuDatPhong(request);
            if (coMaDoan)
            {
                cmd.Parameters.Add("@MaDoan", SqlDbType.Int).Value = request.MaDoan.HasValue ? request.MaDoan.Value : DBNull.Value;
            }
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static int TaoHoacCapNhatPhieuThueKhiNhanPhong(SqlConnection conn, SqlTransaction tran, int maDatPhong, ThongTinNhanPhong thongTin)
        {
            int existing = LayMaThueTheoDatPhong(conn, tran, maDatPhong);
            bool coMaDoan = ColumnExists(conn, tran, "PHIEUTHUE", "MaDoan");
            string trangThaiThue = LayGiaTriHopLeTheoCheck(conn, tran, "PHIEUTHUE", "TrangThai", "Đang thuê", "Dang thue");
            if (existing > 0)
            {
                string updateSql = @"
UPDATE dbo.PHIEUTHUE
SET NgayNhan = @NgayNhan,
    NgayTraDuKien = @NgayTra,
    TrangThai = @TrangThai,
    TienCoc = @TienCoc,
    SoNguoi = @SoNguoi,
    GhiChu = @GhiChu
WHERE MaThue = @MaThue";

                using SqlCommand update = new(updateSql, conn, tran);
                update.Parameters.Add("@NgayNhan", SqlDbType.DateTime2).Value = thongTin.NgayNhanThucTe;
                update.Parameters.Add("@NgayTra", SqlDbType.DateTime2).Value = thongTin.NgayTraDuKienMoi;
                update.Parameters.Add("@TrangThai", SqlDbType.NVarChar, 20).Value = trangThaiThue;
                update.Parameters.Add("@TienCoc", SqlDbType.Decimal).Value = thongTin.TienCoc;
                update.Parameters.Add("@SoNguoi", SqlDbType.Int).Value = thongTin.SoNguoi;
                update.Parameters.Add("@GhiChu", SqlDbType.NVarChar, 1000).Value = (object?)thongTin.GhiChu ?? DBNull.Value;
                update.Parameters.Add("@MaThue", SqlDbType.Int).Value = existing;
                update.ExecuteNonQuery();
                if (coMaDoan)
                {
                    using SqlCommand updateDoan = new("UPDATE dbo.PHIEUTHUE SET MaDoan = @MaDoan WHERE MaThue = @MaThue", conn, tran);
                    updateDoan.Parameters.Add("@MaDoan", SqlDbType.Int).Value = thongTin.MaDoan.HasValue ? thongTin.MaDoan.Value : DBNull.Value;
                    updateDoan.Parameters.Add("@MaThue", SqlDbType.Int).Value = existing;
                    updateDoan.ExecuteNonQuery();
                }
                return existing;
            }

            string maDoanColumn = coMaDoan ? ", MaDoan" : string.Empty;
            string maDoanValue = coMaDoan ? ", @MaDoan" : string.Empty;
            string insertSql = @"
INSERT INTO dbo.PHIEUTHUE
    (MaDatPhong, MaKH, MaNV, MaPhong, NgayNhan, NgayTraDuKien, SoNguoi, TienCoc, TrangThai, GhiChu" + maDoanColumn + @")
VALUES
    (@MaDatPhong, @MaKH, @MaNV, @MaPhong, @NgayNhan, @NgayTra, @SoNguoi, @TienCoc, @TrangThai, @GhiChu" + maDoanValue + @");
SELECT CONVERT(int, SCOPE_IDENTITY());";

            using SqlCommand insert = new(insertSql, conn, tran);
            insert.Parameters.Add("@MaDatPhong", SqlDbType.Int).Value = maDatPhong;
            insert.Parameters.Add("@MaKH", SqlDbType.Int).Value = thongTin.MaKhachHang;
            insert.Parameters.Add("@MaNV", SqlDbType.Int).Value = thongTin.MaNhanVien;
            insert.Parameters.Add("@MaPhong", SqlDbType.Int).Value = thongTin.MaPhong;
            insert.Parameters.Add("@NgayNhan", SqlDbType.DateTime2).Value = thongTin.NgayNhanThucTe;
            insert.Parameters.Add("@NgayTra", SqlDbType.DateTime2).Value = thongTin.NgayTraDuKienMoi;
            insert.Parameters.Add("@SoNguoi", SqlDbType.Int).Value = thongTin.SoNguoi;
            insert.Parameters.Add("@TienCoc", SqlDbType.Decimal).Value = thongTin.TienCoc;
            insert.Parameters.Add("@TrangThai", SqlDbType.NVarChar, 20).Value = trangThaiThue;
            insert.Parameters.Add("@GhiChu", SqlDbType.NVarChar, 1000).Value = (object?)thongTin.GhiChu ?? DBNull.Value;
            if (coMaDoan)
            {
                insert.Parameters.Add("@MaDoan", SqlDbType.Int).Value = thongTin.MaDoan.HasValue ? thongTin.MaDoan.Value : DBNull.Value;
            }
            return Convert.ToInt32(insert.ExecuteScalar());
        }

        private static void ThemDichVuPhatSinh(SqlConnection conn, SqlTransaction tran, int maDatPhong, int maThue, DatPhongRequestDTO request)
        {
            const string sql = @"
INSERT INTO dbo.CHITIETPHATSINH
    (MaThue, MaDatPhong, MaPhong, MaDVVT, MaNV, SoLuong, DonGia, GhiChu)
VALUES
    (@MaThue, @MaDatPhong, @MaPhong, @MaDVVT, @MaNV, @SoLuong, @DonGia, @GhiChu);";

            foreach (DichVuDatPhongDTO dichVu in request.DichVuDaThem.Where(item => item.SoLuong > 0))
            {
                using SqlCommand cmd = new(sql, conn, tran);
                cmd.Parameters.Add("@MaThue", SqlDbType.Int).Value = maThue;
                cmd.Parameters.Add("@MaDatPhong", SqlDbType.Int).Value = maDatPhong;
                cmd.Parameters.Add("@MaPhong", SqlDbType.Int).Value = request.Phong.Ma;
                cmd.Parameters.Add("@MaDVVT", SqlDbType.Int).Value = dichVu.Ma;
                cmd.Parameters.Add("@MaNV", SqlDbType.Int).Value = request.MaNhanVien;
                cmd.Parameters.Add("@SoLuong", SqlDbType.Decimal).Value = dichVu.SoLuong;
                cmd.Parameters.Add("@DonGia", SqlDbType.Decimal).Value = dichVu.DonGia;
                cmd.Parameters.Add("@GhiChu", SqlDbType.NVarChar, 500).Value = "[DICHVU_CHECKIN]";
                cmd.ExecuteNonQuery();
            }
        }

        private static void ThemDichVuDatTruocKhiNhanPhong(SqlConnection conn, SqlTransaction tran, int maDatPhong, int maThue, int maNhanVien)
        {
            const string loadSql = @"
SELECT MaPhong, GhiChu
FROM dbo.CHITIETDATPHONG
WHERE MaDatPhong = @MaDatPhong
  AND CHARINDEX(@Marker, ISNULL(GhiChu, N'')) > 0";

            List<(int MaPhong, List<DichVuDatPhongDTO> DichVu)> rows = new();
            using (SqlCommand load = new(loadSql, conn, tran))
            {
                load.Parameters.Add("@MaDatPhong", SqlDbType.Int).Value = maDatPhong;
                load.Parameters.Add("@Marker", SqlDbType.NVarChar, 50).Value = "[DICHVU_DAT]";
                using SqlDataReader reader = load.ExecuteReader();
                while (reader.Read())
                {
                    List<DichVuDatPhongDTO> dichVu = DocMarkerDichVuDatTruoc(reader["GhiChu"]?.ToString() ?? string.Empty);
                    if (dichVu.Count > 0)
                    {
                        rows.Add((Convert.ToInt32(reader["MaPhong"]), dichVu));
                    }
                }
            }

            const string insertSql = @"
INSERT INTO dbo.CHITIETPHATSINH
    (MaThue, MaDatPhong, MaPhong, MaDVVT, MaNV, SoLuong, DonGia, GhiChu)
VALUES
    (@MaThue, @MaDatPhong, @MaPhong, @MaDVVT, @MaNV, @SoLuong, @DonGia, @GhiChu);";

            foreach ((int maPhong, List<DichVuDatPhongDTO> dichVuTheoPhong) in rows)
            {
                foreach (DichVuDatPhongDTO dichVu in dichVuTheoPhong)
                {
                    using SqlCommand cmd = new(insertSql, conn, tran);
                    cmd.Parameters.Add("@MaThue", SqlDbType.Int).Value = maThue;
                    cmd.Parameters.Add("@MaDatPhong", SqlDbType.Int).Value = maDatPhong;
                    cmd.Parameters.Add("@MaPhong", SqlDbType.Int).Value = maPhong;
                    cmd.Parameters.Add("@MaDVVT", SqlDbType.Int).Value = dichVu.Ma;
                    cmd.Parameters.Add("@MaNV", SqlDbType.Int).Value = maNhanVien;
                    cmd.Parameters.Add("@SoLuong", SqlDbType.Decimal).Value = dichVu.SoLuong;
                    cmd.Parameters.Add("@DonGia", SqlDbType.Decimal).Value = dichVu.DonGia;
                    cmd.Parameters.Add("@GhiChu", SqlDbType.NVarChar, 500).Value = "[DICHVU_CHECKIN]";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static int LayHoacThemKhachHang(SqlConnection conn, SqlTransaction tran, KhachHangDTO khachHang)
        {
            int existing = LayMaKhachHangTonTai(conn, tran, khachHang);
            if (existing > 0)
            {
                CapNhatKhachHang(conn, tran, existing, khachHang);
                return existing;
            }

            const string sql = @"
INSERT INTO dbo.KHACHHANG
    (HoTen, GioiTinh, NgaySinh, CCCD, SDT, DiaChi, LoaiKhach, PhanTramGiamGia, TrangThai)
VALUES
    (@HoTen, @GioiTinh, @NgaySinh, @CCCD, @SDT, @DiaChi, @LoaiKhach, @PhanTramGiamGia, @TrangThai);
SELECT CONVERT(int, SCOPE_IDENTITY());";

            using SqlCommand cmd = new(sql, conn, tran);
            GanThamSoKhachHang(cmd, khachHang);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static int LayMaKhachHangTonTai(SqlConnection conn, SqlTransaction tran, KhachHangDTO khachHang)
        {
            string where;
            if (!string.IsNullOrWhiteSpace(khachHang.CCCD))
            {
                where = "CCCD = @Value";
            }
            else if (!string.IsNullOrWhiteSpace(khachHang.SDT))
            {
                where = "SDT = @Value";
            }
            else
            {
                return 0;
            }

            using SqlCommand cmd = new("SELECT TOP 1 MaKH FROM dbo.KHACHHANG WHERE " + where + " ORDER BY MaKH DESC", conn, tran);
            cmd.Parameters.Add("@Value", SqlDbType.VarChar, 20).Value = !string.IsNullOrWhiteSpace(khachHang.CCCD) ? khachHang.CCCD.Trim() : khachHang.SDT.Trim();
            object? value = cmd.ExecuteScalar();
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static int? TaoDoanKhach(SqlConnection conn, SqlTransaction tran, List<DatPhongRequestDTO> danhSach)
        {
            if (!TableExists(conn, tran, "DOANKHACH") || !ColumnExists(conn, tran, "DOANKHACH", "TenDoan"))
            {
                return null;
            }

            DatPhongRequestDTO daiDien = danhSach[0];
            List<string> columns = new() { "TenDoan" };
            List<string> values = new() { "@TenDoan" };
            List<SqlParameter> parameters = new()
            {
                new SqlParameter("@TenDoan", SqlDbType.NVarChar, 100)
                {
                    Value = "Đoàn " + (string.IsNullOrWhiteSpace(daiDien.KhachHang.HoTen) ? DateTime.Now.ToString("ddMMyyyyHHmm") : daiDien.KhachHang.HoTen.Trim())
                }
            };

            AddOptionalDoanParameter(conn, tran, columns, values, parameters, "TruongDoan", "@TruongDoan", daiDien.KhachHang.HoTen, SqlDbType.NVarChar, 100);
            AddOptionalDoanParameter(conn, tran, columns, values, parameters, "NguoiDaiDien", "@NguoiDaiDien", daiDien.KhachHang.HoTen, SqlDbType.NVarChar, 100);
            AddOptionalDoanParameter(conn, tran, columns, values, parameters, "SDTTruongDoan", "@SDTTruongDoan", daiDien.KhachHang.SDT, SqlDbType.VarChar, 15);
            AddOptionalDoanParameter(conn, tran, columns, values, parameters, "SoDienThoai", "@SoDienThoai", daiDien.KhachHang.SDT, SqlDbType.VarChar, 15);
            AddOptionalDoanParameter(conn, tran, columns, values, parameters, "GhiChu", "@GhiChuDoan", "Đặt phòng theo đoàn: " + danhSach.Count + " phòng", SqlDbType.NVarChar, 500);

            string sql = "INSERT INTO dbo.DOANKHACH (" + string.Join(", ", columns) + ") VALUES (" + string.Join(", ", values) + "); SELECT CONVERT(int, SCOPE_IDENTITY());";
            using SqlCommand cmd = new(sql, conn, tran);
            cmd.Parameters.AddRange(parameters.ToArray());
            object? value = cmd.ExecuteScalar();
            return value == null || value == DBNull.Value ? null : Convert.ToInt32(value);
        }

        private static void AddOptionalDoanParameter(
            SqlConnection conn,
            SqlTransaction tran,
            List<string> columns,
            List<string> values,
            List<SqlParameter> parameters,
            string columnName,
            string parameterName,
            string? parameterValue,
            SqlDbType type,
            int size)
        {
            if (!ColumnExists(conn, tran, "DOANKHACH", columnName))
            {
                return;
            }

            columns.Add(columnName);
            values.Add(parameterName);
            parameters.Add(new SqlParameter(parameterName, type, size)
            {
                Value = string.IsNullOrWhiteSpace(parameterValue) ? DBNull.Value : parameterValue.Trim()
            });
        }

        private static void CapNhatKhachHang(SqlConnection conn, SqlTransaction tran, int maKhachHang, KhachHangDTO khachHang)
        {
            const string sql = @"
UPDATE dbo.KHACHHANG
SET HoTen = @HoTen,
    GioiTinh = @GioiTinh,
    NgaySinh = @NgaySinh,
    DiaChi = @DiaChi,
    LoaiKhach = @LoaiKhach,
    PhanTramGiamGia = @PhanTramGiamGia
WHERE MaKH = @MaKH";

            using SqlCommand cmd = new(sql, conn, tran);
            GanThamSoKhachHang(cmd, khachHang);
            cmd.Parameters.Add("@MaKH", SqlDbType.Int).Value = maKhachHang;
            cmd.ExecuteNonQuery();
        }

        private static void GanThamSoKhachHang(SqlCommand cmd, KhachHangDTO khachHang)
        {
            string loaiKhach = string.IsNullOrWhiteSpace(khachHang.LoaiKhach) ? "Thường" : khachHang.LoaiKhach;
            bool laVip = loaiKhach.Contains("VIP", StringComparison.OrdinalIgnoreCase);
            cmd.Parameters.Add("@HoTen", SqlDbType.NVarChar, 100).Value = khachHang.HoTen.Trim();
            cmd.Parameters.Add("@GioiTinh", SqlDbType.NVarChar, 10).Value = string.IsNullOrWhiteSpace(khachHang.GioiTinh) ? DBNull.Value : khachHang.GioiTinh;
            cmd.Parameters.Add("@NgaySinh", SqlDbType.Date).Value = khachHang.NgaySinh.HasValue ? khachHang.NgaySinh.Value.Date : DBNull.Value;
            cmd.Parameters.Add("@CCCD", SqlDbType.VarChar, 20).Value = string.IsNullOrWhiteSpace(khachHang.CCCD) ? DBNull.Value : khachHang.CCCD.Trim();
            cmd.Parameters.Add("@SDT", SqlDbType.VarChar, 15).Value = string.IsNullOrWhiteSpace(khachHang.SDT) ? DBNull.Value : khachHang.SDT.Trim();
            cmd.Parameters.Add("@DiaChi", SqlDbType.NVarChar, 255).Value = string.IsNullOrWhiteSpace(khachHang.DiaChi) ? DBNull.Value : khachHang.DiaChi.Trim();
            cmd.Parameters.Add("@LoaiKhach", SqlDbType.NVarChar, 20).Value = laVip ? "VIP" : "Thường";
            cmd.Parameters.Add("@PhanTramGiamGia", SqlDbType.Decimal).Value = laVip ? 10 : 0;
            cmd.Parameters.Add("@TrangThai", SqlDbType.NVarChar, 30).Value = string.IsNullOrWhiteSpace(khachHang.TrangThai) ? "Đang hoạt động" : khachHang.TrangThai;
        }

        private static ThongTinNhanPhong LayThongTinNhanPhong(SqlConnection conn, SqlTransaction tran, int maDatPhong)
        {
            string maDoanExpr = ColumnExists(conn, tran, "DATPHONG", "MaDoan")
                ? "DP.MaDoan"
                : "CAST(NULL AS int)";
            string sql = @"
SELECT DP.MaKH,
       DP.MaNV,
       " + maDoanExpr + @" AS MaDoan,
       ISNULL(DP.MaPhong, (SELECT TOP 1 CT.MaPhong FROM dbo.CHITIETDATPHONG CT WHERE CT.MaDatPhong = DP.MaDatPhong ORDER BY CT.MaPhong)) AS MaPhong,
       DP.SoNguoi,
       DP.NgayTraDuKien,
       DP.TienCoc,
       DP.GhiChu
FROM dbo.DATPHONG DP
WHERE DP.MaDatPhong = @MaDatPhong";

            using SqlCommand cmd = new(sql, conn, tran);
            cmd.Parameters.Add("@MaDatPhong", SqlDbType.Int).Value = maDatPhong;
            using SqlDataReader reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException("Không tìm thấy phiếu đặt phòng để nhận phòng.");
            }

            return new ThongTinNhanPhong
            {
                MaKhachHang = Convert.ToInt32(reader["MaKH"]),
                MaNhanVien = Convert.ToInt32(reader["MaNV"]),
                MaDoan = reader["MaDoan"] == DBNull.Value ? null : Convert.ToInt32(reader["MaDoan"]),
                MaPhong = Convert.ToInt32(reader["MaPhong"]),
                SoNguoi = Convert.ToInt32(reader["SoNguoi"]),
                NgayNhanThucTe = DateTime.Now,
                NgayTraDuKienMoi = Convert.ToDateTime(reader["NgayTraDuKien"]),
                TienCoc = Convert.ToDecimal(reader["TienCoc"]),
                GhiChu = reader["GhiChu"] == DBNull.Value ? null : reader["GhiChu"]?.ToString()
            };
        }

        private static List<int> LayDanhSachPhongDat(SqlConnection conn, SqlTransaction tran, int maDatPhong, int fallbackMaPhong)
        {
            using SqlCommand cmd = new("SELECT DISTINCT MaPhong FROM dbo.CHITIETDATPHONG WHERE MaDatPhong = @MaDatPhong", conn, tran);
            cmd.Parameters.Add("@MaDatPhong", SqlDbType.Int).Value = maDatPhong;

            List<int> result = new();
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(Convert.ToInt32(reader["MaPhong"]));
            }

            return result.Count == 0 ? new List<int> { fallbackMaPhong } : result;
        }

        private static int LayMaThueTheoDatPhong(SqlConnection conn, SqlTransaction tran, int maDatPhong)
        {
            using SqlCommand cmd = new("SELECT TOP 1 MaThue FROM dbo.PHIEUTHUE WHERE MaDatPhong = @MaDatPhong ORDER BY MaThue DESC", conn, tran);
            cmd.Parameters.Add("@MaDatPhong", SqlDbType.Int).Value = maDatPhong;
            object? value = cmd.ExecuteScalar();
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static void CapNhatTrangThaiPhong(SqlConnection conn, SqlTransaction tran, int maPhong, params string[] trangThai)
        {
            string trangThaiHopLe = LayGiaTriHopLeTheoCheck(conn, tran, "PHONG", "TrangThai", trangThai);
            using SqlCommand cmd = new("UPDATE dbo.PHONG SET TrangThai = @TrangThai WHERE MaPhong = @MaPhong", conn, tran);
            cmd.Parameters.Add("@TrangThai", SqlDbType.NVarChar, 20).Value = trangThaiHopLe;
            cmd.Parameters.Add("@MaPhong", SqlDbType.Int).Value = maPhong;
            cmd.ExecuteNonQuery();
        }

        private static void CapNhatTrangThaiDatPhong(SqlConnection conn, SqlTransaction tran, int maDatPhong, params string[] trangThai)
        {
            string trangThaiHopLe = LayGiaTriHopLeTheoCheck(conn, tran, "DATPHONG", "TrangThai", trangThai);
            using SqlCommand cmd = new("UPDATE dbo.DATPHONG SET TrangThai = @TrangThai WHERE MaDatPhong = @MaDatPhong", conn, tran);
            cmd.Parameters.Add("@TrangThai", SqlDbType.NVarChar, 20).Value = trangThaiHopLe;
            cmd.Parameters.Add("@MaDatPhong", SqlDbType.Int).Value = maDatPhong;
            cmd.ExecuteNonQuery();
        }

        private static string TaoGhiChuDatPhong(DatPhongRequestDTO request)
        {
            List<string> parts = new()
            {
                "TongTienPhong=" + request.TienPhong.ToString(CultureInfo.InvariantCulture),
                "TongTienDichVu=" + request.TienDichVu.ToString(CultureInfo.InvariantCulture),
                "CheDo=" + request.CheDoDatPhong
            };

            if (!string.IsNullOrWhiteSpace(request.GhiChu))
            {
                parts.Add(request.GhiChu.Trim());
            }

            return string.Join("; ", parts);
        }

        private static string TaoGhiChuDoan(List<DatPhongRequestDTO> danhSach)
        {
            string phong = string.Join(", ", danhSach.Select(item => item.Phong.MaHienThi).Where(value => !string.IsNullOrWhiteSpace(value)));
            string ghiChu = danhSach.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.GhiChu))?.GhiChu ?? string.Empty;
            string marker = $"[DAT_DOAN] SoPhong={danhSach.Count}; Phong={phong}; TongTienPhong={danhSach.Sum(item => item.TienPhong).ToString(CultureInfo.InvariantCulture)}; TongTienDichVu={danhSach.Sum(item => item.TienDichVu).ToString(CultureInfo.InvariantCulture)}; TongCoc={danhSach.Sum(item => item.TienCoc).ToString(CultureInfo.InvariantCulture)}";
            return string.IsNullOrWhiteSpace(ghiChu) ? marker : marker + " - " + ghiChu;
        }

        private static decimal LayDonGiaTheoCheDo(DatPhongRequestDTO request)
        {
            string mode = BoDau(request.CheDoDatPhong);
            if (mode.Contains("gio", StringComparison.OrdinalIgnoreCase)) return request.Phong.GiaGio;
            if (mode.Contains("dem", StringComparison.OrdinalIgnoreCase)) return request.Phong.GiaDem;
            return request.Phong.GiaNgay > 0 ? request.Phong.GiaNgay : request.Phong.GiaPhong;
        }

        private static string TaoMarkerDichVuDatTruoc(IEnumerable<DichVuDatPhongDTO> dichVu)
        {
            List<string> parts = dichVu
                .Where(item => item.SoLuong > 0)
                .Select(item => string.Join("|", item.Ma, item.SoLuong, item.DonGia.ToString(CultureInfo.InvariantCulture)))
                .ToList();
            return parts.Count == 0 ? string.Empty : "[DICHVU_DAT] " + string.Join(";", parts);
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
                    decimal.TryParse(parts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal donGia) &&
                    soLuong > 0)
                {
                    result.Add(new DichVuDatPhongDTO { Ma = ma, SoLuong = soLuong, DonGia = donGia });
                }
            }

            return result;
        }

        private static void DamBaoNhanVienHopLe(int maNhanVien)
        {
            if (maNhanVien <= 0)
            {
                throw new InvalidOperationException("Tài khoản đăng nhập chưa liên kết nhân viên. Vui lòng liên kết MaNV trước khi đặt/nhận phòng.");
            }
        }

        private static string LayGiaTriHopLeTheoCheck(SqlConnection conn, SqlTransaction tran, string tableName, string columnName, params string[] priorities)
        {
            string priority = priorities.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
            List<string> allowed = LayGiaTriTrongCheckConstraint(conn, tran, tableName, columnName);
            if (allowed.Count == 0)
            {
                return priority;
            }

            foreach (string candidatePriority in priorities.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                string? exact = allowed.FirstOrDefault(item => string.Equals(item, candidatePriority, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(exact))
                {
                    return exact;
                }

                string priorityKey = ChuanHoaTrangThai(candidatePriority);
                string? normalized = allowed.FirstOrDefault(item => string.Equals(ChuanHoaTrangThai(item), priorityKey, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }
            }

            foreach (string candidatePriority in priorities.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                string priorityKey = ChuanHoaTrangThai(candidatePriority);
                string? semantic = allowed.FirstOrDefault(value =>
                {
                    string candidate = ChuanHoaTrangThai(value);
                    return (priorityKey.Contains("check-in") &&
                            (candidate.Contains("check-in") ||
                             candidate.Contains("dang thue") ||
                             candidate.Contains("co khach") ||
                             candidate.Contains("da xac nhan"))) ||
                           ((priorityKey.Contains("dang thue") || priorityKey.Contains("co khach")) &&
                            (candidate.Contains("dang thue") || candidate.Contains("co khach"))) ||
                           ((priorityKey.Contains("da dat") || priorityKey.Contains("da xac nhan") || priorityKey.Contains("cho xac nhan")) &&
                            (candidate.Contains("da dat") || candidate.Contains("da xac nhan") || candidate.Contains("cho xac nhan"))) ||
                           (priorityKey.Contains("huy") && candidate.Contains("huy")) ||
                           (priorityKey.Contains("trong") && candidate.Contains("trong")) ||
                           ((priorityKey.Contains("nhan ngay") || priorityKey.Contains("walk-in")) &&
                            (candidate.Contains("nhan ngay") || candidate.Contains("walk-in"))) ||
                           (priorityKey.Contains("dat truoc") && candidate.Contains("dat truoc"));
                });
                if (!string.IsNullOrWhiteSpace(semantic))
                {
                    return semantic;
                }
            }

            return priority;
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
            cmd.Parameters.Add("@TableName", SqlDbType.NVarChar, 128).Value = tableName;
            cmd.Parameters.Add("@ColumnName", SqlDbType.NVarChar, 260).Value = "%" + columnName + "%";

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

        private static string GetFirstExistingColumn(SqlConnection conn, SqlTransaction tran, string table, params string[] candidates)
        {
            return candidates.FirstOrDefault(column => ColumnExists(conn, tran, table, column)) ?? string.Empty;
        }

        private static string ChuanHoaTrangThai(string value)
        {
            string text = (value ?? string.Empty)
                .Replace('đ', 'd')
                .Replace('Đ', 'D');
            return BoDau(text).ToLowerInvariant();
        }

        private static string BoDau(string value)
        {
            string formD = (value ?? string.Empty).Normalize(System.Text.NormalizationForm.FormD);
            char[] chars = formD
                .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .Select(ch => ch == 'đ' ? 'd' : ch == 'Đ' ? 'D' : ch)
                .ToArray();
            return new string(chars).Normalize(System.Text.NormalizationForm.FormC);
        }

        private sealed class ThongTinNhanPhong
        {
            public int MaKhachHang { get; set; }
            public int MaNhanVien { get; set; }
            public int? MaDoan { get; set; }
            public int MaPhong { get; set; }
            public int SoNguoi { get; set; }
            public DateTime NgayNhanThucTe { get; set; }
            public DateTime NgayTraDuKienMoi { get; set; }
            public decimal TienCoc { get; set; }
            public string? GhiChu { get; set; }
        }
    }
}
