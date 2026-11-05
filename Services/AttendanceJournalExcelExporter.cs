using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using EduTrack.Models;
using EduTrack.ViewModels;

namespace EduTrack.Services
{
    /// <summary>
    /// Oylik davomat jurnalini Excel (.xlsx) faylga chiqaradi: sarlavha,
    /// kunlar bo'yicha +/−/K/S kataklari, talaba jamlanmalari va umumiy xulosa.
    /// </summary>
    public sealed class AttendanceJournalExcelExporter : IAttendanceJournalExcelExporter
    {
        private const string SheetName = "Davomat";
        private const int TitleRow = 1;
        private const int MetaRow = 2;
        private const int InfoRow = 3;
        private const int LegendRow = 4;
        private const int HeaderRow = 5;
        private const int FixedColumns = 2;
        private const int TotalColumns = 5;
        private const int MaxFileNamePartLength = 50;

        private static readonly XLColor TitleFill = XLColor.FromHtml("#E6F4F1");
        private static readonly XLColor TitleFont = XLColor.FromHtml("#12343B");
        private static readonly XLColor HeaderFill = XLColor.FromHtml("#1F4E5A");
        private static readonly XLColor SummaryFill = XLColor.FromHtml("#E6F4F1");
        private static readonly XLColor EmptyFill = XLColor.FromHtml("#F1F4F3");
        private static readonly XLColor EmptyFont = XLColor.FromHtml("#7F8C88");
        private static readonly XLColor GreenFill = XLColor.FromHtml("#C6EFCE");
        private static readonly XLColor GreenFont = XLColor.FromHtml("#006100");
        private static readonly XLColor YellowFill = XLColor.FromHtml("#FFEB9C");
        private static readonly XLColor YellowFont = XLColor.FromHtml("#9C6500");
        private static readonly XLColor RedFill = XLColor.FromHtml("#FFC7CE");
        private static readonly XLColor RedFont = XLColor.FromHtml("#9C0006");
        private static readonly XLColor BlueFill = XLColor.FromHtml("#DDEBF7");
        private static readonly XLColor BlueFont = XLColor.FromHtml("#1F4E79");

