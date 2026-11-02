using ClosedXML.Excel;
using EduTrack.Tests.Infrastructure;
using EduTrack.ViewModels;
using Xunit;

namespace EduTrack.Tests
{
    public class ExamResultsExcelExporterTests : IClassFixture<ExamResultsTestFixture>
    {
        private readonly ExamResultsTestFixture _fixture;

        public ExamResultsExcelExporterTests(ExamResultsTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Export_WritesMetadataHeadersRowsAndSheetSettings()
        {
            var (data, result) = await ExportExam1Async();

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.Equal(
                $"Imtihon_Matematika 1_{data.GeneratedAt:yyyy-MM-dd}.xlsx",
                result.FileName);

            using var workbook = new XLWorkbook(new MemoryStream(result.Content));
            var ws = workbook.Worksheet(1);

            Assert.Equal("Natijalar", ws.Name);
            Assert.Equal("Imtihon natijalari", ws.Cell(1, 1).GetString());
            Assert.Contains("Matematika 1", ws.Cell(2, 1).GetString());
            Assert.Contains("Fan: Matematika", ws.Cell(3, 1).GetString());
            Assert.Contains("guruh: hozirgi", ws.Cell(3, 1).GetString());
            Assert.Contains("Vaqt: mahalliy vaqt", ws.Cell(4, 1).GetString());
            Assert.Contains("Filtr: yo'q", ws.Cell(5, 1).GetString());
            Assert.Contains("Jami: 8 ta natija", ws.Cell(6, 1).GetString());

            var expectedHeaders = new[]
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

            for (var i = 0; i < expectedHeaders.Length; i++)
            {
                Assert.Equal(expectedHeaders[i], ws.Cell(8, i + 1).GetString());
            }

            Assert.Equal(8, ws.SheetView.SplitRow);
            Assert.Equal(XLPageOrientation.Landscape, ws.PageSetup.PageOrientation);
            Assert.Equal(1, ws.PageSetup.PagesWide);
            Assert.Equal(0, ws.PageSetup.PagesTall);
            Assert.Equal("A8:J16", ws.AutoFilter.Range.RangeAddress.ToString());

            Assert.Equal("10008", ws.Cell(9, 4).GetString());
            Assert.Equal("10001", ws.Cell(16, 4).GetString());
        }

        [Theory]
        [InlineData("10004", 59.9, "#FFC7CE")]
        [InlineData("10005", 60.0, "#FFEB9C")]
        [InlineData("10003", 85.9, "#FFEB9C")]
        [InlineData("10002", 86.0, "#C6EFCE")]
        public async Task Export_ColorsPercentAtBoundaries(
            string loginId,
            double expectedPercent,
            string expectedFill)
        {
            var (_, result) = await ExportExam1Async();

            using var workbook = new XLWorkbook(new MemoryStream(result.Content));
            var ws = workbook.Worksheet(1);
            var row = FindRowByLogin(ws, loginId);
            var cell = ws.Cell(row, 7);

            Assert.Equal(expectedPercent, cell.GetDouble(), 3);
            Assert.Equal(
                XLColor.FromHtml(expectedFill).Color.ToArgb(),
                cell.Style.Fill.BackgroundColor.Color.ToArgb());
        }

        [Fact]
        public async Task Export_WritesNumericPercentAndDate()
        {
            var (_, result) = await ExportExam1Async();

            using var workbook = new XLWorkbook(new MemoryStream(result.Content));
            var ws = workbook.Worksheet(1);
            var row = FindRowByLogin(ws, "10002");

            var percent = ws.Cell(row, 7);
            Assert.Equal(XLDataType.Number, percent.DataType);
            Assert.Contains("%", percent.Style.NumberFormat.Format);
            Assert.Equal(86.0, percent.GetDouble(), 3);

            var date = ws.Cell(row, 8);
            Assert.Equal(XLDataType.DateTime, date.DataType);
            Assert.Equal("yyyy-MM-dd HH:mm", date.Style.DateFormat.Format);
            Assert.Equal(new DateTime(2026, 1, 15, 11, 0, 0), date.GetDateTime());
        }

        [Fact]
        public async Task Export_WritesStatusAndReliabilityLabelsOnly()
        {
            var (_, result) = await ExportExam1Async();

            using var workbook = new XLWorkbook(new MemoryStream(result.Content));
            var ws = workbook.Worksheet(1);

            var approvedRow = FindRowByLogin(ws, "10001");
            Assert.Equal("Tasdiqlangan", ws.Cell(approvedRow, 9).GetString());
            Assert.Equal("Ishonchli", ws.Cell(approvedRow, 10).GetString());

            var suspiciousRow = FindRowByLogin(ws, "10002");
            Assert.Equal("Kutilmoqda", ws.Cell(suspiciousRow, 9).GetString());
            Assert.Equal("Shubhali", ws.Cell(suspiciousRow, 10).GetString());

            foreach (var cell in ws.CellsUsed())
            {
                var text = cell.GetString();
                Assert.DoesNotContain("TabSwitchCount", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("ViolationCount", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("ExitedFullscreen", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("QuestionIdsJson", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("SelectedOptionIdsJson", text, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task Export_WritesSummaryRow()
        {
            var (_, result) = await ExportExam1Async();

            using var workbook = new XLWorkbook(new MemoryStream(result.Content));
            var ws = workbook.Worksheet(1);
            var summary = ws.RowsUsed()
                .First(r => r.Cell(1).GetString().StartsWith("Xulosa", StringComparison.Ordinal))
                .Cell(1)
                .GetString();

            Assert.Contains("jami 8 ta", summary);
            Assert.Contains("o'rtacha 62.1%", summary);
            Assert.Contains("eng yuqori 86.0%", summary);
            Assert.Contains("eng past 0.0%", summary);
        }

        [Fact]
        public async Task Export_UsesFilteredSet()
        {
            var (data, result) = await ExportExam1Async(new ExamResultsFilterViewModel
            {
                GroupId = _fixture.Seed.Group1Id,
                Status = ExamResultStatusFilter.Approved
            });

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.Equal(2, data.TotalCount);

            using var workbook = new XLWorkbook(new MemoryStream(result.Content));
            var ws = workbook.Worksheet(1);

            Assert.Contains("guruh: Guruh A", ws.Cell(5, 1).GetString());
            Assert.Contains("holat: tasdiqlangan", ws.Cell(5, 1).GetString());
            Assert.Equal("A8:J10", ws.AutoFilter.Range.RangeAddress.ToString());
        }

        [Fact]
        public void Export_NeutralizesFormulaInjection()
        {
            var data = new ExamResultsExportData
            {
                ExamTitle = "=cmd|' /C calc'!A0",
                SubjectName = "@SUM(1,1)",
                TotalCount = 2,
                AveragePercent = 50,
                HighestPercent = 60,
                LowestPercent = 40,
                GeneratedAt = new DateTime(2026, 10, 31, 12, 0, 0),
                Rows = new List<ExamResultRowViewModel>
                {
                    new()
                    {
                        StudentName = "=HYPERLINK(\"http://evil\")",
                        GroupName = "-Guruh",
                        LoginId = "+10001",
                        CorrectAnswers = 1,
                        TotalQuestions = 2,
                        Percent = 50,
                        CompletedDate = new DateTime(2026, 10, 30, 10, 0, 0),
                        IsApproved = true,
                        IsSuspicious = false
                    },
                    new()
                    {
                        StudentName = "@cmd",
                        GroupName = "+Guruh",
                        LoginId = "=1+1",
                        CorrectAnswers = 2,
                        TotalQuestions = 2,
                        Percent = 100,
                        CompletedDate = new DateTime(2026, 10, 30, 11, 0, 0),
                        IsApproved = false,
                        IsSuspicious = true
                    }
                }
            };

            var result = _fixture.Exporter.Export(data);

            Assert.True(result.Succeeded, result.ErrorMessage);

            using var workbook = new XLWorkbook(new MemoryStream(result.Content));
            var ws = workbook.Worksheet(1);

            Assert.All(ws.CellsUsed(), cell => Assert.False(cell.HasFormula));

            Assert.True(ws.Cell(9, 2).Style.IncludeQuotePrefix);
            Assert.True(ws.Cell(9, 3).Style.IncludeQuotePrefix);
            Assert.True(ws.Cell(9, 4).Style.IncludeQuotePrefix);
            Assert.True(ws.Cell(10, 2).Style.IncludeQuotePrefix);
            Assert.True(ws.Cell(10, 3).Style.IncludeQuotePrefix);
            Assert.True(ws.Cell(10, 4).Style.IncludeQuotePrefix);

            Assert.Equal("=HYPERLINK(\"http://evil\")", ws.Cell(9, 2).GetString());
            Assert.Equal("+10001", ws.Cell(9, 4).GetString());
        }

        [Fact]
        public void Export_RejectsMoreThanFiveThousandRows()
        {
            var result = _fixture.Exporter.Export(new ExamResultsExportData
            {
                ExamTitle = "Katta imtihon",
                TotalCount = 5001
            });

            Assert.False(result.Succeeded);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("5001", result.ErrorMessage);
            Assert.Contains("5000", result.ErrorMessage);
            Assert.Contains("filtrlarni toraytiring", result.ErrorMessage);
        }

        [Fact]
        public void Export_EmptyResultProducesValidWorkbook()
        {
            var result = _fixture.Exporter.Export(new ExamResultsExportData
            {
                ExamTitle = "Bo'sh imtihon",
                TotalCount = 0,
                GeneratedAt = new DateTime(2026, 10, 31, 12, 0, 0)
            });

            Assert.True(result.Succeeded, result.ErrorMessage);

            using var workbook = new XLWorkbook(new MemoryStream(result.Content));
            var ws = workbook.Worksheet(1);

            Assert.Equal("Jami: 0 ta natija", ws.Cell(6, 1).GetString());
            Assert.Equal("Xulosa: natija yo'q", ws.Cell(10, 1).GetString());
            Assert.False(ws.AutoFilter.IsEnabled);
        }

        [Fact]
        public void BuildFileName_SanitizesInvalidCharacters()
        {
            var name = _fixture.Exporter.BuildFileName(
                "Matematika: 1/2? <test>",
                new DateTime(2026, 10, 31));

            Assert.Equal("Imtihon_Matematika 1 2 test_2026-10-31.xlsx", name);
            Assert.DoesNotContain(':', name);
            Assert.DoesNotContain('/', name);
            Assert.DoesNotContain('?', name);
            Assert.DoesNotContain('<', name);
            Assert.DoesNotContain('>', name);
        }

        [Fact]
        public void BuildFileName_TruncatesLongTitle()
        {
            var name = _fixture.Exporter.BuildFileName(
                new string('a', 200),
                new DateTime(2026, 10, 31));

            Assert.True(
                name.Length <= "Imtihon_".Length + 80 + "_2026-10-31.xlsx".Length,
                $"Fayl nomi juda uzun: {name.Length}");
        }

        private async Task<(ExamResultsExportData Data, ExamResultsExcelExportResult Result)>
            ExportExam1Async(ExamResultsFilterViewModel? filter = null)
        {
            var data = await _fixture.Service.GetExportForTeacherAsync(
                _fixture.Seed.Teacher1Id,
                _fixture.Seed.Exam1Id,
                filter ?? new ExamResultsFilterViewModel());

            Assert.NotNull(data);
            var result = _fixture.Exporter.Export(data!);
            return (data!, result);
        }

        private static int FindRowByLogin(IXLWorksheet ws, string loginId)
        {
            return ws.RowsUsed()
                .Select(r => r.RowNumber())
                .First(row => ws.Cell(row, 4).GetString() == loginId);
        }
    }
}
