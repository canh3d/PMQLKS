using QLKS_AnPhu.DAL;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.BUS
{
    public class DichVuVatTuBUS
    {
        private readonly DichVuVatTuDAL dichVuVatTuDAL = new();

        public List<DichVuVatTuDTO> LayDanhSach()
        {
            return dichVuVatTuDAL.LayDanhSach();
        }

        public List<DichVuVatTuDTO> TimKiem(string tuKhoa)
        {
            List<DichVuVatTuDTO> danhSach = LayDanhSach();

            if (string.IsNullOrWhiteSpace(tuKhoa))
            {
                return danhSach;
            }

            string keyword = tuKhoa.Trim();
            return danhSach
                .Where(item =>
                    item.Ma.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.Ten.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.Loai.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.TrangThai.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public void Them(DichVuVatTuDTO item)
        {
            KiemTraHopLe(item);
            dichVuVatTuDAL.Them(item);
        }

        public void Sua(DichVuVatTuDTO item)
        {
            KiemTraHopLe(item);
            dichVuVatTuDAL.Sua(item);
        }

        public void Xoa(DichVuVatTuDTO item)
        {
            if (item.Ma <= 0)
            {
                throw new InvalidOperationException("Dịch vụ/vật tư cần xóa không hợp lệ.");
            }

            dichVuVatTuDAL.Xoa(item);
        }

        private static void KiemTraHopLe(DichVuVatTuDTO item)
        {
            if (string.IsNullOrWhiteSpace(item.Ten))
            {
                throw new InvalidOperationException("Vui lòng nhập tên dịch vụ/vật tư.");
            }

            if (string.IsNullOrWhiteSpace(item.Loai))
            {
                throw new InvalidOperationException("Vui lòng chọn loại.");
            }

            if (item.DonGia < 0)
            {
                throw new InvalidOperationException("Đơn giá không được âm.");
            }

            if (item.SoLuongTon < 0)
            {
                throw new InvalidOperationException("Số lượng tồn không được âm.");
            }
        }
    }
}
