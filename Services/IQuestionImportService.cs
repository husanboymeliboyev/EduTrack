namespace EduTrack.Services
{
    public class ParsedOption
    {
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }

    public class ParsedQuestion
    {
        public string Text { get; set; } = string.Empty;
        public List<ParsedOption> Options { get; set; } = new();
    }

    public class QuestionImportResult
    {
        public List<ParsedQuestion> Questions { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public bool HasErrors => Errors.Count > 0;
    }

    public interface IQuestionImportService
    {
        QuestionImportResult Parse(string rawText);
        string CreateTemplateText();
    }
}