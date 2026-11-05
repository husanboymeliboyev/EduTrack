using ClosedXML.Excel;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.ViewModels;
using Xunit;

namespace EduTrack.Tests
{
    public class AttendanceJournalExcelExporterTests
    {
        private static readonly DateTime GeneratedAt = new(2026, 11, 2, 10, 30, 0);

        private readonly AttendanceJournalExcelExporter _exporter = new();

        [Fact]
        public void Export_WritesHeaderRowsTotalsAndSheetSettings()
        {
            var journal = BuildJournal();
            var content = _exporter.Export(journal, GeneratedAt);

            using var workbook = new XLWorkbook(new MemoryStream(content));
            var ws = workbook.Worksheet(1);

            Assert.Equal("Davomat", ws.Name);
            Assert.Equal("Davomat jurnali", ws.Cell(1, 1).GetString());
            Assert.Contains("Fan: Matematika", ws.Cell(2, 1).GetString());
            Assert.Contains("Guruh: 1-guruh", ws.Cell(2, 1).GetString());
            Assert.Contains("Oy: Noyabr 2026", ws.Cell(2, 1).GetString());
            Assert.Contains("Talabalar: 2", ws.Cell(3, 1).GetString());
            Assert.Contains("Dars kunlari: 2", ws.Cell(3, 1).GetString());
            Assert.Contains("Eksport: 2026-11-02 10:30", ws.Cell(3, 1).GetString());
            Assert.Contains("+ Keldi", ws.Cell(4, 1).GetString());
            Assert.Contains("S Sababli", ws.Cell(4, 1).GetString());

            Assert.Equal("№", ws.Cell(5, 1).GetString());
            Assert.Equal("F.I.Sh.", ws.Cell(5, 2).GetString());
            Assert.Equal("Du 2", ws.Cell(5, 3).GetString());
            Assert.Equal("Se 3", ws.Cell(5, 4).GetString());

            var totals = new[] { "Keldi", "Kelmadi", "Kech", "Sababli", "Foiz" };
            for (var i = 0; i < totals.Length; i++)
            {
                Assert.Equal(totals[i], ws.Cell(5, 5 + i).GetString());
            }

            Assert.Equal(1, ws.Cell(6, 1).GetValue<int>());
            Assert.Equal("Ali Valiyev", ws.Cell(6, 2).GetString());
            Assert.Equal("+", ws.Cell(6, 3).GetString());
            Assert.Equal("−", ws.Cell(6, 4).GetString());
            Assert.Equal(1, ws.Cell(6, 5).GetValue<int>());
            Assert.Equal(1, ws.Cell(6, 6).GetValue<int>());
            Assert.Equal(0, ws.Cell(6, 7).GetValue<int>());
            Assert.Equal(0, ws.Cell(6, 8).GetValue<int>());
            Assert.Equal(50, ws.Cell(6, 9).GetValue<int>());

            Assert.Equal(2, ws.Cell(7, 1).GetValue<int>());
            Assert.Equal("Vali Aliyev", ws.Cell(7, 2).GetString());
            Assert.Equal("K", ws.Cell(7, 3).GetString());
            Assert.Equal("S", ws.Cell(7, 4).GetString());
            Assert.Equal(0, ws.Cell(7, 5).GetValue<int>());
            Assert.Equal(1, ws.Cell(7, 7).GetValue<int>());
            Assert.Equal(1, ws.Cell(7, 8).GetValue<int>());
            Assert.Equal(0, ws.Cell(7, 9).GetValue<int>());

            Assert.Equal("Jami: 2 talaba", ws.Cell(9, 1).GetString());
            Assert.Equal(1, ws.Cell(9, 5).GetValue<int>());
            Assert.Equal(1, ws.Cell(9, 6).GetValue<int>());
            Assert.Equal(1, ws.Cell(9, 7).GetValue<int>());
            Assert.Equal(1, ws.Cell(9, 8).GetValue<int>());
            Assert.Equal(25, ws.Cell(9, 9).GetValue<int>());

            Assert.Equal(5, ws.SheetView.SplitRow);
            Assert.Equal(2, ws.SheetView.SplitColumn);
            Assert.Equal(XLPageOrientation.Landscape, ws.PageSetup.PageOrientation);
            Assert.Equal(1, ws.PageSetup.PagesWide);
            Assert.Equal(0, ws.PageSetup.PagesTall);

            Assert.All(ws.CellsUsed(), cell => Assert.False(cell.HasFormula));
        }

        [Fact]
        public void Export_EmptyJournal_WritesSummaryWithoutStudentRows()
        {
            var journal = new AttendanceJournalViewModel
            {
                SubjectId = 1,
                SubjectName = "Matematika",
                GroupId = 1,
                GroupName = "1-guruh",
                Month = new DateTime(2026, 11, 1),
                MonthLabel = "Noyabr 2026",
                Days = BuildDays(),
                Rows = Array.Empty<AttendanceJournalRow>()
            };

            var content = _exporter.Export(journal, GeneratedAt);

            using var workbook = new XLWorkbook(new MemoryStream(content));
            var ws = workbook.Worksheet(1);

            Assert.Equal("Jami: 0 talaba", ws.Cell(7, 1).GetString());
            Assert.Equal(0, ws.Cell(7, 5).GetValue<int>());
            Assert.Equal(0, ws.Cell(7, 9).GetValue<int>());
            Assert.True(string.IsNullOrEmpty(ws.Cell(6, 2).GetString()));
        }

