using System.Globalization;
using System.Windows;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.View
{
    public partial class GiaHanPhongWindow : Window
    {
        private readonly GiaHanPhongRequestDTO request;
        private readonly PhongThueOperationBUS bus = new();
        private bool isUpdatingFields;

        public bool DuLieuDaThayDoi { get; private set; }

        public GiaHanPhongWindow(GiaHanPhongRequestDTO request, string soPhong)
        {
            this.request = request;
            InitializeComponent();

            TxtPhong.Text = "Phòng " + soPhong;
            TxtNgayTraCu.Text = FormatDateTime(request.NgayTraCu);
            DateTime baseTime = GetExtensionBaseTime();
            DpNgayTraMoi.DisplayDateStart = baseTime.Date;
            SetReturnDateTime(baseTime.AddHours(1));
        }

        private void QuickExtend_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element ||
                !double.TryParse(Convert.ToString(element.Tag, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out double hours))
            {
                return;
            }

            SetReturnDateTime(GetExtensionBaseTime().AddHours(hours));
        }

        private void NgayTraMoi_Changed(object sender, RoutedEventArgs e)
        {
            if (!isUpdatingFields)
            {
                UpdatePreview();
            }
        }

        private void TxtGioTraMoi_LostFocus(object sender, RoutedEventArgs e)
        {
            if (TryGetSelectedReturn(out DateTime value))
            {
                TxtGioTraMoi.Text = value.ToString("HH:mm", CultureInfo.InvariantCulture);
            }
        }

        private void BtnXacNhan_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedReturn(out DateTime ngayTraMoi))
            {
                MessageBox.Show("Vui lòng nhập ngày trả mới và giờ trả mới theo định dạng HH:mm.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ngayTraMoi <= request.NgayTraCu)
            {
                MessageBox.Show("Giờ trả mới phải sau giờ trả hiện tại.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (request.NgayTraCu < DateTime.Now && ngayTraMoi <= DateTime.Now)
            {
                MessageBox.Show("Phòng đã quá giờ trả, giờ trả mới phải sau thời điểm hiện tại.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            request.NgayTraMoi = ngayTraMoi;
            if (MessageBox.Show("Xác nhận gia hạn phòng đến " + request.NgayTraMoi.ToString("dd/MM/yyyy HH:mm") + "?", "Gia hạn phòng", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                bus.GiaHan(request);
                DuLieuDaThayDoi = true;
                MessageBox.Show("Đã gia hạn phòng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể gia hạn phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDong_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SetReturnDateTime(DateTime value)
        {
            isUpdatingFields = true;
            DpNgayTraMoi.SelectedDate = value.Date;
            TxtGioTraMoi.Text = value.ToString("HH:mm", CultureInfo.InvariantCulture);
            isUpdatingFields = false;

            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (!TryGetSelectedReturn(out DateTime ngayTraMoi))
            {
                TxtNgayTraMoiPreview.Text = "--/--/---- --:--";
                TxtPreview.Text = "Chưa đủ thông tin ngày giờ trả mới.";
                TxtTienGiaHan.Text = string.Empty;
                TxtWarning.Text = "Giờ trả mới cần nhập theo định dạng HH:mm, ví dụ 14:30.";
                BtnXacNhan.IsEnabled = false;
                return;
            }

            TxtNgayTraMoiPreview.Text = FormatDateTime(ngayTraMoi);
            if (ngayTraMoi <= request.NgayTraCu)
            {
                TxtPreview.Text = "Thời gian gia hạn chưa hợp lệ.";
                TxtTienGiaHan.Text = string.Empty;
                TxtWarning.Text = "Giờ trả mới phải sau giờ trả hiện tại.";
                BtnXacNhan.IsEnabled = false;
                return;
            }

            if (request.NgayTraCu < DateTime.Now && ngayTraMoi <= DateTime.Now)
            {
                TxtPreview.Text = "Thời gian gia hạn chưa hợp lệ.";
                TxtTienGiaHan.Text = string.Empty;
                TxtWarning.Text = "Phòng đã quá giờ trả, giờ trả mới phải sau thời điểm hiện tại.";
                BtnXacNhan.IsEnabled = false;
                return;
            }

            TimeSpan duration = ngayTraMoi - request.NgayTraCu;
            TxtPreview.Text = "Gia hạn thêm " + FormatDuration(duration) + MoTaSoPhongGiaHan() + ".";
            TxtTienGiaHan.Text = "Tiền gia hạn dự kiến: " + TinhTienGiaHan(request.NgayTraCu, ngayTraMoi).ToString("N0", CultureInfo.InvariantCulture) + " VND";
            TxtWarning.Text = "Tiền gia hạn sẽ được tính tự động khi xác nhận.";
            BtnXacNhan.IsEnabled = true;
        }

        private decimal TinhTienGiaHan(DateTime start, DateTime end)
        {
            if (end <= start)
            {
                return 0;
            }

            decimal giaGio = request.GiaGio;
            decimal giaNgay = request.GiaNgay > 0
                ? request.GiaNgay
                : request.GiaDem > 0
                    ? request.GiaDem
                    : request.GiaGio * 24m;
            decimal giaDem = request.GiaDem > 0 ? request.GiaDem : giaNgay;

            bool quaDem = end.Date == start.Date.AddDays(1) &&
                           start.TimeOfDay >= TimeSpan.FromHours(21) &&
                           end.TimeOfDay <= TimeSpan.FromHours(8.5);
            if (quaDem)
            {
                return Math.Round(giaDem * SoPhongGiaHan(), 0);
            }

            if (start.Date == end.Date)
            {
                int hours = Math.Max(1, (int)Math.Ceiling((end - start).TotalMinutes / 60.0));
                return Math.Round(hours * giaGio * SoPhongGiaHan(), 0);
            }

            int days = Math.Max(1, (end.Date - start.Date).Days);
            return Math.Round(days * giaNgay * SoPhongGiaHan(), 0);
        }

        private int SoPhongGiaHan()
        {
            return Math.Max(1, request.SoPhongGiaHan);
        }

        private string MoTaSoPhongGiaHan()
        {
            int soPhong = SoPhongGiaHan();
            return soPhong > 1 ? $" cho {soPhong} phòng" : string.Empty;
        }
        private DateTime GetExtensionBaseTime()
        {
            DateTime now = DateTime.Now;
            return now > request.NgayTraCu ? now : request.NgayTraCu;
        }

        private bool TryGetSelectedReturn(out DateTime value)
        {
            value = default;
            if (!DpNgayTraMoi.SelectedDate.HasValue)
            {
                return false;
            }

            string input = TxtGioTraMoi.Text.Trim();
            string[] formats = { @"h\:mm", @"hh\:mm" };
            if (!TimeSpan.TryParseExact(input, formats, CultureInfo.InvariantCulture, out TimeSpan time))
            {
                return false;
            }

            value = DpNgayTraMoi.SelectedDate.Value.Date.Add(time);
            return true;
        }

        private static string FormatDateTime(DateTime value)
        {
            return value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            int days = duration.Days;
            int hours = duration.Hours;
            int minutes = duration.Minutes;
            List<string> parts = new();

            if (days > 0)
            {
                parts.Add(days + " ngày");
            }

            if (hours > 0)
            {
                parts.Add(hours + " giờ");
            }

            if (minutes > 0)
            {
                parts.Add(minutes + " phút");
            }

            return parts.Count == 0 ? "0 phút" : string.Join(" ", parts);
        }
    }
}

