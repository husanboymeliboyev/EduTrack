namespace EduTrack.ViewModels
{
    public class AdminExamListSubjectOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class AdminExamListTeacherOptionViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class AdminExamListGroupOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // Admin imtihonlar ro'yxati uchun to'liq model: filtr, qatorlar,
    // filtr dropdown'lari va sahifalash.
    public class AdminExamListPageViewModel
    {
        public AdminExamListFilterViewModel Filter { get; set; } = new();
        public IReadOnlyList<AdminExamListItemViewModel> Rows { get; set; } =
            Array.Empty<AdminExamListItemViewModel>();
        public IReadOnlyList<AdminExamListSubjectOptionViewModel> Subjects { get; set; } =
            Array.Empty<AdminExamListSubjectOptionViewModel>();
        public IReadOnlyList<AdminExamListTeacherOptionViewModel> Teachers { get; set; } =
            Array.Empty<AdminExamListTeacherOptionViewModel>();
        public IReadOnlyList<AdminExamListGroupOptionViewModel> Groups { get; set; } =
            Array.Empty<AdminExamListGroupOptionViewModel>();
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = AdminExamListFilterViewModel.DefaultPageSize;

        public int TotalPages =>
            PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

        public Dictionary<string, string> ToRouteValues(int? page = null)
        {
            return Filter.ToRouteValues(page);
        }
    }
}
