using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    public class Subject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Fan nomini kiriting")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        // Fanni o'qitadigan o'qituvchi
        public string? TeacherId { get; set; }
        public ApplicationUser? Teacher { get; set; }
    }
}