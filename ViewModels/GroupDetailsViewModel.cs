using EduTrack.Models;

namespace EduTrack.ViewModels
{
    public class GroupDetailsViewModel
    {
        public Group Group { get; set; } = null!;
        public IReadOnlyList<GroupMemberRowViewModel> Members { get; set; } = Array.Empty<GroupMemberRowViewModel>();
    }

    public class GroupMemberRowViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string LoginId { get; set; } = string.Empty;
        public string? ClassName { get; set; }
        public string? StreamName { get; set; }
    }
}
