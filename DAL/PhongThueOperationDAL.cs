using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DTO;
using System.Globalization;
using System.Text.RegularExpressions;

namespace QLKS_AnPhu.DAL
{
    public class PhongThueOperationDAL
    {
        public void GiaHan(GiaHanPhongRequestDTO request)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();
            try
            {
                List<int> danhSachPhongGiaHan = LayDanhSachPhongGiaHan(conn, tran, request);
                foreach (int maPhong in danhSachPhongGiaHan)
                {
                    KiemTraXungDot(conn, tran, maPhong, request.NgayTraCu, request.NgayTraMoi, request.MaThue, request.MaDatPhong);
                }
                DamBaoTienPhongCheckIn(conn, tran, request.MaThue);

                using (SqlCommand cmd = new("UPDATE dbo.PHIEUTHUE SET NgayTraDuKien = @NgayTraMoi WHERE MaThue = @MaThue", conn, tran))
                {
                    cmd.Parameters.AddWithValue("@NgayTraMoi", request.NgayTraMoi);
                    cmd.Parameters.AddWithValue("@MaThue", request.MaThue);
                    if (cmd.ExecuteNonQuery() == 0)
                    {
                        throw new InvalidOperationException("Không tìm thấy phiếu thuê cần gia hạn.");
                    }
                }

                CapNhatNgayTraDatPhongLienKet(conn, tran, request);
                CapNhatTrangThaiPhieuThueDangThue(conn, tran, request.MaThue);
                CapNhatTrangThaiDatPhongDangThue(conn, tran, request.MaDatPhong);
                CapNhatTrangThaiPhong(conn, tran, request.MaPhong, "Đang thuê", "Dang thue", "Có khách", "Co khach", "Occupied");

                decimal tienGiaHan = danhSachPhongGiaHan.Sum(maPhong => TinhTienKhoang(conn, tran, maPhong, request.NgayTraCu, request.NgayTraMoi));
                GhiNhanGiaHan(conn, tran, request.MaThue, request.NgayTraCu, request.NgayTraMoi, tienGiaHan);

                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        private static List<int> LayDanhSachPhongGiaHan(SqlConnection conn, SqlTransaction tran, GiaHanPhongRequestDTO request)
        {
            List<int> result = new();
            if (request.MaDatPhong.HasValue && TableExists(conn, tran, "CHITIETDATPHONG"))
            {
                using SqlCommand cmd = new(
                    "SELECT DISTINCT MaPhong FROM dbo.CHITIETDATPHONG WHERE MaDatPhong = @MaDatPhong AND MaPhong IS NOT NULL",
                    conn,
                    tran);
                cmd.Parameters.AddWithValue("@MaDatPhong", request.MaDatPhong.Value);
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(Convert.ToInt32(reader["MaPhong"]));
                }
            }

            if (result.Count == 0)
            {
                result.Add(request.MaPhong);
            }

            return result.Distinct().ToList();
        }

        private static void CapNhatNgayTraDatPhongLienKet(SqlConnection conn, SqlTransaction tran, GiaHanPhongRequestDTO request)
        {
            if (!request.MaDatPhong.HasValue)
            {
                return;
            }

            string bangDatPhong = LayBangDatPhong(conn, tran);
            if (!string.IsNullOrWhiteSpace(bangDatPhong))
            {
                string ngayTraColumn = ColumnExists(conn, tran, bangDatPhong, "NgayTraDuKien")
                    ? "NgayTraDuKien"
                    : ColumnExists(conn, tran, bangDatPhong, "NgayTraPhong")
                        ? "NgayTraPhong"
                        : string.Empty;
                if (!string.IsNullOrWhiteSpace(ngayTraColumn))
                {
                    using SqlCommand cmd = new("UPDATE dbo." + bangDatPhong + " SET " + ngayTraColumn + " = @NgayTraMoi WHERE MaDatPhong = @MaDatPhong", conn, tran);
                    cmd.Parameters.AddWithValue("@NgayTraMoi", request.NgayTraMoi);
                    cmd.Parameters.AddWithValue("@MaDatPhong", request.MaDatPhong.Value);
                    cmd.ExecuteNonQuery();
                }
            }

            if (!TableExists(conn, tran, "CHITIETDATPHONG"))
            {
                return;
            }

            string chiTietNgayTraColumn = ColumnExists(conn, tran, "CHITIETDATPHONG", "NgayTraDuKien")
                ? "NgayTraDuKien"
                : ColumnExists(conn, tran, "CHITIETDATPHONG", "NgayTraPhong")
                    ? "NgayTraPhong"
                    : string.Empty;
            if (string.IsNullOrWhiteSpace(chiTietNgayTraColumn))
            {
                return;
            }

            using SqlCommand detailCmd = new("UPDATE dbo.CHITIETDATPHONG SET " + chiTietNgayTraColumn + " = @NgayTraMoi WHERE MaDatPhong = @MaDatPhong", conn, tran);
            detailCmd.Parameters.AddWithValue("@NgayTraMoi", request.NgayTraMoi);
            detailCmd.Parameters.AddWithValue("@MaDatPhong", request.MaDatPhong.Value);
            detailCmd.ExecuteNonQuery();
        }

