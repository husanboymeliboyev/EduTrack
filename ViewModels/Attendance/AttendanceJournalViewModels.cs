using EduTrack.Models;

namespace EduTrack.ViewModels
{
    public sealed class AttendanceJournalOption
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    public sealed class AttendanceJournalDay
    {
        public DateTime Date { get; init; }
        public string WeekdayShort { get; init; } = string.Empty;
        public bool IsEditable { get; init; }
        public bool IsToday { get; init; }
    }

    public sealed class AttendanceJournalRow
    {
        public string StudentId { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;

        /// <summary>Kalit: yyyy-MM-dd. Faqat shu oy uchun yozuvlar.</summary>
        public IReadOnlyDictionary<string, AttendanceStatus?> Statuses { get; init; }
            = new Dictionary<string, AttendanceStatus?>();

        public int Keldi { get; init; }
        public int Kelmadi { get; init; }
        public int KechQoldi { get; init; }
        public int Sababli { get; init; }

        public int Total => Keldi + Kelmadi + KechQoldi + Sababli;

        public int Percentage => Total == 0
            ? 0
            : (int)Math.Round(100.0 * Keldi / Total, MidpointRounding.AwayFromZero);
    }

    public sealed class AttendanceJournalViewModel
    {
        public IReadOnlyList<AttendanceJournalOption> Subjects { get; init; }
            = Array.Empty<AttendanceJournalOption>();

        public IReadOnlyList<AttendanceJournalOption> Groups { get; init; }
            = Array.Empty<AttendanceJournalOption>();

        public int? SubjectId { get; init; }
        public string? SubjectName { get; init; }

        public int? GroupId { get; init; }
        public string? GroupName { get; init; }

        public DateTime Month { get; init; }
        public string MonthLabel { get; init; } = string.Empty;

        public DateTime PreviousMonth { get; init; }
        public DateTime NextMonth { get; init; }
        public bool CanGoNext { get; init; }

        /// <summary>Shu fan+guruh uchun dars jadvali topildi va kunlar shundan olindi.</summary>
        public bool HasSchedule { get; init; }

        public IReadOnlyList<AttendanceJournalDay> Days { get; init; }
            = Array.Empty<AttendanceJournalDay>();

        public IReadOnlyList<AttendanceJournalRow> Rows { get; init; }
            = Array.Empty<AttendanceJournalRow>();
    }
}
