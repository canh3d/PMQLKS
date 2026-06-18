using System.Globalization;
using System.Text;

namespace QLKS_AnPhu.Security
{
    public static class RoleHelper
    {
        public static bool IsManagerRole(string? vaiTro)
        {
            string normalized = RemoveDiacritics(vaiTro).Trim().ToLowerInvariant();
            return normalized is "quan ly" or "quan tri" or "admin" or "administrator";
        }

        private static string RemoveDiacritics(string? value)
        {
            string formD = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
            StringBuilder builder = new();

            foreach (char ch in formD)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch == 'đ' ? 'd' : ch == 'Đ' ? 'D' : ch);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
