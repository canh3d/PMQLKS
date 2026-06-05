using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DTO;

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
                KiemTraXungDot(conn, tran, request.MaPhong, request.NgayTraCu, request.NgayTraMoi, request.MaThue, request.MaDatPhong);
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

                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
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
                        using SqlCommand cmd = new("UPDATE dbo.CHITIETDATPHONG SET MaPhong = @MaPhongMoi WHERE MaDatPhong = @MaDatPhong AND MaPhong = @MaPhongCu", conn, tran);
                        GanThamSoDoiPhong(cmd, request);
                        cmd.ExecuteNonQuery();
                    }
                }

                string bangDichVu = TableExists(conn, tran, "PHATSINHDICHVU") ? "PHATSINHDICHVU" : TableExists(conn, tran, "CHITIETPHATSINH") ? "CHITIETPHATSINH" : string.Empty;
                if (request.MaThue > 0 && !string.IsNullOrWhiteSpace(bangDichVu) && ColumnExists(conn, tran, bangDichVu, "MaPhong") && ColumnExists(conn, tran, bangDichVu, "MaThue"))
                {
                    using SqlCommand cmd = new("UPDATE dbo." + bangDichVu + " SET MaPhong = @MaPhongMoi WHERE MaThue = @MaThue AND MaPhong = @MaPhongCu", conn, tran);
                    cmd.Parameters.AddWithValue("@MaPhongMoi", request.MaPhongMoi);
                    cmd.Parameters.AddWithValue("@MaPhongCu", request.MaPhongCu);
                    cmd.Parameters.AddWithValue("@MaThue", request.MaThue);
                    cmd.ExecuteNonQuery();
                }

                if (request.MaThue > 0)
                {
                    CapNhatTrangThaiPhong(conn, tran, request.MaPhongCu, "Chưa dọn dẹp", "Chua don dep");
                    CapNhatTrangThaiPhong(conn, tran, request.MaPhongMoi, "Đang thuê", "Dang thue", "Có khách", "Co khach");
                }
                else
                {
                    CapNhatTrangThaiPhong(conn, tran, request.MaPhongCu, "Phòng trống", "Phong trong");
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
            if (!normalized.Contains("trong", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Phòng đích không còn ở trạng thái phòng trống.");
            }
        }

        private static void KiemTraXungDot(SqlConnection conn, SqlTransaction tran, int maPhong, DateTime tuNgay, DateTime denNgay, int maThueBoQua, int? maDatPhongBoQua)
        {
            if (denNgay <= tuNgay)
            {
                return;
            }

            using (SqlCommand cmd = new(
                @"SELECT COUNT(*)
                  FROM dbo.PHIEUTHUE
                  WHERE MaPhong = @MaPhong
                    AND MaThue <> @MaThue
                    AND TrangThai NOT IN (N'Đã trả phòng', N'Da tra phong', N'Đã trả', N'Da tra', N'Đã thanh toán', N'Da thanh toan')
                    AND NgayNhan < @DenNgay
                    AND ISNULL(NgayTraPhong, NgayTraDuKien) > @TuNgay", conn, tran))
            {
                cmd.Parameters.AddWithValue("@MaPhong", maPhong);
                cmd.Parameters.AddWithValue("@MaThue", maThueBoQua);
                cmd.Parameters.AddWithValue("@TuNgay", tuNgay);
                cmd.Parameters.AddWithValue("@DenNgay", denNgay);
                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                {
                    throw new InvalidOperationException("Phòng đã có phiếu thuê trùng thời gian.");
                }
            }

            string bangDatPhong = LayBangDatPhong(conn, tran);
            if (string.IsNullOrWhiteSpace(bangDatPhong))
            {
                return;
            }

            string ngayNhan = ColumnExists(conn, tran, bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong";
            string ngayTra = ColumnExists(conn, tran, bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong";
            string maPhongExpr;
            string join = string.Empty;
            if (TableExists(conn, tran, "CHITIETDATPHONG"))
            {
                maPhongExpr = "CT.MaPhong";
                join = " JOIN dbo.CHITIETDATPHONG CT ON DP.MaDatPhong = CT.MaDatPhong";
            }
            else if (ColumnExists(conn, tran, bangDatPhong, "MaPhong"))
            {
                maPhongExpr = "DP.MaPhong";
            }
            else
            {
                return;
            }

            using SqlCommand datPhongCmd = new(
                @"SELECT COUNT(*)
                  FROM dbo." + bangDatPhong + @" DP" + join + @"
                  WHERE " + maPhongExpr + @" = @MaPhong
                    AND (@MaDatPhong IS NULL OR DP.MaDatPhong <> @MaDatPhong)
                    AND DP.TrangThai NOT IN (N'Đã hủy', N'Da huy', N'Hủy', N'Huy', N'Đã trả phòng', N'Da tra phong')
                    AND DP." + ngayNhan + @" < @DenNgay
                    AND DP." + ngayTra + @" > @TuNgay", conn, tran);
            datPhongCmd.Parameters.AddWithValue("@MaPhong", maPhong);
            datPhongCmd.Parameters.AddWithValue("@MaDatPhong", maDatPhongBoQua.HasValue ? maDatPhongBoQua.Value : DBNull.Value);
            datPhongCmd.Parameters.AddWithValue("@TuNgay", tuNgay);
            datPhongCmd.Parameters.AddWithValue("@DenNgay", denNgay);
            if (Convert.ToInt32(datPhongCmd.ExecuteScalar()) > 0)
            {
                throw new InvalidOperationException("Phòng đã có lịch đặt trùng thời gian.");
            }
        }

        private static void CapNhatTrangThaiPhong(SqlConnection conn, SqlTransaction tran, int maPhong, params string[] candidates)
        {
            string value = LayGiaTriHopLeTheoCheck(conn, tran, "PHONG", "TrangThai", candidates);
            using SqlCommand cmd = new("UPDATE dbo.PHONG SET TrangThai = @TrangThai WHERE MaPhong = @MaPhong", conn, tran);
            cmd.Parameters.AddWithValue("@TrangThai", value);
            cmd.Parameters.AddWithValue("@MaPhong", maPhong);
            cmd.ExecuteNonQuery();
        }

        private static void GanThamSoDoiPhong(SqlCommand cmd, DoiPhongRequestDTO request)
        {
            cmd.Parameters.AddWithValue("@MaPhongMoi", request.MaPhongMoi);
            cmd.Parameters.AddWithValue("@MaPhongCu", request.MaPhongCu);
            cmd.Parameters.AddWithValue("@MaDatPhong", request.MaDatPhong!.Value);
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
            using SqlCommand cmd = new(
                @"SELECT cc.definition
                  FROM sys.check_constraints cc
                  JOIN sys.tables t ON cc.parent_object_id = t.object_id
                  WHERE t.name = @TableName AND cc.definition LIKE N'%' + @ColumnName + N'%'", conn, tran);
            cmd.Parameters.AddWithValue("@TableName", table);
            cmd.Parameters.AddWithValue("@ColumnName", column);
            string definition = cmd.ExecuteScalar()?.ToString() ?? string.Empty;
            return candidates.FirstOrDefault(value => definition.Contains(value, StringComparison.OrdinalIgnoreCase)) ?? candidates[0];
        }

        private static string BoDau(string value)
        {
            string normalized = value.Normalize(System.Text.NormalizationForm.FormD);
            return new string(normalized.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray())
                .Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}
