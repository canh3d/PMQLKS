using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.View
{
    internal static class CheckInPhuPhiHelper
    {
        public static CheckInPhuPhiResult Tinh(PhongDTO phong, DateTime? ngayNhanDuKien, DateTime? ngayTraDuKien, DateTime ngayNhanThucTe, decimal tienPhongDaChot = 0, string? cheDoDatPhong = null)
        {
            if (!ngayNhanDuKien.HasValue || ngayNhanThucTe >= ngayNhanDuKien.Value)
            {
                return new CheckInPhuPhiResult(0, "Phụ phí nhận sớm");
            }

            int soPhutSom = Math.Max(0, (int)Math.Ceiling((ngayNhanDuKien.Value - ngayNhanThucTe).TotalMinutes));
            if (soPhutSom <= 30)
            {
                return new CheckInPhuPhiResult(0, $"Phụ phí nhận sớm ({soPhutSom} phút - miễn phí trong 30 phút)");
            }

            int soPhutTinhPhi = soPhutSom - 30;
            int soGioTinhPhi = Math.Max(1, (int)Math.Ceiling(soPhutTinhPhi / 60.0));
            bool theoGio = LaTheoGio(phong, ngayNhanDuKien.Value, ngayTraDuKien, tienPhongDaChot, cheDoDatPhong);
            bool quaDem = LaQuaDem(ngayNhanDuKien.Value, ngayTraDuKien, cheDoDatPhong) ||
                           ngayTraDuKien.HasValue &&
                           ngayTraDuKien.Value.Date == ngayNhanDuKien.Value.Date.AddDays(1) &&
                           ngayNhanDuKien.Value.TimeOfDay >= TimeSpan.FromHours(20) &&
                           ngayTraDuKien.Value.TimeOfDay <= TimeSpan.FromHours(8.5);

            decimal giaGioTinhPhi = theoGio
                ? phong.GiaGio > 0 ? phong.GiaGio : Math.Round(LayGiaNgayMacDinh(phong) / 24m, 0)
                : quaDem
                    ? Math.Round(LayGiaDemMacDinh(phong) / 12m, 0)
                    : Math.Round(LayGiaNgayMacDinh(phong) / 24m, 0);
            decimal phuPhi = Math.Round(soGioTinhPhi * giaGioTinhPhi, 0);

            return new CheckInPhuPhiResult(
                phuPhi,
                $"Phụ phí nhận sớm ({soGioTinhPhi} giờ x giá giờ {giaGioTinhPhi:N0})");
        }

        private static bool LaTheoGio(PhongDTO phong, DateTime ngayNhanDuKien, DateTime? ngayTraDuKien, decimal tienPhongDaChot, string? cheDoDatPhong)
        {
            string mode = BoDau(cheDoDatPhong ?? string.Empty).ToLowerInvariant();
            if (mode.Contains("theo gio") || mode.Contains("gio"))
            {
                return true;
            }

            if (!ngayTraDuKien.HasValue)
            {
                return false;
            }

            if (ngayNhanDuKien.Date == ngayTraDuKien.Value.Date)
            {
                return true;
            }

            decimal giaGio = phong.GiaGio > 0 ? phong.GiaGio : 0;
            if (giaGio <= 0 || tienPhongDaChot <= 0)
            {
                return false;
            }

            int soGioThue = Math.Max(1, (int)Math.Ceiling((ngayTraDuKien.Value - ngayNhanDuKien).TotalHours));
            decimal tienTheoGio = soGioThue * giaGio;
            return Math.Abs(tienPhongDaChot - tienTheoGio) < 1m;
        }

        private static bool LaQuaDem(DateTime ngayNhanDuKien, DateTime? ngayTraDuKien, string? cheDoDatPhong)
        {
            string mode = BoDau(cheDoDatPhong ?? string.Empty).ToLowerInvariant();
            if (mode.Contains("dem"))
            {
                return true;
            }

            return ngayTraDuKien.HasValue &&
                   ngayTraDuKien.Value.Date == ngayNhanDuKien.Date.AddDays(1) &&
                   ngayNhanDuKien.TimeOfDay >= TimeSpan.FromHours(20) &&
                   ngayTraDuKien.Value.TimeOfDay <= TimeSpan.FromHours(8.5);
        }

        private static string BoDau(string value)
        {
            string formD = value.Normalize(System.Text.NormalizationForm.FormD);
            char[] chars = formD
                .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray();
            return new string(chars).Normalize(System.Text.NormalizationForm.FormC);
        }

        private static decimal LayGiaMacDinh(PhongDTO phong)
        {
            if (phong.GiaNgay > 0) return phong.GiaNgay;
            if (phong.GiaDem > 0) return phong.GiaDem;
            if (phong.GiaGio > 0) return phong.GiaGio;
            return phong.GiaPhong;
        }

        private static decimal LayGiaNgayMacDinh(PhongDTO phong)
        {
            if (phong.GiaNgay > 0) return phong.GiaNgay;
            if (phong.GiaDem > 0) return phong.GiaDem;
            if (phong.GiaGio > 0) return phong.GiaGio * 24m;
            return phong.GiaPhong;
        }

        private static decimal LayGiaDemMacDinh(PhongDTO phong)
        {
            if (phong.GiaDem > 0) return phong.GiaDem;
            if (phong.GiaNgay > 0) return phong.GiaNgay;
            if (phong.GiaGio > 0) return phong.GiaGio * 12m;
            return phong.GiaPhong;
        }
    }

    internal sealed record CheckInPhuPhiResult(decimal SoTien, string MoTa);
}
