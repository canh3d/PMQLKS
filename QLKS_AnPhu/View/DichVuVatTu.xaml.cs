using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DTO;
using QLKS_AnPhu.Services;

namespace QLKS_AnPhu.View
{
    /// <summary>
    /// Interaction logic for DichVuVatTu.xaml
    /// </summary>
    public partial class DichVuVatTu : UserControl
    {
        private readonly DichVuVatTuBUS dichVuVatTuBUS = new();
        private List<DichVuVatTuDTO> danhSachGoc = new();

        public DichVuVatTu()
        {
            InitializeComponent();
            SearchSuggestionService.Attach(TxtTimKiem, TaoGoiYTimKiem, _ => BtnTimKiem_Click(TxtTimKiem, new RoutedEventArgs()));
            Loaded += DichVuVatTu_Loaded;
        }

        private void DichVuVatTu_Loaded(object sender, RoutedEventArgs e)
        {
            TaiDuLieu();
        }

        private void TaiDuLieu()
        {
            try
            {
                TxtLoi.Text = string.Empty;
                danhSachGoc = dichVuVatTuBUS.LayDanhSach();
                HienThiDanhSach(danhSachGoc);
            }
            catch (Exception ex)
            {
                danhSachGoc = new List<DichVuVatTuDTO>();
                HienThiDanhSach(danhSachGoc);
                TxtLoi.Text = "Không tải được dữ liệu dịch vụ - vật tư từ database: " + ex.Message;
            }
        }

        private void HienThiDanhSach(List<DichVuVatTuDTO> danhSach)
        {
            DgDichVuVatTu.ItemsSource = new ObservableCollection<DichVuVatTuDTO>(danhSach);
            TxtTongDong.Text = $"Tổng: {danhSach.Count} dòng";

            if (danhSach.Count > 0)
            {
                DgDichVuVatTu.SelectedIndex = 0;
            }
            else
            {
                DataContext = null;
            }
        }

