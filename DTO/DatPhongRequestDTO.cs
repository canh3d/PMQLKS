namespace QLKS_AnPhu.DTO
{
    public class DatPhongRequestDTO
    {
        public PhongDTO Phong { get; set; } = new();
        public KhachHangDTO KhachHang { get; set; } = new();
        public DateTime NgayNhan { get; set; }
        public DateTime NgayTra { get; set; }
        public int SoNguoi { get; set; }
        public bool NhanNgay { get; set; }
        public string CheDoDatPhong { get; set; } = "Theo ngày";
        public decimal TienCoc { get; set; }
        public decimal TienPhong { get; set; }
        public decimal TienDichVu { get; set; }
        public List<DichVuDatPhongDTO> DichVuDaThem { get; set; } = new();
        public string GhiChu { get; set; } = string.Empty;
    }

    public class DichVuDatPhongDTO
    {
        public int Ma { get; set; }
        public string Ten { get; set; } = string.Empty;
        public int SoLuong { get; set; }
        public decimal DonGia { get; set; }
        public decimal ThanhTien => SoLuong * DonGia;
    }
}
