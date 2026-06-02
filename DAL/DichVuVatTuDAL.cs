using System.Data;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.DAL
{
    public class DichVuVatTuDAL
    {
        private static readonly string[] CandidateTables =
        {
            "DichVuVatTu",
            "DichVu_VatTu",
            "DichVu",
            "VatTu",
            "DICHVUVATTU",
            "DICHVU",
            "VATTU"
        };

        public List<DichVuVatTuDTO> LayDanhSach()
        {
            List<TableMap> maps = GetTableMaps();
            List<DichVuVatTuDTO> result = new();

            foreach (TableMap map in maps)
            {
                DataTable data = ConnectDB.GetData($"SELECT * FROM [{map.Schema}].[{map.Name}]");
                string defaultType = GetDefaultType(map.Name);

                foreach (DataRow row in data.Rows)
                {
                    result.Add(MapRow(row, map, defaultType));
                }
            }

            return result.OrderBy(item => item.Ma).ThenBy(item => item.Ten).ToList();
        }

        public int Them(DichVuVatTuDTO item)
        {
            TableMap map = GetTargetMap(item.Loai);
            Dictionary<string, object?> values = BuildColumnValues(map, item, includeKey: false);

            if (values.Count == 0)
            {
                throw new InvalidOperationException("Không tìm thấy cột phù hợp để thêm dữ liệu.");
            }

            string columns = string.Join(", ", values.Keys.Select(Quote));
            string parameters = string.Join(", ", values.Keys.Select(ToParameterName));
            string sql = $"INSERT INTO [{map.Schema}].[{map.Name}] ({columns}) VALUES ({parameters})";

            return ConnectDB.ExecuteNonQuery(sql, values.Select(pair => new SqlParameter(ToParameterName(pair.Key), pair.Value ?? DBNull.Value)).ToArray());
        }

        public int Sua(DichVuVatTuDTO item)
        {
            TableMap map = GetMapForItem(item);
            string keyColumn = GetKeyColumn(map, item);
            Dictionary<string, object?> values = BuildColumnValues(map, item, includeKey: false);

            if (values.Count == 0)
            {
                throw new InvalidOperationException("Không tìm thấy cột phù hợp để sửa dữ liệu.");
            }

            string setClause = string.Join(", ", values.Keys.Select(column => $"{Quote(column)} = {ToParameterName(column)}"));
            string sql = $"UPDATE [{map.Schema}].[{map.Name}] SET {setClause} WHERE {Quote(keyColumn)} = @KeyValue";

            List<SqlParameter> parameters = values
                .Select(pair => new SqlParameter(ToParameterName(pair.Key), pair.Value ?? DBNull.Value))
                .ToList();
            parameters.Add(new SqlParameter("@KeyValue", item.Ma));

            return ConnectDB.ExecuteNonQuery(sql, parameters.ToArray());
        }

        public int Xoa(DichVuVatTuDTO item)
        {
            TableMap map = GetMapForItem(item);
            string keyColumn = GetKeyColumn(map, item);
            string sql = $"DELETE FROM [{map.Schema}].[{map.Name}] WHERE {Quote(keyColumn)} = @KeyValue";

            return ConnectDB.ExecuteNonQuery(sql, new SqlParameter("@KeyValue", item.Ma));
        }

        private static DichVuVatTuDTO MapRow(DataRow row, TableMap map, string defaultType)
        {
            int soLuongTon = GetInt(row, "SoLuongTon", "SLTon", "SoLuong", "TonKho", "SoLuongCon", "SL");
            string statusColumn = GetFirstExisting(map.Columns, "TrangThai", "TinhTrang", "Status");
            string trangThai = GetTrangThai(row, map, statusColumn, soLuongTon);

            if (string.IsNullOrWhiteSpace(trangThai))
            {
                trangThai = soLuongTon <= 0 ? "Sắp hết" : "Còn hàng";
            }

            return new DichVuVatTuDTO
            {
                Ma = GetInt(row, "MaDVVT", "MaDichVuVatTu", "MaDichVu", "MaVatTu", "MaDV", "MaVT", "Ma", "ID"),
                Ten = GetString(row, "TenDVVT", "TenDichVuVatTu", "TenDichVu", "TenVatTu", "TenDV", "TenVT", "Ten", "TenHang", "TenMatHang"),
                Loai = GetStringOrDefault(row, defaultType, "Loai", "LoaiDichVu", "PhanLoai", "Nhom", "LoaiHang", "Type"),
                DonViTinh = GetString(row, "DonViTinh", "DVT", "DonVi", "Unit"),
                DonGia = GetDecimal(row, "DonGia", "Gia", "GiaBan", "GiaTien", "Price"),
                SoLuongTon = soLuongTon,
                TrangThai = trangThai,
                GhiChu = GetString(row, "GhiChu", "MoTa", "Note"),
                SourceSchema = map.Schema,
                SourceTable = map.Name,
                KeyColumn = map.KeyColumn
            };
        }

        private static Dictionary<string, object?> BuildColumnValues(TableMap map, DichVuVatTuDTO item, bool includeKey)
        {
            Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);

            AddIfExists(values, map, item.Ten, "TenDVVT", "TenDichVuVatTu", "TenDichVu", "TenVatTu", "TenDV", "TenVT", "Ten", "TenHang", "TenMatHang");
            AddIfExists(values, map, item.Loai, "Loai", "LoaiDichVu", "PhanLoai", "Nhom", "LoaiHang", "Type");
            AddIfExists(values, map, item.DonViTinh, "DonViTinh", "DVT", "DonVi", "Unit");
            AddIfExists(values, map, item.DonGia, "DonGia", "Gia", "GiaBan", "GiaTien", "Price");
            AddIfExists(values, map, item.SoLuongTon, "SoLuongTon", "SLTon", "SoLuong", "TonKho", "SoLuongCon", "SL");
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
                        KeyColumn = GetFirstExisting(tableColumns, "MaDVVT", "MaDichVuVatTu", "MaDichVu", "MaVatTu", "MaDV", "MaVT", "Ma", "ID")
                    };
                })
                .Where(map => !string.IsNullOrWhiteSpace(map.KeyColumn))
                .ToList();
        }

        private static TableMap GetTargetMap(string loai)
        {
            List<TableMap> maps = GetTableMaps();

            TableMap? unified = maps.FirstOrDefault(map =>
                map.Name.Equals("DichVuVatTu", StringComparison.OrdinalIgnoreCase) ||
                map.Name.Equals("DichVu_VatTu", StringComparison.OrdinalIgnoreCase));

            if (unified != null)
            {
                return unified;
            }

            bool isVatTu = loai.Contains("Vật tư", StringComparison.OrdinalIgnoreCase) ||
                           loai.Contains("Vat tu", StringComparison.OrdinalIgnoreCase);

            TableMap? target = maps.FirstOrDefault(map =>
                isVatTu
                    ? map.Name.Contains("VatTu", StringComparison.OrdinalIgnoreCase) || map.Name.Contains("VATTU", StringComparison.OrdinalIgnoreCase)
                    : map.Name.Contains("DichVu", StringComparison.OrdinalIgnoreCase) || map.Name.Contains("DICHVU", StringComparison.OrdinalIgnoreCase));

            return target ?? maps.FirstOrDefault() ?? throw new InvalidOperationException("Không tìm thấy bảng Dịch vụ/Vật tư trong database.");
        }

        private static TableMap GetMapForItem(DichVuVatTuDTO item)
        {
            TableMap? map = GetTableMaps().FirstOrDefault(table =>
                table.Schema.Equals(item.SourceSchema, StringComparison.OrdinalIgnoreCase) &&
                table.Name.Equals(item.SourceTable, StringComparison.OrdinalIgnoreCase));

            return map ?? GetTargetMap(item.Loai);
        }

        private static string GetKeyColumn(TableMap map, DichVuVatTuDTO item)
        {
            if (!string.IsNullOrWhiteSpace(item.KeyColumn) && map.Columns.Contains(item.KeyColumn))
            {
                return item.KeyColumn;
            }

            if (!string.IsNullOrWhiteSpace(map.KeyColumn))
            {
                return map.KeyColumn;
            }

            throw new InvalidOperationException("Không xác định được cột khóa chính để cập nhật dữ liệu.");
        }

        private static string GetFirstExisting(HashSet<string> columns, params string[] candidates)
        {
            return candidates.FirstOrDefault(columns.Contains) ?? string.Empty;
        }

        private static object? ConvertValueForColumn(TableMap map, string column, object? value)
        {
            if (!map.ColumnTypes.TryGetValue(column, out string? dataType))
            {
                return value;
            }

            if (!dataType.Equals("bit", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            string text = value?.ToString()?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (bool.TryParse(text, out bool boolValue))
            {
                return boolValue;
            }

            return text.Equals("Còn hàng", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("Con hang", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("Đang dùng", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("Dang dung", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetTrangThai(DataRow row, TableMap map, string column, int soLuongTon)
        {
            if (string.IsNullOrWhiteSpace(column) || !row.Table.Columns.Contains(column) || row[column] == DBNull.Value)
            {
                return string.Empty;
            }

            if (map.ColumnTypes.TryGetValue(column, out string? dataType) &&
                dataType.Equals("bit", StringComparison.OrdinalIgnoreCase))
            {
                bool isActive = Convert.ToBoolean(row[column]);

                if (!isActive)
                {
                    return "Hết hàng";
                }

                return soLuongTon <= 0 ? "Sắp hết" : "Còn hàng";
            }

            return row[column]?.ToString() ?? string.Empty;
        }

        private static string Quote(string identifier)
        {
            return $"[{identifier.Replace("]", "]]")}]";
        }

        private static string ToParameterName(string column)
        {
            return "@" + column.Replace(" ", "_").Replace("-", "_");
        }

        private static string GetDefaultType(string tableName)
        {
            if (tableName.Contains("VatTu", StringComparison.OrdinalIgnoreCase) ||
                tableName.Contains("VATTU", StringComparison.OrdinalIgnoreCase))
            {
                return "Vật tư";
            }

            if (tableName.Contains("DichVu", StringComparison.OrdinalIgnoreCase) ||
                tableName.Contains("DICHVU", StringComparison.OrdinalIgnoreCase))
            {
                return "Dịch vụ";
            }

            return string.Empty;
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

        private static decimal GetDecimal(DataRow row, params string[] names)
        {
            foreach (string name in names)
            {
                if (row.Table.Columns.Contains(name) && decimal.TryParse(row[name]?.ToString(), out decimal value))
                {
                    return value;
                }
            }

            return 0;
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
