using EduTrack.Models;

namespace EduTrack.ViewModels
{
    public class UsersGroupOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public GroupType Type { get; set; }
    }

    // Foydalanuvchilar (admin) va talabalar (o'qituvchi) ro'yxati uchun
    // to'liq model: filtr, qatorlar, filtr dropdown'lari va sahifalash.
    public class UsersPageViewModel
    {
        public UsersFilterViewModel Filter { get; set; } = new();
        public IReadOnlyList<UserListItemViewModel> Rows { get; set; } = Array.Empty<UserListItemViewModel>();
        public IReadOnlyList<UsersGroupOptionViewModel> Groups { get; set; } = Array.Empty<UsersGroupOptionViewModel>();
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = UsersFilterViewModel.DefaultPageSize;

        // O'qituvchi sahifasida rol filtri va tahrirlash amallari ko'rsatilmaydi.
        public bool IsTeacherScope { get; set; }

        public int TotalPages =>
            PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

        public Dictionary<string, string> ToRouteValues(int? page = null)
        {
            return Filter.ToRouteValues(page);
        }
    }
}
