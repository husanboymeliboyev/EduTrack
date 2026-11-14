using EduTrack.Models;

namespace EduTrack.ViewModels
{
    /// <summary>Topshiriqlar ro'yxati sahifasi uchun yagona model.</summary>
    public class AssignmentsIndexViewModel
    {
        public List<AssignmentRowViewModel> Rows { get; set; } = new();

        // Filtr holati
        public string? Search { get; set; }
        public int? SubjectId { get; set; }
        public string Status { get; set; } = "all";

        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> SubjectOptions { get; set; } = new();

        // KPI (filtrlanmagan, o'qituvchining barcha topshiriqlari bo'yicha)
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int OverdueCount { get; set; }
        public int SubmissionsCount { get; set; }
        public int UngradedCount { get; set; }
    }

    public class AssignmentRowViewModel
    {
        public Assignment Assignment { get; set; } = null!;
        /// <summary>Topshiriqqa tegishli (guruh) o'quvchilar soni.</summary>
        public int Expected { get; set; }
        public int Submitted { get; set; }
        public int Graded { get; set; }
        public int Late { get; set; }
    }
}
