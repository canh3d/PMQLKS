using QLKS_AnPhu.DAL;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.BUS
{
    public class PhongThueOperationBUS
    {
        private readonly PhongThueOperationDAL dal = new();

        public void GiaHan(GiaHanPhongRequestDTO request)
        {
            if (request.MaThue <= 0 || request.MaPhong <= 0)
            {
                throw new InvalidOperationException("Phiếu thuê hoặc phòng không hợp lệ.");
            }
            if (request.NgayTraMoi <= request.NgayTraCu)
            {
                throw new InvalidOperationException("Giờ trả mới phải sau giờ trả hiện tại.");
            }
            dal.GiaHan(request);
        }

        public void DoiPhong(DoiPhongRequestDTO request)
        {
            if ((!request.MaDatPhong.HasValue && request.MaThue <= 0) || request.MaPhongCu <= 0 || request.MaPhongMoi <= 0)
            {
                throw new InvalidOperationException("Thông tin đổi phòng không hợp lệ.");
            }
            if (request.MaPhongCu == request.MaPhongMoi)
            {
                throw new InvalidOperationException("Vui lòng chọn phòng khác phòng hiện tại.");
            }
            if (request.NgayTraDuKien <= request.NgayBatDau)
            {
                throw new InvalidOperationException("Thời gian đổi phòng không hợp lệ.");
            }
            dal.DoiPhong(request);
        }
    }
}