        private static void DamBaoTienPhongCheckIn(SqlConnection conn, SqlTransaction tran, int maThue)
        {
            if (!TableExists(conn, tran, "HOADON") ||
                !ColumnExists(conn, tran, "HOADON", "MaThue") ||
                !ColumnExists(conn, tran, "HOADON", "TongTienPhong"))
            {
                return;
            }

            string hoaDonKey = GetFirstExistingColumn(conn, tran, "HOADON", "MaHoaDon", "MaHD", "IDHoaDon", "HoaDonID", "IdHoaDon", "ID", "Ma");
            if (string.IsNullOrWhiteSpace(hoaDonKey))
            {
                return;
            }

            using SqlCommand cmd = new(@"
UPDATE dbo.HOADON
SET TongTienPhong = CASE
    WHEN ISNULL(TongTienPhong, 0) > 0 THEN TongTienPhong
    ELSE (
        SELECT TOP 1 " + TienPhongPhieuThueExpr(conn, tran) + @"
        FROM dbo.PHIEUTHUE PT
        WHERE PT.MaThue = @MaThue
    )
END
WHERE " + hoaDonKey + @" = (
    SELECT TOP 1 HD2." + hoaDonKey + @"
    FROM dbo.HOADON HD2
    WHERE HD2.MaThue = @MaThue
    ORDER BY HD2." + hoaDonKey + @" DESC
)", conn, tran);
            cmd.Parameters.AddWithValue("@MaThue", maThue);
            cmd.ExecuteNonQuery();
        }

        private static string TienPhongPhieuThueExpr(SqlConnection conn, SqlTransaction tran)
        {
            string ngayTra = ColumnExists(conn, tran, "PHIEUTHUE", "NgayTraPhong")
                ? "ISNULL(PT.NgayTraPhong, PT.NgayTraDuKien)"
                : "PT.NgayTraDuKien";
            string giaNgay = GiaNgayTheoPhongExpr(conn, tran, "PT.MaPhong");
            return "CAST(CASE WHEN " + ngayTra + " IS NULL OR DATEDIFF(day, PT.NgayNhan, " + ngayTra + ") <= 0 THEN " +
                   giaNgay + " ELSE DATEDIFF(day, PT.NgayNhan, " + ngayTra + ") * " + giaNgay + " END AS decimal(18,2))";
        }

        private static string GiaNgayTheoPhongExpr(SqlConnection conn, SqlTransaction tran, string maPhongExpr)
        {
            if (TableExists(conn, tran, "LOAIPHONG") &&
                TableExists(conn, tran, "PHONG") &&
                ColumnExists(conn, tran, "PHONG", "MaLoaiPhong"))
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

        private static string GetFirstExistingColumn(SqlConnection conn, SqlTransaction tran, string table, params string[] candidates)
        {
            return candidates.FirstOrDefault(column => ColumnExists(conn, tran, table, column)) ?? string.Empty;
        }

        public void DoiPhong(DoiPhongRequestDTO request)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();
            try
            {
                KiemTraPhongDich(conn, tran, request.MaPhongMoi);
                KiemTraXungDot(conn, tran, request.MaPhongMoi, request.NgayBatDau, request.NgayTraDuKien, request.MaThue, request.MaDatPhong);
                decimal tienPhongCuConLai = TinhTienKhoang(conn, tran, request.MaPhongCu, request.NgayBatDau, request.NgayTraDuKien);
                decimal tienPhongMoiConLai = TinhTienKhoang(conn, tran, request.MaPhongMoi, request.NgayBatDau, request.NgayTraDuKien);
                decimal chenhLechTien = tienPhongMoiConLai - tienPhongCuConLai;

                if (request.MaThue > 0)
                {
                    using SqlCommand cmd = new("UPDATE dbo.PHIEUTHUE SET MaPhong = @MaPhongMoi WHERE MaThue = @MaThue", conn, tran);
                    cmd.Parameters.AddWithValue("@MaPhongMoi", request.MaPhongMoi);
                    cmd.Parameters.AddWithValue("@MaThue", request.MaThue);
                    if (cmd.ExecuteNonQuery() == 0)
                    {
                        throw new InvalidOperationException("Không tìm thấy phiếu thuê cần đổi phòng.");
                    }
                }

                string bangDatPhong = LayBangDatPhong(conn, tran);
                if (request.MaDatPhong.HasValue && !string.IsNullOrWhiteSpace(bangDatPhong))
                {
                    if (ColumnExists(conn, tran, bangDatPhong, "MaPhong"))
                    {
                        using SqlCommand cmd = new("UPDATE dbo." + bangDatPhong + " SET MaPhong = @MaPhongMoi WHERE MaDatPhong = @MaDatPhong AND MaPhong = @MaPhongCu", conn, tran);
                        GanThamSoDoiPhong(cmd, request);
                        cmd.ExecuteNonQuery();
                    }

                    if (TableExists(conn, tran, "CHITIETDATPHONG"))
                    {
                        CapNhatChiTietDatPhongSauDoiPhong(conn, tran, request);
                    }

                    if (request.MaThue <= 0)
                    {
                        CapNhatTienPhongDatPhongSauDoiPhong(conn, tran, bangDatPhong, request.MaDatPhong.Value);
                    }
                }

                if (request.MaThue > 0)
                {
                    GhiNhanDoiPhong(conn, tran, request, chenhLechTien);
                    CapNhatPhongCuSauDoiPhong(conn, tran, request.MaPhongCu);
                    CapNhatTrangThaiPhong(conn, tran, request.MaPhongMoi, "Đang thuê", "Dang thue", "Có khách", "Co khach");
                }
                else
                {
                    CapNhatTrangThaiPhong(conn, tran, request.MaPhongCu, "Trống", "Phong trong", "Phòng trống");
                    CapNhatTrangThaiPhong(conn, tran, request.MaPhongMoi, "Đã đặt", "Da dat");
                }
                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        private static void KiemTraPhongDich(SqlConnection conn, SqlTransaction tran, int maPhong)
        {
            using SqlCommand cmd = new("SELECT TOP 1 TrangThai FROM dbo.PHONG WHERE MaPhong = @MaPhong", conn, tran);
            cmd.Parameters.AddWithValue("@MaPhong", maPhong);
            string trangThai = cmd.ExecuteScalar()?.ToString() ?? string.Empty;
            string normalized = BoDau(trangThai);
            if (normalized.Contains("sua", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("bao tri", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("don", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Phòng đích chưa sẵn sàng để sử dụng.");
            }
        }

        private static void CapNhatChiTietDatPhongSauDoiPhong(SqlConnection conn, SqlTransaction tran, DoiPhongRequestDTO request)
        {
            if (!request.MaDatPhong.HasValue || !TableExists(conn, tran, "CHITIETDATPHONG"))
            {
                return;
            }

            string ngayNhanColumn = ColumnExists(conn, tran, "CHITIETDATPHONG", "NgayNhanDuKien")
                ? "NgayNhanDuKien"
                : ColumnExists(conn, tran, "CHITIETDATPHONG", "NgayNhanPhong")
                    ? "NgayNhanPhong"
                    : string.Empty;
            string ngayTraColumn = ColumnExists(conn, tran, "CHITIETDATPHONG", "NgayTraDuKien")
                ? "NgayTraDuKien"
                : ColumnExists(conn, tran, "CHITIETDATPHONG", "NgayTraPhong")
                    ? "NgayTraPhong"
                    : string.Empty;
            if (string.IsNullOrWhiteSpace(ngayNhanColumn) || string.IsNullOrWhiteSpace(ngayTraColumn))
            {
                return;
            }

            string bangDatPhong = LayBangDatPhong(conn, tran);
            string ngayNhanDatPhongColumn = string.IsNullOrWhiteSpace(bangDatPhong)
                ? string.Empty
                : GetFirstExistingColumn(conn, tran, bangDatPhong, "NgayNhanDuKien", "NgayNhanPhong", "NgayNhan");
            string ngayTraDatPhongColumn = string.IsNullOrWhiteSpace(bangDatPhong)
                ? string.Empty
                : GetFirstExistingColumn(conn, tran, bangDatPhong, "NgayTraDuKien", "NgayTraPhong", "NgayTra");
            string joinDatPhong = string.IsNullOrWhiteSpace(bangDatPhong)
                ? string.Empty
                : " LEFT JOIN dbo." + bangDatPhong + " DP ON DP.MaDatPhong = CT.MaDatPhong";
            string ngayNhanHieuLucExpr = string.IsNullOrWhiteSpace(ngayNhanDatPhongColumn)
                ? "CT." + ngayNhanColumn
                : "ISNULL(CT." + ngayNhanColumn + ", DP." + ngayNhanDatPhongColumn + ")";
            string ngayTraHieuLucExpr = string.IsNullOrWhiteSpace(ngayTraDatPhongColumn)
                ? "CT." + ngayTraColumn
                : "ISNULL(CT." + ngayTraColumn + ", DP." + ngayTraDatPhongColumn + ")";

            using (SqlCommand replaceFuture = new(
                       "UPDATE CT SET MaPhong = @MaPhongMoi" + (ColumnExists(conn, tran, "CHITIETDATPHONG", "DonGia") ? ", DonGia = @DonGia" : string.Empty) +
                       " FROM dbo.CHITIETDATPHONG CT" + joinDatPhong +
                       " WHERE CT.MaDatPhong = @MaDatPhong AND CT.MaPhong = @MaPhongCu" +
                       " AND " + ngayNhanHieuLucExpr + " >= @NgayDoi",
                       conn,
                       tran))
            {
                replaceFuture.Parameters.AddWithValue("@MaPhongMoi", request.MaPhongMoi);
                replaceFuture.Parameters.AddWithValue("@MaDatPhong", request.MaDatPhong.Value);
                replaceFuture.Parameters.AddWithValue("@MaPhongCu", request.MaPhongCu);
                replaceFuture.Parameters.AddWithValue("@NgayDoi", request.NgayBatDau);
                if (replaceFuture.CommandText.Contains("@DonGia"))
                {
                    replaceFuture.Parameters.AddWithValue("@DonGia", LayDonGiaPhong(conn, tran, request.MaPhongMoi));
                }

                if (replaceFuture.ExecuteNonQuery() > 0)
                {
                    return;
                }
            }

            using (SqlCommand cutOld = new(
                       "UPDATE CT SET " + ngayTraColumn + " = @NgayDoi" +
                       " FROM dbo.CHITIETDATPHONG CT" + joinDatPhong +
                       " WHERE CT.MaDatPhong = @MaDatPhong AND CT.MaPhong = @MaPhongCu" +
                       " AND " + ngayNhanHieuLucExpr + " < @NgayDoi" +
                       " AND " + ngayTraHieuLucExpr + " > @NgayDoi",
                       conn,
                       tran))
            {
                cutOld.Parameters.AddWithValue("@NgayDoi", request.NgayBatDau);
                cutOld.Parameters.AddWithValue("@MaDatPhong", request.MaDatPhong.Value);
                cutOld.Parameters.AddWithValue("@MaPhongCu", request.MaPhongCu);
                cutOld.ExecuteNonQuery();
            }

            using (SqlCommand exists = new(
                       "SELECT COUNT(*) FROM dbo.CHITIETDATPHONG WHERE MaDatPhong = @MaDatPhong AND MaPhong = @MaPhongMoi AND " + ngayNhanColumn + " = @NgayDoi",
                       conn,
                       tran))
            {
                exists.Parameters.AddWithValue("@MaDatPhong", request.MaDatPhong.Value);
                exists.Parameters.AddWithValue("@MaPhongMoi", request.MaPhongMoi);
                exists.Parameters.AddWithValue("@NgayDoi", request.NgayBatDau);
                if (Convert.ToInt32(exists.ExecuteScalar()) > 0)
                {
                    return;
                }
            }

            List<string> columns = new() { "MaDatPhong", "MaPhong", ngayNhanColumn, ngayTraColumn };
            List<string> values = new() { "@MaDatPhong", "@MaPhongMoi", "@NgayDoi", "@NgayTraDuKien" };
            if (ColumnExists(conn, tran, "CHITIETDATPHONG", "DonGia"))
            {
                columns.Add("DonGia");
                values.Add("@DonGia");
            }
            if (ColumnExists(conn, tran, "CHITIETDATPHONG", "GhiChu"))
            {
                columns.Add("GhiChu");
                values.Add("@GhiChu");
            }

            using SqlCommand insert = new("INSERT INTO dbo.CHITIETDATPHONG (" + string.Join(", ", columns) + ") VALUES (" + string.Join(", ", values) + ")", conn, tran);
            insert.Parameters.AddWithValue("@MaDatPhong", request.MaDatPhong.Value);
            insert.Parameters.AddWithValue("@MaPhongMoi", request.MaPhongMoi);
            insert.Parameters.AddWithValue("@NgayDoi", request.NgayBatDau);
            insert.Parameters.AddWithValue("@NgayTraDuKien", request.NgayTraDuKien);
            if (columns.Contains("DonGia"))
            {
                insert.Parameters.AddWithValue("@DonGia", LayDonGiaPhong(conn, tran, request.MaPhongMoi));
            }
            if (columns.Contains("GhiChu"))
            {
                insert.Parameters.AddWithValue("@GhiChu", "[DOI_PHONG] TuPhong=" + request.MaPhongCu + ";ThoiDiem=" + request.NgayBatDau.ToString("O"));
            }
            insert.ExecuteNonQuery();
        }

        private static void CapNhatTienPhongDatPhongSauDoiPhong(SqlConnection conn, SqlTransaction tran, string bangDatPhong, int maDatPhong)
        {
            if (string.IsNullOrWhiteSpace(bangDatPhong) || !ColumnExists(conn, tran, bangDatPhong, "GhiChu"))
            {
                return;
            }

            decimal tongTienPhong = TinhTongTienPhongDatPhong(conn, tran, bangDatPhong, maDatPhong);
            if (tongTienPhong <= 0)
            {
                return;
            }

            using SqlCommand select = new("SELECT TOP 1 ISNULL(GhiChu, N'') FROM dbo." + bangDatPhong + " WHERE MaDatPhong = @MaDatPhong", conn, tran);
            select.Parameters.AddWithValue("@MaDatPhong", maDatPhong);
            string ghiChu = select.ExecuteScalar()?.ToString() ?? string.Empty;
            string ghiChuMoi = CapNhatMarkerTienPhong(ghiChu, tongTienPhong);

            using SqlCommand update = new("UPDATE dbo." + bangDatPhong + " SET GhiChu = @GhiChu WHERE MaDatPhong = @MaDatPhong", conn, tran);
            update.Parameters.AddWithValue("@GhiChu", ghiChuMoi);
            update.Parameters.AddWithValue("@MaDatPhong", maDatPhong);
            update.ExecuteNonQuery();
        }

        private static decimal TinhTongTienPhongDatPhong(SqlConnection conn, SqlTransaction tran, string bangDatPhong, int maDatPhong)
        {
            string ngayNhanDatPhongColumn = GetFirstExistingColumn(conn, tran, bangDatPhong, "NgayNhanDuKien", "NgayNhanPhong", "NgayNhan");
            string ngayTraDatPhongColumn = GetFirstExistingColumn(conn, tran, bangDatPhong, "NgayTraDuKien", "NgayTraPhong", "NgayTra");
            if (string.IsNullOrWhiteSpace(ngayNhanDatPhongColumn) || string.IsNullOrWhiteSpace(ngayTraDatPhongColumn))
            {
                return 0;
            }

            bool coChiTietDatPhong = TableExists(conn, tran, "CHITIETDATPHONG") &&
                                      ColumnExists(conn, tran, "CHITIETDATPHONG", "MaDatPhong") &&
                                      ColumnExists(conn, tran, "CHITIETDATPHONG", "MaPhong");
            string sourceSql = coChiTietDatPhong
                ? @"FROM dbo.CHITIETDATPHONG CT
JOIN dbo." + bangDatPhong + @" DP ON DP.MaDatPhong = CT.MaDatPhong
JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong"
                : @"FROM dbo." + bangDatPhong + @" DP
JOIN dbo.PHONG P ON DP.MaPhong = P.MaPhong";
            string whereSql = coChiTietDatPhong
                ? "WHERE CT.MaDatPhong = @MaDatPhong"
                : "WHERE DP.MaDatPhong = @MaDatPhong";
            string ngayNhanChiTietColumn = coChiTietDatPhong
                ? GetFirstExistingColumn(conn, tran, "CHITIETDATPHONG", "NgayNhanDuKien", "NgayNhanPhong", "NgayNhan")
                : string.Empty;
            string ngayTraChiTietColumn = coChiTietDatPhong
                ? GetFirstExistingColumn(conn, tran, "CHITIETDATPHONG", "NgayTraDuKien", "NgayTraPhong", "NgayTra")
                : string.Empty;
            string startExpr = string.IsNullOrWhiteSpace(ngayNhanChiTietColumn)
                ? "DP." + ngayNhanDatPhongColumn
                : "ISNULL(CT." + ngayNhanChiTietColumn + ", DP." + ngayNhanDatPhongColumn + ")";
            string endExpr = string.IsNullOrWhiteSpace(ngayTraChiTietColumn)
                ? "DP." + ngayTraDatPhongColumn
                : "ISNULL(CT." + ngayTraChiTietColumn + ", DP." + ngayTraDatPhongColumn + ")";
            string giaNgayExpr = "ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0))";
            string giaGioExpr = "ISNULL(LP.DonGiaGio, 0)";
            string tienPhongExpr = @"CAST(CASE
    WHEN " + endExpr + @" IS NULL OR DATEDIFF(minute, " + startExpr + @", " + endExpr + @") <= 0 THEN " + giaNgayExpr + @"
    WHEN CAST(" + startExpr + @" AS date) = CAST(" + endExpr + @" AS date) THEN CEILING(DATEDIFF(minute, " + startExpr + @", " + endExpr + @") / 60.0) * " + giaGioExpr + @"
    WHEN DATEDIFF(hour, " + startExpr + @", " + endExpr + @") <= 12 THEN " + giaNgayExpr + @"
    ELSE CASE WHEN DATEDIFF(day, CAST(" + startExpr + @" AS date), CAST(" + endExpr + @" AS date)) <= 0 THEN 1
              ELSE DATEDIFF(day, CAST(" + startExpr + @" AS date), CAST(" + endExpr + @" AS date))
         END * " + giaNgayExpr + @"
END AS decimal(18, 2))";

            using SqlCommand cmd = new(
                @"SELECT ISNULL(SUM(" + tienPhongExpr + @"), 0)
" + sourceSql + @"
LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
" + whereSql,
                conn,
                tran);
            cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong);
            object? value = cmd.ExecuteScalar();
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static string CapNhatMarkerTienPhong(string ghiChu, decimal tongTienPhong)
        {
            string value = tongTienPhong.ToString("0", CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(ghiChu))
            {
                return "TongTienPhong=" + value;
            }

            string updated = Regex.Replace(
                ghiChu,
                @"(TongTienPhong|TienPhong)\s*=\s*[0-9][0-9.,]*",
                "TongTienPhong=" + value,
                RegexOptions.IgnoreCase);
            return string.Equals(updated, ghiChu, StringComparison.Ordinal)
                ? ghiChu.TrimEnd() + "; TongTienPhong=" + value
                : updated;
        }

        private static decimal LayDonGiaPhong(SqlConnection conn, SqlTransaction tran, int maPhong)
        {
            using SqlCommand cmd = new(
                @"SELECT TOP 1 ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(LP.DonGiaGio, 0) * 24)) AS DonGia
                  FROM dbo.PHONG P
                  LEFT JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
                  WHERE P.MaPhong = @MaPhong",
                conn,
                tran);
            cmd.Parameters.AddWithValue("@MaPhong", maPhong);
            object? value = cmd.ExecuteScalar();
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static void KiemTraXungDot(SqlConnection conn, SqlTransaction tran, int maPhong, DateTime tuNgay, DateTime denNgay, int maThueBoQua, int? maDatPhongBoQua)
        {
            BookingScheduleGuard.EnsureRoomAvailable(conn, tran, maPhong, tuNgay, denNgay, maThueBoQua, maDatPhongBoQua);
        }

        private static void CapNhatTrangThaiPhong(SqlConnection conn, SqlTransaction tran, int maPhong, params string[] candidates)
        {
            string value = LayGiaTriHopLeTheoCheck(conn, tran, "PHONG", "TrangThai", candidates);
            using SqlCommand cmd = new("UPDATE dbo.PHONG SET TrangThai = @TrangThai WHERE MaPhong = @MaPhong", conn, tran);
            cmd.Parameters.AddWithValue("@TrangThai", value);
            cmd.Parameters.AddWithValue("@MaPhong", maPhong);
            cmd.ExecuteNonQuery();
        }

        private static void CapNhatPhongCuSauDoiPhong(SqlConnection conn, SqlTransaction tran, int maPhongCu)
        {
            PhongTrangThaiSchema.DamBaoCoTrangThaiChuaDonDep(conn, tran);
            using SqlCommand update = new("UPDATE dbo.PHONG SET TrangThai = @TrangThai WHERE MaPhong = @MaPhong", conn, tran);
            update.Parameters.AddWithValue("@TrangThai", PhongTrangThaiSchema.ChuaDonDep);
            update.Parameters.AddWithValue("@MaPhong", maPhongCu);
            update.ExecuteNonQuery();
            return;
#pragma warning disable CS0162
            string? trangThaiCanDon = LayGiaTriHopLeNeuCo(conn, tran, "PHONG", "TrangThai", "Chưa dọn dẹp", "Chua don dep", "Dirty");
            if (!string.IsNullOrWhiteSpace(trangThaiCanDon))
            {
                using SqlCommand cmd = new("UPDATE dbo.PHONG SET TrangThai = @TrangThai WHERE MaPhong = @MaPhong", conn, tran);
                cmd.Parameters.AddWithValue("@TrangThai", trangThaiCanDon);
                cmd.Parameters.AddWithValue("@MaPhong", maPhongCu);
                cmd.ExecuteNonQuery();
                return;
            }

            string trangThaiTrong = LayGiaTriHopLeTheoCheck(conn, tran, "PHONG", "TrangThai", "Trống", "Phong trong", "Phòng trống");
            string noteSet = ColumnExists(conn, tran, "PHONG", "GhiChu")
                ? ", GhiChu = CONCAT(NULLIF(GhiChu, N''), CASE WHEN NULLIF(GhiChu, N'') IS NULL THEN N'' ELSE N' - ' END, @GhiChu)"
                : string.Empty;
            using SqlCommand fallback = new("UPDATE dbo.PHONG SET TrangThai = @TrangThai" + noteSet + " WHERE MaPhong = @MaPhong", conn, tran);
            fallback.Parameters.AddWithValue("@TrangThai", trangThaiTrong);
            fallback.Parameters.AddWithValue("@MaPhong", maPhongCu);
            if (!string.IsNullOrWhiteSpace(noteSet))
            {
                fallback.Parameters.AddWithValue("@GhiChu", "[CAN_DON_DEP] Can don dep sau khi doi phong");
            }
            fallback.ExecuteNonQuery();
        }

#pragma warning restore CS0162
        private static void CapNhatTrangThaiPhieuThueDangThue(SqlConnection conn, SqlTransaction tran, int maThue)
        {
            if (!ColumnExists(conn, tran, "PHIEUTHUE", "TrangThai"))
            {
                return;
            }

            string trangThai = LayGiaTriHopLeTheoCheck(conn, tran, "PHIEUTHUE", "TrangThai", "Đang thuê", "Dang thue", "Có khách", "Co khach", "Occupied");
            using SqlCommand cmd = new("UPDATE dbo.PHIEUTHUE SET TrangThai = @TrangThai WHERE MaThue = @MaThue", conn, tran);
            cmd.Parameters.AddWithValue("@TrangThai", trangThai);
            cmd.Parameters.AddWithValue("@MaThue", maThue);
            cmd.ExecuteNonQuery();
        }

        private static void CapNhatTrangThaiDatPhongDangThue(SqlConnection conn, SqlTransaction tran, int? maDatPhong)
        {
            if (!maDatPhong.HasValue)
            {
                return;
            }

            string bangDatPhong = LayBangDatPhong(conn, tran);
            if (string.IsNullOrWhiteSpace(bangDatPhong) || !ColumnExists(conn, tran, bangDatPhong, "TrangThai"))
            {
                return;
            }

            string trangThai = LayGiaTriHopLeTheoCheck(conn, tran, bangDatPhong, "TrangThai", "Đã check-in", "Da check-in", "Đang thuê", "Dang thue", "Có khách", "Co khach", "Đã xác nhận", "Da xac nhan");
            using SqlCommand cmd = new("UPDATE dbo." + bangDatPhong + " SET TrangThai = @TrangThai WHERE MaDatPhong = @MaDatPhong", conn, tran);
            cmd.Parameters.AddWithValue("@TrangThai", trangThai);
            cmd.Parameters.AddWithValue("@MaDatPhong", maDatPhong.Value);
            cmd.ExecuteNonQuery();
        }

        private static void GanThamSoDoiPhong(SqlCommand cmd, DoiPhongRequestDTO request)
        {
            cmd.Parameters.AddWithValue("@MaPhongMoi", request.MaPhongMoi);
            cmd.Parameters.AddWithValue("@MaPhongCu", request.MaPhongCu);
            cmd.Parameters.AddWithValue("@MaDatPhong", request.MaDatPhong!.Value);
        }

        private static void GhiNhanGiaHan(SqlConnection conn, SqlTransaction tran, int maThue, DateTime tuNgay, DateTime denNgay, decimal soTien)
        {
            GhiNhanGiaHanBangRieng(conn, tran, maThue, tuNgay, denNgay, soTien);
            if (!ColumnExists(conn, tran, "PHIEUTHUE", "GhiChu"))
            {
                return;
            }

            string marker = $"[GIAHAN] Tu={tuNgay:O};Den={denNgay:O};SoTien={soTien:0}";
            using SqlCommand cmd = new(
                "UPDATE dbo.PHIEUTHUE SET GhiChu = CONCAT(NULLIF(GhiChu, N''), CASE WHEN NULLIF(GhiChu, N'') IS NULL THEN N'' ELSE N' - ' END, @Marker) WHERE MaThue = @MaThue",
                conn,
                tran);
            cmd.Parameters.AddWithValue("@Marker", marker);
            cmd.Parameters.AddWithValue("@MaThue", maThue);
            cmd.ExecuteNonQuery();
        }

        private static void GhiNhanGiaHanBangRieng(SqlConnection conn, SqlTransaction tran, int maThue, DateTime tuNgay, DateTime denNgay, decimal soTien)
        {
            if (!TableExists(conn, tran, "GIAHANPHONG"))
            {
                return;
            }

            List<string> columns = new();
            List<string> values = new();
            void Add(string column, string parameter)
            {
                if (ColumnExists(conn, tran, "GIAHANPHONG", column))
                {
                    columns.Add(column);
                    values.Add(parameter);
                }
            }

            Add("MaThue", "@MaThue");
            Add("TuNgay", "@TuNgay");
            Add("NgayTraCu", "@TuNgay");
            Add("DenNgay", "@DenNgay");
            Add("NgayTraMoi", "@DenNgay");
            Add("SoTien", "@SoTien");
            Add("TienGiaHan", "@SoTien");
            Add("NgayLap", "@NgayLap");
            Add("ThoiDiemLap", "@NgayLap");

            if (!columns.Contains("MaThue") || columns.Count <= 1)
            {
                return;
            }

            using SqlCommand cmd = new("INSERT INTO dbo.GIAHANPHONG (" + string.Join(", ", columns) + ") VALUES (" + string.Join(", ", values) + ")", conn, tran);
            cmd.Parameters.AddWithValue("@MaThue", maThue);
            cmd.Parameters.AddWithValue("@TuNgay", tuNgay);
            cmd.Parameters.AddWithValue("@DenNgay", denNgay);
            cmd.Parameters.AddWithValue("@SoTien", soTien);
            cmd.Parameters.AddWithValue("@NgayLap", DateTime.Now);
            cmd.ExecuteNonQuery();
        }

        private static void GhiNhanDoiPhong(SqlConnection conn, SqlTransaction tran, DoiPhongRequestDTO request, decimal chenhLechTien)
        {
            if (!TableExists(conn, tran, "DOIPHONG"))
            {
                return;
            }

            string reasonColumn = ColumnExists(conn, tran, "DOIPHONG", "GhiChu")
                ? "GhiChu"
                : ColumnExists(conn, tran, "DOIPHONG", "LyDo")
                    ? "LyDo"
                    : string.Empty;
            string insertSql = string.IsNullOrWhiteSpace(reasonColumn)
                ? @"INSERT INTO dbo.DOIPHONG(MaThue, MaPhongCu, MaPhongMoi, ThoiDiemDoi, ChenhLechTien)
                    VALUES(@MaThue, @MaPhongCu, @MaPhongMoi, @ThoiDiemDoi, @ChenhLechTien)"
                : @"INSERT INTO dbo.DOIPHONG(MaThue, MaPhongCu, MaPhongMoi, ThoiDiemDoi, " + reasonColumn + @", ChenhLechTien)
                    VALUES(@MaThue, @MaPhongCu, @MaPhongMoi, @ThoiDiemDoi, @LyDo, @ChenhLechTien)";
            using SqlCommand cmd = new(
                insertSql,
                conn,
                tran);
            cmd.Parameters.AddWithValue("@MaThue", request.MaThue);
            cmd.Parameters.AddWithValue("@MaPhongCu", request.MaPhongCu);
            cmd.Parameters.AddWithValue("@MaPhongMoi", request.MaPhongMoi);
            cmd.Parameters.AddWithValue("@ThoiDiemDoi", request.NgayBatDau);
            cmd.Parameters.AddWithValue("@LyDo", "Đổi phòng theo yêu cầu");
            cmd.Parameters.AddWithValue("@ChenhLechTien", chenhLechTien);
            cmd.ExecuteNonQuery();
        }

        private static decimal TinhTienKhoang(SqlConnection conn, SqlTransaction tran, int maPhong, DateTime start, DateTime end)
        {
            if (end <= start)
            {
                return 0;
            }

            using SqlCommand cmd = new(
                @"SELECT TOP 1 ISNULL(LP.DonGiaGio, 0) AS GiaGio,
                                  ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24)) AS GiaNgay,
                                  ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(LP.DonGiaGio, 0) * 24)) AS GiaDem
                  FROM dbo.PHONG P
                  JOIN dbo.LOAIPHONG LP ON P.MaLoaiPhong = LP.MaLoaiPhong
                  WHERE P.MaPhong = @MaPhong",
                conn,
                tran);
            cmd.Parameters.AddWithValue("@MaPhong", maPhong);
            using SqlDataReader reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return 0;
            }

            decimal giaGio = Convert.ToDecimal(reader["GiaGio"]);
            decimal giaNgay = Convert.ToDecimal(reader["GiaNgay"]);
            decimal giaDem = Convert.ToDecimal(reader["GiaDem"]);
            bool quaDem = end.Date == start.Date.AddDays(1) &&
                           start.TimeOfDay >= TimeSpan.FromHours(21) &&
                           end.TimeOfDay <= TimeSpan.FromHours(8.5);
            if (quaDem)
            {
                return giaDem;
            }

            if (start.Date == end.Date)
            {
                int hours = Math.Max(1, (int)Math.Ceiling((end - start).TotalMinutes / 60.0));
                return Math.Round(hours * giaGio, 0);
            }

            int days = Math.Max(1, (end.Date - start.Date).Days);
            return days * giaNgay;
        }

        private static string LayBangDatPhong(SqlConnection conn, SqlTransaction tran)
        {
            return TableExists(conn, tran, "PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists(conn, tran, "DATPHONG") ? "DATPHONG" : string.Empty;
        }

        private static bool TableExists(SqlConnection conn, SqlTransaction tran, string table)
        {
            using SqlCommand cmd = new("SELECT COUNT(*) FROM sys.tables WHERE name = @Name", conn, tran);
            cmd.Parameters.AddWithValue("@Name", table);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static bool ColumnExists(SqlConnection conn, SqlTransaction tran, string table, string column)
        {
            using SqlCommand cmd = new(
                @"SELECT COUNT(*) FROM sys.tables t JOIN sys.columns c ON t.object_id = c.object_id
                  WHERE t.name = @TableName AND c.name = @ColumnName", conn, tran);
            cmd.Parameters.AddWithValue("@TableName", table);
            cmd.Parameters.AddWithValue("@ColumnName", column);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static string LayGiaTriHopLeTheoCheck(SqlConnection conn, SqlTransaction tran, string table, string column, params string[] candidates)
        {
            List<string> allowed = LayGiaTriTrongCheck(conn, tran, table, column);
            foreach (string candidate in candidates)
            {
                string? exact = allowed.FirstOrDefault(value => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(exact))
                {
                    return exact;
                }

                string normalizedCandidate = BoDau(candidate);
                string? normalized = allowed.FirstOrDefault(value => string.Equals(BoDau(value), normalizedCandidate, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }
            }

            return ChonTrangThaiGanDung(allowed, candidates) ?? candidates.FirstOrDefault() ?? string.Empty;
        }

        private static string? LayGiaTriHopLeNeuCo(SqlConnection conn, SqlTransaction tran, string table, string column, params string[] candidates)
        {
            List<string> allowed = LayGiaTriTrongCheck(conn, tran, table, column);
            if (allowed.Count == 0)
            {
                return candidates.FirstOrDefault();
            }

            foreach (string candidate in candidates)
            {
                string? exact = allowed.FirstOrDefault(value => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(exact))
                {
                    return exact;
                }

                string normalizedCandidate = BoDau(candidate);
                string? normalized = allowed.FirstOrDefault(value => string.Equals(BoDau(value), normalizedCandidate, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }
            }

            return null;
        }

        private static List<string> LayGiaTriTrongCheck(SqlConnection conn, SqlTransaction tran, string table, string column)
        {
            List<string> allowed = new();
            using SqlCommand cmd = new(
                @"SELECT cc.definition
                  FROM sys.check_constraints cc
                  JOIN sys.tables t ON cc.parent_object_id = t.object_id
                  WHERE t.name = @TableName AND cc.definition LIKE N'%' + @ColumnName + N'%'",
                conn,
                tran);
            cmd.Parameters.AddWithValue("@TableName", table);
            cmd.Parameters.AddWithValue("@ColumnName", column);
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

            return allowed;
        }

        private static string? ChonTrangThaiGanDung(List<string> allowed, params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                string c = BoDau(candidate).ToLowerInvariant();
                string? match = allowed.FirstOrDefault(value =>
                {
                    string v = BoDau(value).ToLowerInvariant();
                    return (c.Contains("trong") && v.Contains("trong")) ||
                           ((c.Contains("bao tri") || c.Contains("sua")) && (v.Contains("bao tri") || v.Contains("sua"))) ||
                           ((c.Contains("check-in") || c.Contains("thue") || c.Contains("co khach")) &&
                            (v.Contains("check-in") || v.Contains("thue") || v.Contains("co khach") || v.Contains("xac nhan"))) ||
                           (c.Contains("dat") && v.Contains("dat")) ||
                           (c.Contains("huy") && v.Contains("huy")) ||
                           (c.Contains("tra") && v.Contains("tra"));
                });
                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }
            }

            return null;
        }

        private static string BoDau(string value)
        {
            string normalized = value.Normalize(System.Text.NormalizationForm.FormD);
            return new string(normalized.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray())
                .Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}
