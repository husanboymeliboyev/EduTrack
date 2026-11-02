using EduTrack.Tests.Infrastructure;
using EduTrack.ViewModels;
using Xunit;

namespace EduTrack.Tests
{
    public class ExamResultsServiceTests : IClassFixture<ExamResultsTestFixture>
    {
        private readonly ExamResultsTestFixture _fixture;

        public ExamResultsServiceTests(ExamResultsTestFixture fixture)
        {
            _fixture = fixture;
        }

        private Task<ExamResultsPageViewModel?> TeacherPageAsync(
            ExamResultsFilterViewModel? filter = null)
        {
            return TeacherPageAsync(_fixture.Seed.Exam1Id, filter);
        }

        private Task<ExamResultsPageViewModel?> TeacherPageAsync(
            int examId,
            ExamResultsFilterViewModel? filter = null)
        {
            return _fixture.Service.GetForTeacherAsync(
                _fixture.Seed.Teacher1Id,
                examId,
                filter ?? new ExamResultsFilterViewModel());
        }

        [Fact]
        public async Task Search_ByFullName_IsCaseInsensitive_ForAscii()
        {
            var page = await TeacherPageAsync(new ExamResultsFilterViewModel { Search = "aliyev" });

            Assert.NotNull(page);
            var row = Assert.Single(page!.Rows);
            Assert.Equal("10001", row.LoginId);
        }

        [Fact]
        public async Task Search_ByLoginId()
        {
            var page = await TeacherPageAsync(new ExamResultsFilterViewModel { Search = "10002" });

            Assert.NotNull(page);
            var row = Assert.Single(page!.Rows);
            Assert.Equal("Bekova Nodira", row.StudentName);
        }

        [Fact]
        public async Task Search_EscapesPercentWildcard()
        {
            var page = await TeacherPageAsync(new ExamResultsFilterViewModel { Search = "%" });

            Assert.NotNull(page);
            var row = Assert.Single(page!.Rows);
            Assert.Equal("10006", row.LoginId);
        }

        [Fact]
        public async Task Search_EscapesUnderscoreWildcard()
        {
            var page = await TeacherPageAsync(new ExamResultsFilterViewModel { Search = "_" });

            Assert.NotNull(page);
            var row = Assert.Single(page!.Rows);
            Assert.Equal("10006", row.LoginId);
        }

        [Fact]
        public async Task Search_MatchesStraightApostrophe()
        {
            var page = await TeacherPageAsync(new ExamResultsFilterViewModel { Search = "O'zbek" });

            Assert.NotNull(page);
            var row = Assert.Single(page!.Rows);
            Assert.Equal("10004", row.LoginId);
        }

        [Fact]
        public async Task Search_MatchesTypographicApostrophe()
        {
            var page = await TeacherPageAsync(new ExamResultsFilterViewModel { Search = "O’zbek" });

            Assert.NotNull(page);
            var row = Assert.Single(page!.Rows);
            Assert.Equal("10005", row.LoginId);
        }

        [Fact]
        public async Task Search_CyrillicLike_IsCaseSensitive()
        {
            // SQLite LIKE faqat ASCII harflar uchun registrga sezgir emas.
            // Kirill harflari uchun qidiruv registrga sezgir bo'lib qoladi.
            var lower = await TeacherPageAsync(new ExamResultsFilterViewModel { Search = "алиев" });
            var upper = await TeacherPageAsync(new ExamResultsFilterViewModel { Search = "Алиев" });

            Assert.NotNull(lower);
            Assert.Empty(lower!.Rows);
            Assert.NotNull(upper);
            var row = Assert.Single(upper!.Rows);
            Assert.Equal("10003", row.LoginId);
        }

        [Fact]
        public async Task Filter_ByGroup_UsesCurrentStudentGroup()
        {
            var page = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                GroupId = _fixture.Seed.Group1Id
            });

