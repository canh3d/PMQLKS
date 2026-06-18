namespace QLKS_AnPhu.Services
{
    public sealed class InvoiceService
    {
        private readonly InvoiceRepository repository;

        public InvoiceService()
            : this(new InvoiceRepository())
        {
        }

        public InvoiceService(InvoiceRepository repository)
        {
            this.repository = repository;
        }

        public InvoiceRentalInfo LoadRentalInfo(int maThue)
        {
            if (maThue <= 0)
            {
                throw new InvalidOperationException("Chưa chọn phiếu thuê.");
            }

            return repository.LoadRentalInfo(maThue);
        }

        public int PayInvoice(InvoicePaymentRequest request)
        {
            if (request.MaThue <= 0)
            {
                throw new InvalidOperationException("Chưa chọn phiếu thuê.");
            }

            return repository.PayInvoice(request);
        }

        public static InvoiceCalculation CalculateTotal(InvoiceRentalInfo rental, decimal phuThu, decimal giamGia, DateTime? ngayTraPhong = null)
        {
            if (rental == null)
            {
                throw new InvalidOperationException("Chưa chọn phiếu thuê.");
            }

            DateTime ngayTra = ngayTraPhong ?? rental.NgayTraPhong;
            if (ngayTra < rental.NgayNhan)
            {
                throw new InvalidOperationException("Ngày trả phòng không được nhỏ hơn ngày nhận phòng.");
            }

            if (phuThu < 0)
            {
                throw new InvalidOperationException("Phụ thu không được âm.");
            }

            if (giamGia < 0)
            {
                throw new InvalidOperationException("Giảm giá không được âm.");
            }

            foreach (InvoiceServiceLine service in rental.DichVu)
            {
                if (service.SoLuong < 0)
                {
                    throw new InvalidOperationException("Số lượng dịch vụ không được âm.");
                }

                if (service.DonGia < 0)
                {
                    throw new InvalidOperationException("Đơn giá dịch vụ không được âm.");
                }
            }

            int soNgay = Math.Max(1, (int)Math.Ceiling((ngayTra - rental.NgayNhan).TotalDays));
            decimal tienPhong = rental.GiaPhong * soNgay;
            decimal tienDichVu = rental.DichVu.Sum(item => item.ThanhTien);
            decimal truocGiamGia = tienPhong + tienDichVu + phuThu;

            if (giamGia > truocGiamGia)
            {
                throw new InvalidOperationException("Giảm giá không được lớn hơn tổng tiền trước giảm giá.");
            }

            return new InvoiceCalculation(
                soNgay,
                tienPhong,
                tienDichVu,
                phuThu,
                giamGia,
                truocGiamGia - giamGia);
        }
    }

    public sealed record InvoiceCalculation(
        int SoNgay,
        decimal TienPhong,
        decimal TienDichVu,
        decimal PhuThu,
        decimal GiamGia,
        decimal TongTien);
}
