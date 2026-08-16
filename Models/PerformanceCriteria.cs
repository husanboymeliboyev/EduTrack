using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    public class PerformanceCriteria
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Key kiritilishi shart")]
        [StringLength(50)]
        public string Key { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ko'rsatkich nomini kiriting")]
        [StringLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [Range(0, 100, ErrorMessage = "Og'irlik 0 dan 100 gacha bo'lishi kerak")]
        public double Weight { get; set; }

        [StringLength(300)]
        public string? Description { get; set; }
    }
}