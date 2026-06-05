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
            return PhuThuSql(startExpr, plannedEndExpr, actualEndExpr, giaNgayExpr, giaGioExpr);
        }

        public static string PhuThuNhanSomSql(string actualStartExpr, string plannedStartExpr)
        {
            string giaNgayExpr = "ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0))";
            return PhuThuNhanSomSql(actualStartExpr, plannedStartExpr, giaNgayExpr);
        }

        public static string PhuThuNhanSomSql(string actualStartExpr, string plannedStartExpr, string giaNgayExpr)
        {
            return @"CAST(CASE
    WHEN " + actualStartExpr + @" IS NULL OR " + plannedStartExpr + @" IS NULL THEN 0
    WHEN " + actualStartExpr + @" >= DATEADD(minute, -30, " + plannedStartExpr + @") THEN 0
    WHEN CAST(" + actualStartExpr + @" AS time) >= CAST('09:00' AS time) THEN " + giaNgayExpr + @" * 0.30
    WHEN CAST(" + actualStartExpr + @" AS time) >= CAST('06:00' AS time) THEN " + giaNgayExpr + @" * 0.50
    ELSE " + giaNgayExpr + @"
END AS decimal(18, 2))";
        }

        public static string PhuThuTraMuonSql(string startExpr, string plannedEndExpr, string actualEndExpr)
        {
            string giaNgayExpr = "ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24.0))";
            string giaGioExpr = "ISNULL(NULLIF(LP.DonGiaGio, 0), " + giaNgayExpr + " / 24.0)";
            return PhuThuTraMuonSql(startExpr, plannedEndExpr, actualEndExpr, giaNgayExpr, giaGioExpr);
        }

        public static string PhuThuTraMuonSql(string startExpr, string plannedEndExpr, string actualEndExpr, string giaNgayExpr, string giaGioExpr)
        {
            string mocTraPhongExpr = "DATEADD(hour, 12, CAST(CAST(" + plannedEndExpr + " AS date) AS datetime))";
            string mocTraDemExpr = "DATEADD(hour, 8, CAST(CAST(" + plannedEndExpr + " AS date) AS datetime))";
            string laThueTheoGioExpr = "(" + plannedEndExpr + @" IS NOT NULL
        AND CAST(" + startExpr + @" AS date) = CAST(" + plannedEndExpr + @" AS date)
        AND DATEDIFF(minute, " + startExpr + @", " + plannedEndExpr + @") > 0)";
            string laThueQuaDemExpr = "(" + plannedEndExpr + @" IS NOT NULL
        AND CAST(" + plannedEndExpr + @" AS date) = DATEADD(day, 1, CAST(" + startExpr + @" AS date))
        AND CAST(" + startExpr + @" AS time) >= CAST('21:00' AS time)
        AND CAST(" + plannedEndExpr + @" AS time) <= CAST('08:30' AS time))";

            return @"CAST(CASE
WHEN " + actualEndExpr + @" IS NULL THEN 0
WHEN " + laThueTheoGioExpr + @" THEN
    CASE WHEN " + actualEndExpr + @" <= DATEADD(minute, 30, " + plannedEndExpr + @") THEN 0
         ELSE CEILING((DATEDIFF(minute, " + plannedEndExpr + @", " + actualEndExpr + @") - 30) / 60.0) * " + giaGioExpr + @" END
WHEN " + laThueQuaDemExpr + @" THEN
    CASE WHEN " + actualEndExpr + @" <= DATEADD(minute, 30, " + mocTraDemExpr + @") THEN 0
         ELSE ((DATEDIFF(minute, DATEADD(minute, 30, " + mocTraDemExpr + @"), " + actualEndExpr + @") / 60.0) * " + giaGioExpr + @" END
ELSE
    CASE
        WHEN " + actualEndExpr + @" > DATEADD(hour, 18, CAST(CAST(" + plannedEndExpr + @" AS date) AS datetime)) THEN " + giaNgayExpr + @"
        WHEN " + actualEndExpr + @" >= DATEADD(hour, 15, CAST(CAST(" + plannedEndExpr + @" AS date) AS datetime)) THEN " + giaNgayExpr + @" * 0.50
        WHEN " + actualEndExpr + @" > DATEADD(minute, 30, " + mocTraPhongExpr + @") THEN " + giaNgayExpr + @" * 0.30
        ELSE 0
    END
END AS decimal(18, 2))";
        }

        public static string PhuThuSql(string startExpr, string plannedEndExpr, string actualEndExpr, string giaNgayExpr, string giaGioExpr)
        {
            string mocNhanPhongExpr = "DATEADD(hour, 14, CAST(CAST(" + startExpr + " AS date) AS datetime))";
            string mocTraPhongExpr = "DATEADD(hour, 12, CAST(CAST(" + plannedEndExpr + " AS date) AS datetime))";
            string mocTraDemExpr = "DATEADD(hour, 8, CAST(CAST(" + plannedEndExpr + " AS date) AS datetime))";
            string laThueTheoGioExpr = "(" + plannedEndExpr + @" IS NOT NULL
        AND CAST(" + startExpr + @" AS date) = CAST(" + plannedEndExpr + @" AS date)
        AND DATEDIFF(minute, " + startExpr + @", " + plannedEndExpr + @") > 0)";
            string laThueQuaDemExpr = "(" + plannedEndExpr + @" IS NOT NULL
        AND CAST(" + plannedEndExpr + @" AS date) = DATEADD(day, 1, CAST(" + startExpr + @" AS date))
        AND CAST(" + startExpr + @" AS time) >= CAST('21:00' AS time)
        AND CAST(" + plannedEndExpr + @" AS time) <= CAST('08:30' AS time))";

            return @"CAST(CASE
