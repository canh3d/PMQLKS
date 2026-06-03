namespace QLKS_AnPhu.DTO
{
    public class KetQuaCheckInThanhToanDTO
    {
        public int MaHoaDon { get; set; }
        public int? MaThue { get; set; }
        public decimal TongTienDuKien { get; set; }
        public decimal TienDatCocTruoc { get; set; }
        public decimal TienThucThuTaiQuay { get; set; }
    }

    public class KetQuaCheckOutThanhToanDTO
    {
        public int MaHoaDon { get; set; }
        public decimal TienThuThem { get; set; }
    }
}
