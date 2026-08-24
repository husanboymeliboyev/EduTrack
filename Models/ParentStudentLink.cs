namespace EduTrack.Models
{
    public class ParentStudentLink
    {
        public int Id { get; set; }

        public string ParentUserId { get; set; } = string.Empty;
        public ApplicationUser ParentUser { get; set; } = null!;

        public string StudentUserId { get; set; } = string.Empty;
        public ApplicationUser StudentUser { get; set; } = null!;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? RevokedAtUtc { get; set; }
    }
}
