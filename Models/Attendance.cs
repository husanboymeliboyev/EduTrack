using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    public class Attendance
    {
        public int Id { get; set; }

        [Required]
        public DateTime Date { get; set; }

        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }

        public int SubjectId { get; set; }
        public Subject? Subject { get; set; }

        public bool IsPresent { get; set; }
    }
}