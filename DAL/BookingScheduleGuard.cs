using System.Data;
using Microsoft.Data.SqlClient;

namespace QLKS_AnPhu.DAL
{
    internal static class BookingScheduleGuard
    {
        public static void EnsureRoomAvailable(
            SqlConnection conn,
            SqlTransaction tran,
            int maPhong,
            DateTime ngayNhanMoi,
            DateTime ngayTraMoi,
            int? maThueBoQua = null,
            int? maDatPhongBoQua = null)
        {
            ValidateRange(ngayNhanMoi, ngayTraMoi);
            EnsureNoRentalConflict(conn, tran, maPhong, ngayNhanMoi, ngayTraMoi, maThueBoQua);
            EnsureNoBookingConflict(conn, tran, maPhong, ngayNhanMoi, ngayTraMoi, maDatPhongBoQua);
        }

        public static void EnsureRoomAvailable(
            SqlConnection conn,
            SqlTransaction tran,
            int maPhong,
            DateTime ngayNhanMoi,
            DateTime ngayTraMoi,
            int? maThueBoQua,
            IReadOnlyCollection<int> maDatPhongBoQua)
        {
            ValidateRange(ngayNhanMoi, ngayTraMoi);
            EnsureNoRentalConflict(conn, tran, maPhong, ngayNhanMoi, ngayTraMoi, maThueBoQua);
            EnsureNoBookingConflict(conn, tran, maPhong, ngayNhanMoi, ngayTraMoi, maDatPhongBoQua);
        }

        private static void ValidateRange(DateTime ngayNhanMoi, DateTime ngayTraMoi)
        {
            if (ngayTraMoi <= ngayNhanMoi)
            {
                throw new InvalidOperationException("Ngay tra phai lon hon ngay nhan.");
            }
        }

        private static void EnsureNoRentalConflict(
            SqlConnection conn,
            SqlTransaction tran,
            int maPhong,
            DateTime ngayNhanMoi,
            DateTime ngayTraMoi,
            int? maThueBoQua)
        {
            bool coChiTietDatPhong = TableExists(conn, tran, "CHITIETDATPHONG") &&
                                      ColumnExists(conn, tran, "PHIEUTHUE", "MaDatPhong") &&
                                      ColumnExists(conn, tran, "CHITIETDATPHONG", "MaDatPhong") &&
                                      ColumnExists(conn, tran, "CHITIETDATPHONG", "MaPhong");
            string joinChiTiet = coChiTietDatPhong
                ? "LEFT JOIN dbo.CHITIETDATPHONG CT ON CT.MaDatPhong = PT.MaDatPhong"
                : string.Empty;
            string phongExpr = coChiTietDatPhong ? "ISNULL(CT.MaPhong, PT.MaPhong)" : "PT.MaPhong";
            string ngayNhanChiTiet = coChiTietDatPhong
                ? GetFirstExistingColumn(conn, tran, "CHITIETDATPHONG", "NgayNhanDuKien", "NgayNhanPhong", "NgayNhan")
                : string.Empty;
            string ngayTraChiTiet = coChiTietDatPhong
                ? GetFirstExistingColumn(conn, tran, "CHITIETDATPHONG", "NgayTraDuKien", "NgayTraPhong", "NgayTra")
                : string.Empty;
            string ngayNhanExpr = string.IsNullOrWhiteSpace(ngayNhanChiTiet)
                ? "PT.NgayNhan"
                : "ISNULL(CT." + ngayNhanChiTiet + ", PT.NgayNhan)";
            string ngayTraPhieuExpr = ColumnExists(conn, tran, "PHIEUTHUE", "NgayTraPhong")
                ? "ISNULL(PT.NgayTraPhong, PT.NgayTraDuKien)"
                : "PT.NgayTraDuKien";
            string ngayTraExpr = string.IsNullOrWhiteSpace(ngayTraChiTiet)
                ? ngayTraPhieuExpr
                : "ISNULL(CT." + ngayTraChiTiet + ", " + ngayTraPhieuExpr + ")";

            string sql = @"
SELECT COUNT(*)
FROM dbo.PHIEUTHUE PT
" + joinChiTiet + @"
WHERE " + phongExpr + @" = @MaPhong
  AND (@MaThueBoQua IS NULL OR PT.MaThue <> @MaThueBoQua)
  AND PT.TrangThai IN (N'Dang thue', N'Dang thuê', N'Đang thuê', N'Co khach', N'Có khách', N'Occupied')
  AND " + ngayNhanExpr + @" < @NgayTraMoi
  AND " + ngayTraExpr + @" > @NgayNhanMoi";

            using SqlCommand cmd = new(sql, conn, tran);
            cmd.Parameters.Add("@MaPhong", SqlDbType.Int).Value = maPhong;
            cmd.Parameters.Add("@MaThueBoQua", SqlDbType.Int).Value = maThueBoQua.HasValue ? maThueBoQua.Value : DBNull.Value;
            cmd.Parameters.Add("@NgayNhanMoi", SqlDbType.DateTime2).Value = ngayNhanMoi;
            cmd.Parameters.Add("@NgayTraMoi", SqlDbType.DateTime2).Value = ngayTraMoi;

            if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
            {
                throw new InvalidOperationException("Phong da co phieu thue trung thoi gian.");
            }
        }

