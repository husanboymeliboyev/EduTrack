namespace EduTrack.ViewModels
{
    // Jadvaldagi bitta natija qatori. View va Excel eksport bir xil modeldan foydalanadi.
    public class ExamResultRowViewModel
    {
        public int Id { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string? GroupName { get; set; }
        public string LoginId { get; set; } = string.Empty;
        public int CorrectAnswers { get; set; }
        public int TotalQuestions { get; set; }
        public double Percent { get; set; }
        public DateTime CompletedDate { get; set; }
        public bool IsApproved { get; set; }
        public bool IsSuspicious { get; set; }
    }
}
