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

        public string MaHienThi => !string.IsNullOrWhiteSpace(SoPhong) ? SoPhong : string.IsNullOrWhiteSpace(TenPhong) ? MaPhong : TenPhong;
        public string GiaPhongHienThi => $"{GiaPhong:N0} đ";
        public string GiaPhongChiTiet => $"{GiaPhong:N0} đ / đêm";
    }
}
