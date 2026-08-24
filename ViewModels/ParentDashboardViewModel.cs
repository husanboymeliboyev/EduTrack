using EduTrack.Models;

namespace EduTrack.ViewModels
{
    public class ParentDashboardViewModel
    {
        public List<ParentChildOptionViewModel> Children { get; set; } = new();
        public string? SelectedChildId { get; set; }
        public StudentShowcaseViewModel? Report { get; set; }
    }

    public class ParentChildOptionViewModel
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
    }
}
