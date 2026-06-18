using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QLKS_AnPhu.DAL;
using QLKS_AnPhu.Services;

namespace QLKS_AnPhu.View
{
    public partial class NhanVien : UserControl
    {
        private readonly ObservableCollection<NhanVienItem> nhanVienHienThi = new();
        private readonly List<NhanVienItem> danhSachGoc = new();
        private DateTime ngayDauTuan;

        public NhanVien()
        {
            InitializeComponent();
            SearchSuggestionService.Attach(TxtTimKiem, TaoGoiYTimKiem, _ => TimKiem());
            DgNhanVien.ItemsSource = nhanVienHienThi;
            DpNgayBatDau.SelectedDate = DateTime.Today;
            DpNgayKetThuc.SelectedDate = DateTime.Today;
            ngayDauTuan = GetMonday(DateTime.Today);
            Loaded += NhanVien_Loaded;
        }

        private void NhanVien_Loaded(object sender, RoutedEventArgs e)
        {
            TaiDuLieu();
        }

        private void TaiDuLieu()
        {
            try
            {
                TxtLoi.Text = string.Empty;
                danhSachGoc.Clear();

                const string sql = @"
SELECT
    nv.MaNV,
    nv.HoTen,
    ISNULL(nv.GioiTinh, N'Chưa cập nhật') AS GioiTinh,
    nv.NgaySinh,
    ISNULL(nv.SDT, N'Chưa cập nhật') AS SDT,
    ISNULL(nv.DiaChi, N'Chưa cập nhật') AS DiaChi,
    ISNULL(nv.ChucVu, N'Nhân viên') AS ChucVu,
    nv.TrangThai,
    ISNULL(ca.MaCa, 0) AS MaCaLamViec,
    ISNULL(ca.TenCa, N'Chưa phân ca') AS CaLamViec
FROM NHANVIEN nv
OUTER APPLY
(
    SELECT TOP 1 cl.MaCa, cl.TenCa
    FROM PHANCONGCA pc
    INNER JOIN CALAM cl ON cl.MaCa = pc.MaCa
    WHERE pc.MaNV = nv.MaNV
    ORDER BY pc.NgayLam DESC, pc.MaPhanCong DESC
) ca
ORDER BY nv.MaNV;";

                DataTable table = ConnectDB.GetData(sql);
                foreach (DataRow row in table.Rows)
                {
                    danhSachGoc.Add(NhanVienItem.FromDataRow(row));
                }

                HienThiDanhSach(danhSachGoc);
                TaiLichTuan();
            }
            catch (Exception ex)
            {
                danhSachGoc.Clear();
                HienThiDanhSach(danhSachGoc);
                DgLichTuan.ItemsSource = null;
                TxtLoi.Text = "Không tải được dữ liệu nhân viên từ database: " + ex.Message;
            }
        }

        private void HienThiDanhSach(IEnumerable<NhanVienItem> danhSach)
        {
            nhanVienHienThi.Clear();
            foreach (NhanVienItem item in danhSach)
            {
                nhanVienHienThi.Add(item);
            }

            TxtTongDong.Text = $"Tổng: {nhanVienHienThi.Count} nhân viên";
            TxtTongNhanVienHeader.Text = danhSachGoc.Count.ToString();
            TxtStatTong.Text = danhSachGoc.Count.ToString();
            CapNhatThongKeCa();

            if (nhanVienHienThi.Count > 0)
            {
                DgNhanVien.SelectedIndex = 0;
            }
            else
            {
                DataContext = null;
            }
        }

        private void CapNhatThongKeCa()
        {
            TxtStatSang.Text = danhSachGoc.Count(item => item.MaCaLamViec == 1).ToString();
            TxtStatChieu.Text = danhSachGoc.Count(item => item.MaCaLamViec == 2).ToString();
            TxtStatDem.Text = danhSachGoc.Count(item => item.MaCaLamViec == 3).ToString();
        }

