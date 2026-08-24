namespace EduTrack.ViewModels
{
    public class ExamReviewViewModel
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public DateTime CompletedDate { get; set; }
        public List<ReviewQuestionItem> Questions { get; set; } = new();
    }

    public class ReviewQuestionItem
    {
        public int QuestionNumber { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public List<ReviewOptionItem> Options { get; set; } = new();
        public int? SelectedOptionId { get; set; }
        public int CorrectOptionId { get; set; }
        public bool IsCorrect { get; set; }
    }

    public class ReviewOptionItem
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public bool IsSelected { get; set; }
    }
}
