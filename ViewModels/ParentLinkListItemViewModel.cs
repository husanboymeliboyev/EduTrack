namespace EduTrack.ViewModels
{
    // Admin panelida ota-ona <-> talaba bog'lanishini ko'rsatish uchun.
    public class ParentLinkListItemViewModel
    {
        public int Id { get; set; }
        public string ParentId { get; set; } = string.Empty;
        public string ParentName { get; set; } = string.Empty;
        public string ParentLoginId { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
    }
}
