using QLKS_AnPhu.DAL;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.BUS
{
    public class ThanhToanFlowBUS
    {
        private readonly ThanhToanFlowDAL thanhToanDAL = new();

        public KetQuaCheckInThanhToanDTO CheckInTuDatPhong(int maDatPhong, decimal tongTienDuKien, decimal tienDatCocTruoc)
        {
            if (maDatPhong <= 0)
            {
                throw new InvalidOperationException("Phieu dat phong khong hop le.");
            }

            decimal tienThucThuTaiQuay = Math.Max(0, tongTienDuKien - tienDatCocTruoc);
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

        public KetQuaCheckOutThanhToanDTO CheckOut(int maThue)
        {
            if (maThue <= 0)
            {
                throw new InvalidOperationException("Phieu thue khong hop le.");
            }

            return thanhToanDAL.CheckOut(maThue);
        }

        public void NoShow(int maDatPhong)
        {
            if (maDatPhong <= 0)
            {
                throw new InvalidOperationException("Phieu dat phong khong hop le.");
            }

            thanhToanDAL.NoShow(maDatPhong);
        }
    }
}
