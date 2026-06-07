using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace QLKS_AnPhu.View
{
    internal sealed class SearchSuggestionItem
    {
        public SearchSuggestionItem(string value, string display)
        {
            Value = value;
            Display = display;
        }

        public string Value { get; }
        public string Display { get; }
    }

    internal sealed class SearchSuggestionService
    {
        private readonly TextBox textBox;
        private readonly Func<IEnumerable<SearchSuggestionItem>> sourceFactory;
        private readonly Action<SearchSuggestionItem>? selected;
        private readonly Popup popup;
        private readonly ListBox listBox;
        private bool choosing;

        private SearchSuggestionService(
            TextBox textBox,
            Func<IEnumerable<SearchSuggestionItem>> sourceFactory,
            Action<SearchSuggestionItem>? selected)
        {
            this.textBox = textBox;
            this.sourceFactory = sourceFactory;
            this.selected = selected;

            listBox = CreateListBox();
            popup = CreatePopup(textBox, listBox);
            WireEvents();
        }

        public static SearchSuggestionService Attach(
            TextBox textBox,
            Func<IEnumerable<SearchSuggestionItem>> sourceFactory,
            Action<SearchSuggestionItem>? selected = null)
        {
            return new SearchSuggestionService(textBox, sourceFactory, selected);
        }

        private void WireEvents()
        {
            textBox.TextChanged += (_, _) =>
            {
                if (!choosing)
                {
                    UpdateSuggestions();
                }
            };

            textBox.GotKeyboardFocus += (_, _) => UpdateSuggestions();
            textBox.LostKeyboardFocus += (_, _) =>
            {
                if (!listBox.IsKeyboardFocusWithin)
                {
                    popup.IsOpen = false;
                }
            };

            textBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Down && popup.IsOpen && listBox.Items.Count > 0)
                {
                    listBox.Focus();
                    listBox.SelectedIndex = 0;
                    e.Handled = true;
                }
            };

            listBox.PreviewMouseLeftButtonUp += (_, _) => ChooseSuggestion();
            listBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ChooseSuggestion();
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.Escape)
                {
                    popup.IsOpen = false;
                    textBox.Focus();
                    e.Handled = true;
                }
            };
        }

        private void UpdateSuggestions()
        {
            string keyword = textBox.Text.Trim();
            if (keyword.Length == 0)
            {
                popup.IsOpen = false;
                return;
            }

            List<SearchSuggestionItem> suggestions = sourceFactory()
                .Where(item => ContainsKeyword(item.Value, keyword) || ContainsKeyword(item.Display, keyword))
                .GroupBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(8)
                .ToList();

            listBox.ItemsSource = suggestions;
            popup.IsOpen = suggestions.Count > 0;
        }

        private void ChooseSuggestion()
        {
            if (listBox.SelectedItem is not SearchSuggestionItem item)
            {
                return;
            }

            choosing = true;
            textBox.Text = item.Value;
            textBox.CaretIndex = textBox.Text.Length;
            choosing = false;

            popup.IsOpen = false;
            selected?.Invoke(item);
        }

        private static bool ContainsKeyword(string? value, string keyword)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        private static Popup CreatePopup(TextBox textBox, ListBox listBox)
        {
            Grid root = new()
            {
                MinWidth = textBox.ActualWidth,
                MaxHeight = 230,
                Margin = new Thickness(0, 6, 0, 0),
                SnapsToDevicePixels = true
            };

            Border shadow = new()
            {
                Margin = new Thickness(3),
                Background = new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0)),
                CornerRadius = new CornerRadius(12),
                IsHitTestVisible = false,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 18,
                    ShadowDepth = 4,
                    Opacity = 0.16,
                    Color = Color.FromRgb(0x11, 0x18, 0x27)
                }
            };

            Border panel = new()
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xDE, 0xE8)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Child = listBox
            };

            root.Children.Add(shadow);
            root.Children.Add(panel);

            Popup popup = new()
            {
                PlacementTarget = textBox,
                Placement = PlacementMode.Bottom,
                AllowsTransparency = true,
                StaysOpen = false,
                PopupAnimation = PopupAnimation.Slide,
                Child = root
            };

            popup.Opened += (_, _) => root.MinWidth = textBox.ActualWidth;
            return popup;
        }

        private static ListBox CreateListBox()
        {
            ListBox listBox = new()
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                DisplayMemberPath = nameof(SearchSuggestionItem.Display),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27)),
                Padding = new Thickness(6)
            };

            Style style = new(typeof(ListBoxItem));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 8, 10, 8)));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 1, 0, 1)));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            listBox.ItemContainerStyle = style;
            return listBox;
        }
    }
}
