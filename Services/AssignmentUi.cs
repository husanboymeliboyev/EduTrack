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

        /// <summary>
        /// Muddatni "kun oxiri" qoidasiga keltiradi: agar o'qituvchi faqat sanani
        /// tanlab, vaqt 00:00 bo'lib qolsa, topshiriq o'sha kunning boshida
        /// (ya'ni darhol) tugagan hisoblanib qolardi — o'quvchilar esa uni
        /// "muddati o'tgan" deb ko'rardi. Shuning uchun 00:00 — shu kunning
        /// oxiri (23:59) deb qabul qilinadi.
        /// </summary>
        public static DateTime NormalizeDueDate(DateTime dueDate)
            => dueDate.TimeOfDay == TimeSpan.Zero
                ? dueDate.Date.AddHours(23).AddMinutes(59)
                : dueDate;
    }
}
