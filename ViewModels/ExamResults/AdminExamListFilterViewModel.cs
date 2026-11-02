using System.Globalization;

namespace EduTrack.ViewModels
{
    public enum AdminExamListSort
    {
        DateDesc = 0,
        DateAsc = 1,
        TitleAsc = 2,
        TitleDesc = 3,
        ResultsDesc = 4
    }

    public enum AdminExamListStatus
    {
        Active = 1,
        Archived = 2
    }

    // Admin imtihonlar ro'yxatidagi filtr holati. GET query string'dan
    // to'ldiriladi; Normalize() mijozdan kelgan qiymatlarni xavfsiz
    // diapazonga keltiradi.
    public class AdminExamListFilterViewModel
    {
        public const int DefaultPageSize = 20;
        public static readonly int[] AllowedPageSizes = { 20, 50, 100 };

        public string? Search { get; set; }
        public int? SubjectId { get; set; }
        public string? TeacherId { get; set; }
        public int? GroupId { get; set; }
        public AdminExamListStatus? Status { get; set; }
        public AdminExamListSort Sort { get; set; } = AdminExamListSort.DateDesc;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = DefaultPageSize;

        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(Search)
            || SubjectId.HasValue
            || !string.IsNullOrWhiteSpace(TeacherId)
            || GroupId.HasValue
            || Status.HasValue;

        public void Normalize()
        {
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
            TeacherId = string.IsNullOrWhiteSpace(TeacherId) ? null : TeacherId.Trim();

            if (Page < 1) Page = 1;
            if (!AllowedPageSizes.Contains(PageSize)) PageSize = DefaultPageSize;

            if (!Enum.IsDefined(typeof(AdminExamListSort), Sort))
            {
                Sort = AdminExamListSort.DateDesc;
            }

            if (Status.HasValue
                && !Enum.IsDefined(typeof(AdminExamListStatus), Status.Value))
            {
                Status = null;
            }
        }

        // Sahifa havolalari uchun GET route qiymatlari. Bo'sh filtrlar
        // tashlab yuboriladi; Page faqat 1 dan katta bo'lsa qo'shiladi.
        public Dictionary<string, string> ToRouteValues(int? page = null)
        {
            var values = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(Search))
            {
                values["Search"] = Search;
            }

            if (SubjectId.HasValue)
            {
                values["SubjectId"] = SubjectId.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (!string.IsNullOrWhiteSpace(TeacherId))
            {
                values["TeacherId"] = TeacherId;
            }

            if (GroupId.HasValue)
            {
                values["GroupId"] = GroupId.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (Status.HasValue)
            {
                values["Status"] = Status.Value.ToString();
            }

            values["Sort"] = Sort.ToString();
            values["PageSize"] = PageSize.ToString(CultureInfo.InvariantCulture);

            var pageValue = page ?? Page;
            if (pageValue > 1)
            {
                values["Page"] = pageValue.ToString(CultureInfo.InvariantCulture);
            }

            return values;
        }
    }
}
