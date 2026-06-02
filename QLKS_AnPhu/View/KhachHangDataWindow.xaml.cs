using System.Data;
using Microsoft.Data.SqlClient;
using System.Windows;
using QLKS_AnPhu.DAL;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.View
{
    public partial class KhachHangDataWindow : Window
    {
        private readonly KhachHangDTO khachHang;
        private readonly DataMode mode;

        public KhachHangDataWindow(KhachHangDTO khachHang, DataMode mode)
        {
            this.khachHang = khachHang;
            this.mode = mode;
            InitializeComponent();
            Loaded += KhachHangDataWindow_Loaded;
        }

        private void KhachHangDataWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TxtTieuDe.Text = mode == DataMode.LichSuThue
                ? $"Lịch sử thuê phòng - {khachHang.HoTen}"
                : $"Danh sách hóa đơn - {khachHang.HoTen}";

            try
            {
                DataTable data = LoadData();
                DgData.ItemsSource = data.DefaultView;
                TxtThongBao.Text = data.Rows.Count == 0
                    ? "Không có dữ liệu phù hợp trong database."
                    : $"Tổng: {data.Rows.Count} dòng";
            }
            catch (Exception ex)
            {
                TxtThongBao.Text = "Không tải được dữ liệu: " + ex.Message;
            }
        }

        private DataTable LoadData()
        {
            string[] candidates = mode == DataMode.LichSuThue
                ? new[] { "PhieuThue", "PhieuDatPhong", "DatPhong", "ThuePhong", "Booking", "Bookings", "RentalHistory" }
                : new[] { "HoaDon", "HOADON", "Invoice", "Invoices", "ThanhToan", "Bill", "Bills" };

            TableMap? map = GetTableMaps()
                .FirstOrDefault(item => candidates.Any(name => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

            if (map == null)
            {
                return new DataTable();
            }

            string sql = $"SELECT * FROM [{map.Schema}].[{map.Name}]";
            List<SqlParameter> parameters = new();
            List<string> filters = new();

            string idColumn = FirstExisting(map.Columns, "MaKhachHang", "MaKH", "KhachHangID", "CustomerID", "MaKhach");
            if (!string.IsNullOrWhiteSpace(idColumn) && khachHang.Ma > 0)
            {
                filters.Add($"[{idColumn}] = @MaKhachHang");
                parameters.Add(new SqlParameter("@MaKhachHang", khachHang.Ma));
            }

            string nameColumn = FirstExisting(map.Columns, "HoTen", "TenKhachHang", "TenKH", "KhachHang", "CustomerName", "FullName");
            if (!string.IsNullOrWhiteSpace(nameColumn) && !string.IsNullOrWhiteSpace(khachHang.HoTen))
            {
                filters.Add($"[{nameColumn}] LIKE @TenKhachHang");
                parameters.Add(new SqlParameter("@TenKhachHang", "%" + khachHang.HoTen + "%"));
            }

            if (filters.Count > 0)
            {
                sql += " WHERE " + string.Join(" OR ", filters);
            }

            return ConnectDB.GetData(sql, parameters.ToArray());
        }

        private static List<TableMap> GetTableMaps()
        {
            DataTable columns = ConnectDB.GetData(
                @"SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME
                  FROM INFORMATION_SCHEMA.COLUMNS
                  ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION");

            return columns.AsEnumerable()
                .GroupBy(row => new
                {
                    Schema = row["TABLE_SCHEMA"].ToString() ?? "dbo",
                    Name = row["TABLE_NAME"].ToString() ?? string.Empty
                })
                .Select(group => new TableMap
                {
                    Schema = group.Key.Schema,
                    Name = group.Key.Name,
                    Columns = group
                        .Select(row => row["COLUMN_NAME"].ToString() ?? string.Empty)
                        .Where(column => !string.IsNullOrWhiteSpace(column))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)
                })
                .ToList();
        }

        private static string FirstExisting(HashSet<string> columns, params string[] candidates)
        {
            return candidates.FirstOrDefault(columns.Contains) ?? string.Empty;
        }

        private void BtnDong_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public enum DataMode
        {
            LichSuThue,
            HoaDon
        }

        private class TableMap
        {
            public string Schema { get; set; } = "dbo";
            public string Name { get; set; } = string.Empty;
            public HashSet<string> Columns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }
    }
}
