using EduTrack.ViewModels;
using Xunit;

namespace EduTrack.Tests
{
    public class AdminExamListFilterViewModelTests
    {
        [Fact]
        public void ToRouteValues_IncludesActiveFiltersAndState()
        {
            var filter = new AdminExamListFilterViewModel
            {
                Search = "Matematika",
                SubjectId = 2,
                TeacherId = "teacher-1",
                GroupId = 3,
                Status = AdminExamListStatus.Archived,
                Sort = AdminExamListSort.TitleAsc,
                Page = 2,
                PageSize = 50
            };

            var values = filter.ToRouteValues();

            Assert.Equal("Matematika", values["Search"]);
            Assert.Equal("2", values["SubjectId"]);
            Assert.Equal("teacher-1", values["TeacherId"]);
            Assert.Equal("3", values["GroupId"]);
            Assert.Equal("Archived", values["Status"]);
            Assert.Equal("TitleAsc", values["Sort"]);
            Assert.Equal("50", values["PageSize"]);
            Assert.Equal("2", values["Page"]);
        }

        [Fact]
        public void ToRouteValues_OmitsEmptyFiltersAndFirstPage()
        {
            var values = new AdminExamListFilterViewModel().ToRouteValues();

            Assert.False(values.ContainsKey("Search"));
            Assert.False(values.ContainsKey("SubjectId"));
            Assert.False(values.ContainsKey("TeacherId"));
            Assert.False(values.ContainsKey("GroupId"));
            Assert.False(values.ContainsKey("Status"));
            Assert.False(values.ContainsKey("Page"));
            Assert.Equal("DateDesc", values["Sort"]);
            Assert.Equal("20", values["PageSize"]);
        }

        [Fact]
        public void ToRouteValues_OverridesPage()
        {
            var filter = new AdminExamListFilterViewModel { Page = 3 };

            Assert.Equal("5", filter.ToRouteValues(5)["Page"]);
            Assert.False(filter.ToRouteValues(1).ContainsKey("Page"));
        }

        [Fact]
        public void HasActiveFilters_IgnoresSortAndPaging()
        {
            var filter = new AdminExamListFilterViewModel
            {
                Sort = AdminExamListSort.ResultsDesc,
                Page = 3,
                PageSize = 50
            };

            Assert.False(filter.HasActiveFilters);

            filter.Status = AdminExamListStatus.Active;

            Assert.True(filter.HasActiveFilters);
        }

        [Fact]
        public void Normalize_TrimsAndResetsInvalidValues()
        {
            var filter = new AdminExamListFilterViewModel
            {
                Search = "  Matematika  ",
                TeacherId = "  teacher-1  ",
                Sort = (AdminExamListSort)99,
                Status = (AdminExamListStatus)99,
                Page = 0,
                PageSize = 7
            };

            filter.Normalize();

            Assert.Equal("Matematika", filter.Search);
            Assert.Equal("teacher-1", filter.TeacherId);
            Assert.Equal(AdminExamListSort.DateDesc, filter.Sort);
            Assert.Null(filter.Status);
            Assert.Equal(1, filter.Page);
            Assert.Equal(AdminExamListFilterViewModel.DefaultPageSize, filter.PageSize);
        }

        [Fact]
        public void Normalize_TurnsWhitespaceIntoNull()
        {
            var filter = new AdminExamListFilterViewModel
            {
                Search = "   ",
                TeacherId = "   "
            };

            filter.Normalize();

            Assert.Null(filter.Search);
            Assert.Null(filter.TeacherId);
            Assert.False(filter.HasActiveFilters);
        }

        [Fact]
        public void Normalize_KeepsValidValues()
        {
            var filter = new AdminExamListFilterViewModel
            {
                Search = "Fizika",
                SubjectId = 2,
                TeacherId = "teacher-2",
                GroupId = 3,
                Status = AdminExamListStatus.Active,
                Sort = AdminExamListSort.ResultsDesc,
                Page = 4,
                PageSize = 100
            };

            filter.Normalize();

            Assert.Equal("Fizika", filter.Search);
            Assert.Equal(2, filter.SubjectId);
            Assert.Equal("teacher-2", filter.TeacherId);
            Assert.Equal(3, filter.GroupId);
            Assert.Equal(AdminExamListStatus.Active, filter.Status);
            Assert.Equal(AdminExamListSort.ResultsDesc, filter.Sort);
            Assert.Equal(4, filter.Page);
            Assert.Equal(100, filter.PageSize);
        }
    }
}
