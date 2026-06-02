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

            return ConnectDB.ExecuteNonQuery(sql, new SqlParameter("@KeyValue", item.Ma));
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
