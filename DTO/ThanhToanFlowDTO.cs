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
        public decimal TienHoanKhach { get; set; }
        public bool DaThanhToan { get; set; }
    }

    public class DuToanCheckOutDTO
    {
        public int MaThue { get; set; }
        public int MaHoaDon { get; set; }
        public DateTime NgayTraDuKien { get; set; }
        public DateTime NgayTraThucTe { get; set; }
        public string CheDoDatPhong { get; set; } = string.Empty;
        public decimal GiaGio { get; set; }
        public decimal GiaNgay { get; set; }
        public decimal GiaDem { get; set; }
        public decimal TienDichVuPhatSinh { get; set; }
        public decimal TienGiaHan { get; set; }
        public decimal ChenhLechDoiPhong { get; set; }
        public decimal PhuPhiTraMuon { get; set; }
        public int SoPhutTraMuon { get; set; }
        public int SoPhutTinhPhi { get; set; }
        public decimal TienPhongPhatSinh => TienGiaHan + ChenhLechDoiPhong;
        public decimal TienHoanDoiPhong => Math.Max(0, -ChenhLechDoiPhong);
        public decimal TongPhatSinhTruocVat => Math.Max(0, TienDichVuPhatSinh + TienGiaHan + Math.Max(0, ChenhLechDoiPhong) + PhuPhiTraMuon);
        public decimal TongDieuChinh => TongPhatSinhTruocVat - TienHoanDoiPhong;
        public decimal ThueVat => Math.Round(TongPhatSinhTruocVat * 0.1m, 0);
        public decimal TongSauVat => TongPhatSinhTruocVat + ThueVat - TienHoanDoiPhong;
        public decimal CanThuThem => Math.Max(0, TongSauVat);
        public decimal CanTraKhach => Math.Max(0, -TongSauVat);
    }

    public class DuToanHuyDatPhongDTO
    {
        public int MaDatPhong { get; set; }
        public DateTime? NgayNhanDuKien { get; set; }
        public DateTime ThoiDiemHuy { get; set; }
        public double? SoGioTruocNhan { get; set; }
        public decimal TienCoc { get; set; }
        public decimal TienCocGiuLai { get; set; }
        public decimal TienCocHoanTra => Math.Max(0, TienCoc - TienCocGiuLai);
        public string ChinhSach { get; set; } = string.Empty;
    }
}
