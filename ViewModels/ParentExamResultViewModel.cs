namespace EduTrack.ViewModels
{
    public class ParentExamResultViewModel
    {
        public int Id { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public string? SubjectName { get; set; }
        public int CorrectAnswers { get; set; }
        public int TotalQuestions { get; set; }
        public DateTime CompletedDate { get; set; }
        public bool IsApproved { get; set; }

        public double Percentage => TotalQuestions > 0
            ? Math.Round(100.0 * CorrectAnswers / TotalQuestions, 1)
            : 0;

        public string StatusLabel => IsApproved ? "Tasdiqlangan" : "Tasdiq kutilmoqda";

        public string StatusClass => IsApproved
            ? "bg-success-subtle text-success-emphasis"
            : "bg-warning-subtle text-warning-emphasis";
    }
}
