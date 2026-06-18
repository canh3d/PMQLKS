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
            KiemTraTrungKhachHang(item, isEdit: false);
            khachHangDAL.Them(item);
        }

        public void Sua(KhachHangDTO item)
        {
            KiemTraHopLe(item);
            KiemTraTrungKhachHang(item, isEdit: true);
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

        public void XoaBatBuoc(KhachHangDTO item)
        {
            if (item.Ma <= 0)
            {
                throw new InvalidOperationException("Khách hàng cần xóa không hợp lệ.");
            }

            throw new InvalidOperationException("Không hỗ trợ xóa bắt buộc vì sẽ làm mất lịch sử đặt phòng, phiếu thuê và hóa đơn. Hãy chuyển khách hàng sang trạng thái ngừng hoạt động.");
        }

        private void KiemTraTrungKhachHang(KhachHangDTO item, bool isEdit)
        {
            string cccd = ChuanHoaDinhDanh(item.CCCD);
            string sdt = ChuanHoaSoDienThoai(item.SDT);
            string hoTen = ChuanHoaVanBan(item.HoTen);

            foreach (KhachHangDTO existing in LayDanhSach())
            {
                if (isEdit && LaCungKhachHang(existing, item))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(cccd) &&
                    ChuanHoaDinhDanh(existing.CCCD) == cccd)
                {
                    throw new InvalidOperationException(
                        $"CCCD {item.CCCD} đã thuộc về khách hàng {existing.HoTen}.");
                }

                if (!string.IsNullOrWhiteSpace(sdt) &&
                    ChuanHoaSoDienThoai(existing.SDT) == sdt)
                {
                    throw new InvalidOperationException(
                        $"Số điện thoại {item.SDT} đã thuộc về khách hàng {existing.HoTen}.");
                }

                if (item.NgaySinh.HasValue &&
                    existing.NgaySinh?.Date == item.NgaySinh.Value.Date &&
                    ChuanHoaVanBan(existing.HoTen) == hoTen)
                {
                    throw new InvalidOperationException(
                        $"Khách hàng {item.HoTen} sinh ngày {item.NgaySinh:dd/MM/yyyy} đã tồn tại.");
                }
            }
        }

        private static bool LaCungKhachHang(KhachHangDTO left, KhachHangDTO right)
        {
            if (left.Ma != right.Ma)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(right.SourceTable) ||
                   string.Equals(left.SourceTable, right.SourceTable, StringComparison.OrdinalIgnoreCase);
        }

        private static string ChuanHoaSoDienThoai(string value)
        {
            string digits = new((value ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.StartsWith("84", StringComparison.Ordinal) && digits.Length > 9
                ? "0" + digits[2..]
                : digits;
        }

        private static string ChuanHoaDinhDanh(string value)
        {
            return new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
        }

        private static string ChuanHoaVanBan(string value)
        {
            return string.Join(
                " ",
                (value ?? string.Empty)
                    .Trim()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .ToUpperInvariant();
        }

        private static void KiemTraHopLe(KhachHangDTO item)
        {
            if (string.IsNullOrWhiteSpace(item.HoTen))
            {
                throw new InvalidOperationException("Vui lòng nhập họ tên khách hàng.");
            }

            if (string.IsNullOrWhiteSpace(item.SDT))
            {
                throw new InvalidOperationException("Vui lòng nhập số điện thoại khách hàng.");
            }

            if (ChuanHoaSoDienThoai(item.SDT).Length < 8)
            {
                throw new InvalidOperationException("Số điện thoại không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(item.CCCD))
            {
                throw new InvalidOperationException("Vui lòng nhập CCCD/CMND khách hàng.");
            }

            if (string.IsNullOrWhiteSpace(item.DiaChi))
            {
                throw new InvalidOperationException("Vui lòng nhập địa chỉ khách hàng.");
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
