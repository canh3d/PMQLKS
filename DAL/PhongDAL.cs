using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.DAL
{
    public class PhongDAL
    {
        private static readonly string[] CandidateTables =
        {
            "Phong",
            "PHONG",
            "PhongKS",
            "PhongKhachSan",
            "Room",
            "Rooms"
        };

        public List<PhongDTO> LayDanhSach()
        {
            DongBoBangGiaPhongMacDinh();
            XoaGhiChuTuDongCu();
            List<TableMap> maps = GetTableMaps();
            List<PhongDTO> result = new();

            foreach (TableMap map in maps)
            {
                DataTable data = ConnectDB.GetData($"SELECT * FROM [{map.Schema}].[{map.Name}]");

                foreach (DataRow row in data.Rows)
                {
                    PhongDTO phong = MapRow(row, map);
                    NapThongTinLuuTruHienTai(phong);
                    result.Add(phong);
                }
            }

            return result
                .OrderBy(item => item.Tang)
                .ThenBy(item => item.MaHienThi)
                .ToList();
        }

        public int CapNhatTrangThai(PhongDTO phong, string trangThai)
        {
            TableMap map = GetMapForItem(phong);
            string keyColumn = GetKeyColumn(map, phong);
            string statusColumn = GetFirstExisting(map.Columns, "TrangThai", "TinhTrang", "TinhTrangPhong", "Status", "DaThue", "IsOccupied");

            if (string.IsNullOrWhiteSpace(statusColumn))
            {
                throw new InvalidOperationException("Không tìm thấy cột trạng thái phòng trong database.");
            }

            object value = ConvertStatusForColumn(map, statusColumn, trangThai);
            string noteColumn = GetFirstExisting(map.Columns, "GhiChu", "MoTa", "Note");

            if (!string.IsNullOrWhiteSpace(noteColumn) && !string.IsNullOrWhiteSpace(phong.GhiChu))
            {
                string sqlWithNote = $"UPDATE [{map.Schema}].[{map.Name}] SET [{statusColumn}] = @TrangThai, [{noteColumn}] = @GhiChu WHERE [{keyColumn}] = @Ma";
                return ConnectDB.ExecuteNonQuery(
                    sqlWithNote,
                    new SqlParameter("@TrangThai", value),
                    new SqlParameter("@GhiChu", phong.GhiChu),
                    new SqlParameter("@Ma", phong.Ma));
            }

            string sql = $"UPDATE [{map.Schema}].[{map.Name}] SET [{statusColumn}] = @TrangThai WHERE [{keyColumn}] = @Ma";
            return ConnectDB.ExecuteNonQuery(sql, new SqlParameter("@TrangThai", value), new SqlParameter("@Ma", phong.Ma));
        }

        public int Them(PhongDTO phong)
        {
            TableMap map = GetTargetMap();
            Dictionary<string, object?> values = BuildColumnValues(map, phong, includeKey: false);

            string columns = string.Join(", ", values.Keys.Select(column => $"[{column}]"));
            string parameters = string.Join(", ", values.Keys.Select(ToParameterName));
            string sql = $"INSERT INTO [{map.Schema}].[{map.Name}] ({columns}) VALUES ({parameters})";

            return ConnectDB.ExecuteNonQuery(sql, values.Select(item => new SqlParameter(ToParameterName(item.Key), item.Value ?? DBNull.Value)).ToArray());
        }

        public int Sua(PhongDTO phong)
        {
            TableMap map = GetMapForItem(phong);
            string keyColumn = GetKeyColumn(map, phong);
            Dictionary<string, object?> values = BuildColumnValues(map, phong, includeKey: false);

            string setClause = string.Join(", ", values.Keys.Select(column => $"[{column}] = {ToParameterName(column)}"));
            string sql = $"UPDATE [{map.Schema}].[{map.Name}] SET {setClause} WHERE [{keyColumn}] = @KeyValue";

            List<SqlParameter> parameters = values.Select(item => new SqlParameter(ToParameterName(item.Key), item.Value ?? DBNull.Value)).ToList();
            parameters.Add(new SqlParameter("@KeyValue", phong.Ma));

            return ConnectDB.ExecuteNonQuery(sql, parameters.ToArray());
        }

        public int Xoa(PhongDTO phong)
        {
            TableMap map = GetMapForItem(phong);
            string keyColumn = GetKeyColumn(map, phong);
            string sql = $"DELETE FROM [{map.Schema}].[{map.Name}] WHERE [{keyColumn}] = @KeyValue";

            return ConnectDB.ExecuteNonQuery(sql, new SqlParameter("@KeyValue", phong.Ma));
        }

        private static PhongDTO MapRow(DataRow row, TableMap map)
        {
            int ma = GetInt(row, "MaPhong", "PhongID", "ID", "Ma");
            int tang = GetInt(row, "Tang", "SoTang", "Floor");
            decimal giaGio = GetDecimal(row, "GiaGio", "GiaTheoGio", "HourlyPrice");
            decimal giaNgay = GetDecimal(row, "GiaNgay", "GiaTheoNgay", "DailyPrice");
            decimal giaDem = GetDecimal(row, "GiaDem", "GiaTheoDem", "NightPrice");
            decimal giaPhong = GetDecimal(row, "GiaPhong", "DonGia", "Gia", "GiaThue", "Price");
            int maLoaiPhong = GetInt(row, "MaLoaiPhong", "LoaiPhongID", "MaLoai", "RoomTypeID");
            string loaiPhong = GetString(row, "LoaiPhong", "TenLoaiPhong", "KieuPhong", "Loai", "RoomType");
            loaiPhong = ChuanHoaLoaiPhong(maLoaiPhong, loaiPhong);
            ApDungGiaMacDinh(loaiPhong, ref giaGio, ref giaNgay, ref giaDem, ref giaPhong);
            giaPhong = giaPhong > 0 ? giaPhong : giaDem > 0 ? giaDem : giaNgay;
            string statusColumn = GetFirstExisting(map.Columns, "TrangThai", "TinhTrang", "TinhTrangPhong", "Status", "DaThue", "IsOccupied");

            string tenPhong = GetString(row, "TenPhong", "SoPhong", "MaSoPhong", "RoomNumber", "Name");
            string maPhong = GetString(row, "MaPhong", "Code", "RoomCode");
            string soPhong = GetString(row, "SoPhong", "RoomNumber");
            string ghiChuGoc = GetStringOrDefault(row, "--", "GhiChu", "MoTa", "Note");
            string trangThaiGoc = GetTrangThai(row, map, statusColumn);
            bool canDonDep = BoDau(trangThaiGoc).Contains("chua don", StringComparison.OrdinalIgnoreCase);
            bool daDonSauDoiPhong = false;

            if (string.IsNullOrWhiteSpace(tenPhong))
            {
                tenPhong = string.IsNullOrWhiteSpace(maPhong) ? ma.ToString() : maPhong;
            }

            return new PhongDTO
            {
                Ma = ma,
                MaPhong = string.IsNullOrWhiteSpace(maPhong) ? tenPhong : maPhong,
                SoPhong = string.IsNullOrWhiteSpace(soPhong) ? tenPhong : soPhong,
                TenPhong = tenPhong,
                Tang = tang == 0 ? GetFloorFromRoomName(tenPhong) : tang,
                MaLoaiPhong = maLoaiPhong,
                LoaiPhong = loaiPhong,
                GiaGio = giaGio,
                GiaNgay = giaNgay,
                GiaDem = giaDem,
                GiaPhong = giaPhong,
                TrangThai = canDonDep ? "Chưa dọn dẹp" : daDonSauDoiPhong ? "Trống" : GetTrangThai(row, map, statusColumn),
                TinhTrangDonDep = canDonDep ? "Chưa dọn dẹp" : daDonSauDoiPhong ? "Đã dọn dẹp" : GetDonDep(row),
                KhachHienTai = GetStringOrDefault(row, "--", "KhachHienTai", "TenKhachHang", "KhachHang", "CustomerName"),
                GioNhanPhong = GetStringOrDefault(row, "--", "GioNhanPhong", "NgayNhanPhong", "CheckIn"),
                GioTraDuKien = GetStringOrDefault(row, "--", "GioTraDuKien", "NgayTraDuKien", "CheckOut"),
                GhiChu = LamSachGhiChuHienThi(ghiChuGoc),
                SourceSchema = map.Schema,
                SourceTable = map.Name,
                KeyColumn = map.KeyColumn
            };
        }

        private static void NapThongTinLuuTruHienTai(PhongDTO phong)
        {
            try
            {
                RoomStayInfo? thue = LayPhieuThueHienTai(phong.Ma);
                if (thue != null)
                {
                    phong.TrangThai = "Đang thuê";
                    phong.KhachHienTai = thue.HoTen;
                    phong.GioNhanPhong = thue.NgayNhan;
                    phong.GioTraDuKien = thue.NgayTra;
                    phong.GhiChu = string.IsNullOrWhiteSpace(thue.GhiChu) ? phong.GhiChu : LamSachGhiChuHienThi(thue.GhiChu);
                    return;
                }

                RoomStayInfo? dat = LayPhieuDatHienTai(phong.Ma);
                if (dat != null)
                {
                    phong.TrangThai = BoDau(dat.TrangThai).Contains("thue", StringComparison.OrdinalIgnoreCase) ? "Đang thuê" : "Đã đặt";
                    phong.KhachHienTai = dat.HoTen;
                    phong.GioNhanPhong = dat.NgayNhan;
                    phong.GioTraDuKien = dat.NgayTra;
                    phong.GhiChu = string.IsNullOrWhiteSpace(dat.GhiChu) ? phong.GhiChu : LamSachGhiChuHienThi(dat.GhiChu);
                    return;
                }

                if (LaPhongCuDoiPhongCanDonDep(phong.Ma, phong.GhiChu))
                {
                    phong.TrangThai = "Chưa dọn dẹp";
                    phong.TinhTrangDonDep = "Chưa dọn dẹp";
                    phong.KhachHienTai = "--";
                    phong.GioNhanPhong = "--";
                    phong.GioTraDuKien = "--";
                    return;
                }

                string normalized = BoDau(phong.TrangThai);
                if (normalized.Contains("trong", StringComparison.OrdinalIgnoreCase) ||
                    normalized.Contains("chua don", StringComparison.OrdinalIgnoreCase) ||
                    normalized.Contains("sua", StringComparison.OrdinalIgnoreCase))
                {
                    phong.KhachHienTai = "--";
                    phong.GioNhanPhong = "--";
                    phong.GioTraDuKien = "--";
                }
            }
            catch
            {
                // Keep the base room row usable if optional rental/booking tables are absent.
            }
        }

        private static RoomStayInfo? LayPhieuThueHienTai(int maPhong)
        {
            if (!TableExists("PHIEUTHUE") || !TableExists("KHACHHANG"))
            {
                return null;
            }

            bool coChiTietDatPhong = TableExists("CHITIETDATPHONG") &&
                                      ColumnExists("PHIEUTHUE", "MaDatPhong") &&
                                      ColumnExists("CHITIETDATPHONG", "MaDatPhong") &&
                                      ColumnExists("CHITIETDATPHONG", "MaPhong");
            string ngayNhanChiTiet = coChiTietDatPhong
                ? GetFirstExistingColumn("CHITIETDATPHONG", "NgayNhanDuKien", "NgayNhanPhong", "NgayNhan")
                : string.Empty;
            string ngayTraChiTiet = coChiTietDatPhong
                ? GetFirstExistingColumn("CHITIETDATPHONG", "NgayTraDuKien", "NgayTraPhong", "NgayTra")
                : string.Empty;
            string ngayTraPhieuExpr = ColumnExists("PHIEUTHUE", "NgayTraPhong")
                ? "ISNULL(PT.NgayTraPhong, PT.NgayTraDuKien)"
                : "PT.NgayTraDuKien";
            string ngayNhanExpr = string.IsNullOrWhiteSpace(ngayNhanChiTiet)
                ? "PT.NgayNhan"
                : "ISNULL(CT." + ngayNhanChiTiet + ", PT.NgayNhan)";
            string ngayTraExpr = string.IsNullOrWhiteSpace(ngayTraChiTiet)
                ? ngayTraPhieuExpr
                : "ISNULL(CT." + ngayTraChiTiet + ", " + ngayTraPhieuExpr + ")";
            string ghiChuExpr = ColumnExists("PHIEUTHUE", "GhiChu") ? "PT.GhiChu" : "CAST(NULL AS nvarchar(1000))";
            string joinChiTiet = coChiTietDatPhong
                ? "LEFT JOIN dbo.CHITIETDATPHONG CT ON PT.MaDatPhong = CT.MaDatPhong"
                : string.Empty;
            string phongExpr = coChiTietDatPhong ? "ISNULL(CT.MaPhong, PT.MaPhong)" : "PT.MaPhong";

            DataTable data = ConnectDB.GetData(@"
SELECT TOP 1 KH.HoTen,
       " + ngayNhanExpr + @" AS NgayNhan,
       " + ngayTraExpr + @" AS NgayTra,
       " + ghiChuExpr + @" AS GhiChu
FROM dbo.PHIEUTHUE PT
JOIN dbo.KHACHHANG KH ON PT.MaKH = KH.MaKH
" + joinChiTiet + @"
LEFT JOIN dbo.PHONG P ON " + phongExpr + @" = P.MaPhong
WHERE " + phongExpr + @" = @MaPhong
  AND (
        PT.TrangThai IN (N'Đang thuê', N'Dang thue', N'Có khách', N'Co khach')
        OR P.TrangThai IN (N'Đang thuê', N'Dang thue', N'Có khách', N'Co khach')
      )
ORDER BY PT.MaThue DESC",
                new SqlParameter("@MaPhong", maPhong));

            return data.Rows.Count == 0 ? null : MapRoomStay(data.Rows[0]);
        }

        private static RoomStayInfo? LayPhieuDatHienTai(int maPhong)
        {
            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists("DATPHONG") ? "DATPHONG" : string.Empty;
            if (string.IsNullOrWhiteSpace(bangDatPhong) || !TableExists("KHACHHANG"))
            {
                return null;
            }

            string ngayNhanExpr = ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "DP.NgayNhanDuKien" : "DP.NgayNhanPhong";
            string ngayTraExpr = ColumnExists(bangDatPhong, "NgayTraDuKien") ? "DP.NgayTraDuKien" : "DP.NgayTraPhong";
            string ghiChuExpr = ColumnExists(bangDatPhong, "GhiChu") ? "DP.GhiChu" : "CAST(NULL AS nvarchar(1000))";
            string joinPhong = TableExists("CHITIETDATPHONG")
                ? @"JOIN dbo.CHITIETDATPHONG CT ON DP.MaDatPhong = CT.MaDatPhong
JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong"
                : "JOIN dbo.PHONG P ON DP.MaPhong = P.MaPhong";
            string wherePhong = TableExists("CHITIETDATPHONG") ? "CT.MaPhong = @MaPhong" : "DP.MaPhong = @MaPhong";

            DataTable data = ConnectDB.GetData(@"
SELECT TOP 1 KH.HoTen,
       " + ngayNhanExpr + @" AS NgayNhan,
       " + ngayTraExpr + @" AS NgayTra,
       " + ghiChuExpr + @" AS GhiChu,
       DP.TrangThai
FROM dbo." + bangDatPhong + @" DP
JOIN dbo.KHACHHANG KH ON DP.MaKH = KH.MaKH
" + joinPhong + @"
WHERE " + wherePhong + @"
  AND DP.TrangThai IN (N'Đã xác nhận', N'Da xac nhan', N'Đã đặt', N'Da dat', N'Đang thuê', N'Dang thue', N'Có khách', N'Co khach')
ORDER BY DP.MaDatPhong DESC",
                new SqlParameter("@MaPhong", maPhong));

            return data.Rows.Count == 0 ? null : MapRoomStay(data.Rows[0]);
        }

        private static bool LaPhongCuDoiPhongCanDonDep(int maPhong, string ghiChuPhong)
        {
            if (!TableExists("DOIPHONG") ||
                !TableExists("PHIEUTHUE") ||
                !ColumnExists("DOIPHONG", "MaDoiPhong") ||
                !ColumnExists("DOIPHONG", "MaThue") ||
                !ColumnExists("DOIPHONG", "MaPhongCu") ||
                !ColumnExists("PHIEUTHUE", "MaThue") ||
                !ColumnExists("PHIEUTHUE", "MaPhong") ||
                !ColumnExists("PHIEUTHUE", "TrangThai"))
            {
                return false;
            }

            string orderBy = ColumnExists("DOIPHONG", "ThoiDiemDoi")
                ? "DP.ThoiDiemDoi DESC, DP.MaDoiPhong DESC"
                : "DP.MaDoiPhong DESC";
            object? result = ConnectDB.ExecuteScalar(@"
SELECT TOP 1 DP.MaDoiPhong
FROM dbo.DOIPHONG DP
JOIN dbo.PHIEUTHUE PT ON DP.MaThue = PT.MaThue
WHERE DP.MaPhongCu = @MaPhong
  AND ISNULL(PT.MaPhong, 0) <> @MaPhong
  AND PT.TrangThai IN (N'Đang thuê', N'Dang thue', N'Có khách', N'Co khach')
ORDER BY " + orderBy,
                new SqlParameter("@MaPhong", maPhong));

            if (result == null || result == DBNull.Value || !int.TryParse(result.ToString(), out int maDoiPhong))
            {
                return false;
            }

            return !ghiChuPhong.Contains("[DA_DON_DEP_DOIPHONG:" + maDoiPhong + "]", StringComparison.OrdinalIgnoreCase);
        }

        private static RoomStayInfo MapRoomStay(DataRow row)
        {
            return new RoomStayInfo
            {
                HoTen = row["HoTen"]?.ToString() ?? "--",
                NgayNhan = FormatDate(row["NgayNhan"]),
                NgayTra = FormatDate(row["NgayTra"]),
                GhiChu = row.Table.Columns.Contains("GhiChu") && row["GhiChu"] != DBNull.Value ? row["GhiChu"]?.ToString() ?? string.Empty : string.Empty,
                TrangThai = row.Table.Columns.Contains("TrangThai") && row["TrangThai"] != DBNull.Value ? row["TrangThai"]?.ToString() ?? string.Empty : string.Empty
            };
        }

        private static Dictionary<string, object?> BuildColumnValues(TableMap map, PhongDTO phong, bool includeKey)
        {
            Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);

            string soPhong = string.IsNullOrWhiteSpace(phong.SoPhong) ? phong.TenPhong : phong.SoPhong;
            string tenPhong = string.IsNullOrWhiteSpace(phong.TenPhong) ? soPhong : phong.TenPhong;

            AddIfExists(values, map, soPhong, "SoPhong", "RoomNumber");
            AddIfExists(values, map, tenPhong, "TenPhong", "MaSoPhong", "Name");
            AddIfExists(values, map, phong.Tang, "Tang", "SoTang", "Floor");
            AddIfExists(values, map, phong.MaLoaiPhong, "MaLoaiPhong", "LoaiPhongID", "MaLoai", "RoomTypeID");
            AddIfExists(values, map, phong.LoaiPhong, "LoaiPhong", "TenLoaiPhong", "KieuPhong", "Loai", "RoomType");
            AddIfExists(values, map, phong.GiaGio, "GiaGio", "GiaTheoGio", "HourlyPrice");
            AddIfExists(values, map, phong.GiaNgay, "GiaNgay", "GiaTheoNgay", "DailyPrice");
            AddIfExists(values, map, phong.GiaDem, "GiaDem", "GiaTheoDem", "NightPrice");
            AddIfExists(values, map, phong.GiaPhong, "GiaPhong", "DonGia", "Gia", "GiaThue", "Price");
            AddIfExists(values, map, ConvertStatusForColumn(map, GetFirstExisting(map.Columns, "TrangThai", "TinhTrang", "TinhTrangPhong", "Status", "DaThue", "IsOccupied"), phong.TrangThai), "TrangThai", "TinhTrang", "TinhTrangPhong", "Status", "DaThue", "IsOccupied");
            AddIfExists(values, map, phong.GhiChu, "GhiChu", "MoTa", "Note");

            if (includeKey && !map.IdentityColumns.Contains(map.KeyColumn))
            {
                values[map.KeyColumn] = phong.Ma;
            }

            return values;
        }

        private static string ChuanHoaLoaiPhong(int maLoaiPhong, string loaiPhong)
        {
            if (!string.IsNullOrWhiteSpace(loaiPhong))
            {
                return loaiPhong;
            }

            return maLoaiPhong switch
            {
                1 => "Phòng đơn",
                2 => "Phòng đôi",
                3 => "Phòng VIP",
                _ => "Loại phòng"
            };
        }

        private static void ApDungGiaMacDinh(string loaiPhong, ref decimal giaGio, ref decimal giaNgay, ref decimal giaDem, ref decimal giaPhong)
        {
            (decimal gio, decimal ngay, decimal dem) = loaiPhong.ToLowerInvariant() switch
            {
                string value when value.Contains("vip") => (200000m, 1200000m, 900000m),
                string value when value.Contains("đôi") || value.Contains("doi") => (120000m, 700000m, 500000m),
                _ => (80000m, 450000m, 350000m)
            };

            if (giaGio <= 0)
            {
                giaGio = gio;
            }

            if (giaNgay <= 0)
            {
                giaNgay = ngay;
            }

            if (giaDem <= 0)
            {
                giaDem = dem;
            }

            if (giaPhong <= 0)
            {
                giaPhong = giaDem;
            }
        }

        private static void AddIfExists(Dictionary<string, object?> values, TableMap map, object? value, params string[] candidates)
        {
            string? column = candidates.FirstOrDefault(column => map.Columns.Contains(column) && !map.ComputedColumns.Contains(column));

            if (!string.IsNullOrWhiteSpace(column) && !values.ContainsKey(column))
            {
                values[column] = value;
            }
        }

        private static string GetTrangThai(DataRow row, TableMap map, string column)
        {
            if (string.IsNullOrWhiteSpace(column) || !row.Table.Columns.Contains(column) || row[column] == DBNull.Value)
            {
                return "Phòng trống";
            }

            if (map.ColumnTypes.TryGetValue(column, out string? dataType) &&
                dataType.Equals("bit", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToBoolean(row[column]) ? "Đang thuê" : "Phòng trống";
            }

            string value = row[column]?.ToString()?.Trim() ?? string.Empty;

            if (string.Equals(value, "True", StringComparison.OrdinalIgnoreCase))
            {
                return "Đang thuê";
            }

            if (string.Equals(value, "False", StringComparison.OrdinalIgnoreCase))
            {
                return "Phòng trống";
            }

            return string.IsNullOrWhiteSpace(value) ? "Phòng trống" : value;
        }

        private static object ConvertStatusForColumn(TableMap map, string column, string trangThai)
        {
            if (map.ColumnTypes.TryGetValue(column, out string? dataType) &&
                dataType.Equals("bit", StringComparison.OrdinalIgnoreCase))
            {
                return trangThai.Contains("thuê", StringComparison.OrdinalIgnoreCase) ||
                       trangThai.Contains("đặt", StringComparison.OrdinalIgnoreCase);
            }

            return ChonTrangThaiHopLe(map, column, trangThai);
        }

        private static string ChonTrangThaiHopLe(TableMap map, string column, string trangThai)
        {
            List<string> allowed = LayTrangThaiHopLe(map, column);
            if (allowed.Count == 0 || allowed.Any(item => string.Equals(item, trangThai, StringComparison.OrdinalIgnoreCase)))
            {
                return trangThai;
            }

            string normalized = BoDau(trangThai);
            string? match = null;

            if (normalized.Contains("dat", StringComparison.OrdinalIgnoreCase))
            {
                match = allowed.FirstOrDefault(item => BoDau(item).Contains("dat", StringComparison.OrdinalIgnoreCase));
            }
            else if (normalized.Contains("thue", StringComparison.OrdinalIgnoreCase) ||
                     normalized.Contains("nhan", StringComparison.OrdinalIgnoreCase) ||
                     normalized.Contains("dang", StringComparison.OrdinalIgnoreCase))
            {
                match = allowed.FirstOrDefault(item =>
                {
                    string value = BoDau(item);
                    return value.Contains("thue", StringComparison.OrdinalIgnoreCase) ||
                           value.Contains("dang", StringComparison.OrdinalIgnoreCase) ||
                           value.Contains("su dung", StringComparison.OrdinalIgnoreCase) ||
                           value.Contains("co khach", StringComparison.OrdinalIgnoreCase) ||
                           value.Contains("ban", StringComparison.OrdinalIgnoreCase);
                });
            }

            match ??= allowed.FirstOrDefault(item =>
            {
                string value = BoDau(item);
                return !value.Contains("trong", StringComparison.OrdinalIgnoreCase) &&
                       !value.Contains("empty", StringComparison.OrdinalIgnoreCase) &&
                       !value.Contains("free", StringComparison.OrdinalIgnoreCase);
            });

            return match ?? allowed.First();
        }

        private static List<string> LayTrangThaiHopLe(TableMap map, string column)
        {
            try
            {
                DataTable data = ConnectDB.GetData(
                    @"SELECT cc.definition
                      FROM sys.check_constraints cc
                      JOIN sys.tables t ON cc.parent_object_id = t.object_id
                      JOIN sys.schemas s ON t.schema_id = s.schema_id
                      WHERE s.name = @Schema
                        AND t.name = @Table
                        AND cc.definition LIKE @ColumnPattern",
                    new SqlParameter("@Schema", map.Schema),
                    new SqlParameter("@Table", map.Name),
                    new SqlParameter("@ColumnPattern", "%" + column + "%"));

                return data.AsEnumerable()
                    .Select(row => row["definition"]?.ToString() ?? string.Empty)
                    .SelectMany(definition => Regex.Matches(definition, @"N?'([^']+)'").Select(match => match.Groups[1].Value))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static string BoDau(string value)
        {
            string formD = value.Normalize(System.Text.NormalizationForm.FormD);
            char[] chars = formD
                .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                .Select(ch => ch == 'đ' ? 'd' : ch == 'Đ' ? 'D' : ch)
                .ToArray();
            return new string(chars).Normalize(System.Text.NormalizationForm.FormC);
        }

        private static string GetDonDep(DataRow row)
        {
            string clean = GetString(row, "TinhTrangDonDep", "DonDep", "DaDonDep", "IsClean");

            if (string.Equals(clean, "True", StringComparison.OrdinalIgnoreCase))
            {
                return "Đã dọn dẹp";
            }

            if (string.Equals(clean, "False", StringComparison.OrdinalIgnoreCase))
            {
                return "Chưa dọn dẹp";
            }

            return string.IsNullOrWhiteSpace(clean) ? "Đã dọn dẹp" : clean;
        }

        private static List<TableMap> GetTableMaps()
        {
            DataTable columns = ConnectDB.GetData(
                @"SELECT c.TABLE_SCHEMA,
                         c.TABLE_NAME,
                         c.COLUMN_NAME,
                         c.DATA_TYPE,
                         COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)), c.COLUMN_NAME, 'IsIdentity') AS IsIdentity,
                         COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)), c.COLUMN_NAME, 'IsComputed') AS IsComputed
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

                    Dictionary<string, string> columnTypes = group
                        .Select(row => new
                        {
                            Column = row["COLUMN_NAME"].ToString() ?? string.Empty,
                            Type = row["DATA_TYPE"].ToString() ?? string.Empty
                        })
                        .Where(item => !string.IsNullOrWhiteSpace(item.Column))
                        .GroupBy(item => item.Column, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(group => group.Key, group => group.First().Type, StringComparer.OrdinalIgnoreCase);

                    HashSet<string> identityColumns = group
                        .Where(row => ToIntMetadata(row["IsIdentity"]) == 1)
                        .Select(row => row["COLUMN_NAME"].ToString() ?? string.Empty)
                        .Where(column => !string.IsNullOrWhiteSpace(column))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    HashSet<string> computedColumns = group
                        .Where(row => ToIntMetadata(row["IsComputed"]) == 1)
                        .Select(row => row["COLUMN_NAME"].ToString() ?? string.Empty)
                        .Where(column => !string.IsNullOrWhiteSpace(column))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    return new TableMap
                    {
                        Schema = group.Key.Schema,
                        Name = group.Key.Name,
                        Columns = tableColumns,
                        IdentityColumns = identityColumns,
                        ComputedColumns = computedColumns,
                        ColumnTypes = columnTypes,
                        KeyColumn = GetFirstExisting(tableColumns, "MaPhong", "PhongID", "ID", "Ma")
                    };
                })
                .Where(map => !string.IsNullOrWhiteSpace(map.KeyColumn))
                .ToList();
        }

        private static TableMap GetMapForItem(PhongDTO item)
        {
            return GetTableMaps().FirstOrDefault(map =>
                       map.Schema.Equals(item.SourceSchema, StringComparison.OrdinalIgnoreCase) &&
                       map.Name.Equals(item.SourceTable, StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidOperationException("Không tìm thấy bảng phòng trong database.");
        }

        private static TableMap GetTargetMap()
        {
            return GetTableMaps().FirstOrDefault()
                   ?? throw new InvalidOperationException("Không tìm thấy bảng phòng trong database.");
        }

        private static string GetKeyColumn(TableMap map, PhongDTO item)
        {
            if (!string.IsNullOrWhiteSpace(item.KeyColumn) && map.Columns.Contains(item.KeyColumn))
            {
                return item.KeyColumn;
            }

            return map.KeyColumn;
        }

        private static string GetFirstExisting(HashSet<string> columns, params string[] candidates)
        {
            return candidates.FirstOrDefault(columns.Contains) ?? string.Empty;
        }

        private static string GetFirstExistingColumn(string tableName, params string[] candidates)
        {
            return candidates.FirstOrDefault(column => ColumnExists(tableName, column)) ?? string.Empty;
        }

        private static int ToIntMetadata(object value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static void DongBoBangGiaPhongMacDinh()
        {
            try
            {
                if (!TableExists("LOAIPHONG") ||
                    !ColumnExists("LOAIPHONG", "TenLoaiPhong") ||
                    !ColumnExists("LOAIPHONG", "DonGiaNgay") ||
                    !ColumnExists("LOAIPHONG", "DonGiaDem"))
                {
                    return;
                }

                string tienCocSet = ColumnExists("LOAIPHONG", "TienCocGoiY")
                    ? @",
    TienCocGoiY = CASE
        WHEN TenLoaiPhong LIKE N'%VIP%' THEN 500000
        WHEN TenLoaiPhong LIKE N'%đôi%' OR TenLoaiPhong LIKE N'%doi%' THEN 300000
        ELSE 200000
    END"
                    : string.Empty;

                ConnectDB.ExecuteNonQuery(@"
UPDATE dbo.LOAIPHONG
SET DonGiaNgay = CASE
        WHEN TenLoaiPhong LIKE N'%VIP%' THEN 1200000
        WHEN TenLoaiPhong LIKE N'%đôi%' OR TenLoaiPhong LIKE N'%doi%' THEN 700000
        ELSE 450000
    END,
    DonGiaDem = CASE
        WHEN TenLoaiPhong LIKE N'%VIP%' THEN 900000
        WHEN TenLoaiPhong LIKE N'%đôi%' OR TenLoaiPhong LIKE N'%doi%' THEN 500000
        ELSE 350000
    END" + tienCocSet + @"
WHERE TenLoaiPhong LIKE N'%VIP%'
   OR TenLoaiPhong LIKE N'%đơn%'
   OR TenLoaiPhong LIKE N'%don%'
   OR TenLoaiPhong LIKE N'%đôi%'
   OR TenLoaiPhong LIKE N'%doi%'");
            }
            catch
            {
                // Keep room loading usable on older/custom schemas.
            }
        }

        private static void XoaGhiChuTuDongCu()
        {
            try
            {
                if (!TableExists("PHONG") || !ColumnExists("PHONG", "GhiChu"))
                {
                    return;
                }

                ConnectDB.ExecuteNonQuery(@"
UPDATE dbo.PHONG
SET GhiChu = STUFF(
        GhiChu,
        CHARINDEX(N'[DA_DON_DEP_DOIPHONG:', GhiChu),
        CHARINDEX(N']', GhiChu, CHARINDEX(N'[DA_DON_DEP_DOIPHONG:', GhiChu))
            - CHARINDEX(N'[DA_DON_DEP_DOIPHONG:', GhiChu) + 1,
        N'')
WHERE CHARINDEX(N'[DA_DON_DEP_DOIPHONG:', ISNULL(GhiChu, N'')) > 0
  AND CHARINDEX(N']', GhiChu, CHARINDEX(N'[DA_DON_DEP_DOIPHONG:', GhiChu)) > 0;

UPDATE dbo.PHONG
SET GhiChu = NULLIF(LTRIM(RTRIM(
    REPLACE(REPLACE(REPLACE(REPLACE(
        ISNULL(GhiChu, N''),
        N'[CAN_DON_DEP] Can don dep sau khi doi phong', N''),
        N'[CAN_DON_DEP] Can don dep sau khi tra phong', N''),
        N'Can don dep sau khi doi phong', N''),
        N'Can don dep sau khi tra phong', N'')
)), N'')
WHERE CHARINDEX(N'[CAN_DON_DEP]', ISNULL(GhiChu, N'')) > 0
   OR CHARINDEX(N'Can don dep sau khi', ISNULL(GhiChu, N'')) > 0;");
            }
            catch
            {
                // Dữ liệu phòng vẫn tải được nếu tài khoản chỉ có quyền đọc.
            }
        }

        private static string ToParameterName(string column)
        {
            return "@" + column.Replace(" ", "_").Replace("-", "_");
        }

        private static int GetFloorFromRoomName(string roomName)
        {
            string digits = new(roomName.Where(char.IsDigit).ToArray());
            return digits.Length >= 3 && int.TryParse(digits[..1], out int floor) ? floor : 1;
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

        private static string FormatDate(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return "--";
            }

            return DateTime.TryParse(value.ToString(), out DateTime date)
                ? date.ToString("dd/MM/yyyy HH:mm")
                : value.ToString() ?? "--";
        }

        private static bool TableExists(string tableName)
        {
            object? result = ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM sys.tables WHERE name = @Name", new SqlParameter("@Name", tableName));
            return Convert.ToInt32(result) > 0;
        }

        private static bool ColumnExists(string tableName, string columnName)
        {
            object? result = ConnectDB.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM sys.tables t
                  JOIN sys.columns c ON t.object_id = c.object_id
                  WHERE t.name = @TableName AND c.name = @ColumnName",
                new SqlParameter("@TableName", tableName),
                new SqlParameter("@ColumnName", columnName));
            return Convert.ToInt32(result) > 0;
        }

        private static string LamSachGhiChuHienThi(string ghiChu)
        {
            if (string.IsNullOrWhiteSpace(ghiChu) || ghiChu.Trim() == "--")
            {
                return "--";
            }

            string cleaned = Regex.Replace(
                ghiChu,
                @"\[(GIAHAN|DICHVU_CHECKIN|DICHVU_PHATSINH|CAN_DON_DEP|DA_DON_DEP_DOIPHONG|DATPHONG|DAT_DOAN|DOI_PHONG)[^\]]*\]\s*",
                string.Empty,
                RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(
                cleaned,
                @"\[(CAN_DON_DEP|DA_DON_DEP_DOIPHONG):?[^\]]*\](\s*-\s*)?",
                string.Empty,
                RegexOptions.IgnoreCase);

            string[] noteParts = cleaned
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(BoKeyTuDongTrongPhanGhiChu)
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToArray();
            cleaned = string.Join("; ", noteParts);
            cleaned = Regex.Replace(cleaned, @"\s*-\s*", " - ").Trim(' ', '-');
            return string.IsNullOrWhiteSpace(cleaned) ? "--" : cleaned;
        }

        private static string BoKeyTuDongTrongPhanGhiChu(string part)
        {
            const string autoKeyPattern =
                @"^(TongTienPhong|TienPhong|TongTienDichVu|TienDichVu|TongCoc|TienCoc|DatCoc|SoTien|CheDo|CheDoDatPhong|LoaiDat|LoaiDatPhong|NhanNgay|Tu|Den|NgayNhan|NgayTra|NgayNhanDuKien|NgayTraDuKien|SoNguoi|SoPhong|Phong|TuPhong|ThoiDiem|PhuPhiNhanSom|DonGia|MaDatPhong|MaThue|MaPhong)\s*=";

            if (!Regex.IsMatch(part, autoKeyPattern, RegexOptions.IgnoreCase))
            {
                string trimmed = part.Trim();
                return Regex.IsMatch(trimmed, @"^\d{2}\s*-\s*\d{2}T\d{2}:\d{2}:\d{2}", RegexOptions.IgnoreCase)
                    ? string.Empty
                    : trimmed;
            }

            int userNoteIndex = part.IndexOf(" - ", StringComparison.Ordinal);
            return userNoteIndex >= 0 ? part[(userNoteIndex + 3)..].Trim() : string.Empty;
        }

        private class RoomStayInfo
        {
            public string HoTen { get; set; } = "--";
            public string NgayNhan { get; set; } = "--";
            public string NgayTra { get; set; } = "--";
            public string GhiChu { get; set; } = string.Empty;
            public string TrangThai { get; set; } = string.Empty;
        }

        private class TableMap
        {
            public string Schema { get; set; } = "dbo";
            public string Name { get; set; } = string.Empty;
            public HashSet<string> Columns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> IdentityColumns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> ComputedColumns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> ColumnTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public string KeyColumn { get; set; } = string.Empty;
        }
    }
}
