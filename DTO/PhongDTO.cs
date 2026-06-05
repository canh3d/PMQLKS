namespace QLKS_AnPhu.DTO
{
    public class PhongDTO
    {
        public int Ma { get; set; }
        public string MaPhong { get; set; } = string.Empty;
        public string SoPhong { get; set; } = string.Empty;
        public string TenPhong { get; set; } = string.Empty;
        public int Tang { get; set; }
        public int MaLoaiPhong { get; set; }
        public string LoaiPhong { get; set; } = string.Empty;
        public decimal GiaGio { get; set; }
        public decimal GiaNgay { get; set; }
        public decimal GiaDem { get; set; }
        public decimal GiaPhong { get; set; }
        public string TrangThai { get; set; } = string.Empty;
        public string TinhTrangDonDep { get; set; } = string.Empty;
        public string KhachHienTai { get; set; } = "--";
        public string GioNhanPhong { get; set; } = "--";
        public string GioTraDuKien { get; set; } = "--";
        public string GhiChu { get; set; } = "--";
        public string SourceSchema { get; set; } = "dbo";
        public string SourceTable { get; set; } = string.Empty;
        public string KeyColumn { get; set; } = string.Empty;
        public string GhiChuHienThi => LamSachGhiChu(GhiChu);

        public string MaHienThi => !string.IsNullOrWhiteSpace(SoPhong) ? SoPhong : string.IsNullOrWhiteSpace(TenPhong) ? MaPhong : TenPhong;
        public string GiaPhongHienThi => $"{GiaPhong:N0} đ";
        public string GiaPhongChiTiet => $"{GiaPhong:N0} đ / đêm";
        private static string LamSachGhiChu(string? ghiChu)
        {
            if (string.IsNullOrWhiteSpace(ghiChu) || ghiChu.Trim() == "--")
            {
                return "--";
            }

            string value = ghiChu.Trim();
            value = BoMetadata(value, "[DATPHONG]");
            value = BoMetadata(value, "[DAT_DOAN]");

            return string.IsNullOrWhiteSpace(value) ? "--" : value.Trim();
        }

        private static string BoMetadata(string value, string marker)
        {
            int markerIndex = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return value;
            }

            string before = value[..markerIndex].Trim();
            string after = value[(markerIndex + marker.Length)..].Trim();
            int noteSeparator = after.IndexOf(" - ", StringComparison.Ordinal);
            string userNote = noteSeparator >= 0 ? after[(noteSeparator + 3)..].Trim() : string.Empty;

            if (!string.IsNullOrWhiteSpace(before) && !string.IsNullOrWhiteSpace(userNote))
            {
                return before + " " + userNote;
            }

            return !string.IsNullOrWhiteSpace(before) ? before : userNote;
        }
    }
}
