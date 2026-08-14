using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    public class Question
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Savol matnini kiriting")]
        [StringLength(3000)]
        public string Text { get; set; } = string.Empty;

        public string? ImagePath { get; set; }

        public int SubjectId { get; set; }
        public Subject? Subject { get; set; }

        // Ixtiyoriy: belgilansa, savol faqat shu guruhga tegishli imtihonlarda ishlatiladi. null = umumiy.
        public int? GroupId { get; set; }
        public Group? Group { get; set; }

        public ICollection<AnswerOption> Options { get; set; } = new List<AnswerOption>();
    }
}