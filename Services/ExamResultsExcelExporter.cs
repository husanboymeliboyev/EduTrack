using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using EduTrack.ViewModels;

namespace EduTrack.Services
{
    // Imtihon natijalarini Excel (.xlsx) faylga chiqaradi.
    // Faqat jadvalda ko'rinadigan maydonlar yoziladi: javob JSON'lari va
    // anti-cheat xom qiymatlari (TabSwitchCount, ViolationCount) hech qachon chiqmaydi.
    public class ExamResultsExcelExporter : IExamResultsExcelExporter
    {
        private const int ColumnCount = 10;
        private const int TitleRow = 1;
        private const int HeaderRow = 8;
        private const int MaxTitleLength = 80;
        private const string SheetName = "Natijalar";

        private static readonly XLColor HeaderFill = XLColor.FromHtml("#1F4E5A");
        private static readonly XLColor TitleFont = XLColor.FromHtml("#12343B");
        private static readonly XLColor SummaryFill = XLColor.FromHtml("#E6F4F1");
        private static readonly XLColor AlternateFill = XLColor.FromHtml("#F7FAFA");
        private static readonly XLColor GreenFill = XLColor.FromHtml("#C6EFCE");
        private static readonly XLColor GreenFont = XLColor.FromHtml("#006100");
        private static readonly XLColor YellowFill = XLColor.FromHtml("#FFEB9C");
        private static readonly XLColor YellowFont = XLColor.FromHtml("#9C6500");
        private static readonly XLColor RedFill = XLColor.FromHtml("#FFC7CE");
        private static readonly XLColor RedFont = XLColor.FromHtml("#9C0006");

        public ExamResultsExcelExportResult Export(ExamResultsExportData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            var fileName = BuildFileName(data.ExamTitle, data.GeneratedAt);

            if (data.ExceedsRowLimit || data.Rows.Count > ExamResultsExportData.MaxRows)
            {
                return ExamResultsExcelExportResult.Failure(
                    $"Eksport uchun {data.TotalCount} ta natija topildi. " +
                    $"Bir marta {ExamResultsExportData.MaxRows} tadan ko'p natijani eksport qilib bo'lmaydi. " +
                    "Iltimos, filtrlarni toraytiring.",
                    fileName);
            }

            using var workbook = new XLWorkbook();
            workbook.Properties.Title = $"Imtihon natijalari - {data.ExamTitle}";
            workbook.Properties.Author = "EduTrack";

            var ws = workbook.Worksheets.Add(SheetName);
            WriteMetadata(ws, data);
            WriteHeader(ws);
            var lastDataRow = WriteRows(ws, data.Rows);
            WriteSummary(ws, data, lastDataRow);

            ws.SheetView.FreezeRows(HeaderRow);

            if (data.Rows.Count > 0)
            {
                ws.Range(HeaderRow, 1, lastDataRow, ColumnCount).SetAutoFilter();
            }

            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.SetRowsToRepeatAtTop(HeaderRow, HeaderRow);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return ExamResultsExcelExportResult.Success(stream.ToArray(), fileName);
        }

        public string BuildFileName(string examTitle, DateTime generatedAt)
        {
            var safeTitle = SanitizeFileNamePart(examTitle);
            return $"Imtihon_{safeTitle}_{generatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.xlsx";
        }

        private static void WriteMetadata(IXLWorksheet ws, ExamResultsExportData data)
        {
            var titleRange = ws.Range(TitleRow, 1, TitleRow, ColumnCount);
            titleRange.Merge();
            SetText(titleRange.FirstCell(), "Imtihon natijalari");
            titleRange.Style.Font.Bold = true;
            titleRange.Style.Font.FontSize = 16;
            titleRange.Style.Font.FontColor = TitleFont;
            titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(TitleRow).Height = 24;

            WriteMetadataRow(ws, 2, $"Imtihon: {data.ExamTitle}", bold: true);
            WriteMetadataRow(
                ws,
                3,
                $"Fan: {ValueOrDash(data.SubjectName)} | guruh: hozirgi");
            WriteMetadataRow(
                ws,
                4,
                $"Vaqt: mahalliy vaqt ({data.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)})");
            WriteMetadataRow(ws, 5, $"Filtr: {BuildFilterSummary(data)}");
            WriteMetadataRow(ws, 6, $"Jami: {data.TotalCount} ta natija", bold: true);

            ws.Row(7).Height = 8;
        }

