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

                if (tienThucThuTaiQuay > 0)
                {
                    ChenChiTietThanhToan(conn, tran, maHoaDon, maDatPhong, maThue, "Tien phong khi check-in", tienThucThuTaiQuay, "Room");
                }

                CapNhatTrangThaiDatPhong(conn, tran, bangDatPhong, maDatPhong, "Occupied", "Dang thue", "Dang o", "Co khach");
                CapNhatTrangThaiPhongTheoDatPhong(conn, tran, bangDatPhong, maDatPhong, "Occupied", "Dang thue", "Co khach");

                tran.Commit();
                return new KetQuaCheckInThanhToanDTO
                {
                    MaHoaDon = maHoaDon,
                    MaThue = maThue,
                    TongTienDuKien = thongTin.TongTienPhong,
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
            if (tongTienDichVuMoi <= 0)
            {
                return;
            }

            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();

            try
            {
                int? maDatPhong = LayMaDatPhongTheoThue(conn, tran, maThue);
                int maHoaDon = LayHoacTaoHoaDonTheoThue(conn, tran, maThue, maDatPhong);
                CongTongTienDichVu(conn, tran, maHoaDon, tongTienDichVuMoi);
                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        public KetQuaCheckOutThanhToanDTO CheckOut(int maThue)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();

            try
            {
                int? maDatPhong = LayMaDatPhongTheoThue(conn, tran, maThue);
                int maHoaDon = LayHoacTaoHoaDonTheoThue(conn, tran, maThue, maDatPhong);
                decimal tienThuThem = LayTongTienDichVuHoaDon(conn, tran, maHoaDon, maThue, maDatPhong);

                if (tienThuThem > 0)
                {
                    ChenChiTietThanhToan(conn, tran, maHoaDon, maDatPhong, maThue, "Thanh toan dich vu khi check-out", tienThuThem, "Service");
                }

                List<int> nhomMaDatPhong = LayNhomDatPhongTheoThue(conn, tran, maThue);
                CapNhatTrangThaiHoaDon(conn, tran, maHoaDon, "Closed", "Da thanh toan", "Da dong");
                CapNhatTrangThaiPhieuThue(conn, tran, maThue, nhomMaDatPhong, "Da tra phong", "Da tra", "Closed");
                CapNhatTrangThaiPhongTheoThue(conn, tran, maThue, nhomMaDatPhong, "Dirty", "Chua don dep", "Can don dep");

                tran.Commit();
                return new KetQuaCheckOutThanhToanDTO
                {
                    MaHoaDon = maHoaDon,
                    TienThuThem = tienThuThem
                };
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

                ThongTinHoaDon thongTin = LayThongTinHoaDonTheoDatPhong(conn, tran, bangDatPhong, maDatPhong, null);
                int maHoaDon = TaoHoacCapNhatHoaDon(conn, tran, thongTin, "Closed");
                if (thongTin.TienCoc > 0)
                {
                    ChenChiTietThanhToan(conn, tran, maHoaDon, maDatPhong, null, "Phat No-Show tu tien coc", thongTin.TienCoc, "No-Show");
                }

                CapNhatTrangThaiDatPhong(conn, tran, bangDatPhong, maDatPhong, "No-Show", "No Show", "Khach khong den");
                CapNhatTrangThaiPhongTheoDatPhong(conn, tran, bangDatPhong, maDatPhong, "Ready", "Phong trong", "San sang");

                tran.Commit();
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

        private static int TaoHoacCapNhatHoaDon(SqlConnection conn, SqlTransaction tran, ThongTinHoaDon thongTin, string trangThai)
        {
            if (!TableExists(conn, tran, "HOADON"))
            {
                return 0;
            }

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
                AddSetIfExists(conn, tran, sets, "HOADON", "TrangThai", "@TrangThai");

                if (sets.Count > 0)
                {
                    using SqlCommand update = new("UPDATE dbo.HOADON SET " + string.Join(",", sets) + " WHERE " + hoaDonKey + " = @MaHoaDon", conn, tran);
                    GanThamSoHoaDon(update, thongTin, trangThai);
                    update.Parameters.AddWithValue("@MaHoaDon", existing);
                    update.ExecuteNonQuery();
                }

                return existing;
            }

            List<string> columns = new();
            List<string> values = new();
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "MaThue", "@MaThue");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "MaDatPhong", "@MaDatPhong");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "MaKH", "@MaKH");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "MaPhong", "@MaPhong");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "NgayLap", "@NgayLap");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "NgayLapHoaDon", "@NgayLap");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TongTienPhong", "@TongTienPhong");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TongTienDuKien", "@TongTienDuKien");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TienDatCocTruoc", "@TienCoc");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TienCoc", "@TienCoc");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TongTienDichVu", "@TongTienDichVu");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TongTien", "@TongTien");
            AddColumnIfExists(conn, tran, columns, values, "HOADON", "TrangThai", "@TrangThai");

            if (columns.Count == 0)
            {
                return 0;
            }

            string identitySelect = !string.IsNullOrWhiteSpace(hoaDonKey)
                ? "; SELECT CONVERT(int, SCOPE_IDENTITY());"
                : string.Empty;
            using SqlCommand insert = new("INSERT INTO dbo.HOADON(" + string.Join(",", columns) + ") VALUES(" + string.Join(",", values) + ")" + identitySelect, conn, tran);
            GanThamSoHoaDon(insert, thongTin, trangThai);
            object? value = insert.ExecuteScalar();
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
            cmd.Parameters.AddWithValue("@TongTien", thongTin.TongTienPhong + thongTin.TongTienDichVu);
            cmd.Parameters.AddWithValue("@TrangThai", trangThai);
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

        private static void CongTongTienDichVu(SqlConnection conn, SqlTransaction tran, int maHoaDon, decimal soTien)
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

            List<string> sets = new();
            if (ColumnExists(conn, tran, "HOADON", "TongTienDichVu")) sets.Add("TongTienDichVu = ISNULL(TongTienDichVu, 0) + @SoTien");
            if (ColumnExists(conn, tran, "HOADON", "TongTien")) sets.Add("TongTien = ISNULL(TongTien, 0) + @SoTien");
            if (sets.Count == 0)
            {
                return;
            }

            using SqlCommand cmd = new("UPDATE dbo.HOADON SET " + string.Join(",", sets) + " WHERE " + hoaDonKey + " = @MaHoaDon", conn, tran);
            cmd.Parameters.AddWithValue("@SoTien", soTien);
            cmd.Parameters.AddWithValue("@MaHoaDon", maHoaDon);
            cmd.ExecuteNonQuery();
        }

        private static decimal LayTongTienDichVuHoaDon(SqlConnection conn, SqlTransaction tran, int maHoaDon, int maThue, int? maDatPhong)
        {
            if (maHoaDon > 0 && TableExists(conn, tran, "HOADON") && ColumnExists(conn, tran, "HOADON", "TongTienDichVu"))
            {
                string hoaDonKey = LayCotKhoaHoaDon(conn, tran);
                if (!string.IsNullOrWhiteSpace(hoaDonKey))
                {
                    using SqlCommand cmd = new("SELECT ISNULL(TongTienDichVu, 0) FROM dbo.HOADON WHERE " + hoaDonKey + " = @MaHoaDon", conn, tran);
                    cmd.Parameters.AddWithValue("@MaHoaDon", maHoaDon);
                    object? value = cmd.ExecuteScalar();
                    if (value != null && value != DBNull.Value)
                    {
                        return Convert.ToDecimal(value);
                    }
                }
            }

            return TinhTongTienDichVuPhatSinh(conn, tran, maThue, maDatPhong);
        }

        private static decimal TinhTongTienDichVuPhatSinh(SqlConnection conn, SqlTransaction tran, int? maThue, int? maDatPhong)
        {
            string table = ResolveTable(conn, tran, "PHATSINHDICHVU", "CHITIETPHATSINH");
            if (string.IsNullOrWhiteSpace(table))
            {
                return 0;
            }

            string amountExpr = ColumnExists(conn, tran, table, "ThanhTien") ? "ThanhTien" : ColumnExists(conn, tran, table, "DonGia") && ColumnExists(conn, tran, table, "SoLuong") ? "(DonGia * SoLuong)" : "0";
            List<string> conditions = new();
            if (maThue.HasValue && ColumnExists(conn, tran, table, "MaThue")) conditions.Add("MaThue = @MaThue");
            if (maDatPhong.HasValue && ColumnExists(conn, tran, table, "MaDatPhong")) conditions.Add("MaDatPhong = @MaDatPhong");
            if (conditions.Count == 0)
            {
                return 0;
            }

            using SqlCommand cmd = new("SELECT ISNULL(SUM(" + amountExpr + "), 0) FROM dbo." + table + " WHERE " + string.Join(" OR ", conditions), conn, tran);
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

            using SqlCommand cmd = new("SELECT TOP 1 " + hoaDonKey + " FROM dbo.HOADON WHERE " + string.Join(" OR ", conditions) + " ORDER BY " + hoaDonKey + " DESC", conn, tran);
            cmd.Parameters.AddWithValue("@MaThue", maThue.HasValue ? maThue.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong.HasValue ? maDatPhong.Value : DBNull.Value);
            object? value = cmd.ExecuteScalar();
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static ThongTinHoaDon LayThongTinHoaDonTheoDatPhong(SqlConnection conn, SqlTransaction tran, string bangDatPhong, int maDatPhong, int? maThue)
        {
            string tienCocColumn = GetFirstExistingColumn(conn, tran, bangDatPhong, "TienCoc", "DatCoc");
            string maPhongExpr = ColumnExists(conn, tran, bangDatPhong, "MaPhong") ? "DP.MaPhong" : TableExists(conn, tran, "CHITIETDATPHONG") ? "(SELECT TOP 1 MaPhong FROM dbo.CHITIETDATPHONG CT WHERE CT.MaDatPhong = DP.MaDatPhong ORDER BY MaPhong)" : "CAST(NULL AS int)";
            string tienCocExpr = string.IsNullOrWhiteSpace(tienCocColumn) ? "CAST(0 AS decimal(18,2))" : "ISNULL(DP." + tienCocColumn + ", 0)";
            string tongTienExpr = TienPhongDatPhongExpr(conn, tran, bangDatPhong, "DP", maPhongExpr);

            using SqlCommand cmd = new(@"
SELECT TOP 1 DP.MaDatPhong,
       DP.MaKH,
       " + maPhongExpr + @" AS MaPhong,
       " + tienCocExpr + @" AS TienCoc,
       " + tongTienExpr + @" AS TongTienPhong
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
            decimal tienCoc = reader["TienCoc"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TienCoc"]);
            decimal tongTienPhong = reader["TongTienPhong"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TongTienPhong"]);

            reader.Close();

            return new ThongTinHoaDon
            {
                MaDatPhong = maDatPhong,
                MaThue = maThue,
                MaKhachHang = maKhachHang,
                MaPhong = maPhong,
                TienCoc = tienCoc,
                TongTienPhong = tongTienPhong,
                TongTienDichVu = TinhTongTienDichVuPhatSinh(conn, tran, maThue, maDatPhong)
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

            using SqlCommand cmd = new(@"
SELECT TOP 1 " + maDatPhongExpr + @" AS MaDatPhong,
       PT.MaKH,
       " + maPhongExpr + @" AS MaPhong,
       " + tienCocExpr + @" AS TienCoc,
       " + tongTienExpr + @" AS TongTienPhong
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
            int? maPhong = reader["MaPhong"] == DBNull.Value ? null : Convert.ToInt32(reader["MaPhong"]);
            decimal tienCoc = reader["TienCoc"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TienCoc"]);
            decimal tongTienPhong = reader["TongTienPhong"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TongTienPhong"]);

            reader.Close();

            return new ThongTinHoaDon
            {
                MaThue = maThue,
                MaDatPhong = maDatPhong,
                MaKhachHang = maKhachHang,
                MaPhong = maPhong,
                TienCoc = tienCoc,
                TongTienPhong = tongTienPhong,
                TongTienDichVu = TinhTongTienDichVuPhatSinh(conn, tran, maThue, maDatPhong)
            };
        }

        private static string TienPhongDatPhongExpr(SqlConnection conn, SqlTransaction tran, string table, string alias, string maPhongExpr)
        {
            string ngayNhan = ColumnExists(conn, tran, table, "NgayNhanDuKien") ? alias + ".NgayNhanDuKien" : ColumnExists(conn, tran, table, "NgayNhanPhong") ? alias + ".NgayNhanPhong" : "GETDATE()";
            string ngayTra = ColumnExists(conn, tran, table, "NgayTraDuKien") ? alias + ".NgayTraDuKien" : ColumnExists(conn, tran, table, "NgayTraPhong") ? alias + ".NgayTraPhong" : "DATEADD(day, 1, GETDATE())";
            string giaNgay = TableExists(conn, tran, "PHONG") && ColumnExists(conn, tran, "PHONG", "GiaNgay") ? "(SELECT TOP 1 ISNULL(GiaNgay, 0) FROM dbo.PHONG P WHERE P.MaPhong = " + maPhongExpr + ")" : "0";
            return TienPhongSql(ngayNhan, ngayTra, giaNgay);
        }

        private static string TienPhongPhieuThueExpr(SqlConnection conn, SqlTransaction tran)
        {
            string ngayTra = ColumnExists(conn, tran, "PHIEUTHUE", "NgayTraPhong") ? "ISNULL(PT.NgayTraPhong, PT.NgayTraDuKien)" : "PT.NgayTraDuKien";
            string giaNgay = TableExists(conn, tran, "PHONG") && ColumnExists(conn, tran, "PHONG", "GiaNgay") ? "(SELECT TOP 1 ISNULL(GiaNgay, 0) FROM dbo.PHONG P WHERE P.MaPhong = PT.MaPhong)" : "0";
            return TienPhongSql("PT.NgayNhan", ngayTra, giaNgay);
        }

        private static string TienPhongSql(string startExpr, string endExpr, string giaNgayExpr)
        {
            return "CAST(CASE WHEN " + endExpr + " IS NULL OR DATEDIFF(day, " + startExpr + ", " + endExpr + ") <= 0 THEN " + giaNgayExpr + " ELSE DATEDIFF(day, " + startExpr + ", " + endExpr + ") * " + giaNgayExpr + " END AS decimal(18,2))";
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

            using SqlCommand cmd = new("UPDATE dbo.HOADON SET TrangThai = @TrangThai WHERE " + hoaDonKey + " = @MaHoaDon", conn, tran);
            cmd.Parameters.AddWithValue("@TrangThai", LayGiaTriHopLeTheoCheck(conn, tran, "HOADON", "TrangThai", priorities));
            cmd.Parameters.AddWithValue("@MaHoaDon", maHoaDon);
            cmd.ExecuteNonQuery();
        }

        private static void CapNhatTrangThaiPhieuThue(SqlConnection conn, SqlTransaction tran, int maThue, List<int> nhomMaDatPhong, params string[] priorities)
        {
            if (!TableExists(conn, tran, "PHIEUTHUE") || !ColumnExists(conn, tran, "PHIEUTHUE", "TrangThai"))
            {
                return;
            }

            string trangThai = LayGiaTriHopLeTheoCheck(conn, tran, "PHIEUTHUE", "TrangThai", priorities);
            string setNgayTra = ColumnExists(conn, tran, "PHIEUTHUE", "NgayTraPhong") ? ", NgayTraPhong = ISNULL(NgayTraPhong, @NgayTra)" : string.Empty;
            using SqlCommand cmd = new("UPDATE dbo.PHIEUTHUE SET TrangThai = @TrangThai" + setNgayTra + " WHERE MaThue = @MaThue", conn, tran);
            cmd.Parameters.AddWithValue("@TrangThai", trangThai);
            cmd.Parameters.AddWithValue("@NgayTra", DateTime.Now);
            cmd.Parameters.AddWithValue("@MaThue", maThue);
            cmd.ExecuteNonQuery();

            if (nhomMaDatPhong.Count > 0 && ColumnExists(conn, tran, "PHIEUTHUE", "MaDatPhong"))
            {
                string danhSachThamSo = TaoDanhSachThamSo(nhomMaDatPhong, "MaDatPhong");
                using SqlCommand updateGroup = new("UPDATE dbo.PHIEUTHUE SET TrangThai = @TrangThai" + setNgayTra + " WHERE MaDatPhong IN (" + danhSachThamSo + ")", conn, tran);
                updateGroup.Parameters.AddWithValue("@TrangThai", trangThai);
                updateGroup.Parameters.AddWithValue("@NgayTra", DateTime.Now);
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

        private static void AddFirstColumnIfExists(SqlConnection conn, SqlTransaction tran, List<string> columns, List<string> values, string tableName, string valueExpression, params string[] candidates)
        {
            string column = GetFirstExistingColumn(conn, tran, tableName, candidates);
            if (!string.IsNullOrWhiteSpace(column))
            {
                columns.Add(column);
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
            public int? MaKhachHang { get; set; }
            public int? MaPhong { get; set; }
            public decimal TongTienPhong { get; set; }
            public decimal TongTienDichVu { get; set; }
            public decimal TienCoc { get; set; }
        }
    }
}
