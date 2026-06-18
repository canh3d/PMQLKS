using System.Globalization;
using System.Text;

namespace QLKS_AnPhu.View
{
    internal static class ThoiLuongThueHelper
    {
        public static string DinhDang(DateTime ngayNhan, DateTime ngayTra, string? cheDoDatPhong, decimal tienPhong, decimal giaGio)
        {
            if (ngayTra <= ngayNhan)
            {
                return "1 giờ";
            }

            string cheDo = BoDau(cheDoDatPhong ?? string.Empty).ToLowerInvariant();
            double tongGio = (ngayTra - ngayNhan).TotalHours;
            int soGio = Math.Max(1, (int)Math.Ceiling(tongGio));
            bool theoGio = cheDo.Contains("gio") ||
                           (giaGio > 0 && tongGio <= 24 && Math.Abs(tienPhong - (soGio * giaGio)) < 1m);
            if (theoGio)
            {
                return soGio + " giờ";
            }

            bool quaDem = cheDo.Contains("qua dem") ||
                          (ngayTra.Date == ngayNhan.Date.AddDays(1) &&
                           ngayNhan.TimeOfDay >= TimeSpan.FromHours(20) &&
                           ngayTra.TimeOfDay <= TimeSpan.FromHours(8.5));
            if (quaDem)
            {
                int soDem = Math.Max(1, (ngayTra.Date - ngayNhan.Date).Days);
                return soDem + " đêm";
            }

            int soNgay = Math.Max(1, (ngayTra.Date - ngayNhan.Date).Days);
            return soNgay + " ngày";
        }

        private static string BoDau(string value)
        {
            string formD = value.Normalize(NormalizationForm.FormD);
            char[] chars = formD
                .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                .ToArray();
            return new string(chars).Normalize(NormalizationForm.FormC);
        }
    }
}
