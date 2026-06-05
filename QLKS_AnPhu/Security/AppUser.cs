namespace QLKS_AnPhu.Security
{
    public sealed class AppUser
    {
        public int MaTK { get; init; }
        public string TenDangNhap { get; init; } = string.Empty;
        public string VaiTro { get; init; } = string.Empty;
        public int? MaNV { get; init; }
        public string HoTenNhanVien { get; init; } = string.Empty;
        public HashSet<string> Permissions { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsManager => VaiTro.Equals("Quản lý", StringComparison.OrdinalIgnoreCase);

        public bool CanAccess(string permissionCode)
        {
            return IsManager || Permissions.Contains(permissionCode);
        }
    }
}
