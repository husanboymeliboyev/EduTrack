namespace EduTrack.ViewModels
{
    public class ExamResultsGroupOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // Natijalar sahifasi uchun to'liq model: imtihon ma'lumoti, filtr, qatorlar,
    // sahifalash va filtrga mos to'liq to'plam bo'yicha xulosa.
    public class ExamResultsPageViewModel
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public string? SubjectName { get; set; }
        public string? ExamGroupName { get; set; }
        public ExamResultsFilterViewModel Filter { get; set; } = new();
        public IReadOnlyList<ExamResultRowViewModel> Rows { get; set; } = Array.Empty<ExamResultRowViewModel>();
        public IReadOnlyList<ExamResultsGroupOptionViewModel> Groups { get; set; } = Array.Empty<ExamResultsGroupOptionViewModel>();
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = ExamResultsFilterViewModel.DefaultPageSize;
        public int TotalPages { get; set; }
        public double AveragePercent { get; set; }
        public double HighestPercent { get; set; }
        public double LowestPercent { get; set; }
        public bool CanApprove { get; set; }
        public bool PercentRangeSwapped => Filter.PercentRangeSwapped;

        // Sahifa va Excel havolalari uchun route qiymatlari: filtr holati + imtihon id.
        // Id ro'yxat ichida beriladi, chunki asp-route-id bilan asp-all-route-data
        // birga ishlatilganda id yo'qolib, havola 404 qaytaradi.
        public Dictionary<string, string> ToRouteValues(int? page = null)
        {
            return Filter.ToRouteValuesWithExamId(ExamId, page);
        }
    }
}