        private static void EnsureNoBookingConflict(
            SqlConnection conn,
            SqlTransaction tran,
            int maPhong,
            DateTime ngayNhanMoi,
            DateTime ngayTraMoi,
            int? maDatPhongBoQua)
        {
            using SqlCommand cmd = new(
                BuildBookingConflictSql(conn, tran, "AND (@MaDatPhongBoQua IS NULL OR DP.MaDatPhong <> @MaDatPhongBoQua)"),
                conn,
                tran);
            cmd.Parameters.Add("@MaPhong", SqlDbType.Int).Value = maPhong;
            cmd.Parameters.Add("@MaDatPhongBoQua", SqlDbType.Int).Value = maDatPhongBoQua.HasValue ? maDatPhongBoQua.Value : DBNull.Value;
            cmd.Parameters.Add("@NgayNhanMoi", SqlDbType.DateTime2).Value = ngayNhanMoi;
            cmd.Parameters.Add("@NgayTraMoi", SqlDbType.DateTime2).Value = ngayTraMoi;

            if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
            {
                throw new InvalidOperationException("Phong da co dat phong trung thoi gian.");
            }
        }

        private static void EnsureNoBookingConflict(
            SqlConnection conn,
            SqlTransaction tran,
            int maPhong,
            DateTime ngayNhanMoi,
            DateTime ngayTraMoi,
            IReadOnlyCollection<int> maDatPhongBoQua)
        {
            List<string> ignoreParameters = new();
            for (int i = 0; i < maDatPhongBoQua.Count; i++)
            {
                ignoreParameters.Add("@IgnoreMaDatPhong" + i);
            }

            string ignoreClause = ignoreParameters.Count == 0
                ? string.Empty
                : " AND DP.MaDatPhong NOT IN (" + string.Join(", ", ignoreParameters) + ")";

            using SqlCommand cmd = new(BuildBookingConflictSql(conn, tran, ignoreClause), conn, tran);
            cmd.Parameters.Add("@MaPhong", SqlDbType.Int).Value = maPhong;
            cmd.Parameters.Add("@NgayNhanMoi", SqlDbType.DateTime2).Value = ngayNhanMoi;
            cmd.Parameters.Add("@NgayTraMoi", SqlDbType.DateTime2).Value = ngayTraMoi;

            int index = 0;
            foreach (int maDatPhong in maDatPhongBoQua)
            {
                cmd.Parameters.Add("@IgnoreMaDatPhong" + index, SqlDbType.Int).Value = maDatPhong;
                index++;
            }

            if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
            {
                throw new InvalidOperationException("Phong da co dat phong trung thoi gian.");
            }
        }