        [Fact]
        public void BuildFileName_SanitizesInvalidCharacters()
        {
            var journal = BuildJournal(subjectName: "Matematika: 1/2", groupName: "1-guruh");

            var fileName = _exporter.BuildFileName(journal);

            Assert.Equal("Davomat_Matematika 1 2_1-guruh_2026-11.xlsx", fileName);
        }

        [Fact]
        public void BuildFileName_UsesFallbacksForEmptyNames()
        {
            var journal = BuildJournal(subjectName: null, groupName: "   ");

            var fileName = _exporter.BuildFileName(journal);

            Assert.Equal("Davomat_Fan_Guruh_2026-11.xlsx", fileName);
        }

        [Fact]
        public void BuildFileName_TruncatesLongNames()
        {
            var journal = BuildJournal(subjectName: new string('A', 80), groupName: "G");

            var fileName = _exporter.BuildFileName(journal);

            Assert.Equal($"Davomat_{new string('A', 50)}_G_2026-11.xlsx", fileName);
        }

        [Fact]
        public void Export_ProtectsAgainstFormulaInjection()
        {
            var journal = new AttendanceJournalViewModel
            {
                SubjectId = 1,
                SubjectName = "Matematika",
                GroupId = 1,
                GroupName = "1-guruh",
                Month = new DateTime(2026, 11, 1),
                MonthLabel = "Noyabr 2026",
                Days = BuildDays(),
                Rows = new[]
                {
                    Row("=1+1", new Dictionary<string, AttendanceStatus?>
                    {
                        ["2026-11-02"] = AttendanceStatus.Keldi
                    }),
                    Row("@SUM(A1:A2)", new Dictionary<string, AttendanceStatus?>
                    {
                        ["2026-11-02"] = AttendanceStatus.Kelmadi
                    })
                }
            };

            var content = _exporter.Export(journal, GeneratedAt);

            using var workbook = new XLWorkbook(new MemoryStream(content));
            var ws = workbook.Worksheet(1);

            Assert.All(ws.CellsUsed(), cell => Assert.False(cell.HasFormula));
            Assert.True(ws.Cell(6, 2).Style.IncludeQuotePrefix);
            Assert.True(ws.Cell(7, 2).Style.IncludeQuotePrefix);
            Assert.Equal("=1+1", ws.Cell(6, 2).GetString());
            Assert.Equal("@SUM(A1:A2)", ws.Cell(7, 2).GetString());
        }

        private static AttendanceJournalViewModel BuildJournal(
            string? subjectName = "Matematika",
            string? groupName = "1-guruh")
        {
            return new AttendanceJournalViewModel
            {
                SubjectId = 1,
                SubjectName = subjectName,
                GroupId = 1,
                GroupName = groupName,
                Month = new DateTime(2026, 11, 1),
                MonthLabel = "Noyabr 2026",
                Days = BuildDays(),
                Rows = new[]
                {
                    Row("Ali Valiyev", new Dictionary<string, AttendanceStatus?>
                    {
                        ["2026-11-02"] = AttendanceStatus.Keldi,
                        ["2026-11-03"] = AttendanceStatus.Kelmadi
                    }),
                    Row("Vali Aliyev", new Dictionary<string, AttendanceStatus?>
                    {
                        ["2026-11-02"] = AttendanceStatus.KechQoldi,
                        ["2026-11-03"] = AttendanceStatus.Sababli
                    })
                }
            };
        }

        private static IReadOnlyList<AttendanceJournalDay> BuildDays()
        {
            return new[]
            {
                new AttendanceJournalDay
                {
                    Date = new DateTime(2026, 11, 2),
                    WeekdayShort = "Du",
                    IsEditable = true
                },
                new AttendanceJournalDay
                {
                    Date = new DateTime(2026, 11, 3),
                    WeekdayShort = "Se",
                    IsEditable = false
                }
            };
        }

        private static AttendanceJournalRow Row(
            string fullName,
            IReadOnlyDictionary<string, AttendanceStatus?> statuses)
        {
            return new AttendanceJournalRow
            {
                StudentId = fullName,
                FullName = fullName,
                Statuses = statuses,
                Keldi = statuses.Values.Count(s => s == AttendanceStatus.Keldi),
                Kelmadi = statuses.Values.Count(s => s == AttendanceStatus.Kelmadi),
                KechQoldi = statuses.Values.Count(s => s == AttendanceStatus.KechQoldi),
                Sababli = statuses.Values.Count(s => s == AttendanceStatus.Sababli)
            };
        }
    }
}