WHEN " + laThueTheoGioExpr + @" THEN
    CASE
        WHEN " + actualEndExpr + @" IS NULL THEN 0
        WHEN " + actualEndExpr + @" <= DATEADD(minute, 30, " + plannedEndExpr + @") THEN 0
        ELSE CEILING((DATEDIFF(minute, " + plannedEndExpr + @", " + actualEndExpr + @") - 30) / 60.0) * " + giaGioExpr + @"
    END
WHEN " + laThueQuaDemExpr + @" THEN
    CASE
        WHEN " + actualEndExpr + @" IS NULL THEN 0
        WHEN " + actualEndExpr + @" <= DATEADD(minute, 30, " + mocTraDemExpr + @") THEN 0
        ELSE ((DATEDIFF(minute, DATEADD(minute, 30, " + mocTraDemExpr + @"), " + actualEndExpr + @") / 60.0) * " + giaGioExpr + @")
    END
ELSE (
    CASE
        WHEN " + startExpr + @" >= DATEADD(minute, -30, " + mocNhanPhongExpr + @") THEN 0
        WHEN CAST(" + startExpr + @" AS time) >= CAST('09:00' AS time) THEN " + giaNgayExpr + @" * 0.30
        WHEN CAST(" + startExpr + @" AS time) >= CAST('06:00' AS time) THEN " + giaNgayExpr + @" * 0.50
        WHEN " + startExpr + @" < " + mocNhanPhongExpr + @" THEN " + giaNgayExpr + @"
        ELSE 0
    END
    +
    CASE
        WHEN " + actualEndExpr + @" IS NULL THEN 0
        WHEN " + actualEndExpr + @" > DATEADD(hour, 18, CAST(CAST(" + plannedEndExpr + @" AS date) AS datetime)) THEN " + giaNgayExpr + @"
        WHEN " + actualEndExpr + @" >= DATEADD(hour, 15, CAST(CAST(" + plannedEndExpr + @" AS date) AS datetime)) THEN " + giaNgayExpr + @" * 0.50
        WHEN " + actualEndExpr + @" > DATEADD(minute, 30, " + mocTraPhongExpr + @") THEN " + giaNgayExpr + @" * 0.30
        ELSE 0
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
            if (plannedEnd > start && start.Date == plannedEnd.Date)
            {
                if (actualEnd <= plannedEnd.AddMinutes(30)) return 0;

                int soGio = Math.Max(1, (int)Math.Ceiling((actualEnd - plannedEnd).TotalMinutes / 60.0 - 0.5));
                return soGio * LayGiaGioPhuThu(giaGio, giaNgay);
            }

            if (LaThueQuaDem(start, plannedEnd))
            {
                DateTime mocTraDem = plannedEnd.Date.AddHours(8);
                if (actualEnd <= mocTraDem.AddMinutes(30)) return 0;

                decimal soGioTinhPhi = (decimal)(actualEnd - mocTraDem.AddMinutes(30)).TotalMinutes / 60m;
                return Math.Round(soGioTinhPhi * LayGiaGioPhuThu(giaGio, giaNgay), 0);
            }

            return TinhPhuThuNhanSom(start, giaGio, giaNgay) + TinhPhuThuTraMuon(plannedEnd, actualEnd, giaGio, giaNgay);
        }

        public static decimal TinhPhuThuNhanSom(DateTime actualStart, DateTime plannedStart, decimal giaGio, decimal giaNgay)
        {
            if (actualStart >= plannedStart.AddMinutes(-30)) return 0;
            return TinhPhuThuNhanSom(actualStart, giaGio, giaNgay);
        }

        private static decimal TinhPhuThuNhanSom(DateTime start, decimal giaGio, decimal giaNgay)
        {
            TimeSpan gioNhan = start.TimeOfDay;
            if (gioNhan >= TimeSpan.FromHours(13.5)) return 0;

            decimal giaNgayTinhPhi = LayGiaNgayPhuThu(giaGio, giaNgay);
            if (gioNhan < TimeSpan.FromHours(6)) return Math.Round(giaNgayTinhPhi, 0);
            if (gioNhan < TimeSpan.FromHours(9)) return Math.Round(giaNgayTinhPhi * 0.50m, 0);
            return Math.Round(giaNgayTinhPhi * 0.30m, 0);
        }

        public static decimal TinhPhuThuTraMuon(DateTime start, DateTime plannedEnd, DateTime actualEnd, decimal giaGio, decimal giaNgay)
        {
            if (plannedEnd > start && start.Date == plannedEnd.Date)
            {
                if (actualEnd <= plannedEnd.AddMinutes(30)) return 0;

                int soGio = Math.Max(1, (int)Math.Ceiling((actualEnd - plannedEnd).TotalMinutes / 60.0 - 0.5));
                return soGio * LayGiaGioPhuThu(giaGio, giaNgay);
            }

            if (LaThueQuaDem(start, plannedEnd))
            {
                DateTime mocTraDem = plannedEnd.Date.AddHours(8);
                if (actualEnd <= mocTraDem.AddMinutes(30)) return 0;

                decimal soGioTinhPhi = (decimal)(actualEnd - mocTraDem.AddMinutes(30)).TotalMinutes / 60m;
                return Math.Round(soGioTinhPhi * LayGiaGioPhuThu(giaGio, giaNgay), 0);
            }

            return TinhPhuThuTraMuon(plannedEnd, actualEnd, giaGio, giaNgay);
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
    }
}
