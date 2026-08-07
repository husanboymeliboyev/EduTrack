using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    public class Exam
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Imtihon nomini kiriting")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public int SubjectId { get; set; }
        public Subject? Subject { get; set; }

        [Required]
        [Display(Name = "Savollar soni")]
        [Range(1, 100, ErrorMessage = "1 dan 100 gacha bo'lishi kerak")]
        public int QuestionCount { get; set; }

        [Required]
        [Display(Name = "Vaqt chegarasi (daqiqa)")]
        [Range(1, 300)]
        public int DurationMinutes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public ICollection<ExamResult> Results { get; set; } = new List<ExamResult>();
    }
}