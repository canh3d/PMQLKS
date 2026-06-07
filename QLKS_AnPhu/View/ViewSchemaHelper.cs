using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DAL;

namespace QLKS_AnPhu.View
{
    internal static class ViewSchemaHelper
    {
        private static readonly ConcurrentDictionary<string, bool> TableCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, bool> ColumnCache = new(StringComparer.OrdinalIgnoreCase);

        public static bool TableExists(string tableName)
        {
            return TableCache.GetOrAdd(tableName, name =>
            {
                object? result = ConnectDB.ExecuteScalar(
                    "SELECT COUNT(*) FROM sys.tables WHERE name = @Name",
                    new SqlParameter("@Name", name));
                return Convert.ToInt32(result) > 0;
            });
        }

        public static bool ColumnExists(string tableName, string columnName)
        {
            string key = tableName + "." + columnName;
            return ColumnCache.GetOrAdd(key, _ =>
            {
                object? result = ConnectDB.ExecuteScalar(
                    @"SELECT COUNT(*)
                      FROM sys.tables t
                      JOIN sys.columns c ON t.object_id = c.object_id
                      WHERE t.name = @TableName AND c.name = @ColumnName",
                    new SqlParameter("@TableName", tableName),
                    new SqlParameter("@ColumnName", columnName));
                return Convert.ToInt32(result) > 0;
            });
        }

        public static string GetFirstExistingColumn(string tableName, params string[] candidates)
        {
            return candidates.FirstOrDefault(column => ColumnExists(tableName, column)) ?? string.Empty;
        }

        public static string TenPhongSql(string alias)
        {
            if (ColumnExists("PHONG", "TenPhong")) return alias + ".TenPhong";
            if (ColumnExists("PHONG", "SoPhong")) return alias + ".SoPhong";
            if (ColumnExists("PHONG", "MaSoPhong")) return alias + ".MaSoPhong";
            return "N'P' + CAST(" + alias + ".MaPhong AS nvarchar(20))";
        }

        public static string DichVuTheoLoaiHoaDonFilter(string tableAlias, string loaiThanhToan)
        {
            string prefix = string.IsNullOrWhiteSpace(tableAlias) ? string.Empty : tableAlias + ".";
            return loaiThanhToan == "PHATSINH"
                ? " AND ISNULL(" + prefix + "GhiChu, N'') NOT LIKE N'%[DICHVU_CHECKIN]%'"
                : " AND ISNULL(" + prefix + "GhiChu, N'') LIKE N'%[DICHVU_CHECKIN]%'";
        }

        public static decimal DocTienPhongDaChot(string ghiChu)
        {
            if (string.IsNullOrWhiteSpace(ghiChu))
            {
                return 0;
            }

            Match match = Regex.Match(
                ghiChu,
                @"(?:TongTienPhong|TienPhong)\s*=\s*([0-9][0-9.,]*)",
                RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return 0;
            }

            string raw = match.Groups[1].Value.Replace(",", string.Empty).Replace(".", string.Empty);
            return decimal.TryParse(raw, out decimal value) ? value : 0;
        }
    }
}
