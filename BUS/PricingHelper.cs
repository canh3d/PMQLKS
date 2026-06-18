namespace QLKS_AnPhu.BUS
{
    public static class PricingHelper
    {
        public static string TienPhongSql(string startExpr, string plannedEndExpr, string actualEndExpr)
        {
            string giaNgayExpr = "ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0))";
            string giaGioExpr = "ISNULL(LP.DonGiaGio, 0)";
            return TienPhongSql(startExpr, plannedEndExpr, actualEndExpr, giaNgayExpr, giaGioExpr);
        }

        public static string TienPhongSql(string startExpr, string plannedEndExpr, string actualEndExpr, string giaNgayExpr, string giaGioExpr)
        {
            return @"CAST(CASE
    WHEN " + plannedEndExpr + @" IS NULL OR DATEDIFF(minute, " + startExpr + @", " + plannedEndExpr + @") <= 0 THEN " + giaNgayExpr + @"
    WHEN CAST(" + startExpr + @" AS date) = CAST(" + plannedEndExpr + @" AS date) THEN
        CEILING(DATEDIFF(minute, " + startExpr + @", " + plannedEndExpr + @") / 60.0) * " + giaGioExpr + @"
    WHEN DATEDIFF(hour, " + startExpr + @", " + plannedEndExpr + @") <= 12 THEN
        " + giaNgayExpr + @"
    ELSE
        CASE WHEN DATEDIFF(day, CAST(" + startExpr + @" AS date), CAST(" + plannedEndExpr + @" AS date)) <= 0 THEN 1
             ELSE DATEDIFF(day, CAST(" + startExpr + @" AS date), CAST(" + plannedEndExpr + @" AS date))
        END * " + giaNgayExpr + @"
END AS decimal(18, 2))";
        }

        public static string PhuThuSql(string startExpr, string plannedEndExpr, string actualEndExpr)
        {
            string giaNgayExpr = "ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0))";
            string giaGioExpr = "ISNULL(NULLIF(LP.DonGiaGio, 0), " + giaNgayExpr + " / 24.0)";
            string giaDemExpr = "ISNULL(NULLIF(LP.DonGiaDem, 0), " + giaNgayExpr + ")";
            return PhuThuSql(startExpr, plannedEndExpr, actualEndExpr, giaNgayExpr, giaGioExpr, giaDemExpr);
        }

        public static string PhuThuNhanSomSql(string actualStartExpr, string plannedStartExpr)
        {
            string giaNgayExpr = "ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0))";
            string giaGioExpr = "ISNULL(NULLIF(LP.DonGiaGio, 0), " + giaNgayExpr + " / 24.0)";
            return PhuThuNhanSomSql(actualStartExpr, plannedStartExpr, giaNgayExpr, giaGioExpr);
        }

        public static string PhuThuNhanSomSql(string actualStartExpr, string plannedStartExpr, string giaNgayExpr)
        {
            string giaGioExpr = "(" + giaNgayExpr + " / 24.0)";
            return PhuThuNhanSomSql(actualStartExpr, plannedStartExpr, giaNgayExpr, giaGioExpr);
        }

        public static string PhuThuNhanSomSql(string actualStartExpr, string plannedStartExpr, string giaNgayExpr, string giaGioExpr)
        {
            return @"CAST(CASE
    WHEN " + actualStartExpr + @" IS NULL OR " + plannedStartExpr + @" IS NULL THEN 0
    WHEN " + actualStartExpr + @" >= DATEADD(minute, -30, " + plannedStartExpr + @") THEN 0
    ELSE CEILING((DATEDIFF(minute, " + actualStartExpr + @", " + plannedStartExpr + @") - 30) / 60.0) * " + giaGioExpr + @"
END AS decimal(18, 2))";
        }

        public static string PhuThuNhanSomSql(string actualStartExpr, string plannedStartExpr, string plannedEndExpr, string giaNgayExpr, string giaGioExpr, string giaDemExpr)
        {
            string laThueTheoGioExpr = LaThueTheoGioSql(plannedStartExpr, plannedEndExpr);
            string laThueQuaDemExpr = LaThueQuaDemSql(plannedStartExpr, plannedEndExpr);
            string giaTheoLoaiExpr = @"CASE
        WHEN " + laThueTheoGioExpr + @" THEN " + giaGioExpr + @"
        WHEN " + laThueQuaDemExpr + @" THEN (" + giaDemExpr + @") / 12.0
        ELSE (" + giaNgayExpr + @") / 24.0
    END";

            return @"CAST(CASE
    WHEN " + actualStartExpr + @" IS NULL OR " + plannedStartExpr + @" IS NULL THEN 0
    WHEN " + actualStartExpr + @" >= DATEADD(minute, -30, " + plannedStartExpr + @") THEN 0
    ELSE CEILING((DATEDIFF(minute, " + actualStartExpr + @", " + plannedStartExpr + @") - 30) / 60.0) * " + giaTheoLoaiExpr + @"
END AS decimal(18, 2))";
        }

        public static string PhuThuTraMuonSql(string startExpr, string plannedEndExpr, string actualEndExpr)
        {
            string giaNgayExpr = "ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0))";
            string giaGioExpr = "ISNULL(NULLIF(LP.DonGiaGio, 0), " + giaNgayExpr + " / 24.0)";
            string giaDemExpr = "ISNULL(NULLIF(LP.DonGiaDem, 0), " + giaNgayExpr + ")";
            return PhuThuTraMuonSql(startExpr, plannedEndExpr, actualEndExpr, giaNgayExpr, giaGioExpr, giaDemExpr);
        }

        public static string PhuThuTraMuonSql(string startExpr, string plannedEndExpr, string actualEndExpr, string giaNgayExpr, string giaGioExpr)
        {
            return PhuThuTraMuonSql(startExpr, plannedEndExpr, actualEndExpr, giaNgayExpr, giaGioExpr, giaNgayExpr);
        }

        public static string PhuThuTraMuonSql(string startExpr, string plannedEndExpr, string actualEndExpr, string giaNgayExpr, string giaGioExpr, string giaDemExpr)
        {
            return @"CAST(CASE
WHEN " + actualEndExpr + @" IS NULL OR " + plannedEndExpr + @" IS NULL OR " + actualEndExpr + @" <= " + plannedEndExpr + @" THEN 0
WHEN DATEDIFF(minute, " + plannedEndExpr + @", " + actualEndExpr + @") <= 30 THEN 0
ELSE CEILING((DATEDIFF(minute, " + plannedEndExpr + @", " + actualEndExpr + @") - 30) / 60.0) *
     CASE
        WHEN " + LaThueTheoGioSql(startExpr, plannedEndExpr) + @" THEN " + giaGioExpr + @"
        WHEN " + LaThueQuaDemSql(startExpr, plannedEndExpr) + @" THEN (" + giaDemExpr + @") / 12.0
        ELSE (" + giaNgayExpr + @") / 24.0
     END
END AS decimal(18, 2))";
        }

        public static string PhuThuSql(string startExpr, string plannedEndExpr, string actualEndExpr, string giaNgayExpr, string giaGioExpr)
        {
            return PhuThuSql(startExpr, plannedEndExpr, actualEndExpr, giaNgayExpr, giaGioExpr, giaNgayExpr);
        }

        public static string PhuThuSql(string startExpr, string plannedEndExpr, string actualEndExpr, string giaNgayExpr, string giaGioExpr, string giaDemExpr)
        {
            string laThueTheoGioExpr = LaThueTheoGioSql(startExpr, plannedEndExpr);
            string laThueQuaDemExpr = LaThueQuaDemSql(startExpr, plannedEndExpr);
            string giaTheoLoaiExpr = @"CASE
        WHEN " + laThueTheoGioExpr + @" THEN " + giaGioExpr + @"
        WHEN " + laThueQuaDemExpr + @" THEN (" + giaDemExpr + @") / 12.0
        ELSE (" + giaNgayExpr + @") / 24.0
    END";

            return @"CAST(CASE
WHEN " + laThueTheoGioExpr + @" THEN
    CASE
        WHEN " + actualEndExpr + @" IS NULL THEN 0
        WHEN " + actualEndExpr + @" <= " + plannedEndExpr + @" THEN 0
        WHEN DATEDIFF(minute, " + plannedEndExpr + @", " + actualEndExpr + @") <= 30 THEN 0
        ELSE CEILING((DATEDIFF(minute, " + plannedEndExpr + @", " + actualEndExpr + @") - 30) / 60.0) * " + giaTheoLoaiExpr + @"
    END
WHEN " + laThueQuaDemExpr + @" THEN
    CASE
        WHEN " + actualEndExpr + @" IS NULL THEN 0
        WHEN " + actualEndExpr + @" <= " + plannedEndExpr + @" THEN 0
        WHEN DATEDIFF(minute, " + plannedEndExpr + @", " + actualEndExpr + @") <= 30 THEN 0
        ELSE CEILING((DATEDIFF(minute, " + plannedEndExpr + @", " + actualEndExpr + @") - 30) / 60.0) * " + giaTheoLoaiExpr + @"
    END
ELSE (
    CASE
        WHEN " + actualEndExpr + @" IS NULL THEN 0
        WHEN " + actualEndExpr + @" <= " + plannedEndExpr + @" THEN 0
        WHEN DATEDIFF(minute, " + plannedEndExpr + @", " + actualEndExpr + @") <= 30 THEN 0
        ELSE CEILING((DATEDIFF(minute, " + plannedEndExpr + @", " + actualEndExpr + @") - 30) / 60.0) * " + giaTheoLoaiExpr + @"
    END
) END AS decimal(18, 2))";
        }

        public static decimal TinhTienPhong(DateTime start, DateTime plannedEnd, decimal giaGio, decimal giaNgay)
        {
            double totalMinutes = Math.Max(0, (plannedEnd - start).TotalMinutes);
            if (totalMinutes <= 0)
            {
                return giaNgay;
            }

            if (start.Date == plannedEnd.Date)
            {
                int hours = Math.Max(1, (int)Math.Ceiling(totalMinutes / 60.0));
                return hours * giaGio;
            }

            if ((plannedEnd - start).TotalHours <= 12)
            {
                return giaNgay;
            }

            int days = Math.Max(1, (plannedEnd.Date - start.Date).Days);
            return days * giaNgay;
        }

        public static decimal TinhPhuThu(DateTime start, DateTime plannedEnd, DateTime actualEnd, decimal giaGio, decimal giaNgay)
        {
            return TinhPhuThuNhanSom(start, giaGio, giaNgay) +
                   TinhPhuThuTraMuon(start, plannedEnd, actualEnd, giaGio, giaNgay);
        }

        public static decimal TinhPhuThuNhanSom(DateTime actualStart, DateTime plannedStart, decimal giaGio, decimal giaNgay)
        {
            if (actualStart >= plannedStart.AddMinutes(-30)) return 0;
            return TinhPhuThuNhanSom(actualStart, giaGio, giaNgay);
        }

        public static decimal TinhPhuThuNhanSom(DateTime actualStart, DateTime plannedStart, DateTime plannedEnd, decimal giaGio, decimal giaNgay, decimal giaDem)
        {
            if (actualStart >= plannedStart) return 0;
            int soPhutSom = Math.Max(0, (int)Math.Ceiling((plannedStart - actualStart).TotalMinutes));
            if (soPhutSom <= 30) return 0;

            int soGioTinhPhi = Math.Max(1, (int)Math.Ceiling((soPhutSom - 30) / 60.0));
            return Math.Round(soGioTinhPhi * LayGiaGioPhuThuTheoLoai(plannedStart, plannedEnd, giaGio, giaNgay, giaDem), 0);
        }

        private static decimal TinhPhuThuNhanSom(DateTime start, decimal giaGio, decimal giaNgay)
        {
            TimeSpan gioNhan = start.TimeOfDay;
            if (gioNhan >= TimeSpan.FromHours(13.5)) return 0;

            DateTime mocNhanPhong = start.Date.AddHours(14);
            int soPhutTinhPhi = Math.Max(0, (int)Math.Ceiling((mocNhanPhong - start).TotalMinutes) - 30);
            if (soPhutTinhPhi <= 0) return 0;

            int soGioTinhPhi = Math.Max(1, (int)Math.Ceiling(soPhutTinhPhi / 60.0));
            return Math.Round(soGioTinhPhi * LayGiaGioPhuThu(giaGio, giaNgay), 0);
        }

        public static decimal TinhPhuThuTraMuon(DateTime start, DateTime plannedEnd, DateTime actualEnd, decimal giaGio, decimal giaNgay)
        {
            return TinhPhuThuTraMuon(start, plannedEnd, actualEnd, giaGio, giaNgay, 0);
        }

        public static decimal TinhPhuThuTraMuon(DateTime start, DateTime plannedEnd, DateTime actualEnd, decimal giaGio, decimal giaNgay, decimal giaDem)
        {
            if (actualEnd <= plannedEnd) return 0;
            int soPhutTre = Math.Max(0, (int)Math.Ceiling((actualEnd - plannedEnd).TotalMinutes));
            if (soPhutTre <= 30) return 0;

            int soGioTinhPhi = Math.Max(1, (int)Math.Ceiling((soPhutTre - 30) / 60.0));
            return Math.Round(soGioTinhPhi * LayGiaGioPhuThuTheoLoai(start, plannedEnd, giaGio, giaNgay, giaDem), 0);
        }

        private static decimal TinhPhuThuTraMuon(DateTime plannedEnd, DateTime actualEnd, decimal giaGio, decimal giaNgay)
        {
            DateTime mocTraPhong = plannedEnd.Date.AddHours(12);
            if (actualEnd <= mocTraPhong.AddMinutes(30)) return 0;

            decimal giaNgayTinhPhi = LayGiaNgayPhuThu(giaGio, giaNgay);
            if (actualEnd > plannedEnd.Date.AddHours(18)) return Math.Round(giaNgayTinhPhi, 0);
            if (actualEnd >= plannedEnd.Date.AddHours(15)) return Math.Round(giaNgayTinhPhi * 0.50m, 0);
            return Math.Round(giaNgayTinhPhi * 0.30m, 0);
        }

        private static bool LaThueQuaDem(DateTime start, DateTime plannedEnd)
        {
            return plannedEnd.Date == start.Date.AddDays(1) &&
                   start.TimeOfDay >= TimeSpan.FromHours(21) &&
                   plannedEnd.TimeOfDay <= TimeSpan.FromHours(8.5);
        }

        private static decimal LayGiaGioPhuThu(decimal giaGio, decimal giaNgay)
        {
            if (giaGio > 0) return giaGio;
            if (giaNgay > 0) return Math.Round(giaNgay / 24m, 0);
            return 0;
        }

        private static decimal LayGiaNgayPhuThu(decimal giaGio, decimal giaNgay)
        {
            if (giaNgay > 0) return giaNgay;
            if (giaGio > 0) return giaGio * 24m;
            return 0;
        }

        private static decimal LayGiaGioPhuThuTheoLoai(DateTime start, DateTime plannedEnd, decimal giaGio, decimal giaNgay, decimal giaDem)
        {
            if (LaThueTheoGio(start, plannedEnd)) return LayGiaGioPhuThu(giaGio, giaNgay);
            if (LaThueQuaDem(start, plannedEnd))
            {
                decimal giaDemTinhPhi = giaDem > 0 ? giaDem : LayGiaNgayPhuThu(giaGio, giaNgay);
                return Math.Round(giaDemTinhPhi / 12m, 0);
            }

            return Math.Round(LayGiaNgayPhuThu(giaGio, giaNgay) / 24m, 0);
        }

        private static bool LaThueTheoGio(DateTime start, DateTime plannedEnd)
        {
            return start.Date == plannedEnd.Date && plannedEnd > start;
        }

        private static string LaThueTheoGioSql(string startExpr, string plannedEndExpr)
        {
            return "(" + plannedEndExpr + @" IS NOT NULL
        AND CAST(" + startExpr + @" AS date) = CAST(" + plannedEndExpr + @" AS date)
        AND DATEDIFF(minute, " + startExpr + @", " + plannedEndExpr + @") > 0)";
        }

        private static string LaThueQuaDemSql(string startExpr, string plannedEndExpr)
        {
            return "(" + plannedEndExpr + @" IS NOT NULL
        AND CAST(" + plannedEndExpr + @" AS date) = DATEADD(day, 1, CAST(" + startExpr + @" AS date))
        AND CAST(" + startExpr + @" AS time) >= CAST('21:00' AS time)
        AND CAST(" + plannedEndExpr + @" AS time) <= CAST('08:30' AS time))";
        }
    }
}
