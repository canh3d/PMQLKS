using System.Data;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.DAL
{
    public class KhachHangDAL
    {
        private static readonly string[] CandidateTables =
        {
            "KhachHang",
            "Khach_Hang",
            "KHACHHANG",
            "Khach",
            "KHACH",
            "Customer",
            "Customers"
        };

        public List<KhachHangDTO> LayDanhSach()
        {
            TableMap map = GetTargetMap();
            DataTable data = ConnectDB.GetData($"SELECT * FROM [{map.Schema}].[{map.Name}]");
            List<KhachHangDTO> result = new();

            foreach (DataRow row in data.Rows)
            {
                result.Add(MapRow(row, map));
            }

            return result.OrderBy(item => item.Ma).ThenBy(item => item.HoTen).ToList();
        }

        public int Them(KhachHangDTO item)
        {
            TableMap map = GetTargetMap();
            Dictionary<string, object?> values = BuildColumnValues(map, item, includeKey: false);

            if (!map.IdentityColumns.Contains(map.KeyColumn) && !values.ContainsKey(map.KeyColumn))
            {
                int nextId = GetNextId(map);
                values[map.KeyColumn] = nextId;
                item.Ma = nextId;
            }

            if (values.Count == 0)
            {
                throw new InvalidOperationException("Không tìm thấy cột phù hợp để thêm dữ liệu khách hàng.");
            }

            string columns = string.Join(", ", values.Keys.Select(Quote));
            string parameters = string.Join(", ", values.Keys.Select(ToParameterName));
            string sql = $"INSERT INTO [{map.Schema}].[{map.Name}] ({columns}) VALUES ({parameters})";

            return ConnectDB.ExecuteNonQuery(sql, values.Select(pair => new SqlParameter(ToParameterName(pair.Key), pair.Value ?? DBNull.Value)).ToArray());
        }

        public int Sua(KhachHangDTO item)
        {
            TableMap map = GetMapForItem(item);
            string keyColumn = GetKeyColumn(map, item);
            Dictionary<string, object?> values = BuildColumnValues(map, item, includeKey: false);
            values.Remove(keyColumn);

            if (values.Count == 0)
            {
                throw new InvalidOperationException("Không tìm thấy cột phù hợp để sửa dữ liệu khách hàng.");
            }

            string setClause = string.Join(", ", values.Keys.Select(column => $"{Quote(column)} = {ToParameterName(column)}"));
            string sql = $"UPDATE [{map.Schema}].[{map.Name}] SET {setClause} WHERE {Quote(keyColumn)} = @KeyValue";

            List<SqlParameter> parameters = values
                .Select(pair => new SqlParameter(ToParameterName(pair.Key), pair.Value ?? DBNull.Value))
                .ToList();
            parameters.Add(new SqlParameter("@KeyValue", item.Ma));

            return ConnectDB.ExecuteNonQuery(sql, parameters.ToArray());
        }

        public int Xoa(KhachHangDTO item)
        {
            TableMap map = GetMapForItem(item);
            string keyColumn = GetKeyColumn(map, item);
            string sql = $"DELETE FROM [{map.Schema}].[{map.Name}] WHERE {Quote(keyColumn)} = @KeyValue";

            try
            {
                return ConnectDB.ExecuteNonQuery(sql, new SqlParameter("@KeyValue", item.Ma));
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                throw new InvalidOperationException(
                    "Không thể xóa khách hàng vì đã có dữ liệu đặt phòng, phiếu thuê hoặc hóa đơn liên quan. " +
                    "Hãy giữ khách hàng này để bảo toàn lịch sử giao dịch.",
                    ex);
            }
        }

        public int XoaBatBuoc(KhachHangDTO item)
        {
            TableMap map = GetMapForItem(item);
            string keyColumn = GetKeyColumn(map, item);

            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();

            try
            {
                int affected = 0;

                affected += XoaChiTietHoaDon(conn, tran, item.Ma);
                affected += XoaChiTietThanhToan(conn, tran, item.Ma);
                affected += XoaDichVuPhatSinh(conn, tran, item.Ma);
                affected += XoaTheoMaThue(conn, tran, "CHITIETPHUTHU", item.Ma);
                affected += XoaTheoMaThue(conn, tran, "DOIPHONG", item.Ma);
                affected += XoaHoaDon(conn, tran, item.Ma);
                affected += XoaPhieuThue(conn, tran, item.Ma);
                affected += XoaChiTietDatPhong(conn, tran, item.Ma);
                affected += XoaDatPhong(conn, tran, item.Ma);

                string sql = $"DELETE FROM [{map.Schema}].[{map.Name}] WHERE {Quote(keyColumn)} = @MaKH";
                affected += ExecuteNonQuery(conn, tran, sql, new SqlParameter("@MaKH", item.Ma));

                tran.Commit();
                return affected;
            }
            catch (Exception ex)
            {
                try
                {
                    tran.Rollback();
                }
                catch
                {
                }

                throw new InvalidOperationException("Không xóa bắt buộc được khách hàng và dữ liệu liên quan: " + ex.Message, ex);
            }
        }

        private static int XoaChiTietHoaDon(SqlConnection conn, SqlTransaction tran, int maKhachHang)
        {
            if (!TableExists(conn, tran, "HOADON") || !TableExists(conn, tran, "CHITIETHOADON"))
            {
                return 0;
            }

            string hoaDonKey = GetFirstExistingColumn(conn, tran, "HOADON", "MaHoaDon", "MaHD", "IDHoaDon", "HoaDonID", "IdHoaDon", "ID", "Ma");
            string chiTietHoaDonKey = GetFirstExistingColumn(conn, tran, "CHITIETHOADON", "MaHoaDon", "MaHD", "IDHoaDon", "HoaDonID");

            if (string.IsNullOrWhiteSpace(hoaDonKey) || string.IsNullOrWhiteSpace(chiTietHoaDonKey))
            {
                return 0;
            }

            string where = TaoDieuKienHoaDonKhachHang(conn, tran, "HD");
            if (string.IsNullOrWhiteSpace(where))
            {
                return 0;
            }

            string sql = $@"DELETE CT
FROM dbo.CHITIETHOADON CT
WHERE CT.{Quote(chiTietHoaDonKey)} IN (
    SELECT HD.{Quote(hoaDonKey)}
    FROM dbo.HOADON HD
    WHERE {where}
)";

            return ExecuteNonQuery(conn, tran, sql, new SqlParameter("@MaKH", maKhachHang));
        }

        private static int XoaChiTietThanhToan(SqlConnection conn, SqlTransaction tran, int maKhachHang)
        {
            if (!TableExists(conn, tran, "CHITIETTHANHTOAN"))
            {
                return 0;
            }

            List<string> conditions = new();

            if (TableExists(conn, tran, "HOADON"))
            {
                string thanhToanHoaDonKey = GetFirstExistingColumn(conn, tran, "CHITIETTHANHTOAN", "MaHoaDon", "MaHD", "IDHoaDon", "HoaDonID");
                string hoaDonKey = GetFirstExistingColumn(conn, tran, "HOADON", "MaHoaDon", "MaHD", "IDHoaDon", "HoaDonID", "IdHoaDon", "ID", "Ma");
                string hoaDonWhere = TaoDieuKienHoaDonKhachHang(conn, tran, "HD");

                if (!string.IsNullOrWhiteSpace(thanhToanHoaDonKey) &&
                    !string.IsNullOrWhiteSpace(hoaDonKey) &&
                    !string.IsNullOrWhiteSpace(hoaDonWhere))
                {
                    conditions.Add($@"CT.{Quote(thanhToanHoaDonKey)} IN (
    SELECT HD.{Quote(hoaDonKey)} FROM dbo.HOADON HD WHERE {hoaDonWhere}
)");
                }
            }

            if (ColumnExists(conn, tran, "CHITIETTHANHTOAN", "MaThue") && TableExists(conn, tran, "PHIEUTHUE"))
            {
                conditions.Add("CT.MaThue IN (" + TaoTruyVanMaThueKhachHang(conn, tran) + ")");
            }

            if (ColumnExists(conn, tran, "CHITIETTHANHTOAN", "MaDatPhong") && TableExists(conn, tran, "DATPHONG"))
            {
                conditions.Add("CT.MaDatPhong IN (SELECT DP.MaDatPhong FROM dbo.DATPHONG DP WHERE DP.MaKH = @MaKH)");
            }

            if (conditions.Count == 0)
            {
                return 0;
            }

            string sql = "DELETE CT FROM dbo.CHITIETTHANHTOAN CT WHERE " + string.Join(" OR ", conditions);
            return ExecuteNonQuery(conn, tran, sql, new SqlParameter("@MaKH", maKhachHang));
        }

        private static int XoaDichVuPhatSinh(SqlConnection conn, SqlTransaction tran, int maKhachHang)
        {
            int affected = 0;

            foreach (string table in new[] { "PHATSINHDICHVU", "CHITIETPHATSINH" })
            {
                if (!TableExists(conn, tran, table))
                {
                    continue;
                }

                List<string> conditions = new();
                if (ColumnExists(conn, tran, table, "MaThue") && TableExists(conn, tran, "PHIEUTHUE"))
                {
                    conditions.Add("MaThue IN (" + TaoTruyVanMaThueKhachHang(conn, tran) + ")");
                }

                if (ColumnExists(conn, tran, table, "MaDatPhong") && TableExists(conn, tran, "DATPHONG"))
                {
                    conditions.Add("MaDatPhong IN (SELECT DP.MaDatPhong FROM dbo.DATPHONG DP WHERE DP.MaKH = @MaKH)");
                }

                if (conditions.Count > 0)
                {
                    affected += ExecuteNonQuery(
                        conn,
                        tran,
                        "DELETE FROM dbo." + Quote(table) + " WHERE " + string.Join(" OR ", conditions),
                        new SqlParameter("@MaKH", maKhachHang));
                }
            }

            return affected;
        }

        private static int XoaTheoMaThue(SqlConnection conn, SqlTransaction tran, string table, int maKhachHang)
        {
            if (!TableExists(conn, tran, table) || !ColumnExists(conn, tran, table, "MaThue") || !TableExists(conn, tran, "PHIEUTHUE"))
            {
                return 0;
            }

            string sql = "DELETE FROM dbo." + Quote(table) + " WHERE MaThue IN (" + TaoTruyVanMaThueKhachHang(conn, tran) + ")";
            return ExecuteNonQuery(conn, tran, sql, new SqlParameter("@MaKH", maKhachHang));
        }

        private static int XoaHoaDon(SqlConnection conn, SqlTransaction tran, int maKhachHang)
        {
            if (!TableExists(conn, tran, "HOADON"))
            {
                return 0;
            }

            string where = TaoDieuKienHoaDonKhachHang(conn, tran, string.Empty);
            if (string.IsNullOrWhiteSpace(where))
            {
                return 0;
            }

            return ExecuteNonQuery(conn, tran, "DELETE FROM dbo.HOADON WHERE " + where, new SqlParameter("@MaKH", maKhachHang));
        }

        private static int XoaPhieuThue(SqlConnection conn, SqlTransaction tran, int maKhachHang)
        {
            if (!TableExists(conn, tran, "PHIEUTHUE"))
            {
                return 0;
            }

            List<string> conditions = new();
            if (ColumnExists(conn, tran, "PHIEUTHUE", "MaKH"))
            {
                conditions.Add("MaKH = @MaKH");
            }

            if (ColumnExists(conn, tran, "PHIEUTHUE", "MaDatPhong") && TableExists(conn, tran, "DATPHONG"))
            {
                conditions.Add("MaDatPhong IN (SELECT DP.MaDatPhong FROM dbo.DATPHONG DP WHERE DP.MaKH = @MaKH)");
            }

            if (conditions.Count == 0)
            {
                return 0;
            }

            return ExecuteNonQuery(conn, tran, "DELETE FROM dbo.PHIEUTHUE WHERE " + string.Join(" OR ", conditions), new SqlParameter("@MaKH", maKhachHang));
        }

        private static int XoaChiTietDatPhong(SqlConnection conn, SqlTransaction tran, int maKhachHang)
        {
            if (!TableExists(conn, tran, "CHITIETDATPHONG") || !ColumnExists(conn, tran, "CHITIETDATPHONG", "MaDatPhong") || !TableExists(conn, tran, "DATPHONG"))
            {
                return 0;
            }

            return ExecuteNonQuery(
                conn,
                tran,
                "DELETE FROM dbo.CHITIETDATPHONG WHERE MaDatPhong IN (SELECT DP.MaDatPhong FROM dbo.DATPHONG DP WHERE DP.MaKH = @MaKH)",
                new SqlParameter("@MaKH", maKhachHang));
        }

        private static int XoaDatPhong(SqlConnection conn, SqlTransaction tran, int maKhachHang)
        {
            if (!TableExists(conn, tran, "DATPHONG") || !ColumnExists(conn, tran, "DATPHONG", "MaKH"))
            {
                return 0;
            }

            return ExecuteNonQuery(conn, tran, "DELETE FROM dbo.DATPHONG WHERE MaKH = @MaKH", new SqlParameter("@MaKH", maKhachHang));
        }

        private static string TaoTruyVanMaThueKhachHang(SqlConnection conn, SqlTransaction tran)
        {
            List<string> conditions = new();

            if (ColumnExists(conn, tran, "PHIEUTHUE", "MaKH"))
            {
                conditions.Add("PT.MaKH = @MaKH");
            }

            if (ColumnExists(conn, tran, "PHIEUTHUE", "MaDatPhong") && TableExists(conn, tran, "DATPHONG"))
            {
                conditions.Add("PT.MaDatPhong IN (SELECT DP.MaDatPhong FROM dbo.DATPHONG DP WHERE DP.MaKH = @MaKH)");
            }

            if (conditions.Count == 0)
            {
                return "SELECT PT.MaThue FROM dbo.PHIEUTHUE PT WHERE 1 = 0";
            }

            return "SELECT PT.MaThue FROM dbo.PHIEUTHUE PT WHERE " + string.Join(" OR ", conditions);
        }

        private static string TaoDieuKienHoaDonKhachHang(SqlConnection conn, SqlTransaction tran, string alias)
        {
            List<string> conditions = new();
            string prefix = string.IsNullOrWhiteSpace(alias) ? string.Empty : alias + ".";

            if (ColumnExists(conn, tran, "HOADON", "MaKH"))
            {
                conditions.Add(prefix + "MaKH = @MaKH");
            }

            if (ColumnExists(conn, tran, "HOADON", "MaThue") && TableExists(conn, tran, "PHIEUTHUE"))
            {
                conditions.Add(prefix + "MaThue IN (" + TaoTruyVanMaThueKhachHang(conn, tran) + ")");
            }

            return string.Join(" OR ", conditions);
        }

        private static bool TableExists(SqlConnection conn, SqlTransaction tran, string table)
        {
            using SqlCommand cmd = new("SELECT CASE WHEN OBJECT_ID(N'dbo." + table.Replace("'", "''") + "', N'U') IS NULL THEN 0 ELSE 1 END", conn, tran);
            return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
        }

        private static bool ColumnExists(SqlConnection conn, SqlTransaction tran, string table, string column)
        {
            using SqlCommand cmd = new(
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName",
                conn,
                tran);
            cmd.Parameters.AddWithValue("@TableName", table);
            cmd.Parameters.AddWithValue("@ColumnName", column);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static string GetFirstExistingColumn(SqlConnection conn, SqlTransaction tran, string table, params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (ColumnExists(conn, tran, table, candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static int ExecuteNonQuery(SqlConnection conn, SqlTransaction tran, string sql, params SqlParameter[] parameters)
        {
            using SqlCommand cmd = new(sql, conn, tran);
            if (parameters.Length > 0)
            {
                cmd.Parameters.AddRange(parameters);
            }

            return cmd.ExecuteNonQuery();
        }

        private static KhachHangDTO MapRow(DataRow row, TableMap map)
        {
            return new KhachHangDTO
            {
                Ma = GetInt(row, "MaKhachHang", "MaKH", "KhachHangID", "CustomerID", "MaKhach", "Ma", "ID"),
                HoTen = GetString(row, "HoTen", "TenKhachHang", "TenKH", "HoVaTen", "TenKhach", "Ten", "FullName", "Name"),
                SDT = GetString(row, "SDT", "SoDienThoai", "DienThoai", "Phone", "PhoneNumber"),
                CCCD = GetString(row, "CCCD", "CMND", "CanCuoc", "SoGiayTo", "IdentityNo"),
                GioiTinh = GetStringOrDefault(row, "Nam", "GioiTinh", "Gender"),
                NgaySinh = GetDate(row, "NgaySinh", "DateOfBirth", "DOB"),
                DiaChi = GetString(row, "DiaChi", "Address"),
                LoaiKhach = GetStringOrDefault(row, "Thường", "LoaiKhach", "LoaiKH", "HangKhachHang", "NhomKhachHang", "CustomerType", "Type"),
                TrangThai = GetStringOrDefault(row, "Đang hoạt động", "TrangThai", "TinhTrang", "Status"),
                GhiChu = GetString(row, "GhiChu", "MoTa", "Note"),
                SourceSchema = map.Schema,
                SourceTable = map.Name,
                KeyColumn = map.KeyColumn
            };
        }

        private static Dictionary<string, object?> BuildColumnValues(TableMap map, KhachHangDTO item, bool includeKey)
        {
            Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);

            AddIfExists(values, map, item.HoTen, "HoTen", "TenKhachHang", "TenKH", "HoVaTen", "TenKhach", "Ten", "FullName", "Name");
            AddIfExists(values, map, item.SDT, "SDT", "SoDienThoai", "DienThoai", "Phone", "PhoneNumber");
            AddIfExists(values, map, item.CCCD, "CCCD", "CMND", "CanCuoc", "SoGiayTo", "IdentityNo");
            AddIfExists(values, map, item.GioiTinh, "GioiTinh", "Gender");
            AddIfExists(values, map, item.NgaySinh, "NgaySinh", "DateOfBirth", "DOB");
            AddIfExists(values, map, item.DiaChi, "DiaChi", "Address");
            AddIfExists(values, map, item.LoaiKhach, "LoaiKhach", "LoaiKH", "HangKhachHang", "NhomKhachHang", "CustomerType", "Type");
            AddIfExists(values, map, item.TrangThai, "TrangThai", "TinhTrang", "Status");
            AddIfExists(values, map, item.GhiChu, "GhiChu", "MoTa", "Note");

            if (includeKey && !map.IdentityColumns.Contains(map.KeyColumn))
            {
                values[map.KeyColumn] = item.Ma;
            }

            return values;
        }

        private static void AddIfExists(Dictionary<string, object?> values, TableMap map, object? value, params string[] candidates)
        {
            string? column = candidates.FirstOrDefault(map.Columns.Contains);

            if (!string.IsNullOrWhiteSpace(column) && !values.ContainsKey(column))
            {
                values[column] = ConvertValueForColumn(map, column, value);
            }
        }

        private static object? ConvertValueForColumn(TableMap map, string column, object? value)
        {
            if (!map.ColumnTypes.TryGetValue(column, out string? dataType))
            {
                return value;
            }

            if (value is DateTime dateValue)
            {
                return dateValue;
            }

            if (dataType.Equals("bit", StringComparison.OrdinalIgnoreCase))
            {
                string text = value?.ToString()?.Trim() ?? string.Empty;
                return text.Equals("Đang hoạt động", StringComparison.OrdinalIgnoreCase) ||
                       text.Equals("Hoạt động", StringComparison.OrdinalIgnoreCase) ||
                       text.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
                       text.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                       text.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            return value;
        }

        private static TableMap GetTargetMap()
        {
            return GetTableMaps().FirstOrDefault()
                ?? throw new InvalidOperationException("Không tìm thấy bảng Khách hàng trong database.");
        }

        private static TableMap GetMapForItem(KhachHangDTO item)
        {
            TableMap? map = GetTableMaps().FirstOrDefault(table =>
                table.Schema.Equals(item.SourceSchema, StringComparison.OrdinalIgnoreCase) &&
                table.Name.Equals(item.SourceTable, StringComparison.OrdinalIgnoreCase));

            return map ?? GetTargetMap();
        }

        private static List<TableMap> GetTableMaps()
        {
            DataTable columns = ConnectDB.GetData(
                @"SELECT c.TABLE_SCHEMA,
                         c.TABLE_NAME,
                         c.COLUMN_NAME,
                         c.DATA_TYPE,
                         COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)), c.COLUMN_NAME, 'IsIdentity') AS IsIdentity
                  FROM INFORMATION_SCHEMA.COLUMNS c
                  ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION");

            return columns.AsEnumerable()
                .GroupBy(row => new
                {
                    Schema = row["TABLE_SCHEMA"].ToString() ?? "dbo",
                    Name = row["TABLE_NAME"].ToString() ?? string.Empty
                })
                .Where(group => CandidateTables.Any(name => string.Equals(name, group.Key.Name, StringComparison.OrdinalIgnoreCase)))
                .Select(group =>
                {
                    HashSet<string> tableColumns = group
                        .Select(row => row["COLUMN_NAME"].ToString() ?? string.Empty)
                        .Where(column => !string.IsNullOrWhiteSpace(column))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    HashSet<string> identityColumns = group
                        .Where(row => Convert.ToInt32(row["IsIdentity"]) == 1)
                        .Select(row => row["COLUMN_NAME"].ToString() ?? string.Empty)
                        .Where(column => !string.IsNullOrWhiteSpace(column))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    Dictionary<string, string> columnTypes = group
                        .Select(row => new
                        {
                            Column = row["COLUMN_NAME"].ToString() ?? string.Empty,
                            Type = row["DATA_TYPE"].ToString() ?? string.Empty
                        })
                        .Where(item => !string.IsNullOrWhiteSpace(item.Column))
                        .GroupBy(item => item.Column, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(group => group.Key, group => group.First().Type, StringComparer.OrdinalIgnoreCase);

                    return new TableMap
                    {
                        Schema = group.Key.Schema,
                        Name = group.Key.Name,
                        Columns = tableColumns,
                        IdentityColumns = identityColumns,
                        ColumnTypes = columnTypes,
                        KeyColumn = GetFirstExisting(tableColumns, "MaKhachHang", "MaKH", "KhachHangID", "CustomerID", "MaKhach", "Ma", "ID")
                    };
                })
                .Where(map => !string.IsNullOrWhiteSpace(map.KeyColumn))
                .ToList();
        }

        private static string GetKeyColumn(TableMap map, KhachHangDTO item)
        {
            if (!string.IsNullOrWhiteSpace(item.KeyColumn) && map.Columns.Contains(item.KeyColumn))
            {
                return item.KeyColumn;
            }

            if (!string.IsNullOrWhiteSpace(map.KeyColumn))
            {
                return map.KeyColumn;
            }

            throw new InvalidOperationException("Không xác định được cột khóa chính để cập nhật dữ liệu khách hàng.");
        }

        private static int GetNextId(TableMap map)
        {
            object? value = ConnectDB.ExecuteScalar($"SELECT ISNULL(MAX({Quote(map.KeyColumn)}), 0) + 1 FROM [{map.Schema}].[{map.Name}]");
            return int.TryParse(value?.ToString(), out int nextId) ? nextId : 1;
        }

        private static string GetFirstExisting(HashSet<string> columns, params string[] candidates)
        {
            return candidates.FirstOrDefault(columns.Contains) ?? string.Empty;
        }

        private static string Quote(string identifier)
        {
            return $"[{identifier.Replace("]", "]]")}]";
        }

        private static string ToParameterName(string column)
        {
            return "@" + column.Replace(" ", "_").Replace("-", "_");
        }

        private static string GetString(DataRow row, params string[] names)
        {
            return GetStringOrDefault(row, string.Empty, names);
        }

        private static string GetStringOrDefault(DataRow row, string defaultValue, params string[] names)
        {
            foreach (string name in names)
            {
                if (row.Table.Columns.Contains(name) && row[name] != DBNull.Value)
                {
                    return row[name]?.ToString() ?? defaultValue;
                }
            }

            return defaultValue;
        }

        private static int GetInt(DataRow row, params string[] names)
        {
            foreach (string name in names)
            {
                if (row.Table.Columns.Contains(name) && int.TryParse(row[name]?.ToString(), out int value))
                {
                    return value;
                }
            }

            return 0;
        }

        private static DateTime? GetDate(DataRow row, params string[] names)
        {
            foreach (string name in names)
            {
                if (row.Table.Columns.Contains(name) && DateTime.TryParse(row[name]?.ToString(), out DateTime value))
                {
                    return value;
                }
            }

            return null;
        }

        private class TableMap
        {
            public string Schema { get; set; } = "dbo";
            public string Name { get; set; } = string.Empty;
            public HashSet<string> Columns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> IdentityColumns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> ColumnTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public string KeyColumn { get; set; } = string.Empty;
        }
    }
}
