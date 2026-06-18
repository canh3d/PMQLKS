using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace QLKS_AnPhu.DAL
{
    public static class PhongTrangThaiSchema
    {
        // Hằng lưu trạng thái "Chưa dọn dẹp" dưới dạng Unicode để tránh lỗi font tiếng Việt
        public const string ChuaDonDep = "\u0043\u0068\u01b0\u0061 \u0064\u1ecdn \u0064\u1eb9\u0070";

        // Hàm đảm bảo bảng PHONG có trạng thái "Chưa dọn dẹp" trong CHECK constraint của cột TrangThai
        public static void DamBaoCoTrangThaiChuaDonDep(SqlConnection conn, SqlTransaction tran)
        {
            // Danh sách lưu tên constraint và nội dung định nghĩa của constraint
            List<(string Name, string Definition)> constraints = new();

            // Truy vấn các CHECK constraint của bảng dbo.PHONG có liên quan đến cột TrangThai
            using (SqlCommand cmd = new(
                       @"SELECT cc.name, cc.definition
                         FROM sys.check_constraints cc
                         JOIN sys.tables t ON cc.parent_object_id = t.object_id
                         JOIN sys.schemas s ON t.schema_id = s.schema_id
                         WHERE s.name = N'dbo'
                           AND t.name = N'PHONG'
                           AND cc.definition LIKE N'%TrangThai%'",
                       conn,
                       tran))
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                // Đọc toàn bộ constraint tìm được và đưa vào danh sách
                while (reader.Read())
                {
                    constraints.Add((reader.GetString(0), reader.GetString(1)));
                }
            }

            // Nếu không có constraint nào thì thoát, không cần xử lý
            if (constraints.Count == 0)
            {
                return;
            }

            // Tách các giá trị trạng thái đang được phép trong CHECK constraint hiện tại
            HashSet<string> allowed = constraints
                .SelectMany(item => Regex.Matches(item.Definition, @"N?'((?:''|[^'])*)'")
                    .Select(match => match.Groups[1].Value.Replace("''", "'")))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Nếu trạng thái "Chưa dọn dẹp" đã tồn tại thì không cần thêm nữa
            if (allowed.Contains(ChuaDonDep))
            {
                return;
            }

            // Thêm trạng thái "Chưa dọn dẹp" vào danh sách trạng thái hợp lệ
            allowed.Add(ChuaDonDep);

            // Xóa các CHECK constraint cũ liên quan đến TrangThai
            foreach ((string name, _) in constraints)
            {
                using SqlCommand drop = new(
                    "ALTER TABLE dbo.PHONG DROP CONSTRAINT [" + name.Replace("]", "]]") + "]",
                    conn,
                    tran);
                drop.ExecuteNonQuery();
            }

            // Ghép danh sách trạng thái thành chuỗi SQL dạng N'...'
            string values = string.Join(", ", allowed.Select(value => "N'" + value.Replace("'", "''") + "'"));

            // Tạo lại CHECK constraint mới, có bổ sung trạng thái "Chưa dọn dẹp"
            using SqlCommand add = new(
                "ALTER TABLE dbo.PHONG WITH CHECK ADD CONSTRAINT CK_PHONG_TrangThai CHECK ([TrangThai] IN (" + values + "))",
                conn,
                tran);
            add.ExecuteNonQuery();
        }
    }
}