            Assert.NotNull(page);
            Assert.Equal(4, page!.TotalCount);
        }

        [Fact]
        public async Task Filter_ByPercentRange_RespectsBoundaries()
        {
            var exact60 = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                PercentFrom = 60,
                PercentTo = 60
            });
            Assert.NotNull(exact60);
            Assert.Equal(2, exact60!.TotalCount);
            Assert.All(exact60.Rows, r => Assert.Equal(60, r.Percent, 3));

            var atLeast599 = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                PercentFrom = 59.9
            });
            Assert.NotNull(atLeast599);
            Assert.Equal(7, atLeast599!.TotalCount);

            var upTo859 = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                PercentTo = 85.9
            });
            Assert.NotNull(upTo859);
            Assert.Equal(7, upTo859!.TotalCount);

            var atLeast86 = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                PercentFrom = 86
            });
            Assert.NotNull(atLeast86);
            var row86 = Assert.Single(atLeast86!.Rows);
            Assert.Equal(86, row86.Percent, 3);
        }

        [Fact]
        public async Task Filter_ByDateRange_IsInclusive()
        {
            var page = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                DateFrom = new DateTime(2026, 1, 15),
                DateTo = new DateTime(2026, 2, 1)
            });

            Assert.NotNull(page);
            Assert.Equal(2, page!.TotalCount);
        }

        [Fact]
        public async Task Filter_ByStatus()
        {
            var approved = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                Status = ExamResultStatusFilter.Approved
            });
            Assert.NotNull(approved);
            Assert.Equal(4, approved!.TotalCount);

            var pending = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                Status = ExamResultStatusFilter.Pending
            });
            Assert.NotNull(pending);
            Assert.Equal(4, pending!.TotalCount);
        }

        [Fact]
        public async Task Filter_ByReliability_IncludesAttemptViolations()
        {
            var suspicious = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                Reliability = ExamResultReliabilityFilter.Suspicious
            });
            Assert.NotNull(suspicious);
            Assert.Equal(3, suspicious!.TotalCount);
            Assert.Contains(suspicious.Rows, r => r.LoginId == "10002");
            Assert.Contains(suspicious.Rows, r => r.LoginId == "10003");
            Assert.Contains(suspicious.Rows, r => r.LoginId == "10004");

            var clean = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                Reliability = ExamResultReliabilityFilter.Clean
            });
            Assert.NotNull(clean);
            Assert.Equal(5, clean!.TotalCount);
        }

        [Fact]
        public async Task Filter_Combined_GroupAndStatus()
        {
            var page = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                GroupId = _fixture.Seed.Group1Id,
                Status = ExamResultStatusFilter.Approved
            });

            Assert.NotNull(page);
            Assert.Equal(2, page!.TotalCount);
        }

        [Fact]
        public async Task Sort_ByName_IsCaseInsensitive()
        {
            var page = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                Sort = ExamResultsSort.NameAsc
            });

            Assert.NotNull(page);
            var names = page!.Rows.Select(r => r.StudentName).ToList();
            Assert.True(
                names.IndexOf("abdulaziz Karimov") < names.IndexOf("Aliyev Vali"),
                "NOCASE bo'yicha kichik harfli ism katta harfli ismdan oldin turishi kerak.");
            Assert.True(
                names.IndexOf("abdulaziz Karimov") < names.IndexOf("Bekova Nodira"));
        }

        [Fact]
        public async Task Sort_ByPercent()
        {
            var descending = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                Sort = ExamResultsSort.PercentDesc
            });
            Assert.NotNull(descending);
            Assert.Equal(86, descending!.Rows.First().Percent, 3);
            Assert.Equal(0, descending.Rows.Last().Percent, 3);

            var ascending = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                Sort = ExamResultsSort.PercentAsc
            });
            Assert.NotNull(ascending);
            Assert.Equal(0, ascending!.Rows.First().Percent, 3);
            Assert.Equal(86, ascending.Rows.Last().Percent, 3);
        }

        [Fact]
        public async Task Sort_ByDate_DefaultIsNewestFirst()
        {
            var page = await TeacherPageAsync();

            Assert.NotNull(page);
            Assert.Equal("10008", page!.Rows.First().LoginId);
            Assert.Equal("10001", page.Rows.Last().LoginId);
        }

        [Fact]
        public async Task Pagination_SplitsRowsAndClampsOutOfRangePage()
        {
            var first = await TeacherPageAsync(_fixture.Seed.Exam3Id, new ExamResultsFilterViewModel
            {
                Page = 1
            });
            Assert.NotNull(first);
            Assert.Equal(25, first!.TotalCount);
            Assert.Equal(20, first.Rows.Count);
            Assert.Equal(2, first.TotalPages);

            var second = await TeacherPageAsync(_fixture.Seed.Exam3Id, new ExamResultsFilterViewModel
            {
                Page = 2
            });
            Assert.NotNull(second);
            Assert.Equal(5, second!.Rows.Count);

            var tooFar = await TeacherPageAsync(_fixture.Seed.Exam3Id, new ExamResultsFilterViewModel
            {
                Page = 99
            });
            Assert.NotNull(tooFar);
            Assert.Equal(2, tooFar!.Page);
            Assert.Equal(5, tooFar.Rows.Count);
        }

        [Fact]
        public async Task Pagination_InvalidPageSizeFallsBackToTwenty()
        {
            var page = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                PageSize = 13
            });

            Assert.NotNull(page);
            Assert.Equal(20, page!.PageSize);
        }

        [Fact]
        public async Task Pagination_PageZeroClampsToOne()
        {
            var page = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                Page = 0
            });

            Assert.NotNull(page);
            Assert.Equal(1, page!.Page);
        }

        [Fact]
        public async Task Filter_InvalidPercent_IsIgnored()
        {
            var page = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                PercentFrom = 101
            });

            Assert.NotNull(page);
            Assert.Equal(8, page!.TotalCount);
            Assert.Null(page.Filter.PercentFrom);
        }

        [Fact]
        public async Task Filter_PercentRangeSwapped_IsNormalizedAndReported()
        {
            var page = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                PercentFrom = 80,
                PercentTo = 20
            });

            Assert.NotNull(page);
            Assert.True(page!.PercentRangeSwapped);
            Assert.Equal(20, page.Filter.PercentFrom);
            Assert.Equal(80, page.Filter.PercentTo);
            Assert.Equal(5, page.TotalCount);
        }

        [Fact]
        public async Task EmptyExam_ReturnsEmptyPageAndZeroSummary()
        {
            var page = await TeacherPageAsync(_fixture.Seed.Exam4Id);

            Assert.NotNull(page);
            Assert.Equal(0, page!.TotalCount);
            Assert.Empty(page.Rows);
            Assert.Equal(0, page.TotalPages);
            Assert.Equal(0, page.AveragePercent, 3);
            Assert.Equal(0, page.HighestPercent, 3);
            Assert.Equal(0, page.LowestPercent, 3);
        }

        [Fact]
        public async Task ZeroTotalQuestions_ProducesZeroPercent()
        {
            var page = await TeacherPageAsync(new ExamResultsFilterViewModel { Search = "Zero" });

            Assert.NotNull(page);
            var row = Assert.Single(page!.Rows);
            Assert.Equal(0, row.Percent, 3);
        }

        [Fact]
        public async Task Teacher_CannotAccessAnotherTeachersExam()
        {
            var page = await _fixture.Service.GetForTeacherAsync(
                _fixture.Seed.Teacher2Id,
                _fixture.Seed.Exam1Id,
                new ExamResultsFilterViewModel());

            Assert.Null(page);
        }

        [Fact]
        public async Task Admin_CanAccessAnyExam_ButCannotApprove()
        {
            var adminPage = await _fixture.Service.GetForAdminAsync(
                _fixture.Seed.Exam1Id,
                new ExamResultsFilterViewModel());
            var teacherPage = await TeacherPageAsync();

            Assert.NotNull(adminPage);
            Assert.False(adminPage!.CanApprove);
            Assert.NotNull(teacherPage);
            Assert.True(teacherPage!.CanApprove);
        }

        [Fact]
        public async Task Summary_ComputesAverageHighestAndLowest()
        {
            var page = await TeacherPageAsync();

            Assert.NotNull(page);
            Assert.Equal(62.1, page!.AveragePercent, 3);
            Assert.Equal(86, page.HighestPercent, 3);
            Assert.Equal(0, page.LowestPercent, 3);
        }

        [Fact]
        public async Task Groups_ContainOnlyGroupsPresentInResults()
        {
            var page = await TeacherPageAsync();

            Assert.NotNull(page);
            Assert.Equal(2, page!.Groups.Count);
            Assert.Contains(page.Groups, g => g.Id == _fixture.Seed.Group1Id);
            Assert.Contains(page.Groups, g => g.Id == _fixture.Seed.Group2Id);
        }

        [Fact]
        public async Task Filter_SearchAndReliability_Combined()
        {
            var page = await TeacherPageAsync(new ExamResultsFilterViewModel
            {
                Search = "O'zbek",
                Reliability = ExamResultReliabilityFilter.Suspicious
            });

            Assert.NotNull(page);
            var row = Assert.Single(page!.Rows);
            Assert.Equal("10004", row.LoginId);
        }
    }
}
