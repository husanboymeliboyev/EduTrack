using EduTrack.Models;

namespace EduTrack.Services
{
    /// <summary>Topshiriqning javob turi uchun yorliq va ikonka.</summary>
    public static class AssignmentUi
    {
        public static string ModeLabel(SubmissionMode mode) => mode switch
        {
            SubmissionMode.FileOnly => "Faqat fayl",
            SubmissionMode.TextOnly => "Faqat matn",
            _ => "Fayl yoki matn"
        };

        public static string ModeIcon(SubmissionMode mode) => mode switch
        {
            SubmissionMode.FileOnly => "bi-paperclip",
            SubmissionMode.TextOnly => "bi-chat-square-text",
            _ => "bi-collection"
        };

        public static bool AllowsFile(SubmissionMode mode) => mode != SubmissionMode.TextOnly;

        public static bool AllowsText(SubmissionMode mode) => mode != SubmissionMode.FileOnly;
    }
}
