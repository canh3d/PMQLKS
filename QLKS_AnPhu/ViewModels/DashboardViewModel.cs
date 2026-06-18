using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using MahApps.Metro.IconPacks;
using Microsoft.Win32;
using QLKS_AnPhu.Security;
using QLKS_AnPhu.Services;

namespace QLKS_AnPhu.ViewModels
{
    public sealed class DashboardViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly DashboardDataService dashboardDataService = new();
        private readonly CultureInfo vietnameseCulture = new("vi-VN");
        private readonly DispatcherTimer refreshTimer;
        private readonly Action? navigateToDatPhong;
        private readonly Action? navigateToBaoCao;
        private readonly Action? navigateToPhieuThue;
        private string welcomeText = string.Empty;
        private string revenueToday = "0 đ";
        private string revenueDelta = "+0% so với hôm qua";
        private Brush revenueDeltaBrush = Brushes.Green;
        private string rentedRooms = "0";
        private string rentedRoomsDelta = "+0 đặt phòng mới";
        private string emptyRooms = "0";
        private string emptyRoomsDelta = "0 phòng đang bận";
        private string customers = "0";
        private string customersDelta = "+0 mới hôm nay";
        private string occupancyRate = "0%";
        private string errorMessage = string.Empty;
        private Visibility revenueVisibility = Visibility.Visible;

