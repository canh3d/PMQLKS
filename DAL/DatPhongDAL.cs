using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.DAL
{
    public class DatPhongDAL
    {
        public int LuuDatPhong(DatPhongRequestDTO request)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();

            try
            {
                int maKhachHang = LayHoacThemKhachHang(conn, tran, request.KhachHang);
                string bangDatPhong = TableExists(conn, tran, "PHIEUDATPHONG") ? "PHIEUDATPHONG" : "DATPHONG";
                if (!TableExists(conn, tran, bangDatPhong))
                {
                    throw new InvalidOperationException("Không tìm thấy bảng PHIEUDATPHONG hoặc DATPHONG trong database.");
                }

                int maDatPhong = ThemPhieuDatPhong(conn, tran, bangDatPhong, request, maKhachHang);
                ThemChiTietDatPhong(conn, tran, maDatPhong, request);

                if (request.NhanNgay)
                {
                    int? maThue = ThemPhieuThue(conn, tran, maDatPhong, request, maKhachHang);
                    ThemDichVuPhatSinh(conn, tran, maDatPhong, maThue, request);
                    CapNhatTrangThaiDatPhong(conn, tran, bangDatPhong, maDatPhong, LayGiaTriHopLeTheoCheck(conn, tran, bangDatPhong, "TrangThai", "Đang thuê", "Dang thue", "Có khách", "Co khach"));
                    CapNhatTrangThaiPhong(conn, tran, request.Phong.Ma, "Đang thuê");
                }
                else
                {
                    CapNhatTrangThaiPhong(conn, tran, request.Phong.Ma, "Đã đặt");
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
            using SqlTransaction tran = conn.BeginTransaction();

            try
            {
                DatPhongRequestDTO daiDien = danhSach[0];
                int maKhachHang = LayHoacThemKhachHang(conn, tran, daiDien.KhachHang);
                string bangDatPhong = TableExists(conn, tran, "PHIEUDATPHONG") ? "PHIEUDATPHONG" : "DATPHONG";
                if (!TableExists(conn, tran, bangDatPhong))
                {
                    throw new InvalidOperationException("Không tìm thấy bảng PHIEUDATPHONG hoặc DATPHONG trong database.");
                }

                DatPhongRequestDTO requestGop = new()
                {
                    Phong = daiDien.Phong,
                    KhachHang = daiDien.KhachHang,
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

                int maDatPhong = ThemPhieuDatPhong(conn, tran, bangDatPhong, requestGop, maKhachHang);
                foreach (DatPhongRequestDTO request in danhSach)
                {
                    ThemChiTietDatPhong(conn, tran, maDatPhong, request);
                }

                if (requestGop.NhanNgay)
                {
                    int? maThue = ThemPhieuThue(conn, tran, maDatPhong, requestGop, maKhachHang);
                    foreach (DatPhongRequestDTO request in danhSach)
                    {
                        ThemDichVuPhatSinh(conn, tran, maDatPhong, maThue, request);
                    }
                    CapNhatTrangThaiDatPhong(conn, tran, bangDatPhong, maDatPhong, LayGiaTriHopLeTheoCheck(conn, tran, bangDatPhong, "TrangThai", "Đang thuê", "Dang thue", "Có khách", "Co khach"));
                    foreach (DatPhongRequestDTO request in danhSach)
                    {
                        CapNhatTrangThaiPhong(conn, tran, request.Phong.Ma, "Đang thuê");
                    }
                }
                else
                {
                    foreach (DatPhongRequestDTO request in danhSach)
                    {
                        CapNhatTrangThaiPhong(conn, tran, request.Phong.Ma, "Đã đặt");
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
            using SqlTransaction tran = conn.BeginTransaction();

            try
            {
                string bangDatPhong = TableExists(conn, tran, "PHIEUDATPHONG") ? "PHIEUDATPHONG" : "DATPHONG";
                if (!TableExists(conn, tran, bangDatPhong))
                {
                    throw new InvalidOperationException("Không tìm thấy bảng đặt phòng.");
                }

                List<int> nhomMaDatPhong = LayNhomDatPhongLienQuan(conn, tran, bangDatPhong, maDatPhong);
                ThongTinNhanPhong thongTinNhanPhong = LayThongTinNhanPhong(conn, tran, bangDatPhong, maDatPhong);
                if (nhomMaDatPhong.Count > 1)
                {
                    BoSungThongTinNhanPhongTheoNhom(conn, tran, bangDatPhong, nhomMaDatPhong, thongTinNhanPhong);
                }
                string status = LayGiaTriHopLeTheoCheck(conn, tran, "PHONG", "TrangThai", "Đang thuê", "Dang thue", "Có khách", "Co khach");
                if (TableExists(conn, tran, "CHITIETDATPHONG"))
                {
                    string danhSachThamSo = TaoDanhSachThamSo(nhomMaDatPhong, "MaDatPhong");
                    using SqlCommand cmd = new(
                        @"UPDATE P
                          SET P.TrangThai = @TrangThai
                          FROM dbo.PHONG P
                          JOIN dbo.CHITIETDATPHONG CT ON P.MaPhong = CT.MaPhong
                          WHERE CT.MaDatPhong IN (" + danhSachThamSo + @")",
                        conn,
                        tran);
                    cmd.Parameters.AddWithValue("@TrangThai", status);
                    GanDanhSachThamSo(cmd, nhomMaDatPhong, "MaDatPhong");
                    cmd.ExecuteNonQuery();
                }
                if (ColumnExists(conn, tran, bangDatPhong, "MaPhong"))
                {
                    string danhSachThamSo = TaoDanhSachThamSo(nhomMaDatPhong, "MaDatPhong");
                    using SqlCommand cmd = new(
                        @"UPDATE P
                          SET P.TrangThai = @TrangThai
                          FROM dbo.PHONG P
                          JOIN dbo." + bangDatPhong + @" DP ON P.MaPhong = DP.MaPhong
                          WHERE DP.MaDatPhong IN (" + danhSachThamSo + @")",
                        conn,
                        tran);
                    cmd.Parameters.AddWithValue("@TrangThai", status);
                    GanDanhSachThamSo(cmd, nhomMaDatPhong, "MaDatPhong");
                    cmd.ExecuteNonQuery();
                }

                CapNhatTrangThaiDatPhong(conn, tran, bangDatPhong, maDatPhong, LayGiaTriHopLeTheoCheck(conn, tran, bangDatPhong, "TrangThai", "Đang thuê", "Dang thue", "Có khách", "Co khach"));
                string trangThaiDatPhongNhom = LayGiaTriHopLeTheoCheck(conn, tran, bangDatPhong, "TrangThai", "Đang thuê", "Dang thue", "Có khách", "Co khach");
                foreach (int ma in nhomMaDatPhong)
                {
                    CapNhatTrangThaiDatPhong(conn, tran, bangDatPhong, ma, trangThaiDatPhongNhom);
                }

                TaoHoacCapNhatPhieuThueKhiNhanPhong(conn, tran, maDatPhong, thongTinNhanPhong);

                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        private static List<int> LayNhomDatPhongLienQuan(SqlConnection conn, SqlTransaction tran, string bangDatPhong, int maDatPhong)
        {
            string ngayNhanColumn = ColumnExists(conn, tran, bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong";
            string ngayTraColumn = ColumnExists(conn, tran, bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong";

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

            using SqlCommand cmd = new(@"
SELECT MaDatPhong
FROM dbo." + bangDatPhong + @"
WHERE MaKH = @MaKH
  AND CONVERT(date, " + ngayNhanColumn + @") = @NgayNhan
  AND CONVERT(date, " + ngayTraColumn + @") = @NgayTra
  AND (TrangThai IS NULL OR TrangThai NOT IN (N'Đã hủy', N'Da huy', N'Đã trả', N'Da tra', N'Đã trả phòng', N'Da tra phong'))
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

        private static void BoSungThongTinNhanPhongTheoNhom(SqlConnection conn, SqlTransaction tran, string bangDatPhong, List<int> nhomMaDatPhong, ThongTinNhanPhong thongTin)
        {
            string danhSachThamSo = TaoDanhSachThamSo(nhomMaDatPhong, "MaDatPhong");
            string tienCocColumn = ColumnExists(conn, tran, bangDatPhong, "TienCoc") ? "TienCoc" : ColumnExists(conn, tran, bangDatPhong, "DatCoc") ? "DatCoc" : string.Empty;
            string soNguoiExpr = ColumnExists(conn, tran, bangDatPhong, "SoNguoi") ? "SUM(ISNULL(SoNguoi, 1))" : "COUNT(*)";
            string tienCocExpr = string.IsNullOrWhiteSpace(tienCocColumn) ? "CAST(0 AS decimal(18,2))" : "SUM(ISNULL(" + tienCocColumn + ", 0))";
            string ghiChuExpr = ColumnExists(conn, tran, bangDatPhong, "GhiChu") ? "STRING_AGG(CAST(GhiChu AS nvarchar(max)), N'; ')" : "CAST(NULL AS nvarchar(max))";

            using SqlCommand cmd = new(@"
SELECT " + soNguoiExpr + @" AS SoNguoi,
       " + tienCocExpr + @" AS TienCoc,
       " + ghiChuExpr + @" AS GhiChu
FROM dbo." + bangDatPhong + @"
WHERE MaDatPhong IN (" + danhSachThamSo + ")", conn, tran);
            GanDanhSachThamSo(cmd, nhomMaDatPhong, "MaDatPhong");

            using SqlDataReader reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return;
            }

            thongTin.SoNguoi = reader["SoNguoi"] == DBNull.Value ? thongTin.SoNguoi : Convert.ToInt32(reader["SoNguoi"]);
            thongTin.TienCoc = reader["TienCoc"] == DBNull.Value ? thongTin.TienCoc : Convert.ToDecimal(reader["TienCoc"]);
            string? ghiChu = reader["GhiChu"] == DBNull.Value ? null : reader["GhiChu"]?.ToString();
            if (!string.IsNullOrWhiteSpace(ghiChu))
            {
                thongTin.GhiChu = ghiChu;
            }
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

        private static ThongTinNhanPhong LayThongTinNhanPhong(SqlConnection conn, SqlTransaction tran, string bangDatPhong, int maDatPhong)
        {
            string ngayNhanColumn = ColumnExists(conn, tran, bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong";
            string ngayTraColumn = ColumnExists(conn, tran, bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong";
            string tienCocColumn = ColumnExists(conn, tran, bangDatPhong, "TienCoc") ? "TienCoc" : ColumnExists(conn, tran, bangDatPhong, "DatCoc") ? "DatCoc" : string.Empty;
            string maPhongExpr = ColumnExists(conn, tran, bangDatPhong, "MaPhong")
                ? "DP.MaPhong"
                : TableExists(conn, tran, "CHITIETDATPHONG")
                    ? "(SELECT TOP 1 CT.MaPhong FROM dbo.CHITIETDATPHONG CT WHERE CT.MaDatPhong = DP.MaDatPhong ORDER BY CT.MaPhong)"
                    : "CAST(NULL AS int)";
            string soNguoiExpr = ColumnExists(conn, tran, bangDatPhong, "SoNguoi") ? "DP.SoNguoi" : "1";
            string maNvExpr = ColumnExists(conn, tran, bangDatPhong, "MaNV") ? "ISNULL(DP.MaNV, 1)" : "1";
            string tienCocExpr = string.IsNullOrWhiteSpace(tienCocColumn) ? "CAST(0 AS decimal(18,2))" : "ISNULL(DP." + tienCocColumn + ", 0)";
            string ghiChuExpr = ColumnExists(conn, tran, bangDatPhong, "GhiChu") ? "DP.GhiChu" : "CAST(NULL AS nvarchar(1000))";

            using SqlCommand cmd = new(@"
SELECT TOP 1
       DP.MaKH,
       " + maNvExpr + @" AS MaNV,
       " + maPhongExpr + @" AS MaPhong,
       " + soNguoiExpr + @" AS SoNguoi,
       DP." + ngayNhanColumn + @" AS NgayNhanDuKien,
       DP." + ngayTraColumn + @" AS NgayTraDuKien,
       " + tienCocExpr + @" AS TienCoc,
       " + ghiChuExpr + @" AS GhiChu
FROM dbo." + bangDatPhong + @" DP
WHERE DP.MaDatPhong = @MaDatPhong", conn, tran);
            cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong);

            using SqlDataReader reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException("Không tìm thấy phiếu đặt phòng để nhận phòng.");
            }

            DateTime now = DateTime.Now;
            DateTime? ngayNhanDuKien = reader["NgayNhanDuKien"] == DBNull.Value ? null : Convert.ToDateTime(reader["NgayNhanDuKien"]);
            DateTime? ngayTraDuKien = reader["NgayTraDuKien"] == DBNull.Value ? null : Convert.ToDateTime(reader["NgayTraDuKien"]);
            return new ThongTinNhanPhong
            {
                MaKhachHang = Convert.ToInt32(reader["MaKH"]),
                MaNhanVien = Convert.ToInt32(reader["MaNV"]),
                MaPhong = Convert.ToInt32(reader["MaPhong"]),
                SoNguoi = Convert.ToInt32(reader["SoNguoi"]),
                NgayNhanThucTe = now,
                NgayTraDuKienMoi = ngayTraDuKien ?? now.AddDays(1),
                TienCoc = reader["TienCoc"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TienCoc"]),
                GhiChu = reader["GhiChu"] == DBNull.Value ? null : reader["GhiChu"]?.ToString()
            };
        }

        private static void TaoHoacCapNhatPhieuThueKhiNhanPhong(SqlConnection conn, SqlTransaction tran, int maDatPhong, ThongTinNhanPhong thongTin)
        {
            if (!TableExists(conn, tran, "PHIEUTHUE"))
            {
                return;
            }

            string trangThaiThue = LayGiaTriHopLeTheoCheck(conn, tran, "PHIEUTHUE", "TrangThai", "Đang thuê", "Dang thue");
            int existing = 0;
            if (ColumnExists(conn, tran, "PHIEUTHUE", "MaDatPhong"))
            {
                using SqlCommand find = new("SELECT TOP 1 MaThue FROM dbo.PHIEUTHUE WHERE MaDatPhong = @MaDatPhong ORDER BY MaThue DESC", conn, tran);
                find.Parameters.AddWithValue("@MaDatPhong", maDatPhong);
                object? value = find.ExecuteScalar();
                existing = value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }

            if (existing > 0)
            {
                List<string> sets = new();
                AddSetIfExists(conn, tran, sets, "PHIEUTHUE", "NgayNhan", "@NgayNhan");
                AddSetIfExists(conn, tran, sets, "PHIEUTHUE", "NgayTraDuKien", "@NgayTra");
                AddSetIfExists(conn, tran, sets, "PHIEUTHUE", "TrangThai", "@TrangThai");
                AddSetIfExists(conn, tran, sets, "PHIEUTHUE", "TienCoc", "@TienCoc");
                AddSetIfExists(conn, tran, sets, "PHIEUTHUE", "SoNguoi", "@SoNguoi");
                AddSetIfExists(conn, tran, sets, "PHIEUTHUE", "GhiChu", "@GhiChu");

                using SqlCommand update = new("UPDATE dbo.PHIEUTHUE SET " + string.Join(",", sets) + " WHERE MaThue = @MaThue", conn, tran);
                update.Parameters.AddWithValue("@NgayNhan", thongTin.NgayNhanThucTe);
                update.Parameters.AddWithValue("@NgayTra", thongTin.NgayTraDuKienMoi);
                update.Parameters.AddWithValue("@TrangThai", trangThaiThue);
                update.Parameters.AddWithValue("@TienCoc", thongTin.TienCoc);
                update.Parameters.AddWithValue("@SoNguoi", thongTin.SoNguoi);
                update.Parameters.AddWithValue("@GhiChu", string.IsNullOrWhiteSpace(thongTin.GhiChu) ? DBNull.Value : thongTin.GhiChu);
                update.Parameters.AddWithValue("@MaThue", existing);
                update.ExecuteNonQuery();
                return;
            }

            List<string> columns = new();
            List<string> values = new();
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "MaDatPhong", "@MaDatPhong");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "MaKH", "@MaKH");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "MaPhong", "@MaPhong");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "MaNV", "@MaNV");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "NgayNhan", "@NgayNhan");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "NgayTraDuKien", "@NgayTra");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "SoNguoi", "@SoNguoi");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "TienCoc", "@TienCoc");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "TrangThai", "@TrangThai");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "GhiChu", "@GhiChu");

            using SqlCommand insert = new("INSERT INTO dbo.PHIEUTHUE(" + string.Join(",", columns) + ") VALUES(" + string.Join(",", values) + ")", conn, tran);
            insert.Parameters.AddWithValue("@MaDatPhong", maDatPhong);
            insert.Parameters.AddWithValue("@MaKH", thongTin.MaKhachHang);
            insert.Parameters.AddWithValue("@MaPhong", thongTin.MaPhong);
            insert.Parameters.AddWithValue("@MaNV", thongTin.MaNhanVien);
            insert.Parameters.AddWithValue("@NgayNhan", thongTin.NgayNhanThucTe);
            insert.Parameters.AddWithValue("@NgayTra", thongTin.NgayTraDuKienMoi);
            insert.Parameters.AddWithValue("@SoNguoi", thongTin.SoNguoi);
            insert.Parameters.AddWithValue("@TienCoc", thongTin.TienCoc);
            insert.Parameters.AddWithValue("@TrangThai", trangThaiThue);
            insert.Parameters.AddWithValue("@GhiChu", string.IsNullOrWhiteSpace(thongTin.GhiChu) ? DBNull.Value : thongTin.GhiChu);
            insert.ExecuteNonQuery();
        }

        private static int LayHoacThemKhachHang(SqlConnection conn, SqlTransaction tran, KhachHangDTO khachHang)
        {
            string table = ResolveTable(conn, tran, "KHACHHANG", "KhachHang", "Khach_Hang", "Customer", "Customers");
            if (string.IsNullOrWhiteSpace(table))
            {
                throw new InvalidOperationException("Không tìm thấy bảng khách hàng.");
            }

            int existing = LayMaKhachHangTonTai(conn, tran, table, khachHang);
            if (existing > 0)
            {
                CapNhatKhachHang(conn, tran, table, existing, khachHang);
                return existing;
            }

            List<string> columns = new();
            List<string> values = new();
            AddColumnIfExists(conn, tran, columns, values, table, "HoTen", "@HoTen");
            AddColumnIfExists(conn, tran, columns, values, table, "GioiTinh", "@GioiTinh");
            AddColumnIfExists(conn, tran, columns, values, table, "NgaySinh", "@NgaySinh");
            AddColumnIfExists(conn, tran, columns, values, table, "CCCD", "@CCCD");
            AddColumnIfExists(conn, tran, columns, values, table, "SDT", "@SDT");
            AddColumnIfExists(conn, tran, columns, values, table, "DiaChi", "@DiaChi");
            AddColumnIfExists(conn, tran, columns, values, table, "LoaiKhach", "@LoaiKhach");
            AddColumnIfExists(conn, tran, columns, values, table, "PhanTramGiamGia", "@PhanTramGiamGia");
            AddColumnIfExists(conn, tran, columns, values, table, "TrangThai", "@TrangThai");

            using SqlCommand cmd = new("INSERT INTO dbo." + table + "(" + string.Join(",", columns) + ") VALUES(" + string.Join(",", values) + "); SELECT SCOPE_IDENTITY();", conn, tran);
            GanThamSoKhachHang(cmd, khachHang);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static int ThemPhieuDatPhong(SqlConnection conn, SqlTransaction tran, string table, DatPhongRequestDTO request, int maKhachHang)
        {
            List<string> columns = new();
            List<string> values = new();
            AddColumnIfExists(conn, tran, columns, values, table, "MaKH", "@MaKH");
            AddColumnIfExists(conn, tran, columns, values, table, "MaNV", "@MaNV");
            AddColumnIfExists(conn, tran, columns, values, table, "MaPhong", "@MaPhong");
            AddColumnIfExists(conn, tran, columns, values, table, "LoaiDatPhong", "@LoaiDatPhong");
            AddColumnIfExists(conn, tran, columns, values, table, "LoaiDat", "@LoaiDat");
            AddColumnIfExists(conn, tran, columns, values, table, "NgayDat", "GETDATE()");
            AddColumnIfExists(conn, tran, columns, values, table, "NgayNhanDuKien", "@NgayNhan");
            AddColumnIfExists(conn, tran, columns, values, table, "NgayNhanPhong", "@NgayNhan");
            AddColumnIfExists(conn, tran, columns, values, table, "NgayTraDuKien", "@NgayTra");
            AddColumnIfExists(conn, tran, columns, values, table, "NgayTraPhong", "@NgayTra");
            AddColumnIfExists(conn, tran, columns, values, table, "SoNguoi", "@SoNguoi");
            AddColumnIfExists(conn, tran, columns, values, table, "TienCoc", "@TienCoc");
            AddColumnIfExists(conn, tran, columns, values, table, "DatCoc", "@TienCoc");
            AddColumnIfExists(conn, tran, columns, values, table, "TrangThai", "@TrangThai");
            AddColumnIfExists(conn, tran, columns, values, table, "TienCocHoan", "0");
            AddColumnIfExists(conn, tran, columns, values, table, "TienCocGiu", "0");
            AddColumnIfExists(conn, tran, columns, values, table, "GhiChu", "@GhiChu");

            using SqlCommand cmd = new("INSERT INTO dbo." + table + "(" + string.Join(",", columns) + ") VALUES(" + string.Join(",", values) + "); SELECT SCOPE_IDENTITY();", conn, tran);
            cmd.Parameters.AddWithValue("@MaKH", maKhachHang);
            cmd.Parameters.AddWithValue("@MaNV", 1);
            cmd.Parameters.AddWithValue("@MaPhong", request.Phong.Ma);
            cmd.Parameters.AddWithValue("@LoaiDatPhong", LayGiaTriHopLeTheoCheck(conn, tran, table, "LoaiDatPhong", request.NhanNgay ? "Nhận ngay" : "Đặt trước", request.NhanNgay ? "Nhan ngay" : "Dat truoc", request.CheDoDatPhong));
            cmd.Parameters.AddWithValue("@LoaiDat", LayGiaTriHopLeTheoCheck(conn, tran, table, "LoaiDat", request.CheDoDatPhong, request.NhanNgay ? "Nhận ngay" : "Đặt trước", "Theo ngày"));
            cmd.Parameters.AddWithValue("@NgayNhan", request.NgayNhan);
            cmd.Parameters.AddWithValue("@NgayTra", request.NgayTra);
            cmd.Parameters.AddWithValue("@SoNguoi", request.SoNguoi);
            cmd.Parameters.AddWithValue("@TienCoc", request.NhanNgay ? 0 : request.TienCoc);
            cmd.Parameters.AddWithValue("@TrangThai", LayGiaTriHopLeTheoCheck(conn, tran, table, "TrangThai", "Đã xác nhận", "Da xac nhan", "Đã đặt", "Da dat", "Đang thuê"));
            cmd.Parameters.AddWithValue("@GhiChu", string.IsNullOrWhiteSpace(request.GhiChu) ? DBNull.Value : request.GhiChu);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static void ThemChiTietDatPhong(SqlConnection conn, SqlTransaction tran, int maDatPhong, DatPhongRequestDTO request)
        {
            if (!TableExists(conn, tran, "CHITIETDATPHONG"))
            {
                return;
            }

            List<string> columns = new();
            List<string> values = new();
            AddColumnIfExists(conn, tran, columns, values, "CHITIETDATPHONG", "MaDatPhong", "@MaDatPhong");
            AddColumnIfExists(conn, tran, columns, values, "CHITIETDATPHONG", "MaPhong", "@MaPhong");
            AddColumnIfExists(conn, tran, columns, values, "CHITIETDATPHONG", "DonGiaDuKien", "@DonGia");

            if (columns.Count == 0)
            {
                return;
            }

            using SqlCommand cmd = new("INSERT INTO dbo.CHITIETDATPHONG(" + string.Join(",", columns) + ") VALUES(" + string.Join(",", values) + ")", conn, tran);
            cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong);
            cmd.Parameters.AddWithValue("@MaPhong", request.Phong.Ma);
            cmd.Parameters.AddWithValue("@DonGia", LayDonGiaTheoCheDo(request));
            cmd.ExecuteNonQuery();
        }

        private static int? ThemPhieuThue(SqlConnection conn, SqlTransaction tran, int maDatPhong, DatPhongRequestDTO request, int maKhachHang)
        {
            if (!TableExists(conn, tran, "PHIEUTHUE"))
            {
                return null;
            }

            List<string> columns = new();
            List<string> values = new();
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "MaDatPhong", "@MaDatPhong");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "MaKH", "@MaKH");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "MaPhong", "@MaPhong");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "MaNV", "@MaNV");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "NgayNhan", "@NgayNhan");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "NgayTraDuKien", "@NgayTra");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "SoNguoi", "@SoNguoi");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "TienCoc", "@TienCoc");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "TrangThai", "@TrangThai");
            AddColumnIfExists(conn, tran, columns, values, "PHIEUTHUE", "GhiChu", "@GhiChu");

            using SqlCommand cmd = new("INSERT INTO dbo.PHIEUTHUE(" + string.Join(",", columns) + ") VALUES(" + string.Join(",", values) + "); SELECT SCOPE_IDENTITY();", conn, tran);
            cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong);
            cmd.Parameters.AddWithValue("@MaKH", maKhachHang);
            cmd.Parameters.AddWithValue("@MaPhong", request.Phong.Ma);
            cmd.Parameters.AddWithValue("@MaNV", 1);
            cmd.Parameters.AddWithValue("@NgayNhan", request.NgayNhan);
            cmd.Parameters.AddWithValue("@NgayTra", request.NgayTra);
            cmd.Parameters.AddWithValue("@SoNguoi", request.SoNguoi);
            cmd.Parameters.AddWithValue("@TienCoc", request.TienCoc);
            cmd.Parameters.AddWithValue("@TrangThai", LayGiaTriHopLeTheoCheck(conn, tran, "PHIEUTHUE", "TrangThai", "Đang thuê", "Dang thue", "Đã đặt", "Da dat"));
            cmd.Parameters.AddWithValue("@GhiChu", string.IsNullOrWhiteSpace(request.GhiChu) ? DBNull.Value : request.GhiChu);
            object? value = cmd.ExecuteScalar();
            return value == null || value == DBNull.Value ? null : Convert.ToInt32(value);
        }

        private static void ThemDichVuPhatSinh(SqlConnection conn, SqlTransaction tran, int maDatPhong, int? maThue, DatPhongRequestDTO request)
        {
            if (request.DichVuDaThem.Count == 0)
            {
                return;
            }

            string table = TableExists(conn, tran, "PHATSINHDICHVU")
                ? "PHATSINHDICHVU"
                : TableExists(conn, tran, "CHITIETPHATSINH")
                    ? "CHITIETPHATSINH"
                    : string.Empty;
            if (string.IsNullOrWhiteSpace(table))
            {
                return;
            }

            bool requiresMaThue = ColumnRequired(conn, tran, table, "MaThue");
            if (requiresMaThue && !maThue.HasValue)
            {
                return;
            }

            string maDvColumn = ColumnExists(conn, tran, table, "MaDVVT")
                ? "MaDVVT"
                : ColumnExists(conn, tran, table, "MaDichVu")
                    ? "MaDichVu"
                    : string.Empty;
            if (string.IsNullOrWhiteSpace(maDvColumn))
            {
                return;
            }

            foreach (DichVuDatPhongDTO dichVu in request.DichVuDaThem.Where(item => item.SoLuong > 0))
            {
                List<string> columns = new();
                List<string> values = new();
                AddColumnIfExists(conn, tran, columns, values, table, "MaThue", "@MaThue");
                AddColumnIfExists(conn, tran, columns, values, table, "MaDatPhong", "@MaDatPhong");
                AddColumnIfExists(conn, tran, columns, values, table, "MaPhong", "@MaPhong");
                AddColumnIfExists(conn, tran, columns, values, table, maDvColumn, "@MaDichVu");
                AddColumnIfExists(conn, tran, columns, values, table, "SoLuong", "@SoLuong");
                AddColumnIfExists(conn, tran, columns, values, table, "DonGia", "@DonGia");
                AddColumnIfExists(conn, tran, columns, values, table, "ThanhTien", "@ThanhTien");
                AddColumnIfExists(conn, tran, columns, values, table, "NgayPhatSinh", "@NgayPhatSinh");
                AddColumnIfExists(conn, tran, columns, values, table, "NgaySuDung", "@NgayPhatSinh");

                using SqlCommand cmd = new("INSERT INTO dbo." + table + "(" + string.Join(",", columns) + ") VALUES(" + string.Join(",", values) + ")", conn, tran);
                cmd.Parameters.AddWithValue("@MaThue", maThue.HasValue ? maThue.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong);
                cmd.Parameters.AddWithValue("@MaPhong", request.Phong.Ma);
                cmd.Parameters.AddWithValue("@MaDichVu", dichVu.Ma);
                cmd.Parameters.AddWithValue("@SoLuong", dichVu.SoLuong);
                cmd.Parameters.AddWithValue("@DonGia", dichVu.DonGia);
                cmd.Parameters.AddWithValue("@ThanhTien", dichVu.ThanhTien);
                cmd.Parameters.AddWithValue("@NgayPhatSinh", DateTime.Now);
                cmd.ExecuteNonQuery();
            }
        }

        private static void CapNhatTrangThaiPhong(SqlConnection conn, SqlTransaction tran, int maPhong, string trangThai)
        {
            string status = LayGiaTriHopLeTheoCheck(conn, tran, "PHONG", "TrangThai", trangThai, "Có khách", "Co khach", "Đang thuê", "Dang thue", "Đã đặt", "Da dat");
            using SqlCommand cmd = new("UPDATE dbo.PHONG SET TrangThai = @TrangThai WHERE MaPhong = @MaPhong", conn, tran);
            cmd.Parameters.AddWithValue("@TrangThai", status);
            cmd.Parameters.AddWithValue("@MaPhong", maPhong);
            cmd.ExecuteNonQuery();
        }

        private static void CapNhatTrangThaiDatPhong(SqlConnection conn, SqlTransaction tran, string table, int maDatPhong, string trangThai)
        {
            if (!ColumnExists(conn, tran, table, "TrangThai"))
            {
                return;
            }

            using SqlCommand cmd = new("UPDATE dbo." + table + " SET TrangThai = @TrangThai WHERE MaDatPhong = @MaDatPhong", conn, tran);
            cmd.Parameters.AddWithValue("@TrangThai", trangThai);
            cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong);
            cmd.ExecuteNonQuery();
        }

        private static int LayMaKhachHangTonTai(SqlConnection conn, SqlTransaction tran, string table, KhachHangDTO khachHang)
        {
            List<string> conditions = new();
            if (!string.IsNullOrWhiteSpace(khachHang.CCCD) && ColumnExists(conn, tran, table, "CCCD")) conditions.Add("CCCD = @CCCD");
            if (!string.IsNullOrWhiteSpace(khachHang.SDT) && ColumnExists(conn, tran, table, "SDT")) conditions.Add("SDT = @SDT");
            if (conditions.Count == 0) return 0;

            string key = GetFirstExistingColumn(conn, tran, table, "MaKH", "MaKhachHang", "Ma", "ID");
            if (string.IsNullOrWhiteSpace(key)) return 0;

            using SqlCommand cmd = new("SELECT TOP 1 " + key + " FROM dbo." + table + " WHERE " + string.Join(" OR ", conditions) + " ORDER BY " + key + " DESC", conn, tran);
            cmd.Parameters.AddWithValue("@CCCD", string.IsNullOrWhiteSpace(khachHang.CCCD) ? DBNull.Value : khachHang.CCCD.Trim());
            cmd.Parameters.AddWithValue("@SDT", string.IsNullOrWhiteSpace(khachHang.SDT) ? DBNull.Value : khachHang.SDT.Trim());
            object? value = cmd.ExecuteScalar();
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static void CapNhatKhachHang(SqlConnection conn, SqlTransaction tran, string table, int maKhachHang, KhachHangDTO khachHang)
        {
            List<string> sets = new();
            AddSetIfExists(conn, tran, sets, table, "HoTen", "@HoTen");
            AddSetIfExists(conn, tran, sets, table, "GioiTinh", "@GioiTinh");
            AddSetIfExists(conn, tran, sets, table, "NgaySinh", "@NgaySinh");
            AddSetIfExists(conn, tran, sets, table, "DiaChi", "@DiaChi");
            AddSetIfExists(conn, tran, sets, table, "LoaiKhach", "@LoaiKhach");
            AddSetIfExists(conn, tran, sets, table, "PhanTramGiamGia", "@PhanTramGiamGia");

            if (sets.Count == 0) return;

            string key = GetFirstExistingColumn(conn, tran, table, "MaKH", "MaKhachHang", "Ma", "ID");
            using SqlCommand cmd = new("UPDATE dbo." + table + " SET " + string.Join(",", sets) + " WHERE " + key + " = @MaKH", conn, tran);
            GanThamSoKhachHang(cmd, khachHang);
            cmd.Parameters.AddWithValue("@MaKH", maKhachHang);
            cmd.ExecuteNonQuery();
        }

        private static void GanThamSoKhachHang(SqlCommand cmd, KhachHangDTO khachHang)
        {
            string loaiKhach = string.IsNullOrWhiteSpace(khachHang.LoaiKhach) ? "Thường" : khachHang.LoaiKhach;
            cmd.Parameters.AddWithValue("@HoTen", khachHang.HoTen.Trim());
            cmd.Parameters.AddWithValue("@GioiTinh", string.IsNullOrWhiteSpace(khachHang.GioiTinh) ? DBNull.Value : khachHang.GioiTinh);
            cmd.Parameters.AddWithValue("@NgaySinh", khachHang.NgaySinh.HasValue ? khachHang.NgaySinh.Value.Date : DBNull.Value);
            cmd.Parameters.AddWithValue("@CCCD", string.IsNullOrWhiteSpace(khachHang.CCCD) ? DBNull.Value : khachHang.CCCD.Trim());
            cmd.Parameters.AddWithValue("@SDT", string.IsNullOrWhiteSpace(khachHang.SDT) ? DBNull.Value : khachHang.SDT.Trim());
            cmd.Parameters.AddWithValue("@DiaChi", string.IsNullOrWhiteSpace(khachHang.DiaChi) ? DBNull.Value : khachHang.DiaChi.Trim());
            cmd.Parameters.AddWithValue("@LoaiKhach", loaiKhach.Contains("VIP", StringComparison.OrdinalIgnoreCase) ? "VIP" : "Thường");
            cmd.Parameters.AddWithValue("@PhanTramGiamGia", loaiKhach.Contains("VIP", StringComparison.OrdinalIgnoreCase) ? 10 : 0);
            cmd.Parameters.AddWithValue("@TrangThai", string.IsNullOrWhiteSpace(khachHang.TrangThai) ? "Đang hoạt động" : khachHang.TrangThai);
        }

        private static decimal LayDonGiaTheoCheDo(DatPhongRequestDTO request)
        {
            string mode = BoDau(request.CheDoDatPhong);
            if (mode.Contains("gio", StringComparison.OrdinalIgnoreCase)) return request.Phong.GiaGio;
            if (mode.Contains("dem", StringComparison.OrdinalIgnoreCase)) return request.Phong.GiaDem;
            return request.Phong.GiaNgay > 0 ? request.Phong.GiaNgay : request.Phong.GiaPhong;
        }

        private static string TaoGhiChuDoan(List<DatPhongRequestDTO> danhSach)
        {
            string phong = string.Join(", ", danhSach.Select(item => item.Phong.MaHienThi).Where(value => !string.IsNullOrWhiteSpace(value)));
            string ghiChu = danhSach.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.GhiChu))?.GhiChu ?? string.Empty;
            string marker = $"[DAT_DOAN] SoPhong={danhSach.Count}; Phong={phong}; TongTienPhong={danhSach.Sum(item => item.TienPhong):N0}; TongTienDichVu={danhSach.Sum(item => item.TienDichVu):N0}; TongCoc={danhSach.Sum(item => item.TienCoc):N0}";
            return string.IsNullOrWhiteSpace(ghiChu) ? marker : marker + " - " + ghiChu;
        }

        private static string LayGiaTriHopLeTheoCheck(SqlConnection conn, SqlTransaction tran, string tableName, string columnName, params string[] priorities)
        {
            List<string> allowed = LayGiaTriTrongCheckConstraint(conn, tran, tableName, columnName);
            foreach (string priority in priorities)
            {
                string? exact = allowed.FirstOrDefault(item => string.Equals(item, priority, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(exact)) return exact;
            }

            return allowed.Count > 0 ? allowed[0] : priorities.FirstOrDefault() ?? string.Empty;
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

        private static void AddColumnIfExists(SqlConnection conn, SqlTransaction tran, List<string> columns, List<string> values, string tableName, string columnName, string valueExpression)
        {
            if (ColumnExists(conn, tran, tableName, columnName))
            {
                columns.Add(columnName);
                values.Add(valueExpression);
            }
        }

        private static void AddSetIfExists(SqlConnection conn, SqlTransaction tran, List<string> sets, string tableName, string columnName, string parameter)
        {
            if (ColumnExists(conn, tran, tableName, columnName))
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

        private static string BoDau(string value)
        {
            string formD = value.Normalize(System.Text.NormalizationForm.FormD);
            char[] chars = formD
                .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .Select(ch => ch == 'đ' ? 'd' : ch == 'Đ' ? 'D' : ch)
                .ToArray();
            return new string(chars).Normalize(System.Text.NormalizationForm.FormC);
        }

        private class ThongTinNhanPhong
        {
            public int MaKhachHang { get; set; }
            public int MaNhanVien { get; set; }
            public int MaPhong { get; set; }
            public int SoNguoi { get; set; }
            public DateTime NgayNhanThucTe { get; set; }
            public DateTime NgayTraDuKienMoi { get; set; }
            public decimal TienCoc { get; set; }
            public string? GhiChu { get; set; }
        }
    }
}
