namespace EduTrack.ViewModels
{
    public class ExamRetakeIndexViewModel
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public List<ExamRetakeRowViewModel> Rows { get; set; } = new();
    }

    public class ExamRetakeRowViewModel
    {
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? GroupName { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public string BadgeClass { get; set; } = "bg-secondary";
        public bool CanGrant { get; set; }
        public int? PendingGrantId { get; set; }
        public string? PendingReason { get; set; }
        public int UsedRetakeCount { get; set; }
    }
}
