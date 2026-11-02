using System.Globalization;

namespace EduTrack.ViewModels
{
    public enum ExamResultsSort
    {
        DateDesc = 0,
        DateAsc = 1,
        NameAsc = 2,
        NameDesc = 3,
        PercentAsc = 4,
        PercentDesc = 5
    }

    public enum ExamResultStatusFilter
    {
        Approved = 1,
        Pending = 2
    }

    public enum ExamResultReliabilityFilter
    {
        Clean = 1,
        Suspicious = 2
    }

    // Imtihon natijalari sahifasidagi filtr holati. GET query string'dan to'ldiriladi.
    // Normalize() mijozdan kelgan qiymatlarni xavfsiz diapazonga keltiradi.
    public class ExamResultsFilterViewModel
    {
        public const int DefaultPageSize = 20;
        public static readonly int[] AllowedPageSizes = { 20, 50, 100 };

        public string? Search { get; set; }
        public int? GroupId { get; set; }
        public double? PercentFrom { get; set; }
        public double? PercentTo { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public ExamResultStatusFilter? Status { get; set; }
        public ExamResultReliabilityFilter? Reliability { get; set; }
        public ExamResultsSort Sort { get; set; } = ExamResultsSort.DateDesc;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = DefaultPageSize;

        // percentFrom > percentTo bo'lsa qiymatlar almashtiriladi va sahifada
        // foydalanuvchiga kichik xabar ko'rsatiladi.
        public bool PercentRangeSwapped { get; private set; }

        // Qidiruv/foiz/sana/status/ishonchlilik filtrlaridan birortasi faolmi.
        // Sort va sahifalash filtr hisoblanmaydi.
        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(Search)
            || GroupId.HasValue
            || PercentFrom.HasValue
            || PercentTo.HasValue
            || DateFrom.HasValue
            || DateTo.HasValue
            || Status.HasValue
            || Reliability.HasValue;

        public void Normalize()
        {
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();

            if (Page < 1) Page = 1;
            if (!AllowedPageSizes.Contains(PageSize)) PageSize = DefaultPageSize;

            if (PercentFrom is < 0 or > 100) PercentFrom = null;
            if (PercentTo is < 0 or > 100) PercentTo = null;

            PercentRangeSwapped = false;
            if (PercentFrom.HasValue && PercentTo.HasValue && PercentFrom.Value > PercentTo.Value)
            {
                (PercentFrom, PercentTo) = (PercentTo, PercentFrom);
                PercentRangeSwapped = true;
            }

            if (!Enum.IsDefined(typeof(ExamResultsSort), Sort)) Sort = ExamResultsSort.DateDesc;
            if (Status.HasValue && !Enum.IsDefined(typeof(ExamResultStatusFilter), Status.Value)) Status = null;
            if (Reliability.HasValue && !Enum.IsDefined(typeof(ExamResultReliabilityFilter), Reliability.Value)) Reliability = null;
        }

        // Sahifa havolalari va Excel eksport havolasi uchun GET route qiymatlari.
        // Bo'sh filtrlar tashlab yuboriladi; Page faqat 1 dan katta bo'lsa qo'shiladi.
        public Dictionary<string, string> ToRouteValues(int? page = null)
        {
            var values = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(Search))
            {
                values["Search"] = Search;
            }

            if (GroupId.HasValue)
            {
                values["GroupId"] = GroupId.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (PercentFrom.HasValue)
            {
                values["PercentFrom"] = PercentFrom.Value.ToString("0.###", CultureInfo.InvariantCulture);
            }

            if (PercentTo.HasValue)
            {
                values["PercentTo"] = PercentTo.Value.ToString("0.###", CultureInfo.InvariantCulture);
            }

            if (DateFrom.HasValue)
            {
                values["DateFrom"] = DateFrom.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            if (DateTo.HasValue)
            {
                values["DateTo"] = DateTo.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            if (Status.HasValue)
            {
                values["Status"] = Status.Value.ToString();
            }

            if (Reliability.HasValue)
            {
                values["Reliability"] = Reliability.Value.ToString();
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

        // Controller'lar uchun: filtr holati + imtihon id'si (redirect va
        // Excel havolalarida ishlatiladi). Id ro'yxat ichida beriladi, chunki
        // asp-route-id bilan asp-all-route-data birga ishlatilganda id yo'qoladi.
        public Dictionary<string, string> ToRouteValuesWithExamId(int examId, int? page = null)
        {
            var values = ToRouteValues(page);
            values["id"] = examId.ToString(CultureInfo.InvariantCulture);
            return values;
        }
    }
}