        public byte[] Export(AttendanceJournalViewModel journal, DateTime generatedAt)
        {
            ArgumentNullException.ThrowIfNull(journal);

            var dayCount = journal.Days.Count;
            var lastColumn = FixedColumns + dayCount + TotalColumns;
            var lastDataRow = HeaderRow + journal.Rows.Count;
            var summaryRow = lastDataRow + 2;
            var firstTotalColumn = FixedColumns + dayCount + 1;

            using var workbook = new XLWorkbook();
            workbook.Properties.Title = "Davomat jurnali";
            workbook.Properties.Author = "EduTrack";

            var ws = workbook.Worksheets.Add(SheetName);
            WriteHeaderBlock(ws, journal, generatedAt, lastColumn);
            WriteTableHeader(ws, journal, lastColumn, firstTotalColumn);
            WriteStudentRows(ws, journal, firstTotalColumn);
            WriteSummary(ws, journal, summaryRow, firstTotalColumn, lastColumn);

            StyleTable(ws, lastColumn, lastDataRow);
            SetColumnWidths(ws, journal, firstTotalColumn);

            ws.SheetView.FreezeRows(HeaderRow);
            ws.SheetView.FreezeColumns(FixedColumns);
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.SetRowsToRepeatAtTop(HeaderRow, HeaderRow);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public string BuildFileName(AttendanceJournalViewModel journal)
        {
            ArgumentNullException.ThrowIfNull(journal);

            var subject = SanitizeFileNamePart(journal.SubjectName, "Fan");
            var group = SanitizeFileNamePart(journal.GroupName, "Guruh");
            var month = journal.Month.ToString("yyyy-MM", CultureInfo.InvariantCulture);

            return $"Davomat_{subject}_{group}_{month}.xlsx";
        }

        private static void WriteHeaderBlock(
            IXLWorksheet ws,
            AttendanceJournalViewModel journal,
            DateTime generatedAt,
            int lastColumn)
        {
            var titleRange = ws.Range(TitleRow, 1, TitleRow, lastColumn);
            titleRange.Merge();
            SetText(titleRange.FirstCell(), "Davomat jurnali");
            titleRange.Style.Font.Bold = true;
            titleRange.Style.Font.FontSize = 16;
            titleRange.Style.Font.FontColor = TitleFont;
            titleRange.Style.Fill.BackgroundColor = TitleFill;
            titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(TitleRow).Height = 26;

            WriteMergedRow(
                ws,
                MetaRow,
                lastColumn,
                $"Fan: {ValueOrDash(journal.SubjectName)}    |    " +
                $"Guruh: {ValueOrDash(journal.GroupName)}    |    Oy: {ValueOrDash(journal.MonthLabel)}",
                bold: true);

            WriteMergedRow(
                ws,
                InfoRow,
                lastColumn,
                $"Talabalar: {journal.Rows.Count}    |    Dars kunlari: {journal.Days.Count}    |    " +
                $"Eksport: {generatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}");

            var legendRange = ws.Range(LegendRow, 1, LegendRow, lastColumn);
            legendRange.Merge();
            SetText(
                legendRange.FirstCell(),
                "Belgilar:  + Keldi     − Kelmadi     K Kech qoldi     S Sababli");
            legendRange.Style.Font.FontColor = EmptyFont;
            legendRange.Style.Font.FontSize = 9;
            legendRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(LegendRow).Height = 18;
        }

        private static void WriteMergedRow(
            IXLWorksheet ws,
            int row,
            int lastColumn,
            string text,
            bool bold = false)
        {
            var range = ws.Range(row, 1, row, lastColumn);
            range.Merge();
            SetText(range.FirstCell(), text);
            range.Style.Font.Bold = bold;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        private static void WriteTableHeader(
            IXLWorksheet ws,
            AttendanceJournalViewModel journal,
            int lastColumn,
            int firstTotalColumn)
        {
            SetText(ws.Cell(HeaderRow, 1), "№");
            SetText(ws.Cell(HeaderRow, 2), "F.I.Sh.");

            for (var i = 0; i < journal.Days.Count; i++)
            {
                var day = journal.Days[i];
                SetText(
                    ws.Cell(HeaderRow, FixedColumns + i + 1),
                    $"{day.WeekdayShort} {day.Date.Day}");
            }

            var totals = new[] { "Keldi", "Kelmadi", "Kech", "Sababli", "Foiz" };
            for (var i = 0; i < totals.Length; i++)
            {
                SetText(ws.Cell(HeaderRow, firstTotalColumn + i), totals[i]);
            }

            var headerRange = ws.Range(HeaderRow, 1, HeaderRow, lastColumn);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Fill.BackgroundColor = HeaderFill;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Alignment.WrapText = true;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.Row(HeaderRow).Height = 34;
        }

        private static void WriteStudentRows(
            IXLWorksheet ws,
            AttendanceJournalViewModel journal,
            int firstTotalColumn)
        {
            var rowNumber = HeaderRow + 1;
            var index = 1;

            foreach (var student in journal.Rows)
            {
                ws.Cell(rowNumber, 1).Value = index;
                ws.Cell(rowNumber, 1).Style.NumberFormat.Format = "0";
                ws.Cell(rowNumber, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                SetText(ws.Cell(rowNumber, 2), student.FullName);
                ws.Cell(rowNumber, 2).Style.Alignment.WrapText = true;

                for (var i = 0; i < journal.Days.Count; i++)
                {
                    var key = journal.Days[i].Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    student.Statuses.TryGetValue(key, out var status);

                    var cell = ws.Cell(rowNumber, FixedColumns + i + 1);
                    var symbol = StatusSymbol(status);
                    if (!string.IsNullOrEmpty(symbol))
                    {
                        cell.Value = symbol;
                    }

                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    ApplyStatusStyle(cell, status);
                }

                WriteCount(ws, rowNumber, firstTotalColumn, student.Keldi, GreenFont);
                WriteCount(ws, rowNumber, firstTotalColumn + 1, student.Kelmadi, RedFont);
                WriteCount(ws, rowNumber, firstTotalColumn + 2, student.KechQoldi, YellowFont);
                WriteCount(ws, rowNumber, firstTotalColumn + 3, student.Sababli, BlueFont);

                var percentCell = ws.Cell(rowNumber, firstTotalColumn + 4);
                percentCell.Value = student.Percentage;
                percentCell.Style.NumberFormat.Format = "0\"%\"";
                percentCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                percentCell.Style.Font.Bold = true;
                ApplyPercentStyle(percentCell, student.Percentage, student.Total > 0);

                ws.Row(rowNumber).Height = 20;
                rowNumber++;
                index++;
            }
        }

        private static void WriteSummary(
            IXLWorksheet ws,
            AttendanceJournalViewModel journal,
            int summaryRow,
            int firstTotalColumn,
            int lastColumn)
        {
            var labelRange = ws.Range(summaryRow, 1, summaryRow, FixedColumns);
            labelRange.Merge();
            SetText(labelRange.FirstCell(), $"Jami: {journal.Rows.Count} talaba");
            labelRange.Style.Font.Bold = true;
            labelRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            var keldi = journal.Rows.Sum(row => row.Keldi);
            var kelmadi = journal.Rows.Sum(row => row.Kelmadi);
            var kech = journal.Rows.Sum(row => row.KechQoldi);
            var sababli = journal.Rows.Sum(row => row.Sababli);
            var total = keldi + kelmadi + kech + sababli;

            WriteCount(ws, summaryRow, firstTotalColumn, keldi, GreenFont);
            WriteCount(ws, summaryRow, firstTotalColumn + 1, kelmadi, RedFont);
            WriteCount(ws, summaryRow, firstTotalColumn + 2, kech, YellowFont);
            WriteCount(ws, summaryRow, firstTotalColumn + 3, sababli, BlueFont);

            var percent = total == 0
                ? 0
                : (int)Math.Round(100.0 * keldi / total, MidpointRounding.AwayFromZero);
            var percentCell = ws.Cell(summaryRow, firstTotalColumn + 4);
            percentCell.Value = percent;
            percentCell.Style.NumberFormat.Format = "0\"%\"";
            percentCell.Style.Font.Bold = true;
            percentCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ApplyPercentStyle(percentCell, percent, total > 0);

            var summaryRange = ws.Range(summaryRow, 1, summaryRow, lastColumn);
            summaryRange.Style.Fill.BackgroundColor = SummaryFill;
            summaryRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            summaryRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            ws.Row(summaryRow).Height = 22;
        }

        private static void WriteCount(
            IXLWorksheet ws,
            int row,
            int column,
            int value,
            XLColor fontColor)
        {
            var cell = ws.Cell(row, column);
            cell.Value = value;
            cell.Style.NumberFormat.Format = "0";
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Font.FontColor = fontColor;
        }

        private static void StyleTable(IXLWorksheet ws, int lastColumn, int lastDataRow)
        {
            var tableRange = ws.Range(HeaderRow, 1, lastDataRow, lastColumn);
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        private static void SetColumnWidths(
            IXLWorksheet ws,
            AttendanceJournalViewModel journal,
            int firstTotalColumn)
        {
            ws.Column(1).Width = 5;
            ws.Column(2).Width = 32;

            for (var i = 0; i < journal.Days.Count; i++)
            {
                ws.Column(FixedColumns + i + 1).Width = 6.5;
            }

            for (var i = 0; i < TotalColumns; i++)
            {
                ws.Column(firstTotalColumn + i).Width = 9;
            }
        }

        private static void ApplyStatusStyle(IXLCell cell, AttendanceStatus? status)
        {
            switch (status)
            {
                case AttendanceStatus.Keldi:
                    cell.Style.Fill.BackgroundColor = GreenFill;
                    cell.Style.Font.FontColor = GreenFont;
                    break;
                case AttendanceStatus.Kelmadi:
                    cell.Style.Fill.BackgroundColor = RedFill;
                    cell.Style.Font.FontColor = RedFont;
                    break;
                case AttendanceStatus.KechQoldi:
                    cell.Style.Fill.BackgroundColor = YellowFill;
                    cell.Style.Font.FontColor = YellowFont;
                    break;
                case AttendanceStatus.Sababli:
                    cell.Style.Fill.BackgroundColor = BlueFill;
                    cell.Style.Font.FontColor = BlueFont;
                    break;
                default:
                    cell.Style.Fill.BackgroundColor = EmptyFill;
                    cell.Style.Font.FontColor = EmptyFont;
                    break;
            }
        }

        private static void ApplyPercentStyle(IXLCell cell, int percent, bool hasRecords)
        {
            if (!hasRecords)
            {
                cell.Style.Fill.BackgroundColor = EmptyFill;
                cell.Style.Font.FontColor = EmptyFont;
                return;
            }

            if (percent >= 80)
            {
                cell.Style.Fill.BackgroundColor = GreenFill;
                cell.Style.Font.FontColor = GreenFont;
            }
            else if (percent >= 60)
            {
                cell.Style.Fill.BackgroundColor = YellowFill;
                cell.Style.Font.FontColor = YellowFont;
            }
            else
            {
                cell.Style.Fill.BackgroundColor = RedFill;
                cell.Style.Font.FontColor = RedFont;
            }
        }

        private static string StatusSymbol(AttendanceStatus? status) => status switch
        {
            AttendanceStatus.Keldi => "+",
            AttendanceStatus.Kelmadi => "−",
            AttendanceStatus.KechQoldi => "K",
            AttendanceStatus.Sababli => "S",
            _ => string.Empty
        };

        private static string SanitizeFileNamePart(string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars())
            {
                '\\', '/', ':', '*', '?', '"', '<', '>', '|'
            };

            var builder = new StringBuilder(value.Length);
            foreach (var ch in value.Trim())
            {
                builder.Append(invalid.Contains(ch) || char.IsControl(ch) ? ' ' : ch);
            }

            var safe = Regex.Replace(builder.ToString(), @"\s+", " ").Trim(' ', '.');
            if (safe.Length > MaxFileNamePartLength)
            {
                safe = safe[..MaxFileNamePartLength].Trim(' ', '.');
            }

            return string.IsNullOrEmpty(safe) ? fallback : safe;
        }

        private static void SetText(IXLCell cell, string? text)
        {
            var value = CleanText(text);
            cell.SetValue(value);

            if (NeedsFormulaProtection(value))
            {
                cell.Style.IncludeQuotePrefix = true;
            }
        }

        private static string CleanText(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                builder.Append(char.IsControl(ch) ? ' ' : ch);
            }

            return builder.ToString().Trim();
        }

        private static bool NeedsFormulaProtection(string value)
        {
            return value.Length > 0
                && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n';
        }

        private static string ValueOrDash(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value;
        }
    }
}
