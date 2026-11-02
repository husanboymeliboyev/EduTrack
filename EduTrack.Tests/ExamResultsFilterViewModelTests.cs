using EduTrack.ViewModels;
using Xunit;

namespace EduTrack.Tests
{
    public class ExamResultsFilterViewModelTests
    {
        [Fact]
        public void ToRouteValues_IncludesActiveFiltersAndState()
        {
            var filter = new ExamResultsFilterViewModel
            {
                Search = "Ali",
                GroupId = 3,
                PercentFrom = 59.9,
                PercentTo = 86,
                DateFrom = new DateTime(2026, 1, 1),
                DateTo = new DateTime(2026, 2, 1),
                Status = ExamResultStatusFilter.Approved,
                Reliability = ExamResultReliabilityFilter.Suspicious,
                Sort = ExamResultsSort.NameAsc,
                Page = 2,
                PageSize = 50
            };

            var values = filter.ToRouteValues();

            Assert.Equal("Ali", values["Search"]);
            Assert.Equal("3", values["GroupId"]);
            Assert.Equal("59.9", values["PercentFrom"]);
            Assert.Equal("86", values["PercentTo"]);
            Assert.Equal("2026-01-01", values["DateFrom"]);
            Assert.Equal("2026-02-01", values["DateTo"]);
            Assert.Equal("Approved", values["Status"]);
            Assert.Equal("Suspicious", values["Reliability"]);
            Assert.Equal("NameAsc", values["Sort"]);
            Assert.Equal("50", values["PageSize"]);
            Assert.Equal("2", values["Page"]);
        }

        [Fact]
        public void ToRouteValues_OmitsEmptyFiltersAndFirstPage()
        {
            var values = new ExamResultsFilterViewModel().ToRouteValues();

            Assert.False(values.ContainsKey("Search"));
            Assert.False(values.ContainsKey("GroupId"));
            Assert.False(values.ContainsKey("PercentFrom"));
            Assert.False(values.ContainsKey("PercentTo"));
            Assert.False(values.ContainsKey("DateFrom"));
            Assert.False(values.ContainsKey("DateTo"));
            Assert.False(values.ContainsKey("Status"));
            Assert.False(values.ContainsKey("Reliability"));
            Assert.False(values.ContainsKey("Page"));
            Assert.Equal("DateDesc", values["Sort"]);
            Assert.Equal("20", values["PageSize"]);
        }

        [Fact]
        public void ToRouteValues_OverridesPage()
        {
            var filter = new ExamResultsFilterViewModel { Page = 3 };

            Assert.Equal("5", filter.ToRouteValues(5)["Page"]);
            Assert.False(filter.ToRouteValues(1).ContainsKey("Page"));
        }

        [Fact]
        public void HasActiveFilters_IgnoresSortAndPaging()
        {
            var filter = new ExamResultsFilterViewModel
            {
                Sort = ExamResultsSort.NameAsc,
                Page = 3,
                PageSize = 50
            };

            Assert.False(filter.HasActiveFilters);

            filter.Search = "Ali";

            Assert.True(filter.HasActiveFilters);
        }

        // Sahifa modelidagi havolalar imtihon id'sini o'z ichiga olishi shart,
        // aks holda Excel va sahifalash havolalari 404 qaytaradi.
        [Fact]
        public void PageToRouteValues_IncludesExamIdAndFilterState()
        {
            var model = new ExamResultsPageViewModel
            {
                ExamId = 7,
                Filter = new ExamResultsFilterViewModel
                {
                    Search = "Ali",
                    Sort = ExamResultsSort.PercentDesc,
                    PageSize = 100,
                    Page = 2
                }
            };

            var values = model.ToRouteValues();

            Assert.Equal("7", values["id"]);
            Assert.Equal("Ali", values["Search"]);
            Assert.Equal("PercentDesc", values["Sort"]);
            Assert.Equal("100", values["PageSize"]);
            Assert.Equal("2", values["Page"]);

            var firstPage = model.ToRouteValues(1);

            Assert.Equal("7", firstPage["id"]);
            Assert.False(firstPage.ContainsKey("Page"));
        }

        // Controller'lar (teacher va admin) redirect/Excel uchun shu metodni ishlatadi.
        [Fact]
        public void ToRouteValuesWithExamId_IncludesIdAndFilterState()
        {
            var filter = new ExamResultsFilterViewModel
            {
                Search = "Ali",
                Sort = ExamResultsSort.NameAsc,
                PageSize = 50,
                Page = 2
            };

            var values = filter.ToRouteValuesWithExamId(9);

            Assert.Equal("9", values["id"]);
            Assert.Equal("Ali", values["Search"]);
            Assert.Equal("NameAsc", values["Sort"]);
            Assert.Equal("50", values["PageSize"]);
            Assert.Equal("2", values["Page"]);
            Assert.False(filter.ToRouteValuesWithExamId(9, 1).ContainsKey("Page"));
        }
    }
}