        private static void WriteMetadataRow(
            IXLWorksheet ws,
            int row,
            string text,
            bool bold = false)
        {
            var range = ws.Range(row, 1, row, ColumnCount);
            range.Merge();
            SetText(range.FirstCell(), text);
            range.Style.Font.Bold = bold;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        private static void WriteHeader(IXLWorksheet ws)
        {
            var headers = new[]
            {
                "№",
                "Talaba",
                "Guruh",
                "Login",
                "To'g'ri javoblar",
                "Jami savol",
                "Foiz",
                "Topshirgan vaqt",
                "Holat",
                "Ishonchlilik"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                SetText(ws.Cell(HeaderRow, i + 1), headers[i]);
            }

            var headerRange = ws.Range(HeaderRow, 1, HeaderRow, ColumnCount);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Fill.BackgroundColor = HeaderFill;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Alignment.WrapText = true;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.Row(HeaderRow).Height = 32;
        }

        private static int WriteRows(
            IXLWorksheet ws,
            IReadOnlyList<ExamResultRowViewModel> rows)
        {
            var rowNumber = HeaderRow + 1;
            var index = 1;

            foreach (var row in rows)
            {
                ws.Cell(rowNumber, 1).Value = index;
                SetText(ws.Cell(rowNumber, 2), row.StudentName);
                SetText(ws.Cell(rowNumber, 3), ValueOrDash(row.GroupName));
                SetText(ws.Cell(rowNumber, 4), row.LoginId);

                ws.Cell(rowNumber, 5).Value = row.CorrectAnswers;
                ws.Cell(rowNumber, 6).Value = row.TotalQuestions;

                var percentCell = ws.Cell(rowNumber, 7);
                percentCell.Value = row.Percent;
                percentCell.Style.NumberFormat.Format = "0.0\"%\"";
                ApplyPercentColor(percentCell, row.Percent);

                var dateCell = ws.Cell(rowNumber, 8);
                dateCell.Value = row.CompletedDate;
                dateCell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm";

                var statusCell = ws.Cell(rowNumber, 9);
                SetText(statusCell, row.IsApproved ? "Tasdiqlangan" : "Kutilmoqda");
                statusCell.Style.Font.FontColor = row.IsApproved ? GreenFont : YellowFont;

                var reliabilityCell = ws.Cell(rowNumber, 10);
                SetText(reliabilityCell, row.IsSuspicious ? "Shubhali" : "Ishonchli");
                reliabilityCell.Style.Font.FontColor = row.IsSuspicious ? RedFont : GreenFont;

                ws.Cell(rowNumber, 1).Style.NumberFormat.Format = "0";
                ws.Cell(rowNumber, 5).Style.NumberFormat.Format = "0";
                ws.Cell(rowNumber, 6).Style.NumberFormat.Format = "0";

                if (index % 2 == 0)
                {
                    ws.Range(rowNumber, 1, rowNumber, ColumnCount)
                        .Style.Fill.BackgroundColor = AlternateFill;
                    ApplyPercentColor(percentCell, row.Percent);
                }

                rowNumber++;
                index++;
            }

            if (rows.Count > 0)
            {
                var dataRange = ws.Range(HeaderRow + 1, 1, rowNumber - 1, ColumnCount);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }

            SetColumnWidths(ws);
            return rowNumber - 1;
        }

        private static void WriteSummary(
            IXLWorksheet ws,
            ExamResultsExportData data,
            int lastDataRow)
        {
            var summaryRow = lastDataRow + 2;
            var range = ws.Range(summaryRow, 1, summaryRow, ColumnCount);
            range.Merge();

            var summary = data.Rows.Count == 0
                ? "Xulosa: natija yo'q"
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "Xulosa: jami {0} ta | o'rtacha {1:0.0}% | eng yuqori {2:0.0}% | eng past {3:0.0}%",
                    data.TotalCount,
                    data.AveragePercent,
                    data.HighestPercent,
                    data.LowestPercent);

            SetText(range.FirstCell(), summary);
            range.Style.Font.Bold = true;
            range.Style.Fill.BackgroundColor = SummaryFill;
            range.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        private static void SetColumnWidths(IXLWorksheet ws)
        {
            ws.Column(1).Width = 6;
            ws.Column(2).Width = 30;
            ws.Column(3).Width = 18;
            ws.Column(4).Width = 14;
            ws.Column(5).Width = 12;
            ws.Column(6).Width = 10;
            ws.Column(7).Width = 10;
            ws.Column(8).Width = 18;
            ws.Column(9).Width = 15;
            ws.Column(10).Width = 14;
        }

        private static void ApplyPercentColor(IXLCell cell, double percent)
        {
            switch (ExamResultPercent.GetBand(percent))
            {
                case ExamResultPercentBand.High:
                    cell.Style.Fill.BackgroundColor = GreenFill;
                    cell.Style.Font.FontColor = GreenFont;
                    break;
                case ExamResultPercentBand.Medium:
                    cell.Style.Fill.BackgroundColor = YellowFill;
                    cell.Style.Font.FontColor = YellowFont;
                    break;
                default:
                    cell.Style.Fill.BackgroundColor = RedFill;
                    cell.Style.Font.FontColor = RedFont;
                    break;
            }
        }

        private static string BuildFilterSummary(ExamResultsExportData data)
        {
            var filter = data.Filter;
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                parts.Add($"qidiruv: \"{filter.Search}\"");
            }

            if (filter.GroupId.HasValue)
            {
                parts.Add($"guruh: {data.FilterGroupName ?? filter.GroupId.Value.ToString()}");
            }

            if (filter.PercentFrom.HasValue || filter.PercentTo.HasValue)
            {
                var from = filter.PercentFrom?.ToString("0.#", CultureInfo.InvariantCulture) ?? "0";
                var to = filter.PercentTo?.ToString("0.#", CultureInfo.InvariantCulture) ?? "100";
                parts.Add($"foiz: {from}-{to}");
            }

            if (filter.DateFrom.HasValue || filter.DateTo.HasValue)
            {
                var from = filter.DateFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "...";
                var to = filter.DateTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "...";
                parts.Add($"sana: {from}-{to}");
            }

            if (filter.Status.HasValue)
            {
                parts.Add(filter.Status == ExamResultStatusFilter.Approved
                    ? "holat: tasdiqlangan"
                    : "holat: kutilmoqda");
            }

            if (filter.Reliability.HasValue)
            {
                parts.Add(filter.Reliability == ExamResultReliabilityFilter.Clean
                    ? "ishonchlilik: ishonchli"
                    : "ishonchlilik: shubhali");
            }

            return parts.Count == 0 ? "yo'q" : string.Join("; ", parts);
        }

        private static string SanitizeFileNamePart(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Imtihon";
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
            if (safe.Length > MaxTitleLength)
            {
                safe = safe[..MaxTitleLength].Trim(' ', '.');
            }

            return string.IsNullOrEmpty(safe) ? "Imtihon" : safe;
        }

        private static void SetText(IXLCell cell, string? text)
        {
            var value = CleanText(text);
            cell.SetValue(value);

            // Excel formula-injection himoyasi: "=", "+", "-", "@" bilan
            // boshlanadigan matnlar formula sifatida talqin qilinmasligi uchun
            // text + quote-prefix rejimida yoziladi.
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
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }
    }
}
