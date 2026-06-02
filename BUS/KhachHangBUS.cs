using QLKS_AnPhu.DAL;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.BUS
{
    public class KhachHangBUS
    {
        private readonly KhachHangDAL khachHangDAL = new();

        public List<KhachHangDTO> LayDanhSach()
        {
            return khachHangDAL.LayDanhSach();
        }

        public void Them(KhachHangDTO item)
        {
            KiemTraHopLe(item);
            khachHangDAL.Them(item);
        }

        public void Sua(KhachHangDTO item)
        {
            KiemTraHopLe(item);
            khachHangDAL.Sua(item);
        }

        public void Xoa(KhachHangDTO item)
        {
            if (item.Ma <= 0)
            {
                throw new InvalidOperationException("Khách hàng cần xóa không hợp lệ.");
            }

            khachHangDAL.Xoa(item);
        }

        private static void KiemTraHopLe(KhachHangDTO item)
        {
            if (string.IsNullOrWhiteSpace(item.HoTen))
            {
                throw new InvalidOperationException("Vui lòng nhập họ tên khách hàng.");
            }

            if (!string.IsNullOrWhiteSpace(item.SDT) && item.SDT.Length < 8)
            {
                throw new InvalidOperationException("Số điện thoại không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(item.LoaiKhach))
            {
                item.LoaiKhach = "Thường";
            }

            if (string.IsNullOrWhiteSpace(item.TrangThai))
            {
                item.TrangThai = "Đang hoạt động";
            }
        }
    }
}