        private void TaiLichTuan()
        {
            DateTime ngayCuoiTuan = ngayDauTuan.AddDays(6);
            TxtTuanLamViec.Text = $"{ngayDauTuan:dd/MM/yyyy} - {ngayCuoiTuan:dd/MM/yyyy}";

            const string sql = @"
SELECT nv.MaNV, nv.HoTen, pc.NgayLam, cl.MaCa, cl.TenCa
FROM NHANVIEN nv
LEFT JOIN PHANCONGCA pc
    ON pc.MaNV = nv.MaNV
    AND pc.NgayLam BETWEEN @TuNgay AND @DenNgay
LEFT JOIN CALAM cl ON cl.MaCa = pc.MaCa
ORDER BY nv.MaNV, pc.NgayLam;";

            DataTable table = ConnectDB.GetData(
                sql,
                new SqlParameter("@TuNgay", SqlDbType.Date) { Value = ngayDauTuan.Date },
                new SqlParameter("@DenNgay", SqlDbType.Date) { Value = ngayCuoiTuan.Date });

            List<LichTuanItem> lich = danhSachGoc
                .Select(item => new LichTuanItem { MaNV = item.MaNV, HoTen = item.HoTen })
                .ToList();

            foreach (DataRow row in table.Rows)
            {
                if (row["NgayLam"] == DBNull.Value)
                {
                    continue;
                }

                int maNv = Convert.ToInt32(row["MaNV"]);
                LichTuanItem? item = lich.FirstOrDefault(x => x.MaNV == maNv);
                if (item == null)
                {
                    continue;
                }

                DateTime ngayLam = Convert.ToDateTime(row["NgayLam"]);
                string tenCa = row["TenCa"]?.ToString() ?? string.Empty;
                int maCa = row["MaCa"] == DBNull.Value ? 0 : Convert.ToInt32(row["MaCa"]);
                item.SetShift(ngayLam.DayOfWeek, FormatTenCa(maCa, tenCa));
            }

            DgLichTuan.ItemsSource = lich;
        }

        private static string FormatTenCa(int maCa, string tenCa)
        {
            return maCa switch
            {
                1 => "Ca sáng\n06:00-14:00",
                2 => "Ca chiều\n14:00-22:00",
                3 => "Ca đêm\n22:00-06:00",
                _ => string.IsNullOrWhiteSpace(tenCa) ? "-" : tenCa
            };
        }

