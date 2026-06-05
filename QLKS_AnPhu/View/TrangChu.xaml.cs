using System.Globalization;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DAL;

namespace QLKS_AnPhu.View
{
    public partial class TrangChu : UserControl
    {
        private readonly CultureInfo vietnameseCulture = new("vi-VN");

        public TrangChu()
        {
            InitializeComponent();
            Loaded += TrangChu_Loaded;
        }

        private void TrangChu_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            LoadDashboardData();
        }

        private void LoadDashboardData()
        {
            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);

            decimal doanhThuHomNay = ToDecimal(ConnectDB.ExecuteScalar(
                @"
SELECT ISNULL(SUM(TongThanhToan), 0)
FROM HOADON
WHERE NgayLap >= @TuNgay
  AND NgayLap < @DenNgay
  AND (TrangThai = N'Đã thanh toán' OR DaThanhToan > 0);",
                new SqlParameter("@TuNgay", today),
                new SqlParameter("@DenNgay", tomorrow)));

            int tongPhong = ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM PHONG"));
            int phongDangThue = ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM PHONG WHERE TrangThai IN (N'Có khách', N'Đã đặt')"));
            int phongTrong = ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM PHONG WHERE TrangThai = N'Phòng trống'"));
            int khachHang = ToInt(ConnectDB.ExecuteScalar("SELECT COUNT(*) FROM KHACHHANG"));
            decimal tyLeLapDay = tongPhong == 0 ? 0 : Math.Round(phongDangThue * 100m / tongPhong, 1);

            TxtDoanhThuHomNay.Text = doanhThuHomNay.ToString("N0", vietnameseCulture) + " đ";
            TxtPhongDangThue.Text = phongDangThue.ToString("N0", vietnameseCulture);
            TxtPhongTrong.Text = phongTrong.ToString("N0", vietnameseCulture);
            TxtKhachHang.Text = khachHang.ToString("N0", vietnameseCulture);
            TxtTyLeLapDay.Text = tyLeLapDay.ToString("N1", vietnameseCulture) + "%";
        }

        private static decimal ToDecimal(object? value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static int ToInt(object? value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }
    }
}
