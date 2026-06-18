using System.Data;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DAL;

namespace QLKS_AnPhu.Services
{
    public sealed class InvoiceRepository
    {
        private const string PaidStatusCanonical = "Đã thanh toán";
        private const string ReturnedRentalStatusCanonical = "Đã trả";
        private const string DirtyRoomStatusCanonical = "Trống";
        private const string PaidStatus = "Đã thanh toán";
        private const string UnpaidStatus = "Chưa thanh toán";
        private const string ReturnedRentalStatus = "Đã trả";
        private const string EmptyRoomStatus = "Trống";

        public InvoiceRentalInfo LoadRentalInfo(int maThue)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            return LoadRentalInfo(conn, null, maThue);
        }

        public int PayInvoice(InvoicePaymentRequest request)
        {
            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable);

            try
            {
                InvoiceRentalInfo rental = LoadRentalInfo(conn, tran, request.MaThue);
                InvoiceCalculation calculation = InvoiceService.CalculateTotal(
                    rental,
                    request.PhuThu,
                    request.GiamGia,
                    request.NgayTraPhong);

                InvoiceRow? existingInvoice = GetExistingInvoice(conn, tran, request.MaThue);
                if (existingInvoice is { IsPaid: true })
                {
                    throw new InvalidOperationException("Phiếu thuê này đã có hóa đơn đã thanh toán, không được thanh toán lại.");
                }

                int maNhanVien = ResolveEmployeeId(conn, tran, request.MaNhanVien, rental.MaNhanVien);
                int maHoaDon = existingInvoice?.MaHoaDon ?? InsertInvoice(conn, tran, request, rental, calculation, maNhanVien);
                if (existingInvoice != null)
                {
                    UpdateInvoice(conn, tran, maHoaDon, request, rental, calculation, maNhanVien);
                }

                ReplaceInvoiceDetails(conn, tran, maHoaDon, rental, calculation);
                MarkRentalReturned(conn, tran, request.MaThue, request.NgayTraPhong);
                MarkRoomsEmpty(conn, tran, request.MaThue);

                tran.Commit();
                return maHoaDon;
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        private static InvoiceRentalInfo LoadRentalInfo(SqlConnection conn, SqlTransaction? tran, int maThue)
        {
            string sql = @"
SELECT TOP 1
       PT.MaThue,
       PT.MaKH,
       PT.MaNV,
       PT.MaPhong,
       PT.MaDatPhong,
       KH.HoTen AS TenKhachHang,
       P.TenPhong AS SoPhong,
       LP.TenLoaiPhong,
       ISNULL(NULLIF(LP.DonGiaNgay, 0), ISNULL(NULLIF(LP.DonGiaDem, 0), ISNULL(LP.DonGiaGio, 0) * 24)) AS GiaPhong,
       PT.NgayNhan,
       ISNULL(PT.NgayTraPhong, PT.NgayTraDuKien) AS NgayTraPhong,
       ISNULL(PT.TienCoc, 0) AS TienCoc,
       PT.TrangThai
FROM dbo.PHIEUTHUE PT
JOIN dbo.KHACHHANG KH ON KH.MaKH = PT.MaKH
JOIN dbo.PHONG P ON P.MaPhong = PT.MaPhong
LEFT JOIN dbo.LOAIPHONG LP ON LP.MaLoaiPhong = P.MaLoaiPhong
WHERE PT.MaThue = @MaThue";

            using SqlCommand cmd = new(sql, conn, tran);
            cmd.Parameters.Add("@MaThue", SqlDbType.Int).Value = maThue;

            using SqlDataReader reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException("Không tìm thấy phiếu thuê trong cơ sở dữ liệu.");
            }

            InvoiceRentalInfo rental = new()
            {
                MaThue = reader.GetInt32(reader.GetOrdinal("MaThue")),
                MaKhachHang = reader.GetInt32(reader.GetOrdinal("MaKH")),
                MaNhanVien = reader.GetInt32(reader.GetOrdinal("MaNV")),
                MaPhong = reader.GetInt32(reader.GetOrdinal("MaPhong")),
                MaDatPhong = reader.IsDBNull(reader.GetOrdinal("MaDatPhong")) ? null : reader.GetInt32(reader.GetOrdinal("MaDatPhong")),
                TenKhachHang = reader["TenKhachHang"]?.ToString() ?? string.Empty,
                SoPhong = reader["SoPhong"]?.ToString() ?? string.Empty,
                TenLoaiPhong = reader["TenLoaiPhong"]?.ToString() ?? string.Empty,
                GiaPhong = ReadDecimal(reader, "GiaPhong"),
                NgayNhan = reader.GetDateTime(reader.GetOrdinal("NgayNhan")),
                NgayTraPhong = reader.GetDateTime(reader.GetOrdinal("NgayTraPhong")),
                TienCoc = ReadDecimal(reader, "TienCoc"),
                TrangThai = reader["TrangThai"]?.ToString() ?? string.Empty
            };

            reader.Close();
            rental.DichVu.AddRange(LoadServices(conn, tran, maThue));
            return rental;
        }

