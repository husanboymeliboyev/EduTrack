using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    public class Group
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Guruh nomini kiriting")]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        // Guruhga biriktirilgan talabalar
        public ICollection<ApplicationUser> Students { get; set; } = new List<ApplicationUser>();
    }
}