        public DashboardViewModel(
            AppUser user,
            Action? navigateToDatPhong = null,
            Action? navigateToBaoCao = null,
            Action? navigateToPhieuThue = null)
        {
            User = user;
            this.navigateToDatPhong = navigateToDatPhong;
            this.navigateToBaoCao = navigateToBaoCao;
            this.navigateToPhieuThue = navigateToPhieuThue;

            string displayName = string.IsNullOrWhiteSpace(user.HoTenNhanVien) ? user.TenDangNhap : user.HoTenNhanVien;
            WelcomeText = $"Chào mừng trở lại, {displayName}!";
            RevenueVisibility = user.IsManager ? Visibility.Visible : Visibility.Collapsed;

            RefreshCommand = new AsyncRelayCommand(LoadDashboardDataAsync);
            DatPhongMoiCommand = new RelayCommand(_ => this.navigateToDatPhong?.Invoke());
            XemBaoCaoCommand = new RelayCommand(_ => this.navigateToBaoCao?.Invoke());
            XemTatCaThongBaoCommand = new RelayCommand(_ => this.navigateToPhieuThue?.Invoke());
            PrintRecentCustomersCommand = new RelayCommand(_ => PrintRecentCustomers());
            ExportReportCommand = new RelayCommand(_ => ExportReport());

            refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            refreshTimer.Tick += async (_, _) => await LoadDashboardDataAsync();
            refreshTimer.Start();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public AppUser User { get; }
        public ObservableCollection<DashboardNoticeItem> ThongBao { get; } = new();
        public ObservableCollection<RecentCustomerItem> KhachHangGanDay { get; } = new();
        public AsyncRelayCommand RefreshCommand { get; }
        public RelayCommand DatPhongMoiCommand { get; }
        public RelayCommand XemBaoCaoCommand { get; }
        public RelayCommand XemTatCaThongBaoCommand { get; }
        public RelayCommand PrintRecentCustomersCommand { get; }
        public RelayCommand ExportReportCommand { get; }

        public string WelcomeText
        {
            get => welcomeText;
            private set => SetProperty(ref welcomeText, value);
        }

        public string RevenueToday
        {
            get => revenueToday;
            private set => SetProperty(ref revenueToday, value);
        }

        public string RevenueDelta
        {
            get => revenueDelta;
            private set => SetProperty(ref revenueDelta, value);
        }

        public Brush RevenueDeltaBrush
        {
            get => revenueDeltaBrush;
            private set => SetProperty(ref revenueDeltaBrush, value);
        }

        public string RentedRooms
        {
            get => rentedRooms;
            private set => SetProperty(ref rentedRooms, value);
        }

        public string RentedRoomsDelta
        {
            get => rentedRoomsDelta;
            private set => SetProperty(ref rentedRoomsDelta, value);
        }

        public string EmptyRooms
        {
            get => emptyRooms;
            private set => SetProperty(ref emptyRooms, value);
        }

        public string EmptyRoomsDelta
        {
            get => emptyRoomsDelta;
            private set => SetProperty(ref emptyRoomsDelta, value);
        }

        public string Customers
        {
            get => customers;
            private set => SetProperty(ref customers, value);
        }

        public string CustomersDelta
        {
            get => customersDelta;
            private set => SetProperty(ref customersDelta, value);
        }

        public string OccupancyRate
        {
            get => occupancyRate;
            private set => SetProperty(ref occupancyRate, value);
        }

        public string ErrorMessage
        {
            get => errorMessage;
            private set => SetProperty(ref errorMessage, value);
        }

        public Visibility RevenueVisibility
        {
            get => revenueVisibility;
            private set => SetProperty(ref revenueVisibility, value);
        }

        public async Task LoadDashboardDataAsync()
        {
            try
            {
                DashboardSnapshot snapshot = await dashboardDataService.LoadSnapshotAsync();
                ApplySnapshot(snapshot);
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Không tải được dữ liệu dashboard: " + ex.Message;
            }
        }

        public void Dispose()
        {
            refreshTimer.Stop();
        }

        private void ApplySnapshot(DashboardSnapshot snapshot)
        {
            RevenueToday = snapshot.DoanhThuHomNay.ToString("N0", vietnameseCulture) + " đ";
            RevenueDelta = FormatDelta(snapshot.DoanhThuDelta, "% so với hôm qua");
            RevenueDeltaBrush = snapshot.DoanhThuDelta >= 0 ? Brushes.SeaGreen : Brushes.IndianRed;
            RentedRooms = snapshot.PhongDangThue.ToString("N0", vietnameseCulture);
            RentedRoomsDelta = "+" + snapshot.DatPhongMoiHomNay.ToString("N0", vietnameseCulture) + " đặt phòng mới";
            EmptyRooms = snapshot.PhongTrong.ToString("N0", vietnameseCulture);
            EmptyRoomsDelta = snapshot.TongPhong > 0
                ? (snapshot.TongPhong - snapshot.PhongTrong).ToString("N0", vietnameseCulture) + " phòng đang bận"
                : "Chưa có dữ liệu phòng";
            Customers = snapshot.KhachHang.ToString("N0", vietnameseCulture);
            CustomersDelta = "+" + snapshot.KhachMoiHomNay.ToString("N0", vietnameseCulture) + " mới hôm nay";
            OccupancyRate = snapshot.TyLeLapDay.ToString("N2", vietnameseCulture) + "%";

            ThongBao.Clear();
            ThongBao.Add(new DashboardNoticeItem(
                $"{snapshot.KhachSapCheckoutHomNay:N0} khách sắp check-out hôm nay",
                DateTime.Now.ToString("HH:mm"),
                PackIconMaterialKind.Bell,
                "#E0F2FE",
                "#0284C7"));
            ThongBao.Add(new DashboardNoticeItem(
                $"{snapshot.PhongDaDatChuaNhan:N0} phòng đã đặt nhưng chưa nhận",
                DateTime.Now.ToString("HH:mm"),
                PackIconMaterialKind.AccountClock,
                "#DCFCE7",
                "#16A34A"));
            ThongBao.Add(new DashboardNoticeItem(
                $"{snapshot.HoaDonChuaThanhToan:N0} hóa đơn chưa thanh toán",
                DateTime.Now.ToString("HH:mm"),
                PackIconMaterialKind.AlertOutline,
                "#FEF3C7",
                "#D97706"));

            KhachHangGanDay.Clear();
            foreach (RecentCustomerDashboardItem item in snapshot.KhachHangGanDay)
            {
                KhachHangGanDay.Add(new RecentCustomerItem
                {
                    HoTen = item.HoTen,
                    Phong = item.Phong,
                    TrangThai = item.TrangThai,
                    NgayNhanPhong = item.NgayNhanPhong?.ToString("dd/MM/yyyy HH:mm", vietnameseCulture) ?? string.Empty
                });
            }
        }

        private void PrintRecentCustomers()
        {
            if (KhachHangGanDay.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu khách hàng để in.", "In danh sách", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            PrintDialog dialog = new();
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            FlowDocument document = new()
            {
                PagePadding = new Thickness(40),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12
            };

            document.Blocks.Add(new Paragraph(new Run("DANH SÁCH KHÁCH HÀNG GẦN ĐÂY"))
            {
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 18)
            });

            Table table = new();
            table.Columns.Add(new TableColumn { Width = new GridLength(220) });
            table.Columns.Add(new TableColumn { Width = new GridLength(90) });
            table.Columns.Add(new TableColumn { Width = new GridLength(130) });
            table.Columns.Add(new TableColumn { Width = new GridLength(150) });
            TableRowGroup group = new();
            table.RowGroups.Add(group);
            group.Rows.Add(CreatePrintRow("Họ tên", "Phòng", "Trạng thái", "Ngày nhận phòng", true));

            foreach (RecentCustomerItem item in KhachHangGanDay)
            {
                group.Rows.Add(CreatePrintRow(item.HoTen, item.Phong, item.TrangThai, item.NgayNhanPhong, false));
            }

            document.Blocks.Add(table);
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Danh sách khách hàng gần đây");
        }

        private void ExportReport()
        {
            SaveFileDialog dialog = new()
            {
                Title = "Xuất báo cáo tổng quan",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "DashboardKhachSan_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                ExcelDocument document = new()
                {
                    Title = "BÁO CÁO TỔNG QUAN KHÁCH SẠN",
                    Subtitle = $"Tình hình hoạt động ngày {DateTime.Now:dd/MM/yyyy}",
                    SheetName = "Tổng quan"
                };

                ExcelSection overview = new()
                {
                    Title = "Chỉ số hoạt động",
                    Headers = ["Chỉ số", "Giá trị"],
                    ColumnWidths = [30, 22]
                };
                if (User.IsManager)
                {
                    overview.Rows.Add(["Doanh thu hôm nay", RevenueToday]);
                }
                overview.Rows.Add(["Phòng đang thuê", RentedRooms]);
                overview.Rows.Add(["Phòng trống", EmptyRooms]);
                overview.Rows.Add(["Khách hàng", Customers]);
                overview.Rows.Add(["Tỷ lệ lấp đầy", OccupancyRate]);
                document.Sections.Add(overview);

                ExcelSection recent = new()
                {
                    Title = "Khách hàng gần đây",
                    Headers = ["Họ tên", "Phòng", "Trạng thái", "Ngày nhận phòng"],
                    ColumnWidths = [28, 14, 20, 20],
                    Summary = $"Tổng số khách gần đây: {KhachHangGanDay.Count:N0}"
                };
                recent.Rows.AddRange(KhachHangGanDay.Select(item => (IReadOnlyList<object?>)
                    [item.HoTen, item.Phong, item.TrangThai, item.NgayNhanPhong]));
                document.Sections.Add(recent);

                ExcelExportService.Export(dialog.FileName, document);
                MessageBox.Show("Đã xuất báo cáo tổng quan.", "Xuất Excel", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không xuất được Excel: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static TableRow CreatePrintRow(string col1, string col2, string col3, string col4, bool header)
        {
            TableRow row = new();
            row.Cells.Add(CreatePrintCell(col1, header));
            row.Cells.Add(CreatePrintCell(col2, header));
            row.Cells.Add(CreatePrintCell(col3, header));
            row.Cells.Add(CreatePrintCell(col4, header));
            return row;
        }

        private static TableCell CreatePrintCell(string text, bool header)
        {
            return new TableCell(new Paragraph(new Run(text)))
            {
                Padding = new Thickness(6),
                FontWeight = header ? FontWeights.Bold : FontWeights.Normal,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
        }

        private string FormatDelta(decimal value, string suffix)
        {
            string prefix = value >= 0 ? "+" : string.Empty;
            return prefix + value.ToString("N1", vietnameseCulture) + suffix;
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public sealed class DashboardNoticeItem
    {
        public DashboardNoticeItem(string noiDung, string thoiGian, PackIconMaterialKind icon, string iconBackground, string iconBrush)
        {
            NoiDung = noiDung;
            ThoiGian = thoiGian;
            Icon = icon;
            IconBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(iconBackground));
            IconBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(iconBrush));
        }

        public string NoiDung { get; }
        public string ThoiGian { get; }
        public PackIconMaterialKind Icon { get; }
        public Brush IconBackground { get; }
        public Brush IconBrush { get; }
    }

    public sealed class RecentCustomerItem
    {
        public string HoTen { get; init; } = string.Empty;
        public string Phong { get; init; } = string.Empty;
        public string TrangThai { get; init; } = string.Empty;
        public string NgayNhanPhong { get; init; } = string.Empty;
    }
}