        private static List<InvoiceServiceLine> LoadServices(SqlConnection conn, SqlTransaction? tran, int maThue)
        {
            const string sql = @"
SELECT CT.MaDVVT,
       ISNULL(DV.TenDVVT, CT.GhiChu) AS TenDichVu,
       SUM(ISNULL(CT.SoLuong, 0)) AS SoLuong,
       ISNULL(NULLIF(CT.DonGia, 0), ISNULL(DV.DonGia, 0)) AS DonGia
FROM dbo.CHITIETPHATSINH CT
LEFT JOIN dbo.DICHVUVATTU DV ON DV.MaDVVT = CT.MaDVVT
WHERE CT.MaThue = @MaThue
  AND ISNULL(CT.TrangThai, 1) = 1
GROUP BY CT.MaDVVT, ISNULL(DV.TenDVVT, CT.GhiChu), ISNULL(NULLIF(CT.DonGia, 0), ISNULL(DV.DonGia, 0))
ORDER BY TenDichVu";

            using SqlCommand cmd = new(sql, conn, tran);
            cmd.Parameters.Add("@MaThue", SqlDbType.Int).Value = maThue;

            List<InvoiceServiceLine> result = new();
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new InvoiceServiceLine
                {
                    MaDichVu = reader.IsDBNull(reader.GetOrdinal("MaDVVT")) ? null : reader.GetInt32(reader.GetOrdinal("MaDVVT")),
                    TenDichVu = reader["TenDichVu"]?.ToString() ?? "Dịch vụ",
                    SoLuong = ReadDecimal(reader, "SoLuong"),
                    DonGia = ReadDecimal(reader, "DonGia")
                });
            }

            return result;
        }

        private static InvoiceRow? GetExistingInvoice(SqlConnection conn, SqlTransaction tran, int maThue)
        {
            const string sql = @"
SELECT MaHD, TrangThai, ISNULL(DaThanhToan, 0) AS DaThanhToan
FROM dbo.HOADON WITH (UPDLOCK, HOLDLOCK)
WHERE MaThue = @MaThue
  AND ISNULL(TrangThai, N'') NOT IN (N'Đã hủy', N'Da huy')
ORDER BY MaHD";

            using SqlCommand cmd = new(sql, conn, tran);
            cmd.Parameters.Add("@MaThue", SqlDbType.Int).Value = maThue;

            List<InvoiceRow> invoices = new();
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string trangThai = reader["TrangThai"]?.ToString() ?? string.Empty;
                decimal daThanhToan = ReadDecimal(reader, "DaThanhToan");
                invoices.Add(new InvoiceRow(
                    reader.GetInt32(reader.GetOrdinal("MaHD")),
                    IsPaidStatus(trangThai) || daThanhToan > 0));
            }

            if (invoices.Count > 1)
            {
                throw new InvalidOperationException("Phiếu thuê đã có nhiều hóa đơn. Vui lòng xử lý dữ liệu trùng trước khi thanh toán.");
            }

            return invoices.Count == 0 ? null : invoices[0];
        }

        private static int InsertInvoice(SqlConnection conn, SqlTransaction tran, InvoicePaymentRequest request, InvoiceRentalInfo rental, InvoiceCalculation calculation, int maNhanVien)
        {
            const string sql = @"
INSERT INTO dbo.HOADON
    (MaThue, MaNV, NgayLap, TongTienPhong, TongTienDV, TongPhuThu, GiamGia, TienCoc, TongThanhToan, TrangThai, MaKH, DaThanhToan, PhuongThuc, GhiChu)
VALUES
    (@MaThue, @MaNV, GETDATE(), @TienPhong, @TienDichVu, @PhuThu, @GiamGia, @TienCoc, @TongTien, @TrangThai, @MaKH, @DaThanhToan, @PhuongThuc, @GhiChu);
SELECT CONVERT(int, SCOPE_IDENTITY());";

            using SqlCommand cmd = new(sql, conn, tran);
            AddInvoiceParameters(cmd, request, rental, calculation, maNhanVien);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static void UpdateInvoice(SqlConnection conn, SqlTransaction tran, int maHoaDon, InvoicePaymentRequest request, InvoiceRentalInfo rental, InvoiceCalculation calculation, int maNhanVien)
        {
            const string sql = @"
UPDATE dbo.HOADON
SET MaNV = @MaNV,
    NgayLap = GETDATE(),
    TongTienPhong = @TienPhong,
    TongTienDV = @TienDichVu,
    TongPhuThu = @PhuThu,
    GiamGia = @GiamGia,
    TienCoc = @TienCoc,
    TongThanhToan = @TongTien,
    TrangThai = @TrangThai,
    MaKH = @MaKH,
    DaThanhToan = @DaThanhToan,
    PhuongThuc = @PhuongThuc,
    GhiChu = @GhiChu
WHERE MaHD = @MaHD";

            using SqlCommand cmd = new(sql, conn, tran);
            AddInvoiceParameters(cmd, request, rental, calculation, maNhanVien);
            cmd.Parameters.Add("@MaHD", SqlDbType.Int).Value = maHoaDon;
            cmd.ExecuteNonQuery();
        }

        private static void AddInvoiceParameters(SqlCommand cmd, InvoicePaymentRequest request, InvoiceRentalInfo rental, InvoiceCalculation calculation, int maNhanVien)
        {
            cmd.Parameters.Add("@MaThue", SqlDbType.Int).Value = request.MaThue;
            cmd.Parameters.Add("@MaNV", SqlDbType.Int).Value = maNhanVien;
            cmd.Parameters.Add("@TienPhong", SqlDbType.Decimal).Value = calculation.TienPhong;
            cmd.Parameters.Add("@TienDichVu", SqlDbType.Decimal).Value = calculation.TienDichVu;
            cmd.Parameters.Add("@PhuThu", SqlDbType.Decimal).Value = calculation.PhuThu;
            cmd.Parameters.Add("@GiamGia", SqlDbType.Decimal).Value = calculation.GiamGia;
            cmd.Parameters.Add("@TienCoc", SqlDbType.Decimal).Value = rental.TienCoc;
            cmd.Parameters.Add("@TongTien", SqlDbType.Decimal).Value = calculation.TongTien;
            cmd.Parameters.Add("@TrangThai", SqlDbType.NVarChar, 50).Value = PaidStatusCanonical;
            cmd.Parameters.Add("@MaKH", SqlDbType.Int).Value = rental.MaKhachHang;
            cmd.Parameters.Add("@DaThanhToan", SqlDbType.Decimal).Value = 1;
            cmd.Parameters.Add("@PhuongThuc", SqlDbType.NVarChar, 50).Value = request.PhuongThuc;
            cmd.Parameters.Add("@GhiChu", SqlDbType.NVarChar, 500).Value = (object?)request.GhiChu ?? DBNull.Value;
        }

        private static void ReplaceInvoiceDetails(SqlConnection conn, SqlTransaction tran, int maHoaDon, InvoiceRentalInfo rental, InvoiceCalculation calculation)
        {
            using (SqlCommand delete = new("DELETE FROM dbo.CHITIETHOADON WHERE MaHD = @MaHD", conn, tran))
            {
                delete.Parameters.Add("@MaHD", SqlDbType.Int).Value = maHoaDon;
                delete.ExecuteNonQuery();
            }

            InsertInvoiceDetail(conn, tran, maHoaDon, "Tiền phòng", calculation.SoNgay, rental.GiaPhong, calculation.TienPhong, null);

            foreach (InvoiceServiceLine service in rental.DichVu)
            {
                InsertInvoiceDetail(conn, tran, maHoaDon, service.TenDichVu, service.SoLuong, service.DonGia, service.ThanhTien, service.MaDichVu);
            }

            if (calculation.PhuThu > 0)
            {
                InsertInvoiceDetail(conn, tran, maHoaDon, "Phụ thu", 1, calculation.PhuThu, calculation.PhuThu, null);
            }
        }

        private static void InsertInvoiceDetail(SqlConnection conn, SqlTransaction tran, int maHoaDon, string noiDung, decimal soLuong, decimal donGia, decimal thanhTien, int? maDichVu)
        {
            const string sql = @"
INSERT INTO dbo.CHITIETHOADON (MaHD, NoiDung, SoLuong, DonGia, ThanhTien, MaDVVT)
VALUES (@MaHD, @NoiDung, @SoLuong, @DonGia, @ThanhTien, @MaDVVT)";

            using SqlCommand cmd = new(sql, conn, tran);
            cmd.Parameters.Add("@MaHD", SqlDbType.Int).Value = maHoaDon;
            cmd.Parameters.Add("@NoiDung", SqlDbType.NVarChar, 200).Value = noiDung;
            cmd.Parameters.Add("@SoLuong", SqlDbType.Decimal).Value = soLuong;
            cmd.Parameters.Add("@DonGia", SqlDbType.Decimal).Value = donGia;
            cmd.Parameters.Add("@ThanhTien", SqlDbType.Decimal).Value = thanhTien;
            cmd.Parameters.Add("@MaDVVT", SqlDbType.Int).Value = (object?)maDichVu ?? DBNull.Value;
            cmd.ExecuteNonQuery();
        }

        private static void MarkRentalReturned(SqlConnection conn, SqlTransaction tran, int maThue, DateTime ngayTraPhong)
        {
            const string sql = @"
UPDATE dbo.PHIEUTHUE
SET TrangThai = @TrangThai,
    NgayTraPhong = @NgayTraPhong
WHERE MaThue = @MaThue";

            using SqlCommand cmd = new(sql, conn, tran);
            cmd.Parameters.Add("@TrangThai", SqlDbType.NVarChar, 50).Value = ReturnedRentalStatusCanonical;
            cmd.Parameters.Add("@NgayTraPhong", SqlDbType.DateTime).Value = ngayTraPhong;
            cmd.Parameters.Add("@MaThue", SqlDbType.Int).Value = maThue;
            cmd.ExecuteNonQuery();
        }

        private static void MarkRoomsEmpty(SqlConnection conn, SqlTransaction tran, int maThue)
        {
            const string singleRoomSql = @"
UPDATE P
SET P.TrangThai = @TrangThai
FROM dbo.PHONG P
JOIN dbo.PHIEUTHUE PT ON PT.MaPhong = P.MaPhong
WHERE PT.MaThue = @MaThue";

            using (SqlCommand cmd = new(singleRoomSql, conn, tran))
            {
                cmd.Parameters.Add("@TrangThai", SqlDbType.NVarChar, 50).Value = DirtyRoomStatusCanonical;
                cmd.Parameters.Add("@MaThue", SqlDbType.Int).Value = maThue;
                cmd.ExecuteNonQuery();
            }

            const string groupRoomSql = @"
IF OBJECT_ID(N'dbo.CHITIETDATPHONG', N'U') IS NOT NULL
BEGIN
    UPDATE P
    SET P.TrangThai = @TrangThai
    FROM dbo.PHONG P
    JOIN dbo.CHITIETDATPHONG CT ON CT.MaPhong = P.MaPhong
    JOIN dbo.PHIEUTHUE PT ON PT.MaDatPhong = CT.MaDatPhong
    WHERE PT.MaThue = @MaThue
END";

            using SqlCommand groupCmd = new(groupRoomSql, conn, tran);
            groupCmd.Parameters.Add("@TrangThai", SqlDbType.NVarChar, 50).Value = DirtyRoomStatusCanonical;
            groupCmd.Parameters.Add("@MaThue", SqlDbType.Int).Value = maThue;
            groupCmd.ExecuteNonQuery();
        }

        private static bool IsPaidStatus(string trangThai)
        {
            string normalized = BoDau(trangThai).ToLowerInvariant();
            return normalized.Contains("da thanh toan") ||
                   normalized.Contains("closed") ||
                   normalized.Contains("da dong");
        }

        private static string BoDau(string value)
        {
            string formD = (value ?? string.Empty).Normalize(System.Text.NormalizationForm.FormD);
            string withoutMarks = new(formD
                .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray());
            return withoutMarks
                .Replace("đ", "d")
                .Replace("Đ", "D")
                .Normalize(System.Text.NormalizationForm.FormC);
        }

        private static int ResolveEmployeeId(SqlConnection conn, SqlTransaction tran, int currentEmployeeId, int rentalEmployeeId)
        {
            if (currentEmployeeId > 0)
            {
                return currentEmployeeId;
            }

            throw new InvalidOperationException("Tài khoản đăng nhập chưa liên kết nhân viên. Không thể lập/thanh toán hóa đơn.");
        }

        private static decimal ReadDecimal(SqlDataReader reader, string columnName)
        {
            object value = reader[columnName];
            return value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private sealed record InvoiceRow(int MaHoaDon, bool IsPaid);
    }

    public sealed class InvoiceRentalInfo
    {
        public int MaThue { get; set; }
        public int MaKhachHang { get; set; }
        public int MaNhanVien { get; set; }
        public int MaPhong { get; set; }
        public int? MaDatPhong { get; set; }
        public string TenKhachHang { get; set; } = string.Empty;
        public string SoPhong { get; set; } = string.Empty;
        public string TenLoaiPhong { get; set; } = string.Empty;
        public decimal GiaPhong { get; set; }
        public DateTime NgayNhan { get; set; }
        public DateTime NgayTraPhong { get; set; }
        public decimal TienCoc { get; set; }
        public string TrangThai { get; set; } = string.Empty;
        public List<InvoiceServiceLine> DichVu { get; } = new();
    }

    public sealed class InvoiceServiceLine
    {
        public int? MaDichVu { get; set; }
        public string TenDichVu { get; set; } = string.Empty;
        public decimal SoLuong { get; set; }
        public decimal DonGia { get; set; }
        public decimal ThanhTien => SoLuong * DonGia;
    }

    public sealed record InvoicePaymentRequest(
        int MaThue,
        int MaNhanVien,
        DateTime NgayTraPhong,
        decimal PhuThu,
        decimal GiamGia,
        string PhuongThuc,
        string? GhiChu);
}