        private void BtnPhanCongCa_Click(object sender, RoutedEventArgs e)
        {
            if (DgNhanVien.SelectedItem is not NhanVienItem selected)
            {
                MessageBox.Show("Vui lòng chọn nhân viên cần phân công ca.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DpNgayBatDau.SelectedDate == null || DpNgayKetThuc.SelectedDate == null)
            {
                MessageBox.Show("Vui lòng chọn ngày bắt đầu và ngày kết thúc.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime tuNgay = DpNgayBatDau.SelectedDate.Value.Date;
            DateTime denNgay = DpNgayKetThuc.SelectedDate.Value.Date;
            if (denNgay < tuNgay)
            {
                MessageBox.Show("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int maCa = RbCaChieu.IsChecked == true ? 2 : RbCaDem.IsChecked == true ? 3 : 1;
            string tenCa = maCa == 1 ? "Ca sáng" : maCa == 2 ? "Ca chiều" : "Ca đêm";

            try
            {
                bool coGhiChu = ViewSchemaHelper.ColumnExists("PHANCONGCA", "GhiChu");
                for (DateTime ngay = tuNgay; ngay <= denNgay; ngay = ngay.AddDays(1))
                {
                    if (!coGhiChu)
                    {
                        const string sqlKhongGhiChu = @"
IF EXISTS (SELECT 1 FROM PHANCONGCA WHERE MaNV = @MaNV AND NgayLam = @NgayLam)
BEGIN
    UPDATE PHANCONGCA
    SET MaCa = @MaCa, TrangThai = N'Đang hoạt động'
    WHERE MaNV = @MaNV AND NgayLam = @NgayLam
END
ELSE
BEGIN
    INSERT INTO PHANCONGCA (MaNV, MaCa, NgayLam, TrangThai)
    VALUES (@MaNV, @MaCa, @NgayLam, N'Đang hoạt động')
END";

                        ConnectDB.ExecuteNonQuery(
                            sqlKhongGhiChu,
                            new SqlParameter("@MaNV", selected.MaNV),
                            new SqlParameter("@MaCa", maCa),
                            new SqlParameter("@NgayLam", SqlDbType.Date) { Value = ngay.Date });
                        continue;
                    }

                    const string sql = @"
IF EXISTS (SELECT 1 FROM PHANCONGCA WHERE MaNV = @MaNV AND NgayLam = @NgayLam)
BEGIN
    UPDATE PHANCONGCA
    SET MaCa = @MaCa, GhiChu = @GhiChu, TrangThai = N'Đang hoạt động'
    WHERE MaNV = @MaNV AND NgayLam = @NgayLam
END
ELSE
BEGIN
    INSERT INTO PHANCONGCA (MaNV, MaCa, NgayLam, GhiChu, TrangThai)
    VALUES (@MaNV, @MaCa, @NgayLam, @GhiChu, N'Đang hoạt động')
END";

                    ConnectDB.ExecuteNonQuery(
                        sql,
                        new SqlParameter("@MaNV", selected.MaNV),
                        new SqlParameter("@MaCa", maCa),
                        new SqlParameter("@NgayLam", SqlDbType.Date) { Value = ngay.Date },
                        new SqlParameter("@GhiChu", $"Phân công {tenCa.ToLower()}"));
                }

                MessageBox.Show("Phân công ca thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không phân công được ca làm việc: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TimKiem()
        {
            string keyword = TxtTimKiem.Text.Trim();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                HienThiDanhSach(danhSachGoc);
                return;
            }

            HienThiDanhSach(danhSachGoc.Where(item =>
                item.MaHienThi.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.HoTen.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.GioiTinh.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.ChucVu.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.SDT.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.CaLamViec.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.TrangThaiHienThi.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }

        private void TxtTimKiem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TimKiem();
            }
        }

        private IEnumerable<SearchSuggestionItem> TaoGoiYTimKiem()
        {
            foreach (NhanVienItem item in danhSachGoc)
            {
                if (!string.IsNullOrWhiteSpace(item.MaHienThi))
                {
                    yield return new SearchSuggestionItem(item.MaHienThi, $"{item.MaHienThi} - {item.HoTen}");
                }

                if (!string.IsNullOrWhiteSpace(item.HoTen))
                {
                    yield return new SearchSuggestionItem(item.HoTen, $"{item.HoTen} - {item.ChucVu}");
                }

                if (!string.IsNullOrWhiteSpace(item.SDT))
                {
                    yield return new SearchSuggestionItem(item.SDT, $"{item.SDT} - {item.HoTen}");
                }

                if (!string.IsNullOrWhiteSpace(item.CaLamViec))
                {
                    yield return new SearchSuggestionItem(item.CaLamViec, $"{item.CaLamViec} - {item.HoTen}");
                }
            }
        }

        private void BtnLamMoi_Click(object sender, RoutedEventArgs e)
        {
            TxtTimKiem.Clear();
            TaiDuLieu();
        }

        private void BtnHomNay_Click(object sender, RoutedEventArgs e)
        {
            ngayDauTuan = GetMonday(DateTime.Today);
            try
            {
                TaiLichTuan();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không tải được lịch tuần: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DgNhanVien_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DataContext = DgNhanVien.SelectedItem as NhanVienItem;
        }

        private void DgNhanVien_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgNhanVien.SelectedItem is not NhanVienItem selected)
            {
                return;
            }

            NhanVienTaiKhoanWindow window = new(selected)
            {
                Owner = Window.GetWindow(this)
            };

            window.ShowDialog();
            TaiDuLieu();
        }

        private void BtnThem_Click(object sender, RoutedEventArgs e)
        {
            NhanVienForm form = new()
            {
                Owner = Window.GetWindow(this)
            };

            if (form.ShowDialog() != true)
            {
                return;
            }

            try
            {
                if (KiemTraNhanVienBiTrung(form))
                {
                    return;
                }

                const string sql = @"
INSERT INTO NHANVIEN (HoTen, GioiTinh, NgaySinh, SDT, DiaChi, ChucVu, TrangThai)
VALUES (@HoTen, @GioiTinh, @NgaySinh, @SDT, @DiaChi, @ChucVu, @TrangThai);";

                ConnectDB.ExecuteNonQuery(sql, form.ToSqlParameters());
                MessageBox.Show("Thêm nhân viên thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thêm được nhân viên: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSua_Click(object sender, RoutedEventArgs e)
        {
            if (DgNhanVien.SelectedItem is not NhanVienItem selected)
            {
                MessageBox.Show("Vui lòng chọn nhân viên cần sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NhanVienForm form = new(selected)
            {
                Owner = Window.GetWindow(this)
            };

            if (form.ShowDialog() != true)
            {
                return;
            }

            try
            {
                if (KiemTraNhanVienBiTrung(form, selected.MaNV))
                {
                    return;
                }

                const string sql = @"
UPDATE NHANVIEN
SET HoTen = @HoTen,
    GioiTinh = @GioiTinh,
    NgaySinh = @NgaySinh,
    SDT = @SDT,
    DiaChi = @DiaChi,
    ChucVu = @ChucVu,
    TrangThai = @TrangThai
WHERE MaNV = @MaNV;";

                List<SqlParameter> parameters = form.ToSqlParameters().ToList();
                parameters.Add(new SqlParameter("@MaNV", selected.MaNV));
                ConnectDB.ExecuteNonQuery(sql, parameters.ToArray());
                MessageBox.Show("Sửa nhân viên thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không sửa được nhân viên: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool KiemTraNhanVienBiTrung(NhanVienForm form, int? maNhanVienBoQua = null)
        {
            if (string.IsNullOrWhiteSpace(form.SoDienThoai) && !form.NgaySinh.HasValue)
            {
                return false;
            }

            const string sql = @"
SELECT TOP 1
    MaNV,
    HoTen,
    CASE
        WHEN @SDT <> '' AND
             REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ISNULL(SDT, ''))), ' ', ''), '.', ''), '-', ''), '+84', '0')
             = REPLACE(REPLACE(REPLACE(REPLACE(@SDT, ' ', ''), '.', ''), '-', ''), '+84', '0')
            THEN N'Số điện thoại'
        ELSE N'Họ tên và ngày sinh'
    END AS LyDoTrung
FROM NHANVIEN
WHERE (@MaNVBoQua IS NULL OR MaNV <> @MaNVBoQua)
  AND
  (
      (
          @SDT <> '' AND
          REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ISNULL(SDT, ''))), ' ', ''), '.', ''), '-', ''), '+84', '0')
          = REPLACE(REPLACE(REPLACE(REPLACE(@SDT, ' ', ''), '.', ''), '-', ''), '+84', '0')
      )
      OR
      (
          @NgaySinh IS NOT NULL
          AND NgaySinh = @NgaySinh
          AND UPPER(LTRIM(RTRIM(HoTen))) = UPPER(LTRIM(RTRIM(@HoTen)))
      )
  )
ORDER BY MaNV;";

            DataTable duplicate = ConnectDB.GetData(
                sql,
                new SqlParameter("@MaNVBoQua", SqlDbType.Int)
                {
                    Value = maNhanVienBoQua.HasValue ? maNhanVienBoQua.Value : DBNull.Value
                },
                new SqlParameter("@SDT", form.SoDienThoai),
                new SqlParameter("@NgaySinh", SqlDbType.Date)
                {
                    Value = form.NgaySinh.HasValue ? form.NgaySinh.Value : DBNull.Value
                },
                new SqlParameter("@HoTen", form.HoTen));

            if (duplicate.Rows.Count == 0)
            {
                return false;
            }

            DataRow row = duplicate.Rows[0];
            string maNhanVien = $"NV{Convert.ToInt32(row["MaNV"]):000}";
            string hoTen = row["HoTen"]?.ToString() ?? string.Empty;
            string lyDoTrung = row["LyDoTrung"]?.ToString() ?? "Thông tin định danh";

            MessageBox.Show(
                $"{lyDoTrung} đã thuộc về nhân viên {maNhanVien} - {hoTen}. Vui lòng kiểm tra lại.",
                "Nhân viên đã tồn tại",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return true;
        }

        private void BtnXoa_Click(object sender, RoutedEventArgs e)
        {
            if (DgNhanVien.SelectedItem is not NhanVienItem selected)
            {
                MessageBox.Show("Vui lòng chọn nhân viên cần xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(
                $"Bạn có chắc muốn xóa nhân viên '{selected.HoTen}'? Thao tác này sẽ xóa cả tài khoản và lịch phân công ca liên quan nếu có.",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                XoaNhanVien(selected.MaNV);
                MessageBox.Show("Đã xóa nhân viên.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không xóa được nhân viên: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void XoaNhanVien(int maNhanVien)
        {
            string lyDoKhongTheXoa = LayLyDoNhanVienDangDuocSuDung(maNhanVien);
            if (!string.IsNullOrWhiteSpace(lyDoKhongTheXoa))
            {
                throw new InvalidOperationException("Nhân viên đã có dữ liệu nghiệp vụ: " + lyDoKhongTheXoa + ". Vui lòng chuyển sang trạng thái tạm nghỉ nếu cần giữ lịch sử.");
            }

            using SqlConnection conn = ConnectDB.GetConnection();
            using SqlTransaction tran = conn.BeginTransaction();

            try
            {
                XoaTheoMaNhanVien(conn, tran, "PHANCONGCA", maNhanVien);

                if (ViewSchemaHelper.TableExists("TAIKHOAN") && ViewSchemaHelper.ColumnExists("TAIKHOAN", "MaNV"))
                {
                    List<int> maTaiKhoan = new();
                    using (SqlCommand selectTk = new("SELECT MaTK FROM dbo.TAIKHOAN WHERE MaNV = @MaNV", conn, tran))
                    {
                        selectTk.Parameters.Add("@MaNV", SqlDbType.Int).Value = maNhanVien;
                        using SqlDataReader reader = selectTk.ExecuteReader();
                        while (reader.Read())
                        {
                            maTaiKhoan.Add(Convert.ToInt32(reader["MaTK"]));
                        }
                    }

                    foreach (int maTk in maTaiKhoan)
                    {
                        XoaTheoKhoa(conn, tran, "PHANQUYENTAIKHOAN", "MaTK", maTk);
                    }

                    XoaTheoMaNhanVien(conn, tran, "TAIKHOAN", maNhanVien);
                }

                XoaTheoKhoa(conn, tran, "NHANVIEN", "MaNV", maNhanVien);
                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        private static string LayLyDoNhanVienDangDuocSuDung(int maNhanVien)
        {
            List<string> references = new();
            foreach ((string Table, string Column, string Label) item in new[]
                     {
                         ("DATPHONG", "MaNV", "đặt phòng"),
                         ("PHIEUTHUE", "MaNV", "phiếu thuê"),
                         ("HOADON", "MaNV", "hóa đơn"),
                         ("CHITIETPHATSINH", "MaNV", "dịch vụ phát sinh")
                     })
            {
                if (!ViewSchemaHelper.TableExists(item.Table) || !ViewSchemaHelper.ColumnExists(item.Table, item.Column))
                {
                    continue;
                }

                object? count = ConnectDB.ExecuteScalar(
                    "SELECT COUNT(*) FROM dbo." + item.Table + " WHERE " + item.Column + " = @MaNV",
                    new SqlParameter("@MaNV", maNhanVien));

                int total = Convert.ToInt32(count);
                if (total > 0)
                {
                    references.Add(item.Label + " (" + total + ")");
                }
            }

            return string.Join(", ", references);
        }

        private static void XoaTheoMaNhanVien(SqlConnection conn, SqlTransaction tran, string tableName, int maNhanVien)
        {
            if (!ViewSchemaHelper.TableExists(tableName) || !ViewSchemaHelper.ColumnExists(tableName, "MaNV"))
            {
                return;
            }

            using SqlCommand cmd = new("DELETE FROM dbo." + tableName + " WHERE MaNV = @MaNV", conn, tran);
            cmd.Parameters.Add("@MaNV", SqlDbType.Int).Value = maNhanVien;
            cmd.ExecuteNonQuery();
        }

        private static void XoaTheoKhoa(SqlConnection conn, SqlTransaction tran, string tableName, string keyColumn, int keyValue)
        {
            if (!ViewSchemaHelper.TableExists(tableName) || !ViewSchemaHelper.ColumnExists(tableName, keyColumn))
            {
                return;
            }

            using SqlCommand cmd = new("DELETE FROM dbo." + tableName + " WHERE " + keyColumn + " = @KeyValue", conn, tran);
            cmd.Parameters.Add("@KeyValue", SqlDbType.Int).Value = keyValue;
            cmd.ExecuteNonQuery();
        }
        private void BtnXuatExcel_Click(object sender, RoutedEventArgs e)
        {
            if (nhanVienHienThi.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu nhân viên để xuất Excel.", "Xuất Excel", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog saveFileDialog = new()
            {
                Title = "Xuất danh sách nhân viên",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"DanhSachNhanVien_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                ExcelSection section = new()
                {
                    Title = "Chi tiết nhân viên",
                    Headers = ["Mã NV", "Họ tên", "Giới tính", "Ngày sinh", "Chức vụ", "Số điện thoại", "Ca làm việc", "Trạng thái", "Địa chỉ"],
                    ColumnWidths = [11, 25, 12, 14, 18, 17, 18, 18, 30],
                    Summary = $"Tổng số nhân viên: {nhanVienHienThi.Count:N0}"
                };
                section.Rows.AddRange(nhanVienHienThi.Select(item => (IReadOnlyList<object?>)
                [
                    item.MaHienThi, item.HoTen, item.GioiTinh, item.NgaySinhHienThi,
                    item.ChucVu, item.SDT, item.CaLamViec, item.TrangThaiHienThi, item.DiaChi
                ]));

                ExcelDocument document = new()
                {
                    Title = "DANH SÁCH NHÂN VIÊN",
                    Subtitle = "Danh sách nhân viên đang hiển thị",
                    SheetName = "Nhân viên"
                };
                document.Sections.Add(section);
                ExcelExportService.Export(saveFileDialog.FileName, document);
                MessageBox.Show("Xuất Excel thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không xuất được Excel: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnXuatPdf_Click(object sender, RoutedEventArgs e)
        {
            PrintDanhSach("Xuất PDF danh sách nhân viên", pdfMode: true);
        }

        private void BtnInDanhSach_Click(object sender, RoutedEventArgs e)
        {
            PrintDanhSach("In danh sách nhân viên");
        }

        private void BtnLichLamViec_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DateTime denNgay = ngayDauTuan.AddDays(6);
                bool coGhiChu = ViewSchemaHelper.ColumnExists("PHANCONGCA", "GhiChu");
                if (!coGhiChu)
                {
                    DataTable tableKhongGhiChu = ConnectDB.GetData(
                        @"
SELECT
    nv.MaNV AS [Mã NV],
    nv.HoTen AS [Họ tên],
    cl.TenCa AS [Ca làm],
    CONVERT(varchar(10), pc.NgayLam, 103) AS [Ngày làm],
    CONVERT(varchar(5), cl.GioBatDau, 108) AS [Bắt đầu],
    CONVERT(varchar(5), cl.GioKetThuc, 108) AS [Kết thúc],
    ISNULL(pc.TrangThai, N'Đang hoạt động') AS [Trạng thái],
    CAST(N'' AS nvarchar(255)) AS [Ghi chú]
FROM PHANCONGCA pc
INNER JOIN NHANVIEN nv ON nv.MaNV = pc.MaNV
INNER JOIN CALAM cl ON cl.MaCa = pc.MaCa
WHERE pc.NgayLam BETWEEN @TuNgay AND @DenNgay
ORDER BY pc.NgayLam, cl.MaCa, nv.HoTen;",
                        new SqlParameter("@TuNgay", SqlDbType.Date) { Value = ngayDauTuan.Date },
                        new SqlParameter("@DenNgay", SqlDbType.Date) { Value = denNgay.Date });

                    ShowDataTableWindow($"Lịch làm việc tuần {ngayDauTuan:dd/MM/yyyy} - {denNgay:dd/MM/yyyy}", tableKhongGhiChu);
                    return;
                }
                DataTable table = ConnectDB.GetData(
                    @"
SELECT
    nv.MaNV AS [Mã NV],
    nv.HoTen AS [Họ tên],
    cl.TenCa AS [Ca làm],
    CONVERT(varchar(10), pc.NgayLam, 103) AS [Ngày làm],
    CONVERT(varchar(5), cl.GioBatDau, 108) AS [Bắt đầu],
    CONVERT(varchar(5), cl.GioKetThuc, 108) AS [Kết thúc],
    ISNULL(pc.TrangThai, N'Đang hoạt động') AS [Trạng thái],
    ISNULL(pc.GhiChu, N'') AS [Ghi chú]
FROM PHANCONGCA pc
INNER JOIN NHANVIEN nv ON nv.MaNV = pc.MaNV
INNER JOIN CALAM cl ON cl.MaCa = pc.MaCa
WHERE pc.NgayLam BETWEEN @TuNgay AND @DenNgay
ORDER BY pc.NgayLam, cl.MaCa, nv.HoTen;",
                    new SqlParameter("@TuNgay", SqlDbType.Date) { Value = ngayDauTuan.Date },
                    new SqlParameter("@DenNgay", SqlDbType.Date) { Value = denNgay.Date });

                ShowDataTableWindow($"Lịch làm việc tuần {ngayDauTuan:dd/MM/yyyy} - {denNgay:dd/MM/yyyy}", table);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không tải được lịch làm việc: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBangLuong_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DateTime tuNgay = new(DateTime.Today.Year, DateTime.Today.Month, 1);
                DateTime denNgay = tuNgay.AddMonths(1).AddDays(-1);

                DataTable table = ConnectDB.GetData(
                    @"
SELECT
    nv.MaNV AS [Mã NV],
    nv.HoTen AS [Họ tên],
    nv.ChucVu AS [Chức vụ],
    SUM(CASE WHEN pc.MaCa = 1 THEN 1 ELSE 0 END) AS [Ca sáng],
    SUM(CASE WHEN pc.MaCa = 2 THEN 1 ELSE 0 END) AS [Ca chiều],
    SUM(CASE WHEN pc.MaCa = 3 THEN 1 ELSE 0 END) AS [Ca đêm],
    COUNT(pc.MaPhanCong) AS [Tổng ca],
    CAST(
        SUM(CASE WHEN pc.MaCa = 1 THEN 250000 ELSE 0 END) +
        SUM(CASE WHEN pc.MaCa = 2 THEN 280000 ELSE 0 END) +
        SUM(CASE WHEN pc.MaCa = 3 THEN 350000 ELSE 0 END)
        AS decimal(18, 0)) AS [Lương dự kiến]
FROM NHANVIEN nv
LEFT JOIN PHANCONGCA pc
    ON pc.MaNV = nv.MaNV
    AND pc.NgayLam BETWEEN @TuNgay AND @DenNgay
GROUP BY nv.MaNV, nv.HoTen, nv.ChucVu
ORDER BY nv.MaNV;",
                    new SqlParameter("@TuNgay", SqlDbType.Date) { Value = tuNgay.Date },
                    new SqlParameter("@DenNgay", SqlDbType.Date) { Value = denNgay.Date });

                ShowDataTableWindow($"Bảng lương dự kiến tháng {DateTime.Today:MM/yyyy}", table);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không tải được bảng lương: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLichSuCaLam_Click(object sender, RoutedEventArgs e)
        {
            if (DgNhanVien.SelectedItem is not NhanVienItem selected)
            {
                MessageBox.Show("Vui lòng chọn nhân viên cần xem lịch sử ca làm.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (!ViewSchemaHelper.ColumnExists("PHANCONGCA", "GhiChu"))
                {
                    DataTable tableKhongGhiChu = ConnectDB.GetData(
                        @"
SELECT
    CONVERT(varchar(10), pc.NgayLam, 103) AS [Ngày làm],
    cl.TenCa AS [Ca làm],
    CONVERT(varchar(5), cl.GioBatDau, 108) AS [Bắt đầu],
    CONVERT(varchar(5), cl.GioKetThuc, 108) AS [Kết thúc],
    ISNULL(pc.TrangThai, N'Đang hoạt động') AS [Trạng thái],
    CAST(N'' AS nvarchar(255)) AS [Ghi chú]
FROM PHANCONGCA pc
INNER JOIN CALAM cl ON cl.MaCa = pc.MaCa
WHERE pc.MaNV = @MaNV
ORDER BY pc.NgayLam DESC, pc.MaPhanCong DESC;",
                        new SqlParameter("@MaNV", selected.MaNV));

                    ShowDataTableWindow($"Lịch sử ca làm - {selected.HoTen}", tableKhongGhiChu);
                    return;
                }

                DataTable table = ConnectDB.GetData(
                    @"
SELECT
    CONVERT(varchar(10), pc.NgayLam, 103) AS [Ngày làm],
    cl.TenCa AS [Ca làm],
    CONVERT(varchar(5), cl.GioBatDau, 108) AS [Bắt đầu],
    CONVERT(varchar(5), cl.GioKetThuc, 108) AS [Kết thúc],
    ISNULL(pc.TrangThai, N'Đang hoạt động') AS [Trạng thái],
    ISNULL(pc.GhiChu, N'') AS [Ghi chú]
FROM PHANCONGCA pc
INNER JOIN CALAM cl ON cl.MaCa = pc.MaCa
WHERE pc.MaNV = @MaNV
ORDER BY pc.NgayLam DESC, pc.MaPhanCong DESC;",
                    new SqlParameter("@MaNV", selected.MaNV));

                ShowDataTableWindow($"Lịch sử ca làm - {selected.HoTen}", table);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không tải được lịch sử ca làm: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintDanhSach(string description, bool pdfMode = false)
        {
            if (nhanVienHienThi.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu nhân viên để in.", pdfMode ? "Xuất PDF" : "In danh sách", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                FlowDocument document = CreateNhanVienDocument();
                PrintExportService.Print(document, description, pdfMode);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không in được danh sách: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FlowDocument CreateNhanVienDocument()
        {
            IReadOnlyList<PrintColumn> columns =
            [
                new("Mã NV", 58, TextAlignment.Center, true),
                new("Họ tên", new GridLength(1, GridUnitType.Star)),
                new("Giới tính", 65, TextAlignment.Center, true),
                new("Chức vụ", 90),
                new("Số điện thoại", 92, TextAlignment.Center, true),
                new("Ca làm việc", 95),
                new("Trạng thái", 88, TextAlignment.Center)
            ];

            return PrintExportService.CreateTableDocument(
                "Danh sách nhân viên",
                $"Danh sách đang hiển thị - Tổng cộng {nhanVienHienThi.Count:N0} nhân viên",
                columns,
                nhanVienHienThi.Select(item => (IReadOnlyList<string>)
                [
                    item.MaHienThi,
                    item.HoTen,
                    item.GioiTinh,
                    item.ChucVu,
                    item.SDT,
                    item.CaLamViec,
                    item.TrangThaiHienThi
                ]),
                $"Tổng số nhân viên: {nhanVienHienThi.Count:N0}");
        }

        private void ShowDataTableWindow(string title, DataTable table)
        {
            DataGrid dataGrid = new()
            {
                AutoGenerateColumns = true,
                IsReadOnly = true,
                CanUserAddRows = false,
                ItemsSource = table.DefaultView,
                Margin = new Thickness(16),
                FontSize = 14
            };

            Window window = new()
            {
                Title = title,
                Owner = Window.GetWindow(this),
                Width = 1050,
                Height = 620,
                MinWidth = 760,
                MinHeight = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Brushes.White,
                Content = dataGrid
            };

            window.ShowDialog();
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private static DateTime GetMonday(DateTime date)
        {
            int diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return date.Date.AddDays(-diff);
        }

        public sealed class NhanVienItem
        {
            public int MaNV { get; init; }
            public string MaHienThi => $"NV{MaNV:000}";
            public string HoTen { get; init; } = string.Empty;
            public string GioiTinh { get; init; } = string.Empty;
            public DateTime? NgaySinh { get; init; }
            public string NgaySinhHienThi => NgaySinh?.ToString("dd/MM/yyyy") ?? "Chưa cập nhật";
            public string SDT { get; init; } = string.Empty;
            public string CCCD { get; init; } = "Chưa cập nhật";
            public string DiaChi { get; init; } = string.Empty;
            public string Email { get; init; } = "Chưa cập nhật";
            public string ChucVu { get; init; } = string.Empty;
            public bool TrangThai { get; init; }
            public string TrangThaiHienThi => TrangThai ? "Đang làm việc" : "Tạm nghỉ";
            public int MaCaLamViec { get; init; }
            public string CaLamViec { get; init; } = string.Empty;

            public static NhanVienItem FromDataRow(DataRow row)
            {
                return new NhanVienItem
                {
                    MaNV = Convert.ToInt32(row["MaNV"]),
                    HoTen = row["HoTen"].ToString() ?? string.Empty,
                    GioiTinh = row["GioiTinh"].ToString() ?? string.Empty,
                    NgaySinh = row["NgaySinh"] == DBNull.Value ? null : Convert.ToDateTime(row["NgaySinh"]),
                    SDT = row["SDT"].ToString() ?? string.Empty,
                    DiaChi = row["DiaChi"].ToString() ?? string.Empty,
                    ChucVu = row["ChucVu"].ToString() ?? string.Empty,
                    TrangThai = row["TrangThai"] != DBNull.Value && Convert.ToBoolean(row["TrangThai"]),
                    MaCaLamViec = row.Table.Columns.Contains("MaCaLamViec") && row["MaCaLamViec"] != DBNull.Value ? Convert.ToInt32(row["MaCaLamViec"]) : 0,
                    CaLamViec = row["CaLamViec"].ToString() ?? string.Empty
                };
            }
        }

        private sealed class LichTuanItem
        {
            public int MaNV { get; init; }
            public string HoTen { get; init; } = string.Empty;
            public string Thu2 { get; private set; } = "-";
            public string Thu3 { get; private set; } = "-";
            public string Thu4 { get; private set; } = "-";
            public string Thu5 { get; private set; } = "-";
            public string Thu6 { get; private set; } = "-";
            public string Thu7 { get; private set; } = "-";
            public string ChuNhat { get; private set; } = "-";

            public void SetShift(DayOfWeek dayOfWeek, string value)
            {
                switch (dayOfWeek)
                {
                    case DayOfWeek.Monday:
                        Thu2 = value;
                        break;
                    case DayOfWeek.Tuesday:
                        Thu3 = value;
                        break;
                    case DayOfWeek.Wednesday:
                        Thu4 = value;
                        break;
                    case DayOfWeek.Thursday:
                        Thu5 = value;
                        break;
                    case DayOfWeek.Friday:
                        Thu6 = value;
                        break;
                    case DayOfWeek.Saturday:
                        Thu7 = value;
                        break;
                    case DayOfWeek.Sunday:
                        ChuNhat = value;
                        break;
                }
            }
        }
    }
}
