using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using QLKS_AnPhu.BUS;
using QLKS_AnPhu.DTO;
using QLKS_AnPhu.Services;
using QLKS_AnPhu.UserControls;

namespace QLKS_AnPhu.View
{
    public partial class KhachHang : UserControl
    {
        private readonly KhachHangBUS khachHangBUS = new();
        private readonly PhongBUS phongBUS = new();
        private List<KhachHangDTO> danhSachGoc = new();

        public KhachHang()
        {
            InitializeComponent();
            SearchSuggestionService.Attach(TxtTimKiem, TaoGoiYTimKiem, _ => BtnTimKiem_Click(TxtTimKiem, new RoutedEventArgs()));
            Loaded += KhachHang_Loaded;
        }

        private void KhachHang_Loaded(object sender, RoutedEventArgs e)
        {
            TaiDuLieu();
        }

        private void TaiDuLieu()
        {
            try
            {
                TxtLoi.Text = string.Empty;
                danhSachGoc = khachHangBUS.LayDanhSach();
                HienThiDanhSach(danhSachGoc);
            }
            catch (Exception ex)
            {
                danhSachGoc = new List<KhachHangDTO>();
                HienThiDanhSach(danhSachGoc);
                TxtLoi.Text = "Không tải được dữ liệu khách hàng từ database: " + ex.Message;
            }
        }

        private void HienThiDanhSach(List<KhachHangDTO> danhSach)
        {
            DgKhachHang.ItemsSource = new ObservableCollection<KhachHangDTO>(danhSach);
            TxtTongDong.Text = $"Tổng: {danhSach.Count} dòng";

            if (danhSach.Count > 0)
            {
                DgKhachHang.SelectedIndex = 0;
            }
            else
            {
                DataContext = null;
            }
        }

        private void BtnTimKiem_Click(object sender, RoutedEventArgs e)
        {
            string keyword = TxtTimKiem.Text.Trim();
            string normalizedKeyword = BoDau(keyword).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(keyword))
            {
                HienThiDanhSach(danhSachGoc);
                return;
            }

            HienThiDanhSach(danhSachGoc
                .Where(item =>
                    item.Ma.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    ChuaTuKhoa(item.HoTen, normalizedKeyword) ||
                    ChuaTuKhoa(item.SDT, normalizedKeyword) ||
                    ChuaTuKhoa(item.CCCD, normalizedKeyword) ||
                    ChuaTuKhoa(item.LoaiKhach, normalizedKeyword) ||
                    ChuaTuKhoa(item.TrangThai, normalizedKeyword) ||
                    ChuaTuKhoa(item.DiaChi, normalizedKeyword))
                .ToList());
        }

        private void BtnLamMoi_Click(object sender, RoutedEventArgs e)
        {
            TxtTimKiem.Clear();
            TaiDuLieu();
        }

        private void BtnLocThuong_Click(object sender, RoutedEventArgs e)
        {
            HienThiDanhSach(danhSachGoc.Where(item => item.LoaiKhach.Contains("Thường", StringComparison.OrdinalIgnoreCase)).ToList());
        }

        private void BtnLocVip_Click(object sender, RoutedEventArgs e)
        {
            HienThiDanhSach(danhSachGoc.Where(item => item.LoaiKhach.Contains("VIP", StringComparison.OrdinalIgnoreCase)).ToList());
        }

        private void BtnLocDangHoatDong_Click(object sender, RoutedEventArgs e)
        {
            HienThiDanhSach(danhSachGoc.Where(item =>
                item.TrangThai.Contains("Hoạt", StringComparison.OrdinalIgnoreCase) ||
                item.TrangThai.Contains("Active", StringComparison.OrdinalIgnoreCase)).ToList());
        }

