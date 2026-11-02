namespace EduTrack.ViewModels
{
    // Excel eksport uchun filtrga mos TO'LIQ to'plam (sahifalashsiz).
    // Rows faqat MaxRows chegarasidan oshmagan holatda to'ldiriladi.
    public class ExamResultsExportData
    {
        public const int MaxRows = 5000;

        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public string? SubjectName { get; set; }
        public string? ExamGroupName { get; set; }
        public string? FilterGroupName { get; set; }
        public ExamResultsFilterViewModel Filter { get; set; } = new();
        public IReadOnlyList<ExamResultRowViewModel> Rows { get; set; } = Array.Empty<ExamResultRowViewModel>();
        public int TotalCount { get; set; }
        public double AveragePercent { get; set; }
        public double HighestPercent { get; set; }
        public double LowestPercent { get; set; }
        // Eksport tayyorlangan vaqt — ilova vaqt mintaqasida (IAppClock.Now).
        public DateTime GeneratedAt { get; set; }

        public bool ExceedsRowLimit => TotalCount > MaxRows;
    }
}
