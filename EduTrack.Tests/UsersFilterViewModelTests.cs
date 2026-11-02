using EduTrack.ViewModels;
using Xunit;

namespace EduTrack.Tests
{
    public class UsersFilterViewModelTests
    {
        [Fact]
        public void ToRouteValues_IncludesActiveFiltersAndState()
        {
            var filter = new UsersFilterViewModel
            {
                Search = "Ali",
                Role = "Student",
                GroupId = 3,
                Sort = UsersSort.NameDesc,
                Page = 2,
                PageSize = 50
            };

            var values = filter.ToRouteValues();

            Assert.Equal("Ali", values["Search"]);
            Assert.Equal("Student", values["Role"]);
            Assert.Equal("3", values["GroupId"]);
            Assert.Equal("NameDesc", values["Sort"]);
            Assert.Equal("50", values["PageSize"]);
            Assert.Equal("2", values["Page"]);
        }

        [Fact]
        public void ToRouteValues_OmitsEmptyFiltersAndFirstPage()
        {
            var values = new UsersFilterViewModel().ToRouteValues();

            Assert.False(values.ContainsKey("Search"));
            Assert.False(values.ContainsKey("Role"));
            Assert.False(values.ContainsKey("GroupId"));
            Assert.False(values.ContainsKey("Page"));
            Assert.Equal("NameAsc", values["Sort"]);
            Assert.Equal("20", values["PageSize"]);
        }

        [Fact]
        public void ToRouteValues_OverridesPage()
        {
            var filter = new UsersFilterViewModel { Page = 3 };

            Assert.Equal("5", filter.ToRouteValues(5)["Page"]);
            Assert.False(filter.ToRouteValues(1).ContainsKey("Page"));
        }

        [Fact]
        public void HasActiveFilters_IgnoresSortAndPaging()
        {
            var filter = new UsersFilterViewModel
            {
                Sort = UsersSort.LoginDesc,
                Page = 3,
                PageSize = 50
            };

            Assert.False(filter.HasActiveFilters);

            filter.Search = "Ali";

            Assert.True(filter.HasActiveFilters);
        }

        [Fact]
        public void HasActiveFilters_DetectsRoleAndGroup()
        {
            Assert.True(new UsersFilterViewModel { Role = "Student" }.HasActiveFilters);
            Assert.True(new UsersFilterViewModel { GroupId = 1 }.HasActiveFilters);
            Assert.False(new UsersFilterViewModel { Search = "   " }.HasActiveFilters);
        }

        [Fact]
        public void Normalize_TrimsAndResetsInvalidValues()
        {
            var filter = new UsersFilterViewModel
            {
                Search = "  Ali  ",
                Role = "  Student  ",
                Sort = (UsersSort)99,
                Page = 0,
                PageSize = 7
            };

            filter.Normalize();

            Assert.Equal("Ali", filter.Search);
            Assert.Equal("Student", filter.Role);
            Assert.Equal(UsersSort.NameAsc, filter.Sort);
            Assert.Equal(1, filter.Page);
            Assert.Equal(UsersFilterViewModel.DefaultPageSize, filter.PageSize);
        }

        [Fact]
        public void Normalize_TurnsWhitespaceIntoNull()
        {
            var filter = new UsersFilterViewModel
            {
                Search = "   ",
                Role = "   "
            };

            filter.Normalize();

            Assert.Null(filter.Search);
            Assert.Null(filter.Role);
            Assert.False(filter.HasActiveFilters);
        }

        [Fact]
        public void Normalize_KeepsValidValues()
        {
            var filter = new UsersFilterViewModel
            {
                Search = "Ali",
                Role = "Teacher",
                GroupId = 2,
                Sort = UsersSort.LoginAsc,
                Page = 4,
                PageSize = 100
            };

            filter.Normalize();

            Assert.Equal("Ali", filter.Search);
            Assert.Equal("Teacher", filter.Role);
            Assert.Equal(2, filter.GroupId);
            Assert.Equal(UsersSort.LoginAsc, filter.Sort);
            Assert.Equal(4, filter.Page);
            Assert.Equal(100, filter.PageSize);
        }
    }
}
