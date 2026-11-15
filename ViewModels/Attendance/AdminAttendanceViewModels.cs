using EduTrack.Models;

namespace EduTrack.ViewModels
{
    /// <summary>Admin/Moderator davomat tahlili uchun filtrlar.</summary>
    public sealed class AdminAttendanceFilterViewModel
    {
        public int? SubjectId { get; init; }
        public int? GroupId { get; init; }
        public DateTime? Month { get; init; }
    }

    /// <summary>
    /// Bitta (fan, guruh) juftligi bo'yicha davomat jamlanmasi. Juftlikning
    /// amaldagi o'qituvchisi <see cref="GroupSubject.TeacherId"/> yoki
    /// <see cref="Subject.TeacherId"/> orqali aniqlanadi.
    /// </summary>
    public sealed class AdminAttendancePairRowViewModel
    {
        public int SubjectId { get; init; }
        public int GroupId { get; init; }

        public string SubjectName { get; init; } = string.Empty;
        public string GroupName { get; init; } = string.Empty;
        public string? TeacherName { get; init; }

        public int StudentCount { get; init; }

        public int Keldi { get; init; }
        public int Kelmadi { get; init; }
        public int KechQoldi { get; init; }
        public int Sababli { get; init; }

        public DateTime? LastRecordDate { get; init; }

        public int Total => Keldi + Kelmadi + KechQoldi + Sababli;

        public int Percentage => Total == 0
            ? 0
            : (int)Math.Round(100.0 * Keldi / Total, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Admin/Moderator uchun davomat bo'yicha umumiy ko'rinish: oylik KPI va
    /// fan-guruh juftliklari jadvali.
    /// </summary>
    public sealed class AdminAttendanceOverviewViewModel
    {
        public IReadOnlyList<AttendanceJournalOption> Subjects { get; init; }
            = Array.Empty<AttendanceJournalOption>();

        public IReadOnlyList<AttendanceJournalOption> Groups { get; init; }
            = Array.Empty<AttendanceJournalOption>();

        public int? SubjectId { get; init; }
        public int? GroupId { get; init; }

        public DateTime Month { get; init; }
        public string MonthLabel { get; init; } = string.Empty;
        public DateTime PreviousMonth { get; init; }
        public DateTime NextMonth { get; init; }
        public bool CanGoNext { get; init; }

        public int PairCount { get; init; }
        public int StudentCount { get; init; }

        public int Keldi { get; init; }
        public int Kelmadi { get; init; }
        public int KechQoldi { get; init; }
        public int Sababli { get; init; }

        public int Total => Keldi + Kelmadi + KechQoldi + Sababli;

        public int Percentage => Total == 0
            ? 0
            : (int)Math.Round(100.0 * Keldi / Total, MidpointRounding.AwayFromZero);

        public IReadOnlyList<AdminAttendancePairRowViewModel> Pairs { get; init; }
            = Array.Empty<AdminAttendancePairRowViewModel>();
    }

    /// <summary>
    /// Bitta (fan, guruh) juftligi bo'yicha batafsil o'quvchi × kun matritsasi.
    /// Matritsa faqat o'qish uchun — Admin/Moderator davomatni o'zgartira olmaydi.
    /// </summary>
    public sealed class AdminAttendanceDetailViewModel
    {
        public AttendanceJournalViewModel Journal { get; init; } = new();

        public string? TeacherName { get; init; }

        /// <summary>Oy bo'yicha dars jadvalida belgilangan kunlar soni.</summary>
        public int ScheduledDayCount { get; init; }

        /// <summary>Oy bo'yicha davomat yozuvi mavjud kunlar soni.</summary>
        public int RecordedDayCount { get; init; }

        /// <summary>Dars jadvalida bor, lekin davomat yozuvi yo'q kunlar soni.</summary>
        public int UnmarkedDayCount { get; init; }

        public int StudentCount => Journal.Rows.Count;

        public int Keldi => Journal.Rows.Sum(r => r.Keldi);
        public int Kelmadi => Journal.Rows.Sum(r => r.Kelmadi);
        public int KechQoldi => Journal.Rows.Sum(r => r.KechQoldi);
        public int Sababli => Journal.Rows.Sum(r => r.Sababli);

        public int Total => Keldi + Kelmadi + KechQoldi + Sababli;

        public int Percentage => Total == 0
            ? 0
            : (int)Math.Round(100.0 * Keldi / Total, MidpointRounding.AwayFromZero);
    }
}
