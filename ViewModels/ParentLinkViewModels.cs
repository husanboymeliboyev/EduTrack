namespace EduTrack.ViewModels
{
    public class ParentLinkIndexViewModel
    {
        public List<ParentLinkRowViewModel> Links { get; set; } = new();
    }

    public class ParentLinkRowViewModel
    {
        public int Id { get; set; }
        public string ParentName { get; set; } = string.Empty;
        public string ParentLoginId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class ParentLinkCreateViewModel
    {
        public string ParentUserId { get; set; } = string.Empty;
        public string StudentUserId { get; set; } = string.Empty;
    }
}
