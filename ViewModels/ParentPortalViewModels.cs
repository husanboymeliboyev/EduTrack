using EduTrack.Models;

namespace EduTrack.ViewModels
{
    // Ota-ona panelidagi barcha modul sahifalari uchun umumiy asos:
    // tanlangan farzand + farzandlar ro'yxati (o'tish tugmasi uchun).
    public abstract class ParentPageViewModel
    {
        public List<ParentChildViewModel> Children { get; set; } = new();
        public ParentChildViewModel Child { get; set; } = new();
        public string ActiveAction { get; set; } = string.Empty;
    }

    public class ParentDashboardViewModel
    {
        public string ParentName { get; set; } = string.Empty;
        public List<ParentChildSummaryViewModel> Children { get; set; } = new();
    }

    public class ParentChildSummaryViewModel
    {        public ParentChildViewModel Child { get; set; } = new();

        public double AttendancePercentage { get; set; }
        public double GradePercentage { get; set; }
        public double AssignmentCompletionPercentage { get; set; }
        public double OverallPercentage { get; set; }
        public string StatusZone { get; set; } = "Muted"; // Green / Yellow / Red / Muted

        public int TotalLessons { get; set; }
        public int AbsentCount { get; set; }
        public int LateCount { get; set; }
        public int SubjectCount { get; set; }

        public int AssignmentCount { get; set; }
        public int UnsubmittedAssignments { get; set; }
        public int OverdueAssignments { get; set; }

        public int ExamCount { get; set; }
        public int PendingExamResults { get; set; }
        public double AverageExamPercentage { get; set; }

        public int GradedSubjects { get; set; }
        public int TotalSubjects { get; set; }
        public bool HasGrades => GradedSubjects > 0;

        // ---- Oylik reyting (joriy oy, eng yaxshi qamrov) ----
        public double? MonthlyRatingScore { get; set; }
        public int? MonthlyRatingRank { get; set; }
        public int MonthlyRatingParticipants { get; set; }
        public string MonthlyRatingScopeLabel { get; set; } = string.Empty;
        public string MonthlyRatingPeriodLabel { get; set; } = string.Empty;
        public double MonthlyRatingCoverage { get; set; }
        public bool HasMonthlyRating => MonthlyRatingScore.HasValue;

        public string MonthlyRatingTone => MonthlyRatingScore.HasValue
            ? MonthlyRatingSubjectViewModel.ToneFor(MonthlyRatingScore.Value)
            : "muted";

        public string MonthlyRatingValueLabel => MonthlyRatingScore.HasValue
            ? $"{MonthlyRatingScore.Value:0.#}%"
            : "—";

        public string MonthlyRatingRankLabel => MonthlyRatingRank.HasValue
            ? $"#{MonthlyRatingRank.Value} / {MonthlyRatingParticipants}"
            : string.Empty;

        /// <summary>Oylik reytingning 6 oylik dinamikasi (chart uchun).</summary>
        public List<MonthlyRatingTrendPointViewModel> RatingTrend { get; set; } = new();

        /// <summary>So'nggi 8 haftalik davomat trendi (chart uchun).</summary>
        public List<ParentTrendPointViewModel> AttendanceTrend { get; set; } = new();

        public List<ParentAlertViewModel> Alerts { get; set; } = new();

        public bool HasData =>
            TotalLessons > 0 || ExamCount > 0 || AssignmentCount > 0 || SubjectCount > 0;

        public int AttentionCount =>
            Alerts.Count(a => a.Severity == "danger" || a.Severity == "warning");
    }

    /// <summary>Dashboard grafiklari uchun davomat trend nuqtasi.</summary>
    public sealed class ParentTrendPointViewModel
    {
        public string Label { get; set; } = string.Empty;
        public double Percentage { get; set; }
        public int TotalCount { get; set; }
    }

    public class ParentAttendanceViewModel : ParentPageViewModel
    {
        public int TotalCount { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int LateCount { get; set; }
        public int ExcusedCount { get; set; }
        public double Percentage { get; set; }

        public List<ParentSubjectAttendanceViewModel> BySubject { get; set; } = new();
        public List<ParentAttendanceRecordViewModel> RecentRecords { get; set; } = new();
        public List<DailyAttendancePoint> Calendar { get; set; } = new();
    }

    public class ParentSubjectAttendanceViewModel
    {
        public string SubjectName { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int LateCount { get; set; }
        public int ExcusedCount { get; set; }

        public double Percentage => TotalCount == 0
            ? 0
            : Math.Round(100.0 * PresentCount / TotalCount, 1);
    }

    public class ParentAttendanceRecordViewModel
    {
        public DateTime Date { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public AttendanceStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string BadgeClass { get; set; } = string.Empty;
    }

    public class ParentGradesViewModel : ParentPageViewModel
    {
        public List<StudentSubjectGradeViewModel> Subjects { get; set; } = new();
        public double OverallPercentage { get; set; }
        public int GradedSubjects { get; set; }
        public int TotalSubjects { get; set; }

        public bool HasGrades => GradedSubjects > 0;

        public double CoveragePercentage => TotalSubjects == 0
            ? 0
            : Math.Round(100.0 * GradedSubjects / TotalSubjects, 1);
    }

    public class ParentExamsViewModel : ParentPageViewModel
    {
        public List<ParentExamResultViewModel> Results { get; set; } = new();
        public int PendingCount { get; set; }
        public double AveragePercentage { get; set; }
        public double BestPercentage { get; set; }
    }

    public class ParentAssignmentsViewModel : ParentPageViewModel
    {
        public List<ParentAssignmentItemViewModel> Items { get; set; } = new();
        public int SubmittedCount { get; set; }
        public int PendingCount { get; set; }
        public int OverdueCount { get; set; }
        public double CompletionPercentage { get; set; }
    }

    public class ParentAssignmentItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime DueDate { get; set; }

        public bool IsSubmitted { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public int? Grade { get; set; }
        public string? TeacherComment { get; set; }
        public string? FileName { get; set; }
        public bool IsOverdue { get; set; }
    }

    public class ParentAlertsViewModel : ParentPageViewModel
    {
        public List<ParentAlertViewModel> Alerts { get; set; } = new();
    }

    public class ParentAlertViewModel
    {
        public string Severity { get; set; } = "info"; // danger / warning / info / success
        public string Icon { get; set; } = "info-circle";
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ActionName { get; set; }
        public string? ActionText { get; set; }
    }

    public class ParentScheduleViewModel : ParentPageViewModel
    {
        public WeeklyScheduleViewModel Schedule { get; set; } = new();
        public int LessonCount => Schedule.Blocks.Count;
    }
}
