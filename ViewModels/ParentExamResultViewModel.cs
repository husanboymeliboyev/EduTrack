namespace EduTrack.ViewModels
{
    public class ParentExamResultViewModel
    {
        public string ExamTitle { get; set; } = string.Empty;
        public string? SubjectName { get; set; }
        public int CorrectAnswers { get; set; }
        public int TotalQuestions { get; set; }
        public DateTime CompletedDate { get; set; }

        public double Percentage => TotalQuestions > 0
            ? Math.Round(100.0 * CorrectAnswers / TotalQuestions, 1)
            : 0;
    }
}