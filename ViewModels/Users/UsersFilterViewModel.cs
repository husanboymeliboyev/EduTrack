using System.Globalization;

namespace EduTrack.ViewModels
{
    public enum UsersSort
    {
        NameAsc = 0,
        NameDesc = 1,
        LoginAsc = 2,
        LoginDesc = 3
    }

    // Foydalanuvchilar / talabalar ro'yxatidagi filtr holati.
    // GET query string'dan to'ldiriladi; Normalize() mijozdan kelgan
    // qiymatlarni xavfsiz diapazonga keltiradi.
    public class UsersFilterViewModel
    {
        public const int DefaultPageSize = 20;
        public static readonly int[] AllowedPageSizes = { 20, 50, 100 };

        public string? Search { get; set; }
        public string? Role { get; set; }
        public int? GroupId { get; set; }
        public UsersSort Sort { get; set; } = UsersSort.NameAsc;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = DefaultPageSize;

        // Qidiruv, rol va guruh filtrlaridan birortasi faolmi.
        // Sort va sahifalash filtr hisoblanmaydi.
        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(Search)
            || !string.IsNullOrWhiteSpace(Role)
            || GroupId.HasValue;

        public void Normalize()
        {
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
            Role = string.IsNullOrWhiteSpace(Role) ? null : Role.Trim();

            if (Page < 1) Page = 1;
            if (!AllowedPageSizes.Contains(PageSize)) PageSize = DefaultPageSize;

            if (!Enum.IsDefined(typeof(UsersSort), Sort)) Sort = UsersSort.NameAsc;
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

            if (!string.IsNullOrWhiteSpace(Role))
            {
                values["Role"] = Role;
            }

            if (GroupId.HasValue)
            {
                values["GroupId"] = GroupId.Value.ToString(CultureInfo.InvariantCulture);
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
