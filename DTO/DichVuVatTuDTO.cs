namespace QLKS_AnPhu.DTO
{
    public class DichVuVatTuDTO
    {
        public int Ma { get; set; }
        public string Ten { get; set; } = string.Empty;
        public string Loai { get; set; } = string.Empty;
        public string DonViTinh { get; set; } = string.Empty;
        public decimal DonGia { get; set; }
        public int SoLuongTon { get; set; }
        public string TrangThai { get; set; } = string.Empty;
        public string GhiChu { get; set; } = string.Empty;
        public string SourceSchema { get; set; } = "dbo";
        public string SourceTable { get; set; } = string.Empty;
        public string KeyColumn { get; set; } = string.Empty;

        public string DonGiaHienThi => $"{DonGia:N0} đ";
        public string SoLuongTonHienThi => $"{SoLuongTon} {DonViTinh}".Trim();
    }
}
