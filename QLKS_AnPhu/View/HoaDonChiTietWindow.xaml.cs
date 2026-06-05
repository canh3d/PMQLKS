using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DAL;

namespace QLKS_AnPhu.View
{
    public partial class HoaDonChiTietWindow : Window
    {
        private readonly HoaDonItem hoaDon;
        private readonly ThanhToanFlowBUS thanhToanBUS = new();

        public bool DuLieuDaThayDoi { get; private set; }

        public HoaDonChiTietWindow(HoaDonItem hoaDon)
        {
            this.hoaDon = hoaDon;
            InitializeComponent();
            Loaded += HoaDonChiTietWindow_Loaded;
        }

        private void HoaDonChiTietWindow_Loaded(object sender, RoutedEventArgs e)
        {
            HienThiHoaDon();
        }

        private void HienThiHoaDon()
        {
            TxtMaHoaDon.Text = hoaDon.MaHoaDon;
            TxtKhachHang.Text = hoaDon.TenKhachHang;
            TxtPhong.Text = hoaDon.SoPhong;
            TxtNgayLap.Text = hoaDon.NgayLapText;
            TxtSdt.Text = hoaDon.SoDienThoai;
            TxtLoaiPhong.Text = hoaDon.LoaiPhong;
            TxtThoiGianThue.Text = hoaDon.NgayNhanPhong.ToString("dd/MM/yyyy HH:mm") + " - " + hoaDon.NgayTraPhong.ToString("dd/MM/yyyy HH:mm");
            TxtThoiLuong.Text = TinhThoiLuong(hoaDon.NgayNhanPhong, hoaDon.NgayTraPhong);
            LblTienPhong.Text = hoaDon.LoaiThanhToan == "PHATSINH"
                ? "Tiền gia hạn phòng:"
                : "Tiền phòng lúc check-in:";
            TxtTienPhong.Text = hoaDon.TienPhong.ToString("N0") + " VND";
            LblNhanSom.Text = hoaDon.LoaiThanhToan == "PHATSINH" ? "Nhận sớm:" : "Nhận sớm:";
            LblPhuPhi.Text = hoaDon.LoaiThanhToan == "PHATSINH" ? "Phụ phí trả muộn:" : "Phụ phí nhận sớm:";
            TxtNhanSom.Text = hoaDon.LoaiThanhToan == "PHATSINH"
                ? "Không áp dụng trong hóa đơn phát sinh"
                : TaoNoiDungNhanSom(hoaDon.NgayNhanThucTe, hoaDon.NgayNhanPhong, hoaDon.PhuPhi);
            TxtTraMuon.Text = hoaDon.LoaiThanhToan == "PHATSINH" && hoaDon.PhuPhi > 0
                ? "Có phụ phí trả muộn (" + hoaDon.PhuPhi.ToString("N0") + " VND)"
                : "Phụ phí trả muộn chỉ được chốt khi thực hiện trả phòng.";
            TxtPhuPhi.Text = hoaDon.PhuPhi.ToString("N0") + " VND";
            TxtTrangThai.Text = hoaDon.TrangThai;
            TxtThueVat.Text = hoaDon.ThueVat.ToString("N0") + " VND";
            TxtGiamGia.Text = hoaDon.GiamGia.ToString("N0") + " VND";
            TxtTongTien.Text = hoaDon.TongTien.ToString("N0") + " VND";
            DgDichVu.ItemsSource = LoadDichVu();
            BtnThanhToan.IsEnabled = !hoaDon.DaThanhToan && hoaDon.LoaiPhieu == "THUE";
        }

