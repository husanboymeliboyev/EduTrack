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

        public string? QuestionIdsJson { get; set; }
        public string? SelectedOptionIdsJson { get; set; }

        // Xavfsizlik signallari: talaba imtihon paytida sahifadan chiqib ketganmi,
        // to'liq ekran (fullscreen) rejimidan chiqib ketganmi. O'qituvchi bu ma'lumotni
        // natijalar sahifasida ko'radi va shubhali holatlarni tekshirishi mumkin.
        public int TabSwitchCount { get; set; }
        public bool ExitedFullscreen { get; set; }
    }
}