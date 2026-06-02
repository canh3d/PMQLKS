namespace QLKS_AnPhu.DTO
{
    public class KhachHangDTO
    {
        public int Ma { get; set; }
        public string HoTen { get; set; } = string.Empty;
        public string SDT { get; set; } = string.Empty;
        public string CCCD { get; set; } = string.Empty;
        public string GioiTinh { get; set; } = string.Empty;
        public DateTime? NgaySinh { get; set; }
        public string DiaChi { get; set; } = string.Empty;
        public string LoaiKhach { get; set; } = string.Empty;
        public string TrangThai { get; set; } = string.Empty;
        public string GhiChu { get; set; } = string.Empty;
        public string SourceSchema { get; set; } = "dbo";
        public string SourceTable { get; set; } = string.Empty;
        public string KeyColumn { get; set; } = string.Empty;

        public string NgaySinhHienThi => NgaySinh?.ToString("dd/MM/yyyy") ?? string.Empty;
    }
}
