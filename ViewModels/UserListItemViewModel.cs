namespace EduTrack.ViewModels
{
    public class UserListItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string LoginId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        // Sinf va yo'nalish guruhi alohida ko'rsatiladi. Ikkalasi ham
        // GroupMemberships'dan olinadi; eski yozuvlar uchun GroupId zaxira.
        public string? ClassName { get; set; }
        public string? StreamName { get; set; }
    }
}
