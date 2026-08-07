namespace EduTrack.ViewModels
{
    public class TakeExamViewModel
    {
        public int ExamId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public List<ExamQuestionItem> Questions { get; set; } = new();
    }

    public class ExamQuestionItem
    {
        public int QuestionId { get; set; }
        public string Text { get; set; } = string.Empty;
        public List<ExamOptionItem> Options { get; set; } = new();
    }

    public class ExamOptionItem
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}