        private ObservableCollection<DichVuHoaDonItem> LoadDichVu()
        {
            ObservableCollection<DichVuHoaDonItem> result = new();
            string table = TableExists("PHATSINHDICHVU") ? "PHATSINHDICHVU" : TableExists("CHITIETPHATSINH") ? "CHITIETPHATSINH" : string.Empty;
            if (string.IsNullOrWhiteSpace(table) || !TableExists("DICHVUVATTU"))
            {
                return result;
            }

            string keyColumn = hoaDon.LoaiPhieu == "THUE" && ColumnExists(table, "MaThue") ? "MaThue" : ColumnExists(table, "MaDatPhong") ? "MaDatPhong" : string.Empty;
            if (string.IsNullOrWhiteSpace(keyColumn))
            {
                return result;
            }

            string tenDichVu = ColumnExists("DICHVUVATTU", "TenDVVT") ? "TenDVVT" : "TenDichVu";
            string maDvPs = ColumnExists(table, "MaDVVT") ? "MaDVVT" : "MaDichVu";
            string maDv = ColumnExists("DICHVUVATTU", "MaDVVT") ? "MaDVVT" : "MaDichVu";
            string soLuong = ColumnExists(table, "SoLuong") ? "PS.SoLuong" : "1";
            string donGia = ColumnExists(table, "DonGia") ? "ISNULL(PS.DonGia, DV.DonGia)" : "DV.DonGia";
            string thanhTien = ColumnExists(table, "ThanhTien") ? "PS.ThanhTien" : "(" + soLuong + " * " + donGia + ")";

            DataTable data = ConnectDB.GetData(
                @"SELECT DV." + tenDichVu + @" AS TenDichVu,
                         " + soLuong + @" AS SoLuong,
                         " + donGia + @" AS DonGia,
                         " + thanhTien + @" AS ThanhTien
                  FROM dbo." + table + @" PS
                  JOIN dbo.DICHVUVATTU DV ON PS." + maDvPs + " = DV." + maDv + @"
                  WHERE PS." + keyColumn + " = @Ma",
                new SqlParameter("@Ma", hoaDon.MaGoc));

            int stt = 1;
            foreach (DataRow row in data.Rows)
            {
                result.Add(new DichVuHoaDonItem
                {
                    Stt = stt++,
                    TenDichVu = row["TenDichVu"]?.ToString() ?? string.Empty,
                    SoLuong = GetDecimal(row, "SoLuong"),
                    DonGia = GetDecimal(row, "DonGia"),
                    ThanhTien = GetDecimal(row, "ThanhTien")
                });
            }

            return result;
        }

