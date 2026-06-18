namespace QLKS_AnPhu.Security
{
    public static class CurrentUser
    {
        public static int MaNV { get; private set; }
        public static string HoTen { get; private set; } = string.Empty;
        public static string VaiTro { get; private set; } = string.Empty;
        public static string TenDangNhap { get; private set; } = string.Empty;

        public static bool IsManager => RoleHelper.IsManagerRole(VaiTro);

        public static void Set(AppUser user)
        {
            MaNV = user.MaNV ?? 0;
            HoTen = string.IsNullOrWhiteSpace(user.HoTenNhanVien) ? user.TenDangNhap : user.HoTenNhanVien;
            VaiTro = user.VaiTro;
            TenDangNhap = user.TenDangNhap;
        }

        public static void Clear()
        {
            MaNV = 0;
            HoTen = string.Empty;
            VaiTro = string.Empty;
            TenDangNhap = string.Empty;
        }
    }
}
