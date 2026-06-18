using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace QLKS_AnPhu.Services
{
    internal static class PrintExportService
    {
        private static readonly Brush NavyBrush = new SolidColorBrush(Color.FromRgb(15, 53, 85));
        private static readonly Brush BlueBrush = new SolidColorBrush(Color.FromRgb(11, 78, 162));
        private static readonly Brush HeaderBrush = new SolidColorBrush(Color.FromRgb(230, 238, 246));
        private static readonly Brush LineBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
        private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        private static readonly Brush AlternateBrush = new SolidColorBrush(Color.FromRgb(248, 250, 252));

        public static FlowDocument CreateTableDocument(
            string title,
            string subtitle,
            IReadOnlyList<PrintColumn> columns,
            IEnumerable<IReadOnlyList<string>> rows,
            string summary)
        {
            List<IReadOnlyList<string>> materializedRows = rows.ToList();
            FlowDocument document = new()
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10.5,
                Foreground = Brushes.Black,
                PagePadding = new Thickness(38, 32, 38, 34),
                ColumnGap = 0
            };

            Table brandTable = new()
            {
                CellSpacing = 0,
                Margin = new Thickness(0, 0, 0, 8)
            };
            brandTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            brandTable.Columns.Add(new TableColumn { Width = new GridLength(180) });
            TableRow brandRow = new();
            brandRow.Cells.Add(CreatePlainCell("KHÁCH SẠN AN PHÚ", 14, FontWeights.Bold, NavyBrush, TextAlignment.Left));
            brandRow.Cells.Add(CreatePlainCell("AN PHÚ HOTEL", 10, FontWeights.SemiBold, BlueBrush, TextAlignment.Right));
            TableRowGroup brandGroup = new();
            brandGroup.Rows.Add(brandRow);
            brandTable.RowGroups.Add(brandGroup);
            document.Blocks.Add(brandTable);

            document.Blocks.Add(new Paragraph(new Run(title.ToUpperInvariant()))
            {
                FontSize = 19,
                FontWeight = FontWeights.Bold,
                Foreground = NavyBrush,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 5)
            });

            document.Blocks.Add(new Paragraph(new Run(subtitle))
            {
                FontSize = 10,
                Foreground = MutedBrush,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            });

            Table table = new()
            {
                CellSpacing = 0,
                BorderBrush = LineBrush,
                BorderThickness = new Thickness(0.7)
            };

            foreach (PrintColumn column in columns)
            {
                table.Columns.Add(new TableColumn { Width = column.Width });
            }

            TableRowGroup group = new();
            table.RowGroups.Add(group);
            TableRow header = new();
            foreach (PrintColumn column in columns)
            {
                header.Cells.Add(CreateTableCell(column.Header, true, TextAlignment.Center, false));
            }
            group.Rows.Add(header);

            int rowIndex = 0;
            foreach (IReadOnlyList<string> values in materializedRows)
            {
                TableRow row = new()
                {
                    Background = rowIndex++ % 2 == 1 ? AlternateBrush : Brushes.White
                };

                for (int index = 0; index < columns.Count; index++)
                {
                    string value = index < values.Count ? values[index] : string.Empty;
                    row.Cells.Add(CreateTableCell(value, false, columns[index].Alignment, columns[index].NoWrap));
                }
                group.Rows.Add(row);
            }

            document.Blocks.Add(table);
            document.Blocks.Add(new Paragraph(new Run(summary))
            {
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = NavyBrush,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            });
            document.Blocks.Add(new Paragraph(new Run($"Ngày lập: {DateTime.Now:dd/MM/yyyy HH:mm}"))
            {
                FontSize = 9,
                Foreground = MutedBrush,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 4, 0, 0)
            });

            return document;
        }

        public static bool Print(FlowDocument document, string description, bool pdfMode = false)
        {
            if (pdfMode)
            {
                MessageBox.Show(
                    "Trong hộp thoại in, chọn máy in \"Microsoft Print to PDF\", sau đó chọn nơi lưu tệp.",
                    "Xuất PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            PrintDialog dialog = new();
            if (dialog.ShowDialog() != true)
            {
                return false;
            }

            document.PageWidth = dialog.PrintableAreaWidth;
            document.PageHeight = dialog.PrintableAreaHeight;
            document.PagePadding = new Thickness(38, 32, 38, 34);
            document.ColumnWidth = Math.Max(1, dialog.PrintableAreaWidth - document.PagePadding.Left - document.PagePadding.Right);
            document.ColumnGap = 0;
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, description);
            return true;
        }

        private static TableCell CreatePlainCell(
            string text,
            double fontSize,
            FontWeight fontWeight,
            Brush foreground,
            TextAlignment alignment)
        {
            return new TableCell(new Paragraph(new Run(text))
            {
                FontSize = fontSize,
                FontWeight = fontWeight,
                Foreground = foreground,
                TextAlignment = alignment,
                Margin = new Thickness(0)
            })
            {
                Padding = new Thickness(0)
            };
        }

        private static TableCell CreateTableCell(string text, bool header, TextAlignment alignment, bool noWrap)
        {
            Paragraph paragraph = new(new Run(text ?? string.Empty))
            {
                Margin = new Thickness(0),
                TextAlignment = alignment,
                LineHeight = 14
            };

            if (noWrap)
            {
                paragraph.KeepTogether = true;
            }

            return new TableCell(paragraph)
            {
                Padding = new Thickness(6, 5, 6, 5),
                BorderBrush = LineBrush,
                BorderThickness = new Thickness(0.5),
                Background = header ? HeaderBrush : Brushes.Transparent,
                FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = header ? NavyBrush : Brushes.Black
            };
        }
    }

    internal sealed class PrintColumn
    {
        public PrintColumn(
            string header,
            double width,
            TextAlignment alignment = TextAlignment.Left,
            bool noWrap = false)
        {
            Header = header;
            Width = new GridLength(width);
            Alignment = alignment;
            NoWrap = noWrap;
        }

        public PrintColumn(
            string header,
            GridLength width,
            TextAlignment alignment = TextAlignment.Left,
            bool noWrap = false)
        {
            Header = header;
            Width = width;
            Alignment = alignment;
            NoWrap = noWrap;
        }

        public string Header { get; }
        public GridLength Width { get; }
        public TextAlignment Alignment { get; }
        public bool NoWrap { get; }
    }
}
