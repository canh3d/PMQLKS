using System.Data;
using Microsoft.Data.SqlClient;
using QLKS_AnPhu.DAL;

namespace QLKS_AnPhu.Security
{
    public static class PermissionService
    {
        public const string TrangChu = "TRANG_CHU";
        public const string QLPhong = "QL_PHONG";
        public const string PhieuThue = "PHIEU_THUE";
        public const string KhachHang = "KHACH_HANG";
        public const string HoaDon = "HOA_DON";
        public const string DichVuVatTu = "DICH_VU_VAT_TU";
        public const string NhanVien = "NHAN_VIEN";
        public const string BaoCao = "BAO_CAO";

        public static IReadOnlyList<PermissionDefinition> AllPermissions { get; } =
        [
            new PermissionDefinition(TrangChu, "Trang chủ"),
            new PermissionDefinition(QLPhong, "Quản lý phòng"),
            new PermissionDefinition(PhieuThue, "Phiếu thuê"),
            new PermissionDefinition(KhachHang, "Khách hàng"),
            new PermissionDefinition(HoaDon, "Hóa đơn"),
            new PermissionDefinition(DichVuVatTu, "Dịch vụ - vật tư"),
            new PermissionDefinition(NhanVien, "Nhân viên"),
            new PermissionDefinition(BaoCao, "Báo cáo")
        ];

        public static void EnsurePermissionTable()
        {
            const string sql = @"
IF OBJECT_ID(N'dbo.PHANQUYENTAIKHOAN', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PHANQUYENTAIKHOAN
    (
        MaTK int NOT NULL,
        MaChucNang nvarchar(50) NOT NULL,
        CONSTRAINT PK_PHANQUYENTAIKHOAN PRIMARY KEY (MaTK, MaChucNang),
        CONSTRAINT FK_PHANQUYENTAIKHOAN_TAIKHOAN FOREIGN KEY (MaTK) REFERENCES dbo.TAIKHOAN(MaTK) ON DELETE CASCADE
    );
END";

            ConnectDB.ExecuteNonQuery(sql);
        }

        public static HashSet<string> GetPermissions(int maTk)
        {
            EnsurePermissionTable();
            DataTable table = ConnectDB.GetData(
                "SELECT MaChucNang FROM PHANQUYENTAIKHOAN WHERE MaTK = @MaTK",
                new SqlParameter("@MaTK", maTk));

            return table.Rows
                .Cast<DataRow>()
                .Select(row => row["MaChucNang"].ToString() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public static void SavePermissions(int maTk, IEnumerable<string> permissions)
        {
            EnsurePermissionTable();
            using SqlConnection connection = ConnectDB.GetConnection();
            using SqlTransaction transaction = connection.BeginTransaction();

            try
            {
                using (SqlCommand deleteCommand = new("DELETE FROM PHANQUYENTAIKHOAN WHERE MaTK = @MaTK", connection, transaction))
                {
                    deleteCommand.Parameters.AddWithValue("@MaTK", maTk);
                    deleteCommand.ExecuteNonQuery();
                }

                foreach (string permission in permissions.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    using SqlCommand insertCommand = new(
                        "INSERT INTO PHANQUYENTAIKHOAN (MaTK, MaChucNang) VALUES (@MaTK, @MaChucNang)",
                        connection,
                        transaction);
                    insertCommand.Parameters.AddWithValue("@MaTK", maTk);
                    insertCommand.Parameters.AddWithValue("@MaChucNang", permission);
                    insertCommand.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public static IEnumerable<string> DefaultPermissionsForRole(string vaiTro)
        {
            if (vaiTro.Equals("Quản lý", StringComparison.OrdinalIgnoreCase))
            {
                return AllPermissions.Select(item => item.Code);
            }

            return new[] { TrangChu, QLPhong, PhieuThue, KhachHang };
        }
    }
}
