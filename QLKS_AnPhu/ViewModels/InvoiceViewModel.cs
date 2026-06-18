using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using QLKS_AnPhu.Security;
using QLKS_AnPhu.Services;

namespace QLKS_AnPhu.ViewModels
{
    public sealed class InvoiceViewModel : INotifyPropertyChanged
    {
        private readonly InvoiceService invoiceService;
        private InvoiceRentalInfo? selectedRental;
        private decimal phuThu;
        private decimal giamGia;
        private InvoiceCalculation? calculation;

        public InvoiceViewModel()
            : this(new InvoiceService())
        {
        }

        public InvoiceViewModel(InvoiceService invoiceService)
        {
            this.invoiceService = invoiceService;
            Services = new ObservableCollection<InvoiceServiceLine>();
            PayCommand = new AsyncRelayCommand(PayInvoiceAsync, () => SelectedRental != null);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<InvoiceServiceLine> Services { get; }

        public ICommand PayCommand { get; }

        public InvoiceRentalInfo? SelectedRental
        {
            get => selectedRental;
            private set
            {
                selectedRental = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanPay));
            }
        }

        public decimal PhuThu
        {
            get => phuThu;
            set
            {
                if (phuThu == value)
                {
                    return;
                }

                phuThu = value;
                OnPropertyChanged();
                CalculateTotal();
            }
        }

        public decimal GiamGia
        {
            get => giamGia;
            set
            {
                if (giamGia == value)
                {
                    return;
                }

                giamGia = value;
                OnPropertyChanged();
                CalculateTotal();
            }
        }

        public InvoiceCalculation? Calculation
        {
            get => calculation;
            private set
            {
                calculation = value;
                OnPropertyChanged();
            }
        }

        public bool CanPay => SelectedRental != null;

        public async Task LoadRentalInfo(int maThue)
        {
            InvoiceRentalInfo rental = await Task.Run(() => invoiceService.LoadRentalInfo(maThue));
            SelectedRental = rental;
            Services.Clear();
            foreach (InvoiceServiceLine service in rental.DichVu)
            {
                Services.Add(service);
            }

            CalculateTotal();
        }

        public void CalculateTotal()
        {
            if (SelectedRental == null)
            {
                Calculation = null;
                return;
            }

            Calculation = InvoiceService.CalculateTotal(SelectedRental, PhuThu, GiamGia, DateTime.Now);
        }

        public async Task PayInvoiceAsync()
        {
            if (SelectedRental == null)
            {
                throw new InvalidOperationException("Chưa chọn phiếu thuê.");
            }

            await Task.Run(() => invoiceService.PayInvoice(new InvoicePaymentRequest(
                SelectedRental.MaThue,
                CurrentUser.MaNV,
                DateTime.Now,
                PhuThu,
                GiamGia,
                "Tiền mặt",
                null)));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
