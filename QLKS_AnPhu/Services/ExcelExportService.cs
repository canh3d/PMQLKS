using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml;

namespace QLKS_AnPhu.Services
{
    internal static class ExcelExportService
    {
        public static void Export(string filePath, ExcelDocument document)
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using FileStream stream = File.Create(filePath);
            using ZipArchive archive = new(stream, ZipArchiveMode.Create);
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
            WriteEntry(archive, "_rels/.rels", RootRelationshipsXml);
            WriteEntry(archive, "xl/workbook.xml", WorkbookXml(document.SheetName));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsXml);
            WriteEntry(archive, "xl/styles.xml", StylesXml);
            WriteEntry(archive, "docProps/app.xml", AppPropertiesXml);
            WriteEntry(archive, "docProps/core.xml", CorePropertiesXml(document.Title));
            WriteEntry(archive, "xl/worksheets/sheet1.xml", WorksheetXml(document));
        }

        private static string WorksheetXml(ExcelDocument document)
        {
            int maxColumns = Math.Max(
                1,
                document.Sections.Select(section => section.Headers.Count).DefaultIfEmpty(1).Max());
            List<SheetRow> rows = new();
            List<string> mergedCells = new();

            rows.Add(new SheetRow(1, [TextCell(1, document.Title, 1)], 28));
            mergedCells.Add($"A1:{ColumnName(maxColumns)}1");
            rows.Add(new SheetRow(2, [TextCell(1, document.Subtitle, 2)], 21));
            mergedCells.Add($"A2:{ColumnName(maxColumns)}2");
            rows.Add(new SheetRow(3, [TextCell(1, $"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}", 3)], 18));
            mergedCells.Add($"A3:{ColumnName(maxColumns)}3");

            int currentRow = 5;
            foreach (ExcelSection section in document.Sections)
            {
                rows.Add(new SheetRow(currentRow, [TextCell(1, section.Title, 4)], 22));
                mergedCells.Add($"A{currentRow}:{ColumnName(Math.Max(1, section.Headers.Count))}{currentRow}");
                currentRow++;

                List<SheetCell> headerCells = new();
                for (int index = 0; index < section.Headers.Count; index++)
                {
                    headerCells.Add(TextCell(index + 1, section.Headers[index], 8));
                }
                rows.Add(new SheetRow(currentRow, headerCells, 26));
                int headerRow = currentRow;
                currentRow++;

                int dataIndex = 0;
                foreach (IReadOnlyList<object?> values in section.Rows)
                {
                    List<SheetCell> cells = new();
                    for (int index = 0; index < section.Headers.Count; index++)
                    {
                        object? value = index < values.Count ? values[index] : null;
                        cells.Add(ValueCell(index + 1, value, dataIndex % 2 == 1));
                    }
                    rows.Add(new SheetRow(currentRow++, cells, 22));
                    dataIndex++;
                }

                if (!string.IsNullOrWhiteSpace(section.Summary))
                {
                    rows.Add(new SheetRow(currentRow, [TextCell(1, section.Summary, 4)], 22));
                    mergedCells.Add($"A{currentRow}:{ColumnName(Math.Max(1, section.Headers.Count))}{currentRow}");
                    currentRow++;
                }

                section.HeaderRow = headerRow;
                section.LastDataRow = Math.Max(headerRow, currentRow - 1);
                currentRow += 2;
            }

            double[] widths = new double[maxColumns];
            for (int index = 0; index < widths.Length; index++)
            {
                widths[index] = document.Sections
                    .Where(section => index < section.ColumnWidths.Count)
                    .Select(section => section.ColumnWidths[index])
                    .DefaultIfEmpty(16)
                    .Max();
            }

            StringBuilder builder = new();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            builder.Append("<sheetViews><sheetView workbookViewId=\"0\"/></sheetViews>");
            builder.Append("<sheetFormatPr defaultRowHeight=\"18\"/>");
            builder.Append("<cols>");
            for (int index = 0; index < widths.Length; index++)
            {
                builder.Append($"<col min=\"{index + 1}\" max=\"{index + 1}\" width=\"{widths[index].ToString("0.##", CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
            }
            builder.Append("</cols><sheetData>");
            foreach (SheetRow row in rows)
            {
                builder.Append($"<row r=\"{row.Index}\" ht=\"{row.Height.ToString("0.##", CultureInfo.InvariantCulture)}\" customHeight=\"1\">");
                foreach (SheetCell cell in row.Cells)
                {
                    builder.Append(CellXml(row.Index, cell));
                }
                builder.Append("</row>");
            }
            builder.Append("</sheetData>");

            if (mergedCells.Count > 0)
            {
                builder.Append($"<mergeCells count=\"{mergedCells.Count}\">");
                foreach (string reference in mergedCells)
                {
                    builder.Append($"<mergeCell ref=\"{reference}\"/>");
                }
                builder.Append("</mergeCells>");
            }

            builder.Append("<pageMargins left=\"0.3\" right=\"0.3\" top=\"0.5\" bottom=\"0.5\" header=\"0.2\" footer=\"0.2\"/>");
            builder.Append("</worksheet>");
            return builder.ToString();
        }

        private static SheetCell ValueCell(int column, object? value, bool alternate)
        {
            const int textStyle = 5;
            const int numberStyle = 6;
            const int moneyStyle = 7;

            return value switch
            {
                null => TextCell(column, string.Empty, textStyle),
                ExcelMoney money => new SheetCell(column, money.Value.ToString(CultureInfo.InvariantCulture), moneyStyle, false),
                decimal decimalValue => new SheetCell(column, decimalValue.ToString(CultureInfo.InvariantCulture), numberStyle, false),
                double doubleValue => new SheetCell(column, doubleValue.ToString(CultureInfo.InvariantCulture), numberStyle, false),
                float floatValue => new SheetCell(column, floatValue.ToString(CultureInfo.InvariantCulture), numberStyle, false),
                int intValue => new SheetCell(column, intValue.ToString(CultureInfo.InvariantCulture), numberStyle, false),
                long longValue => new SheetCell(column, longValue.ToString(CultureInfo.InvariantCulture), numberStyle, false),
                DateTime dateValue => TextCell(column, dateValue.ToString("dd/MM/yyyy HH:mm"), textStyle),
                _ => TextCell(column, value.ToString() ?? string.Empty, textStyle)
            };
        }

        private static SheetCell TextCell(int column, string value, int style)
        {
            return new SheetCell(column, value, style, true);
        }

        private static string CellXml(int row, SheetCell cell)
        {
            string reference = ColumnName(cell.Column) + row;
            if (cell.IsText)
            {
                return $"<c r=\"{reference}\" s=\"{cell.Style}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(cell.Value)}</t></is></c>";
            }

            return $"<c r=\"{reference}\" s=\"{cell.Style}\"><v>{cell.Value}</v></c>";
        }

        private static string ColumnName(int column)
        {
            StringBuilder result = new();
            while (column > 0)
            {
                column--;
                result.Insert(0, (char)('A' + column % 26));
                column /= 26;
            }
            return result.ToString();
        }

        private static string Escape(string value)
        {
            string sanitized = new(value.Where(XmlConvert.IsXmlChar).ToArray());
            return SecurityElement.Escape(sanitized) ?? string.Empty;
        }

        private static void WriteEntry(ZipArchive archive, string path, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        private static string WorkbookXml(string sheetName) =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            $"<sheets><sheet name=\"{Escape(SafeSheetName(sheetName))}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";

        private static string CorePropertiesXml(string title) =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
            $"<dc:title>{Escape(title)}</dc:title><dc:creator>Phần mềm quản lý khách sạn An Phú</dc:creator><dcterms:created xsi:type=\"dcterms:W3CDTF\">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</dcterms:created></cp:coreProperties>";

        private static string SafeSheetName(string value)
        {
            foreach (char invalid in new[] { '\\', '/', '?', '*', '[', ']', ':' })
            {
                value = value.Replace(invalid, '-');
            }
            return string.IsNullOrWhiteSpace(value) ? "Dữ liệu" : value[..Math.Min(31, value.Length)];
        }

        private const string ContentTypesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/><Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/><Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/></Types>";

        private const string RootRelationshipsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/><Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/></Relationships>";

        private const string WorkbookRelationshipsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>";

        private const string AppPropertiesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\"><Application>Phần mềm quản lý khách sạn An Phú</Application></Properties>";

        private const string StylesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<numFmts count=\"2\"><numFmt numFmtId=\"164\" formatCode=\"#,##0\"/><numFmt numFmtId=\"165\" formatCode=\"#,##0 &quot;VND&quot;\"/></numFmts>" +
            "<fonts count=\"4\"><font><sz val=\"11\"/><name val=\"Arial\"/></font><font><b/><sz val=\"16\"/><name val=\"Arial\"/></font><font><b/><sz val=\"11\"/><name val=\"Arial\"/></font><font><i/><sz val=\"10\"/><name val=\"Arial\"/></font></fonts>" +
            "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
            "<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style=\"thin\"><color rgb=\"FF000000\"/></left><right style=\"thin\"><color rgb=\"FF000000\"/></right><top style=\"thin\"><color rgb=\"FF000000\"/></top><bottom style=\"thin\"><color rgb=\"FF000000\"/></bottom><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"9\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyAlignment=\"1\"><alignment horizontal=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyAlignment=\"1\"><alignment horizontal=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyAlignment=\"1\"><alignment vertical=\"center\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyAlignment=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyAlignment=\"1\"><alignment horizontal=\"right\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" +
            "</cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>";

        private sealed record SheetRow(int Index, IReadOnlyList<SheetCell> Cells, double Height);
        private sealed record SheetCell(int Column, string Value, int Style, bool IsText);
    }

    internal sealed class ExcelDocument
    {
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public string SheetName { get; init; } = "Dữ liệu";
        public List<ExcelSection> Sections { get; } = new();
    }

    internal sealed class ExcelSection
    {
        public string Title { get; init; } = string.Empty;
        public IReadOnlyList<string> Headers { get; init; } = [];
        public IReadOnlyList<double> ColumnWidths { get; init; } = [];
        public List<IReadOnlyList<object?>> Rows { get; } = new();
        public string Summary { get; init; } = string.Empty;
        internal int HeaderRow { get; set; }
        internal int LastDataRow { get; set; }
    }

    internal readonly record struct ExcelMoney(decimal Value);
}
