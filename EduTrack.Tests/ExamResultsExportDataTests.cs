using EduTrack.Tests.Infrastructure;
using EduTrack.ViewModels;
using Xunit;

namespace EduTrack.Tests
{
    public class ExamResultsExportDataTests : IClassFixture<ExamResultsTestFixture>
    {
        private readonly ExamResultsTestFixture _fixture;

        public ExamResultsExportDataTests(ExamResultsTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ExportData_ContainsAllFilteredRows_NotJustCurrentPage()
        {
            var data = await _fixture.Service.GetExportForTeacherAsync(
                _fixture.Seed.Teacher1Id,
                _fixture.Seed.Exam3Id,
                new ExamResultsFilterViewModel());

            Assert.NotNull(data);
            Assert.Equal(25, data!.TotalCount);
            Assert.Equal(25, data.Rows.Count);
            Assert.False(data.ExceedsRowLimit);
        }

        [Fact]
        public async Task ExportData_AppliesFiltersAndGroupName()
        {
            var data = await _fixture.Service.GetExportForTeacherAsync(
                _fixture.Seed.Teacher1Id,
                _fixture.Seed.Exam1Id,
                new ExamResultsFilterViewModel
                {
                    GroupId = _fixture.Seed.Group1Id,
                    Status = ExamResultStatusFilter.Approved
                });

            Assert.NotNull(data);
            Assert.Equal(2, data!.TotalCount);
            Assert.Equal(2, data.Rows.Count);
            Assert.Equal("Guruh A", data.FilterGroupName);
        }

        [Fact]
        public async Task ExportData_TeacherCannotExportAnotherTeachersExam()
        {
            var data = await _fixture.Service.GetExportForTeacherAsync(
                _fixture.Seed.Teacher2Id,
                _fixture.Seed.Exam1Id,
                new ExamResultsFilterViewModel());

            Assert.Null(data);
        }

        [Fact]
        public async Task ExportData_AdminCanExportAnyExam()
        {
            var data = await _fixture.Service.GetExportForAdminAsync(
                _fixture.Seed.Exam1Id,
                new ExamResultsFilterViewModel());

            Assert.NotNull(data);
            Assert.Equal(8, data!.TotalCount);
            Assert.Equal(8, data.Rows.Count);
        }

        // Moderator eksporti ham faqat tasdiqlangan natijalarni o'z ichiga oladi.
        [Fact]
        public async Task ExportData_ModeratorApprovedOnly_ContainsOnlyApproved()
        {
            var data = await _fixture.Service.GetExportForAdminAsync(
                _fixture.Seed.Exam1Id,
                new ExamResultsFilterViewModel(),
                approvedOnly: true);

            Assert.NotNull(data);
            Assert.Equal(4, data!.TotalCount);
            Assert.Equal(4, data.Rows.Count);
            Assert.All(data.Rows, row => Assert.True(row.IsApproved));
        }

        [Fact]
        public async Task ExportData_UsesAppClockTime()
        {
            var before = _fixture.Clock.Now.AddMinutes(-1);
            var data = await _fixture.Service.GetExportForAdminAsync(
                _fixture.Seed.Exam1Id,
                new ExamResultsFilterViewModel());
            var after = _fixture.Clock.Now.AddMinutes(1);

            Assert.NotNull(data);
            Assert.InRange(data!.GeneratedAt, before, after);
        }

        [Fact]
        public void ExportData_RowLimitFlagUsesFiveThousand()
        {
            var below = new ExamResultsExportData { TotalCount = 5000 };
            var above = new ExamResultsExportData { TotalCount = 5001 };

            Assert.False(below.ExceedsRowLimit);
            Assert.True(above.ExceedsRowLimit);
        }
    }
}