        private void BtnThanhToan_Click(object sender, RoutedEventArgs e)
        {
            if (hoaDon.DaThanhToan || hoaDon.LoaiPhieu != "THUE")
            {
                return;
            }

            if (MessageBox.Show("Xác nhận thanh toán hóa đơn " + hoaDon.MaHoaDon + "?", "Thanh toán", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                thanhToanBUS.CheckOut(hoaDon.MaGoc);
                DuLieuDaThayDoi = true;
                MessageBox.Show("Đã thanh toán hóa đơn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể thanh toán hóa đơn: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnInHoaDon_Click(object sender, RoutedEventArgs e)
        {
            HoaDonPrintWindow window = new(hoaDon);
            DialogService.ShowDimmedDialogResult(window, this);
        }

        private void BtnDong_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static void ThanhToanPhieuThue(int maThue)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();
            try
            {
                string setNgayTra = ColumnExists("PHIEUTHUE", "NgayTraPhong") ? ", NgayTraPhong = ISNULL(NgayTraPhong, @NgayTra)" : string.Empty;
                using (SqlCommand cmd = new("UPDATE dbo.PHIEUTHUE SET TrangThai = @TrangThai" + setNgayTra + " WHERE MaThue = @Ma", conn, tran))
                {
                    cmd.Parameters.AddWithValue("@TrangThai", "Đã trả phòng");
                    cmd.Parameters.AddWithValue("@NgayTra", DateTime.Now);
                    cmd.Parameters.AddWithValue("@Ma", maThue);
                    cmd.ExecuteNonQuery();
                }

                CapNhatTrangThaiPhongTheoNhomThue(conn, tran, maThue, "Chưa dọn dẹp");

                using (SqlCommand cmd = new(
                           @"UPDATE P
                             SET P.TrangThai = @TrangThaiPhong
                             FROM dbo.PHONG P
                             JOIN dbo.PHIEUTHUE PT ON P.MaPhong = PT.MaPhong
                             WHERE PT.MaThue = @Ma",
                           conn,
                           tran))
                {
                    cmd.Parameters.AddWithValue("@TrangThaiPhong", "Chưa dọn dẹp");
                    cmd.Parameters.AddWithValue("@Ma", maThue);
                    cmd.ExecuteNonQuery();
                }

                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        private static void CapNhatTrangThaiPhongTheoNhomThue(SqlConnection conn, SqlTransaction tran, int maThue, string trangThaiPhong)
        {
            string bangDatPhong = TableExists("PHIEUDATPHONG") ? "PHIEUDATPHONG" : TableExists("DATPHONG") ? "DATPHONG" : string.Empty;
            if (ColumnExists("PHIEUTHUE", "MaDatPhong") && !string.IsNullOrWhiteSpace(bangDatPhong) && ColumnExists(bangDatPhong, "MaPhong"))
            {
                string ngayNhanColumn = ColumnExists(bangDatPhong, "NgayNhanDuKien") ? "NgayNhanDuKien" : "NgayNhanPhong";
                string ngayTraColumn = ColumnExists(bangDatPhong, "NgayTraDuKien") ? "NgayTraDuKien" : "NgayTraPhong";
                using SqlCommand cmd = new(
                    @"UPDATE P
                      SET P.TrangThai = @TrangThai
                      FROM dbo.PHIEUTHUE PT
                      JOIN dbo." + bangDatPhong + @" DP0 ON PT.MaDatPhong = DP0.MaDatPhong
                      JOIN dbo." + bangDatPhong + @" DPG ON DPG.MaKH = DP0.MaKH
                         AND CONVERT(date, DPG." + ngayNhanColumn + @") = CONVERT(date, DP0." + ngayNhanColumn + @")
                         AND CONVERT(date, DPG." + ngayTraColumn + @") = CONVERT(date, DP0." + ngayTraColumn + @")
                      JOIN dbo.PHONG P ON DPG.MaPhong = P.MaPhong
                      WHERE PT.MaThue = @Ma",
                    conn,
                    tran);
                cmd.Parameters.AddWithValue("@TrangThai", trangThaiPhong);
                cmd.Parameters.AddWithValue("@Ma", maThue);
                cmd.ExecuteNonQuery();
                return;
            }

            if (ColumnExists("PHIEUTHUE", "MaDatPhong") && TableExists("CHITIETDATPHONG"))
            {
                using SqlCommand cmd = new(
                    @"UPDATE P
                      SET P.TrangThai = @TrangThai
                      FROM dbo.PHIEUTHUE PT
                      JOIN dbo.CHITIETDATPHONG CT ON PT.MaDatPhong = CT.MaDatPhong
                      JOIN dbo.PHONG P ON CT.MaPhong = P.MaPhong
                      WHERE PT.MaThue = @Ma",
                    conn,
                    tran);
                cmd.Parameters.AddWithValue("@TrangThai", trangThaiPhong);
                cmd.Parameters.AddWithValue("@Ma", maThue);
                cmd.ExecuteNonQuery();
            }
        }

        private static string TinhThoiLuong(DateTime start, DateTime end)
        {
            if (end <= start)
            {
                return "1 ngày";
            }

            if (start.Date == end.Date)
            {
                return Math.Max(1, (int)Math.Ceiling((end - start).TotalHours)) + " giờ";
            }

            if ((end - start).TotalHours <= 12)
            {
                return "1 ngày";
            }

            return Math.Max(1, (int)Math.Ceiling((end - start).TotalDays)) + " ngày";
        }

        private static string TaoNoiDungNhanSom(DateTime? actualStart, DateTime plannedStart, decimal fee)
        {
            if (!actualStart.HasValue)
            {
                return "Chưa nhận phòng (0 VND)";
            }

            int earlyMinutes = Math.Max(0, (int)Math.Round((plannedStart - actualStart.Value).TotalMinutes));
            if (earlyMinutes == 0)
            {
                return "Nhận đúng hoặc sau giờ đặt (0 VND)";
            }

            string duration = earlyMinutes >= 60
                ? (earlyMinutes / 60) + " giờ " + (earlyMinutes % 60) + " phút"
                : earlyMinutes + " phút";
            return "Nhận sớm " + duration + " (" + fee.ToString("N0") + " VND)";
        }

        private static decimal GetDecimal(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) && decimal.TryParse(row[column]?.ToString(), out decimal value) ? value : 0;
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
    }

    public class DichVuHoaDonItem
    {
        public int Stt { get; init; }
        public int? MaPhong { get; init; }
        public string TenDichVu { get; init; } = string.Empty;
        public decimal SoLuong { get; init; }
        public decimal DonGia { get; init; }
        public decimal ThanhTien { get; init; }
        public string DonGiaText => DonGia.ToString("N0");
        public string ThanhTienText => ThanhTien.ToString("N0");
    }
}