        private void BtnThem_Click(object sender, RoutedEventArgs e)
        {
            KhachHangForm form = new();

            if (!DialogService.ShowDimmedDialog(form, Window.GetWindow(this)))
            {
                return;
            }

            try
            {
                khachHangBUS.Them(form.DuLieu);
                MessageBox.Show("Thêm khách hàng thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thêm được khách hàng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSua_Click(object sender, RoutedEventArgs e)
        {
            if (DgKhachHang.SelectedItem is not KhachHangDTO selectedItem)
            {
                MessageBox.Show("Vui lòng chọn khách hàng cần sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            KhachHangForm form = new(selectedItem);

            if (!DialogService.ShowDimmedDialog(form, Window.GetWindow(this)))
            {
                return;
            }

            try
            {
                khachHangBUS.Sua(form.DuLieu);
                MessageBox.Show("Sửa khách hàng thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không sửa được khách hàng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnXoa_Click(object sender, RoutedEventArgs e)
        {
            if (DgKhachHang.SelectedItem is not KhachHangDTO selectedItem)
            {
                MessageBox.Show("Vui lòng chọn khách hàng cần xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(
                $"Bạn có chắc muốn xóa khách hàng '{selectedItem.HoTen}'?",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                khachHangBUS.Xoa(selectedItem);
                MessageBox.Show("Xóa khách hàng thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                if (CoTheXoaBatBuoc(ex) && XacNhanXoaBatBuoc(selectedItem))
                {
                    XoaBatBuoc(selectedItem);
                    return;
                }

                MessageBox.Show("Không xóa được khách hàng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool XacNhanXoaBatBuoc(KhachHangDTO selectedItem)
        {
            MessageBoxResult confirm = MessageBox.Show(
                $"Khách hàng '{selectedItem.HoTen}' đã có đặt phòng, phiếu thuê hoặc hóa đơn liên quan.\n\n" +
                "Bạn vẫn muốn xóa khách hàng này và toàn bộ dữ liệu liên quan không?",
                "Vẫn xóa khách hàng?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            return confirm == MessageBoxResult.Yes;
        }

        private void XoaBatBuoc(KhachHangDTO selectedItem)
        {
            try
            {
                khachHangBUS.XoaBatBuoc(selectedItem);
                MessageBox.Show("Đã xóa khách hàng và dữ liệu liên quan.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                TaiDuLieu();
            }
            catch (Exception forceDeleteEx)
            {
                MessageBox.Show("Không xóa bắt buộc được khách hàng: " + forceDeleteEx.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool CoTheXoaBatBuoc(Exception ex)
        {
            return ex.Message.Contains("dữ liệu đặt phòng", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("REFERENCE constraint", StringComparison.OrdinalIgnoreCase) ||
                   (ex.InnerException?.Message.Contains("REFERENCE constraint", StringComparison.OrdinalIgnoreCase) ?? false);
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
            foreach (KhachHangDTO item in danhSachGoc)
            {
                if (!string.IsNullOrWhiteSpace(item.HoTen))
                {
                    yield return new SearchSuggestionItem(item.HoTen, $"{item.HoTen} - {item.SDT}");
                }

                if (!string.IsNullOrWhiteSpace(item.SDT))
                {
                    yield return new SearchSuggestionItem(item.SDT, $"{item.SDT} - {item.HoTen}");
                }

                if (!string.IsNullOrWhiteSpace(item.CCCD))
                {
                    yield return new SearchSuggestionItem(item.CCCD, $"{item.CCCD} - {item.HoTen}");
                }
            }
        }

        private void DgKhachHang_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DataContext = DgKhachHang.SelectedItem as KhachHangDTO;
        }

        private void TxtGhiChuTongHop_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DgKhachHang.SelectedItem is not KhachHangDTO selectedItem)
            {
                return;
            }

            string ghiChuMoi = TxtGhiChuTongHop.Text.Trim();

            try
            {
                selectedItem.GhiChu = ghiChuMoi;
                khachHangBUS.Sua(selectedItem);
                TaiDuLieu();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không lưu được ghi chú khách hàng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLichSu_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedKhachHang(out KhachHangDTO? khachHang))
            {
                return;
            }

            KhachHangDataWindow window = new(khachHang!, KhachHangDataWindow.DataMode.LichSuThue);
            DialogService.ShowDimmedDialogResult(window, Window.GetWindow(this));
        }

        private void BtnHoaDon_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedKhachHang(out KhachHangDTO? khachHang))
            {
                return;
            }

            KhachHangDataWindow window = new(khachHang!, KhachHangDataWindow.DataMode.HoaDon);
            DialogService.ShowDimmedDialogResult(window, Window.GetWindow(this));
        }

        private void BtnInDanhSach_Click(object sender, RoutedEventArgs e)
        {
            List<KhachHangDTO> rows = DgKhachHang.Items.OfType<KhachHangDTO>().ToList();
            if (rows.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu khách hàng để in.", "In danh sách", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                IReadOnlyList<PrintColumn> columns =
                [
                    new("Mã KH", 55, TextAlignment.Center, true),
                    new("Họ tên khách hàng", 150),
                    new("Giới tính", 65, TextAlignment.Center, true),
                    new("Ngày sinh", 78, TextAlignment.Center, true),
                    new("Số điện thoại", 92, TextAlignment.Center, true),
                    new("CCCD/CMND", 100, TextAlignment.Center, true),
                    new("Loại khách", 82, TextAlignment.Center),
                    new("Địa chỉ", new GridLength(1, GridUnitType.Star))
                ];

                FlowDocument document = PrintExportService.CreateTableDocument(
                    "Danh sách khách hàng",
                    $"Danh sách đang hiển thị - Tổng cộng {rows.Count:N0} khách hàng",
                    columns,
                    rows.Select(item => (IReadOnlyList<string>)
                    [
                        $"KH{item.Ma:000}",
                        item.HoTen,
                        item.GioiTinh,
                        item.NgaySinhHienThi,
                        item.SDT,
                        item.CCCD,
                        item.LoaiKhach,
                        item.DiaChi
                    ]),
                    $"Tổng số khách hàng: {rows.Count:N0}");

                PrintExportService.Print(document, "Danh sách khách hàng");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không in được danh sách khách hàng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDatPhong_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedKhachHang(out KhachHangDTO? khachHang))
            {
                return;
            }

            PhongDTO? phongTrong;

            try
            {
                phongTrong = phongBUS.LayDanhSach()
                    .FirstOrDefault(item =>
                        !item.TrangThai.Contains("thuê", StringComparison.OrdinalIgnoreCase) &&
                        !item.TrangThai.Contains("thu", StringComparison.OrdinalIgnoreCase) &&
                        !item.TrangThai.Contains("đặt", StringComparison.OrdinalIgnoreCase) &&
                        !item.TrangThai.Contains("dat", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không tải được danh sách phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (phongTrong == null)
            {
                MessageBox.Show("Không có phòng trống để đặt.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UCDatPhongMoi ucDatPhong = new(phongTrong, khachHang!);
            ucDatPhong.CloseRequested += UcDatPhong_CloseRequested;
            ucDatPhong.DatPhongRequested += UcDatPhong_DatPhongRequested;
            ucDatPhong.DatPhongTheoDoanRequested += UcDatPhong_DatPhongTheoDoanRequested;

            Window dialog = DialogService.CreateContentDialog(ucDatPhong, "Đặt phòng mới", 1100, 650);
            DialogService.ShowDimmedDialogResult(dialog, Window.GetWindow(this));

            ucDatPhong.CloseRequested -= UcDatPhong_CloseRequested;
            ucDatPhong.DatPhongRequested -= UcDatPhong_DatPhongRequested;
            ucDatPhong.DatPhongTheoDoanRequested -= UcDatPhong_DatPhongTheoDoanRequested;
        }

        private void UcDatPhong_CloseRequested(object? sender, EventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                Window.GetWindow(element)?.Close();
            }
        }

        private void UcDatPhong_DatPhongRequested(object? sender, PhongDTO phong)
        {
            try
            {
                bool nhanNgay = sender is UCDatPhongMoi { NhanNgay: true };
                HoaDonItem? billSauThanhToan = null;
                if (sender is UCDatPhongMoi ucDatPhong)
                {
                    DatPhongRequestDTO request = ucDatPhong.TaoYeuCauDatPhong();
                    if (nhanNgay)
                    {
                        decimal giamGia = request.KhachHang.LoaiKhach.Contains("VIP", StringComparison.OrdinalIgnoreCase) ? Math.Round(request.TienPhong * 0.1m, 0) : 0;
                        if (!DialogService.XacNhanThanhToanCheckIn(Window.GetWindow(this), "Phòng " + request.Phong.MaHienThi, request.TienPhong, request.TienDichVu, giamGia: giamGia))
                        {
                            return;
                        }
                        KetQuaCheckInThanhToanDTO result = phongBUS.NhanPhong(request);
                        billSauThanhToan = HoaDonItem.TaoCheckInTam(
                            request,
                            result.MaHoaDon > 0 ? "HD-" + result.MaHoaDon.ToString("0000") : "HD-TAM",
                            result.MaThue.GetValueOrDefault());
                    }
                    else
                    {
                        phongBUS.DatPhong(request);
                    }
                }
                else
                {
                    phongBUS.DatPhong(phong);
                }

                MessageBox.Show(nhanNgay ? "Nhận phòng thành công." : "Đặt phòng thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                if (billSauThanhToan != null)
                {
                    HoaDonPrintWindow window = new(billSauThanhToan);
                    DialogService.ShowDimmedDialogResult(window, Window.GetWindow(this));
                }

                if (sender is FrameworkElement element)
                {
                    Window.GetWindow(element)?.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không đặt được phòng: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UcDatPhong_DatPhongTheoDoanRequested(object? sender, EventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                Window.GetWindow(element)?.Close();
            }

            if (!TryGetSelectedKhachHang(out KhachHangDTO? khachHang) || khachHang == null)
            {
                return;
            }

            List<PhongDTO> phongTrong;
            try
            {
                phongTrong = phongBUS.LayDanhSach()
                    .Where(LaPhongTrongSanSang)
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Khong tai duoc danh sach phong: " + ex.Message, "Loi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (phongTrong.Count == 0)
            {
                MessageBox.Show("Khong co phong trong de dat theo doan.", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UCDatPhongTheoDoan ucDatPhong = new(phongTrong, khachHang);
            ucDatPhong.CloseRequested += UcDatPhongTheoDoan_CloseRequested;
            ucDatPhong.DatPhongDoanRequested += UcDatPhongTheoDoan_DatPhongDoanRequested;

            Window dialog = DialogService.CreateContentDialog(ucDatPhong, "Dat phong cho doan", 1450, 800);
            DialogService.ShowDimmedDialogResult(dialog, Window.GetWindow(this));

            ucDatPhong.CloseRequested -= UcDatPhongTheoDoan_CloseRequested;
            ucDatPhong.DatPhongDoanRequested -= UcDatPhongTheoDoan_DatPhongDoanRequested;
        }

        private void UcDatPhongTheoDoan_CloseRequested(object? sender, EventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                Window.GetWindow(element)?.Close();
            }
        }

        private void UcDatPhongTheoDoan_DatPhongDoanRequested(object? sender, List<DatPhongRequestDTO> requests)
        {
            try
            {
                bool nhanNgay = requests.Any(item => item.NhanNgay);
                if (nhanNgay && !DialogService.XacNhanThanhToanCheckIn(
                        Window.GetWindow(this),
                        "Nhan phong cho doan",
                        requests.Sum(item => item.TienPhong),
                        requests.Sum(item => item.TienDichVu),
                        giamGia: requests.Sum(item => item.KhachHang.LoaiKhach.Contains("VIP", StringComparison.OrdinalIgnoreCase) ? Math.Round(item.TienPhong * 0.1m, 0) : 0)))
                {
                    return;
                }

                phongBUS.LuuDatPhongDoan(requests);
                MessageBox.Show(nhanNgay ? "Nhan phong cho doan thanh cong." : "Dat phong cho doan thanh cong.", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Information);
                if (sender is FrameworkElement element)
                {
                    Window.GetWindow(element)?.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Khong dat phong cho doan duoc: " + ex.Message, "Loi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool LaPhongTrongSanSang(PhongDTO phong)
        {
            string trangThai = phong.TrangThai ?? string.Empty;
            return !trangThai.Contains("thuê", StringComparison.OrdinalIgnoreCase) &&
                   !trangThai.Contains("thue", StringComparison.OrdinalIgnoreCase) &&
                   !trangThai.Contains("đặt", StringComparison.OrdinalIgnoreCase) &&
                   !trangThai.Contains("dat", StringComparison.OrdinalIgnoreCase) &&
                   !trangThai.Contains("sửa", StringComparison.OrdinalIgnoreCase) &&
                   !trangThai.Contains("sua", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryGetSelectedKhachHang(out KhachHangDTO? khachHang)
        {
            khachHang = DgKhachHang.SelectedItem as KhachHangDTO;
            if (khachHang != null)
            {
                return true;
            }

            MessageBox.Show("Vui lòng chọn khách hàng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private static bool ChuaTuKhoa(string? value, string normalizedKeyword)
        {
            return BoDau(value).ToLowerInvariant().Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase);
        }

        private static string BoDau(string? value)
        {
            string formD = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
            StringBuilder builder = new();

            foreach (char ch in formD)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch == 'đ' ? 'd' : ch == 'Đ' ? 'D' : ch);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
