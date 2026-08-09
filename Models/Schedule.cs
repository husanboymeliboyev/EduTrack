using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    public class Schedule
    {
        public int Id { get; set; }

        public int SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;

        public int GroupId { get; set; }
        public Group Group { get; set; } = null!;

        [Required(ErrorMessage = "Hafta kunini tanlang")]
        public DayOfWeek DayOfWeek { get; set; }

        [Required(ErrorMessage = "Boshlanish vaqtini kiriting")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "Tugash vaqtini kiriting")]
        public TimeSpan EndTime { get; set; }
    }
}