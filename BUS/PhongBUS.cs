using QLKS_AnPhu.DAL;
using QLKS_AnPhu.DTO;

namespace QLKS_AnPhu.BUS
{
    public class PhongBUS
    {
        private readonly PhongDAL phongDAL = new();
        private readonly DatPhongDAL datPhongDAL = new();
        private readonly ThanhToanFlowBUS thanhToanBUS = new();

        public List<PhongDTO> LayDanhSach()
        {
            return phongDAL.LayDanhSach();
        }

        public List<PhongDTO> Loc(List<PhongDTO> danhSach, string trangThai, string tang, string tuKhoa)
        {
            IEnumerable<PhongDTO> query = danhSach;

            if (!string.IsNullOrWhiteSpace(trangThai) && trangThai != "Tất cả")
            {
                query = query.Where(phong => phong.TrangThai.Contains(trangThai, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(tang) && tang != "Tất cả")
            {
                string tangValue = tang.Replace("Tầng", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                query = query.Where(phong => phong.Tang.ToString() == tangValue);
            }

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                query = query.Where(phong =>
                    phong.MaHienThi.Contains(tuKhoa, StringComparison.OrdinalIgnoreCase) ||
                    phong.LoaiPhong.Contains(tuKhoa, StringComparison.OrdinalIgnoreCase) ||
                    phong.KhachHienTai.Contains(tuKhoa, StringComparison.OrdinalIgnoreCase));
            }

            return query.ToList();
        }

        public void DatPhong(PhongDTO phong)
        {
            if (DangDuocSuDung(phong))
            {
                throw new InvalidOperationException("Phòng này đang được thuê.");
            }

            phongDAL.CapNhatTrangThai(phong, "Đã đặt");
        }

        public void DatPhong(DatPhongRequestDTO request)
        {
            request.NhanNgay = false;
            datPhongDAL.LuuDatPhong(request);
        }

        public void LuuDatPhongDoan(List<DatPhongRequestDTO> requests)
        {
            if (requests.Count == 0)
            {
                throw new InvalidOperationException("Vui lòng chọn ít nhất một phòng cho đoàn.");
            }

            foreach (DatPhongRequestDTO request in requests)
            {
                if (DangDuocSuDung(request.Phong))
                {
                    throw new InvalidOperationException($"Phòng {request.Phong.MaHienThi} đang được sử dụng, không thể đặt trùng.");
                }
            }

            datPhongDAL.LuuDatPhongDoan(requests);
        }

        public void NhanPhong(PhongDTO phong)
        {
            if (DangDuocSuDung(phong))
            {
                throw new InvalidOperationException("Phòng này đang được thuê.");
            }

            phongDAL.CapNhatTrangThai(phong, "Đang thuê");
        }

        public void NhanPhong(DatPhongRequestDTO request)
        {
            request.NhanNgay = true;
            request.TienCoc = 0;
            datPhongDAL.LuuDatPhong(request);
        }

        public void NhanPhongTuDatPhong(int maDatPhong)
        {
            datPhongDAL.NhanPhongTuDatPhong(maDatPhong);
        }

        public KetQuaCheckInThanhToanDTO NhanPhongTuDatPhong(int maDatPhong, decimal tongTienDuKien, decimal tienDatCocTruoc)
        {
            datPhongDAL.NhanPhongTuDatPhong(maDatPhong);
            return thanhToanBUS.CheckInTuDatPhong(maDatPhong, tongTienDuKien, tienDatCocTruoc);
        }

        public void NoShow(int maDatPhong)
        {
            thanhToanBUS.NoShow(maDatPhong);
        }

        public void DatPhongTheoDoan(IEnumerable<PhongDTO> danhSachPhong)
        {
            foreach (PhongDTO phong in danhSachPhong.Where(item => !DangDuocSuDung(item)))
            {
                phongDAL.CapNhatTrangThai(phong, "Đã đặt");
            }
        }

        public void Them(PhongDTO phong)
        {
            ChuanHoaPhong(phong);
            KiemTraHopLe(phong);
            KiemTraTrungPhong(phong, isEdit: false);
            phongDAL.Them(phong);
        }

        public void Sua(PhongDTO phong)
        {
            ChuanHoaPhong(phong);
            KiemTraHopLe(phong);
            KiemTraTrungPhong(phong, isEdit: true);
            phongDAL.Sua(phong);
        }

        public void Xoa(PhongDTO phong)
        {
            if (phong.Ma <= 0)
            {
                throw new InvalidOperationException("Phòng cần xóa không hợp lệ.");
            }

            phongDAL.Xoa(phong);
        }

        private static bool DangDuocSuDung(PhongDTO phong)
        {
            return phong.TrangThai.Contains("thuê", StringComparison.OrdinalIgnoreCase) ||
                   phong.TrangThai.Contains("thue", StringComparison.OrdinalIgnoreCase) ||
                   phong.TrangThai.Contains("đặt", StringComparison.OrdinalIgnoreCase) ||
                   phong.TrangThai.Contains("dat", StringComparison.OrdinalIgnoreCase) ||
                   phong.TrangThai.Contains("đặt", StringComparison.OrdinalIgnoreCase) ||
                   phong.TrangThai.Contains("đang", StringComparison.OrdinalIgnoreCase) ||
                   phong.TrangThai.Contains("dang", StringComparison.OrdinalIgnoreCase) ||
                   phong.TrangThai.Contains("bận", StringComparison.OrdinalIgnoreCase) ||
                   phong.TrangThai.Contains("ban", StringComparison.OrdinalIgnoreCase);
        }

        private void KiemTraTrungPhong(PhongDTO phong, bool isEdit)
        {
            string soPhong = ChuanHoaSoPhong(phong.SoPhong);
            bool biTrung = LayDanhSach().Any(item =>
            {
                if (isEdit && item.Ma == phong.Ma)
                {
                    return false;
                }

                return ChuanHoaSoPhong(item.SoPhong) == soPhong ||
                       ChuanHoaSoPhong(item.TenPhong) == soPhong ||
                       ChuanHoaSoPhong(item.MaHienThi) == soPhong;
            });

            if (biTrung)
            {
                throw new InvalidOperationException($"Phòng {phong.SoPhong} đã tồn tại. Vui lòng nhập số phòng khác.");
            }
        }

        private static void ChuanHoaPhong(PhongDTO phong)
        {
            string soPhong = string.IsNullOrWhiteSpace(phong.SoPhong) ? phong.TenPhong : phong.SoPhong;
            soPhong = soPhong.Trim();
            phong.SoPhong = soPhong;
            phong.TenPhong = soPhong;
            phong.MaPhong = string.IsNullOrWhiteSpace(phong.MaPhong) ? soPhong : phong.MaPhong.Trim();
        }

        private static string ChuanHoaSoPhong(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static void KiemTraHopLe(PhongDTO phong)
        {
            if (string.IsNullOrWhiteSpace(phong.SoPhong) && string.IsNullOrWhiteSpace(phong.TenPhong))
            {
                throw new InvalidOperationException("Vui lòng nhập số phòng.");
            }

            if (phong.Tang <= 0)
            {
                throw new InvalidOperationException("Tầng phải lớn hơn 0.");
            }

            if (phong.GiaGio < 0 || phong.GiaNgay < 0 || phong.GiaDem < 0)
            {
                throw new InvalidOperationException("Giá phòng không được âm.");
            }
        }
    }
}