        private static string BuildBookingConflictSql(SqlConnection conn, SqlTransaction tran, string ignoreClause)
        {
            bool coChiTietDatPhong = TableExists(conn, tran, "CHITIETDATPHONG") &&
                                      ColumnExists(conn, tran, "CHITIETDATPHONG", "MaDatPhong") &&
                                      ColumnExists(conn, tran, "CHITIETDATPHONG", "MaPhong");
            string joinChiTiet = coChiTietDatPhong
                ? "LEFT JOIN dbo.CHITIETDATPHONG CT ON CT.MaDatPhong = DP.MaDatPhong"
                : string.Empty;
            string phongExpr = coChiTietDatPhong ? "ISNULL(CT.MaPhong, DP.MaPhong)" : "DP.MaPhong";
            string ngayNhanDatPhong = GetFirstExistingColumn(conn, tran, "DATPHONG", "NgayNhanDuKien", "NgayNhanPhong", "NgayNhan");
            string ngayTraDatPhong = GetFirstExistingColumn(conn, tran, "DATPHONG", "NgayTraDuKien", "NgayTraPhong", "NgayTra");
            string ngayNhanChiTiet = coChiTietDatPhong
                ? GetFirstExistingColumn(conn, tran, "CHITIETDATPHONG", "NgayNhanDuKien", "NgayNhanPhong", "NgayNhan")
                : string.Empty;
            string ngayTraChiTiet = coChiTietDatPhong
                ? GetFirstExistingColumn(conn, tran, "CHITIETDATPHONG", "NgayTraDuKien", "NgayTraPhong", "NgayTra")
                : string.Empty;

            if (string.IsNullOrWhiteSpace(ngayNhanDatPhong) || string.IsNullOrWhiteSpace(ngayTraDatPhong))
            {
                throw new InvalidOperationException("Khong tim thay cot ngay nhan/tra trong bang dat phong.");
            }

            string ngayNhanExpr = string.IsNullOrWhiteSpace(ngayNhanChiTiet)
                ? "DP." + ngayNhanDatPhong
                : "ISNULL(CT." + ngayNhanChiTiet + ", DP." + ngayNhanDatPhong + ")";
            string ngayTraExpr = string.IsNullOrWhiteSpace(ngayTraChiTiet)
                ? "DP." + ngayTraDatPhong
                : "ISNULL(CT." + ngayTraChiTiet + ", DP." + ngayTraDatPhong + ")";

            return @"
SELECT COUNT(*)
FROM dbo.DATPHONG DP
" + joinChiTiet + @"
WHERE " + phongExpr + @" = @MaPhong
  " + ignoreClause + @"
  AND DP.TrangThai IN (N'Da dat', N'Đã đặt', N'Da check-in', N'Đã check-in', N'Da xac nhan', N'Đã xác nhận')
  AND " + ngayNhanExpr + @" < @NgayTraMoi
  AND " + ngayTraExpr + @" > @NgayNhanMoi";
        }

        private static string GetFirstExistingColumn(SqlConnection conn, SqlTransaction tran, string tableName, params string[] candidates)
        {
            foreach (string column in candidates)
            {
                if (ColumnExists(conn, tran, tableName, column))
                {
                    return column;
                }
            }

            return string.Empty;
        }

        private static bool TableExists(SqlConnection conn, SqlTransaction tran, string tableName)
        {
            using SqlCommand cmd = new("SELECT COUNT(*) FROM sys.tables WHERE name = @Name", conn, tran);
            cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 128).Value = tableName;
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static bool ColumnExists(SqlConnection conn, SqlTransaction tran, string tableName, string columnName)
        {
            using SqlCommand cmd = new(@"
SELECT COUNT(*)
FROM sys.tables t
JOIN sys.columns c ON t.object_id = c.object_id
WHERE t.name = @TableName AND c.name = @ColumnName", conn, tran);
            cmd.Parameters.Add("@TableName", SqlDbType.NVarChar, 128).Value = tableName;
            cmd.Parameters.Add("@ColumnName", SqlDbType.NVarChar, 128).Value = columnName;
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
    }
}
