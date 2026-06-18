using QLKS_AnPhu.DAL;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.BUS
{
    public class ThanhToanFlowBUS
    {
        private readonly ThanhToanFlowDAL thanhToanDAL = new();

        public KetQuaCheckInThanhToanDTO CheckInTuDatPhong(int maDatPhong, decimal tongTienDuKien, decimal tienDatCocTruoc, decimal? giaTriTinhThue = null)
        {
            if (maDatPhong <= 0)
            {
                throw new InvalidOperationException("Phieu dat phong khong hop le.");
            }

            decimal thueVat = Math.Round(Math.Max(0, giaTriTinhThue ?? tongTienDuKien) * 0.1m, 0);
            decimal tienThucThuTaiQuay = Math.Max(0, tongTienDuKien + thueVat - tienDatCocTruoc);
            return thanhToanDAL.CheckInTuDatPhong(maDatPhong, tienThucThuTaiQuay);
        }

        public void CongDichVuPhatSinh(int maThue, decimal chiPhiDichVuMoi)
        {
            if (maThue <= 0)
            {
                throw new InvalidOperationException("Phieu thue khong hop le.");
            }

            if (chiPhiDichVuMoi < 0)
            {
                throw new InvalidOperationException("Chi phi dich vu khong duoc am.");
            }

            thanhToanDAL.CongDichVuPhatSinh(maThue, chiPhiDichVuMoi);
        }

        public KetQuaCheckOutThanhToanDTO CheckOut(int maThue, bool thanhToanNgay = true)
        {
            if (maThue <= 0)
            {
                throw new InvalidOperationException("Phieu thue khong hop le.");
            }

            return thanhToanDAL.CheckOut(maThue, thanhToanNgay);
        }

        public void ThanhToanHoaDon(int maThue)
        {
            if (maThue <= 0)
            {
                throw new InvalidOperationException("Phieu thue khong hop le.");
            }

            thanhToanDAL.ThanhToanHoaDon(maThue);
        }

        public void ThanhToanHoaDonTheoDoan(int maDoan)
        {
            if (maDoan <= 0)
            {
                throw new InvalidOperationException("Doan khach khong hop le.");
            }

            thanhToanDAL.ThanhToanHoaDonTheoDoan(maDoan);
        }

        public DuToanCheckOutDTO DuToanCheckOut(int maThue)
        {
            if (maThue <= 0)
            {
                throw new InvalidOperationException("Phieu thue khong hop le.");
            }

            return thanhToanDAL.DuToanCheckOut(maThue, DateTime.Now);
        }

        public void NoShow(int maDatPhong)
        {
            if (maDatPhong <= 0)
            {
                throw new InvalidOperationException("Phieu dat phong khong hop le.");
            }

            thanhToanDAL.NoShow(maDatPhong);
        }

        public DuToanHuyDatPhongDTO DuToanHuyDatPhong(int maDatPhong)
        {
            if (maDatPhong <= 0)
            {
                throw new InvalidOperationException("Phieu dat phong khong hop le.");
            }

            return thanhToanDAL.DuToanHuyDatPhong(maDatPhong);
        }
    }
}