        private void BtnTimKiem_Click(object sender, RoutedEventArgs e)
        {
            string keyword = TxtTimKiem.Text.Trim();

            if (string.IsNullOrWhiteSpace(keyword))
            {
                HienThiDanhSach(danhSachGoc);
                return;
            }

            List<DichVuVatTuDTO> ketQua = danhSachGoc
                .Where(item =>
                    item.Ma.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.Ten.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.Loai.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.TrangThai.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();

            HienThiDanhSach(ketQua);
        }

        private void BtnLamMoi_Click(object sender, RoutedEventArgs e)
        {
            TxtTimKiem.Clear();
            TaiDuLieu();
        }

        private void BtnLocDichVu_Click(object sender, RoutedEventArgs e)
        {
            HienThiDanhSach(danhSachGoc.Where(item => item.Loai.Contains("Dịch vụ", StringComparison.OrdinalIgnoreCase)).ToList());
        }

        private void BtnLocVatTu_Click(object sender, RoutedEventArgs e)
        {
            HienThiDanhSach(danhSachGoc.Where(item => item.Loai.Contains("Vật tư", StringComparison.OrdinalIgnoreCase)).ToList());
        }

        private void BtnLocSapHet_Click(object sender, RoutedEventArgs e)
        {
            HienThiDanhSach(danhSachGoc.Where(item => item.SoLuongTon <= 0 || item.TrangThai.Contains("Sắp hết", StringComparison.OrdinalIgnoreCase)).ToList());
        }

        private void BtnThem_Click(object sender, RoutedEventArgs e)
        {
            DichVuVatTuForm form = new();

            if (!DialogService.ShowDimmedDialog(form, Window.GetWindow(this)))
            {
                return;
            }

            try
            {
                dichVuVatTuBUS.Them(form.DuLieu);
                MessageBox.Show("Thêm dịch vụ/vật tư thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thêm được dịch vụ/vật tư: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSua_Click(object sender, RoutedEventArgs e)
        {
            if (DgDichVuVatTu.SelectedItem is not DichVuVatTuDTO selectedItem)
            {
                MessageBox.Show("Vui lòng chọn dịch vụ/vật tư cần sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DichVuVatTuForm form = new(selectedItem);

            if (!DialogService.ShowDimmedDialog(form, Window.GetWindow(this)))
            {
                return;
            }

            try
            {
                dichVuVatTuBUS.Sua(form.DuLieu);
                MessageBox.Show("Sửa dịch vụ/vật tư thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không sửa được dịch vụ/vật tư: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnXoa_Click(object sender, RoutedEventArgs e)
        {
            if (DgDichVuVatTu.SelectedItem is not DichVuVatTuDTO selectedItem)
            {
                MessageBox.Show("Vui lòng chọn dịch vụ/vật tư cần xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(
                $"Bạn có chắc muốn xóa '{selectedItem.Ten}'?",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                dichVuVatTuBUS.Xoa(selectedItem);
                MessageBox.Show("Xóa dịch vụ/vật tư thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không xóa được dịch vụ/vật tư: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtTimKiem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnTimKiem_Click(sender, e);
            }
        }

        private IEnumerable<SearchSuggestionItem> TaoGoiYTimKiem()
        {
            foreach (DichVuVatTuDTO item in danhSachGoc)
            {
                if (!string.IsNullOrWhiteSpace(item.Ten))
                {
                    yield return new SearchSuggestionItem(item.Ten, $"{item.Ten} - {item.Loai}");
                }

                if (!string.IsNullOrWhiteSpace(item.Loai))
                {
                    yield return new SearchSuggestionItem(item.Loai, item.Loai);
                }

                if (!string.IsNullOrWhiteSpace(item.TrangThai))
                {
                    yield return new SearchSuggestionItem(item.TrangThai, $"{item.TrangThai} - {item.Ten}");
                }
            }
        }

        private void DgDichVuVatTu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DataContext = DgDichVuVatTu.SelectedItem as DichVuVatTuDTO;
        }

        private void BtnXuatPdf_Click(object sender, RoutedEventArgs e)
        {
            InDanhSach(pdfMode: true);
        }

        private void BtnXuatExcel_Click(object sender, RoutedEventArgs e)
        {
            List<DichVuVatTuDTO> rows = DgDichVuVatTu.Items.OfType<DichVuVatTuDTO>().ToList();
            if (rows.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu dịch vụ - vật tư để xuất Excel.", "Xuất Excel", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new()
            {
                Title = "Xuất danh sách dịch vụ - vật tư",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"DanhSachDichVuVatTu_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            {
                return;
            }

            try
            {
                ExcelSection section = new()
                {
                    Title = "Chi tiết dịch vụ - vật tư",
                    Headers = ["Mã", "Tên dịch vụ / vật tư", "Loại", "Đơn vị tính", "Đơn giá", "Số lượng tồn", "Trạng thái", "Ghi chú"],
                    ColumnWidths = [10, 32, 16, 14, 16, 15, 18, 30],
                    Summary = $"Tổng số mục: {rows.Count:N0} | Tổng tồn kho: {rows.Sum(item => item.SoLuongTon):N0}"
                };
                section.Rows.AddRange(rows.Select(item => (IReadOnlyList<object?>)
                [
                    $"DV{item.Ma:000}", item.Ten, item.Loai, item.DonViTinh,
                    new ExcelMoney(item.DonGia), item.SoLuongTon, item.TrangThai, item.GhiChu
                ]));

                ExcelDocument document = new()
                {
                    Title = "DANH SÁCH DỊCH VỤ - VẬT TƯ",
                    Subtitle = "Dữ liệu đang hiển thị trong phần mềm quản lý khách sạn",
                    SheetName = "Dịch vụ - vật tư"
                };
                document.Sections.Add(section);
                ExcelExportService.Export(dialog.FileName, document);
                MessageBox.Show("Xuất Excel thành công.", "Xuất Excel", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không xuất được Excel: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnInDanhSach_Click(object sender, RoutedEventArgs e)
        {
            InDanhSach(pdfMode: false);
        }

        private void InDanhSach(bool pdfMode)
        {
            List<DichVuVatTuDTO> rows = DgDichVuVatTu.Items.OfType<DichVuVatTuDTO>().ToList();
            if (rows.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu dịch vụ - vật tư để in.", pdfMode ? "Xuất PDF" : "In danh sách", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                IReadOnlyList<PrintColumn> columns =
                [
                    new("Mã", 55, TextAlignment.Center, true),
                    new("Tên dịch vụ / vật tư", new GridLength(1, GridUnitType.Star)),
                    new("Loại", 85, TextAlignment.Center),
                    new("ĐVT", 65, TextAlignment.Center, true),
                    new("Đơn giá", 90, TextAlignment.Right, true),
                    new("SL tồn", 65, TextAlignment.Right, true),
                    new("Trạng thái", 95, TextAlignment.Center)
                ];

                FlowDocument document = PrintExportService.CreateTableDocument(
                    "Danh sách dịch vụ - vật tư",
                    $"Dữ liệu đang hiển thị - Tổng cộng {rows.Count:N0} mục",
                    columns,
                    rows.Select(item => (IReadOnlyList<string>)
                    [
                        $"DV{item.Ma:000}",
                        item.Ten,
                        item.Loai,
                        item.DonViTinh,
                        item.DonGia.ToString("N0"),
                        item.SoLuongTon.ToString("N0"),
                        item.TrangThai
                    ]),
                    $"Tổng số mục: {rows.Count:N0}  |  Tổng tồn kho: {rows.Sum(item => item.SoLuongTon):N0}");

                PrintExportService.Print(
                    document,
                    pdfMode ? "Xuất PDF danh sách dịch vụ - vật tư" : "In danh sách dịch vụ - vật tư",
                    pdfMode);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    (pdfMode ? "Không xuất được PDF: " : "Không in được danh sách: ") + ex.Message,
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

    }
}
