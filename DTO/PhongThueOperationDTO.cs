namespace QLKS_AnPhu.DTO
{
    public class GiaHanPhongRequestDTO
    {
        public int MaThue { get; set; }
        public int? MaDatPhong { get; set; }
        public int MaPhong { get; set; }
        public DateTime NgayTraCu { get; set; }
        public DateTime NgayTraMoi { get; set; }
    }

    public class DoiPhongRequestDTO
    {
        public int MaThue { get; set; }
        public int? MaDatPhong { get; set; }
        public int MaPhongCu { get; set; }
        public int MaPhongMoi { get; set; }
        public DateTime NgayBatDau { get; set; }
        public DateTime NgayTraDuKien { get; set; }
    }
}
