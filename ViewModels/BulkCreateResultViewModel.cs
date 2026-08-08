namespace EduTrack.ViewModels
{
    public class BulkCreateResultViewModel
    {
        public string GroupName { get; set; } = string.Empty;
        public List<UserCredentialsViewModel> CreatedUsers { get; set; } = new();
        public List<string> Skipped { get; set; } = new();
    }
}