using EduTrack.Tests.Infrastructure;
using EduTrack.ViewModels;
using Xunit;

namespace EduTrack.Tests
{
    public class AdminExamDirectoryServiceTests : IClassFixture<AdminExamDirectoryTestFixture>
    {
        private readonly AdminExamDirectoryTestFixture _fixture;

        public AdminExamDirectoryServiceTests(AdminExamDirectoryTestFixture fixture)
        {
            _fixture = fixture;
        }

        private Task<AdminExamListPageViewModel> PageAsync(
            AdminExamListFilterViewModel filter,
            bool approvedOnly = false)
            => _fixture.Service.GetExamsAsync(filter, approvedOnly);

        [Fact]
        public async Task Admin_EmptyFilter_ReturnsAllExams()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel());

            Assert.Equal(29, page.TotalCount);
            Assert.Equal(20, page.Rows.Count);
            Assert.Equal(2, page.TotalPages);
        }

        [Fact]
        public async Task Admin_Search_ByTitle()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                Search = "Matematika 1",
                PageSize = 100
            });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal("Matematika 1", Assert.Single(page.Rows).Title);
        }

        [Fact]
        public async Task Admin_Search_BySubjectName()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                Search = "Matematika",
                PageSize = 100
            });

            // Fan nomi bo'yicha qidiruv shu fandagi barcha imtihonlarni qaytaradi.
            Assert.Equal(27, page.TotalCount);
            Assert.All(page.Rows, row => Assert.Equal("Matematika", row.SubjectName));
        }

        [Fact]
        public async Task Admin_Search_ByTeacherName()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                Search = "Teacher Two",
                PageSize = 100
            });

            Assert.Equal(2, page.TotalCount);
            Assert.All(page.Rows, row => Assert.Equal("Teacher Two", row.TeacherName));
        }

        // % oddiy belgi sifatida qidirilishi kerak — aks holda barcha
        // imtihonlar qaytadi.
        [Fact]
        public async Task Admin_Search_EscapesPercentWildcard()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                Search = "%",
                PageSize = 100
            });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal("100% Test_Exam", Assert.Single(page.Rows).Title);
        }

        // _ ham oddiy belgi sifatida qidirilishi kerak.
        [Fact]
        public async Task Admin_Search_EscapesUnderscoreWildcard()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                Search = "_",
                PageSize = 100
            });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal("100% Test_Exam", Assert.Single(page.Rows).Title);
        }

        [Fact]
        public async Task Admin_Filter_BySubject()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                SubjectId = _fixture.Seed.Subject1Id,
                PageSize = 100
            });

            Assert.Equal(27, page.TotalCount);
            Assert.All(page.Rows, row => Assert.Equal("Matematika", row.SubjectName));
        }

        [Fact]
        public async Task Admin_Filter_ByTeacher()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                TeacherId = _fixture.Seed.Teacher2Id,
                PageSize = 100
            });

            Assert.Equal(2, page.TotalCount);
            Assert.All(page.Rows, row => Assert.Equal("Teacher Two", row.TeacherName));
        }

        [Fact]
        public async Task Admin_Filter_ByGroup()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                GroupId = _fixture.Seed.GroupBId,
                PageSize = 100
            });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal("100% Test_Exam", Assert.Single(page.Rows).Title);
        }

        [Fact]
        public async Task Admin_Filter_ByStatus_Active()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                Status = AdminExamListStatus.Active,
                PageSize = 100
            });

            Assert.Equal(28, page.TotalCount);
            Assert.All(page.Rows, row => Assert.False(row.IsArchived));
        }

        [Fact]
        public async Task Admin_Filter_ByStatus_Archived()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                Status = AdminExamListStatus.Archived,
                PageSize = 100
            });

            Assert.Equal(1, page.TotalCount);
            Assert.True(Assert.Single(page.Rows).IsArchived);
        }

        [Fact]
        public async Task Admin_Filter_Combined_SearchAndStatus()
        {
            var active = await PageAsync(new AdminExamListFilterViewModel
            {
                Search = "Matematika 1",
                Status = AdminExamListStatus.Active,
                PageSize = 100
            });
            var archived = await PageAsync(new AdminExamListFilterViewModel
            {
                Search = "Matematika 1",
                Status = AdminExamListStatus.Archived,
                PageSize = 100
            });

            Assert.Equal(1, active.TotalCount);
            Assert.Equal("Matematika 1", Assert.Single(active.Rows).Title);
            Assert.Equal(0, archived.TotalCount);
        }

        [Fact]
        public async Task Admin_Sort_ByDateDesc_IsDefault()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                PageSize = 100
            });

            Assert.Equal("Exam 25", page.Rows[0].Title);
        }

        [Fact]
        public async Task Admin_Sort_ByDateAsc()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                Sort = AdminExamListSort.DateAsc,
                PageSize = 100
            });

            Assert.Equal("Matematika 1", page.Rows[0].Title);
        }

        [Fact]
        public async Task Admin_Sort_ByTitleAsc_IsCaseInsensitive()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                Sort = AdminExamListSort.TitleAsc,
                PageSize = 100
            });

            var titles = page.Rows.Select(r => r.Title).ToList();

            Assert.Equal("100% Test_Exam", titles[0]);
            Assert.True(titles.IndexOf("Exam 01") < titles.IndexOf("Fizika umumiy"));
            Assert.True(titles.IndexOf("Fizika umumiy") < titles.IndexOf("Matematika 1"));
        }

        [Fact]
        public async Task Admin_Sort_ByTitleDesc()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                Sort = AdminExamListSort.TitleDesc,
                PageSize = 100
            });

            Assert.Equal("Matematika arxiv", page.Rows[0].Title);
        }

        [Fact]
        public async Task Admin_Sort_ByResultsDesc()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                Sort = AdminExamListSort.ResultsDesc,
                PageSize = 100
            });

            Assert.Equal("Matematika 1", page.Rows[0].Title);
            Assert.Equal(2, page.Rows[0].ResultsCount);
            Assert.Equal("Matematika arxiv", page.Rows[1].Title);
            Assert.Equal(1, page.Rows[1].ResultsCount);
        }

        [Fact]
        public async Task Admin_Pagination_SplitsRowsAndKeepsTotal()
        {
            var first = await PageAsync(new AdminExamListFilterViewModel
            {
                Page = 1,
                PageSize = 20
            });
            var second = await PageAsync(new AdminExamListFilterViewModel
            {
                Page = 2,
                PageSize = 20
            });

            Assert.Equal(29, first.TotalCount);
            Assert.Equal(2, first.TotalPages);
            Assert.Equal(20, first.Rows.Count);
            Assert.Equal(9, second.Rows.Count);

            var firstIds = first.Rows.Select(r => r.Id).ToHashSet();
            var secondIds = second.Rows.Select(r => r.Id).ToHashSet();

            Assert.Empty(firstIds.Intersect(secondIds));
        }

        [Fact]
        public async Task Admin_PageBeyondRange_ClampsToLastPage()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                Page = 99,
                PageSize = 20
            });

            Assert.Equal(2, page.Page);
            Assert.Equal(9, page.Rows.Count);
        }

        [Fact]
        public async Task Admin_InvalidPageSize_FallsBackToDefault()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel
            {
                PageSize = 7
            });

            Assert.Equal(AdminExamListFilterViewModel.DefaultPageSize, page.PageSize);
        }

        [Fact]
        public async Task Admin_ProvidesFilterOptions()
        {
            var page = await PageAsync(new AdminExamListFilterViewModel());

            Assert.Equal(new[] { "Fizika", "Matematika" }, page.Subjects.Select(s => s.Name));
            Assert.Equal(new[] { "Teacher One", "Teacher Two" }, page.Teachers.Select(t => t.Name));
            Assert.Equal(new[] { "Guruh A", "Guruh B" }, page.Groups.Select(g => g.Name));
        }

        // Moderator faqat kamida bitta tasdiqlangan natijasi bor imtihonlarni
        // ko'radi. Seed'da faqat "Matematika 1" va "Matematika arxiv"da
        // tasdiqlangan natijalar bor.
        [Fact]
        public async Task Moderator_ApprovedOnly_HidesExamsWithoutApprovedResults()
        {
            var page = await PageAsync(
                new AdminExamListFilterViewModel { PageSize = 100 },
                approvedOnly: true);

            Assert.Equal(2, page.TotalCount);
            Assert.Equal(
                new[] { "Matematika arxiv", "Matematika 1" },
                page.Rows.Select(r => r.Title));
        }

        // Tasdiqlangan rejimda "Natijalar" ustuni ham faqat tasdiqlangan
        // natijalar sonini ko'rsatadi.
        [Fact]
        public async Task Moderator_ApprovedOnly_CountsApprovedResults()
        {
            var page = await PageAsync(
                new AdminExamListFilterViewModel { PageSize = 100 },
                approvedOnly: true);

            var active = page.Rows.Single(r => r.Title == "Matematika 1");
            var archived = page.Rows.Single(r => r.Title == "Matematika arxiv");

            Assert.Equal(2, active.ResultsCount);
            Assert.Equal(1, archived.ResultsCount);
        }
    }
}
