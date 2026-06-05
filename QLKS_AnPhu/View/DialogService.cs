using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace QLKS_AnPhu.View
{
    public static class DialogService
    {
        public static bool ShowDimmedDialog(Window dialog, Window? owner)
        {
            return ShowDimmedDialogResult(dialog, owner) == true;
        }

        public static bool? ShowDimmedDialogResult(Window dialog, Window? owner)
        {
            owner ??= Application.Current.MainWindow;

            if (owner == null || owner == dialog)
            {
                return dialog.ShowDialog();
            }

            double oldOpacity = owner.Opacity;
            Effect? oldEffect = owner.Effect;

            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.ShowInTaskbar = false;

            owner.Opacity = 0.58;
            owner.Effect = new BlurEffect { Radius = 3 };

            try
            {
                return dialog.ShowDialog();
            }
            finally
            {
                owner.Opacity = oldOpacity;
                owner.Effect = oldEffect;
                owner.Activate();
            }
        }

        public static Window CreateContentDialog(FrameworkElement content, string title, double width, double height)
        {
            return new Window
            {
                Title = title,
                Content = content,
                Width = width,
                Height = height,
                MinWidth = width,
                MinHeight = height,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                Background = Brushes.White,
                SizeToContent = SizeToContent.Manual
            };
        }

        public static bool XacNhanThanhToanCheckIn(Window? owner, string moTa, decimal tienPhong, decimal tienDichVu, decimal phuPhi = 0, decimal datCoc = 0, decimal giamGia = 0)
        {
            ThanhToanCheckInWindow dialog = new(moTa, tienPhong, tienDichVu, phuPhi, datCoc, giamGia);
            return ShowDimmedDialog(dialog, owner);
        }
    }
}
