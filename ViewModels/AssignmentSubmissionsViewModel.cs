using EduTrack.Models;

namespace EduTrack.ViewModels
{
    /// <summary>Bitta topshiriq uchun talabalar ro'yxati + status filtri.</summary>
    public class AssignmentSubmissionsViewModel
    {
        public Assignment Assignment { get; set; } = null!;
        public List<SubmissionRosterRow> Rows { get; set; } = new();

        // Filtr: all | submitted | missing | graded | ungraded
        public string Status { get; set; } = "all";

        public int Total { get; set; }
        public int SubmittedCount { get; set; }
        public int MissingCount { get; set; }
        public int GradedCount { get; set; }
        public int UngradedCount { get; set; }
        public int LateCount { get; set; }
        public double? AverageGrade { get; set; }

        /// <summary>Ro'yxatdan tashqari (masalan guruh almashgan) talabalar topshirganmi — True.</summary>
        public bool HasExtraSubmissions { get; set; }
    }

    public class SubmissionRosterRow
    {
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? LoginId { get; set; }
        public Submission? Submission { get; set; }
        public bool Submitted => Submission != null;
    }
}
