using Microsoft.AspNetCore.Identity;

namespace EduTrack.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }

        // Talaba qaysi guruhga tegishli (faqat Student roli uchun ishlatiladi)
        public int? GroupId { get; set; }
        public Group? Group { get; set; }
    }
}