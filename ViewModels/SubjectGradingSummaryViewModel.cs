namespace EduTrack.ViewModels
{
    public class SubjectGradingSummaryViewModel
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public int ComponentCount { get; set; }
        public int TotalMaxScore { get; set; }
    }
}