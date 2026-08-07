namespace EduTrack.Models
{
    public class ExamResult
    {
        public int Id { get; set; }

        public int ExamId { get; set; }
        public Exam? Exam { get; set; }

        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }

        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }

        public DateTime CompletedDate { get; set; } = DateTime.Now;

        // Qaysi savollar berilgani va qanday javob tanlangani (JSON formatda saqlanadi)
        public string? QuestionIdsJson { get; set; }
        public string? SelectedOptionIdsJson { get; set; }
    }
}