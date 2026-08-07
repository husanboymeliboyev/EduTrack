using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    public class Question
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Savol matnini kiriting")]
        [StringLength(1000)]
        public string Text { get; set; } = string.Empty;

        public int SubjectId { get; set; }
        public Subject? Subject { get; set; }

        public ICollection<AnswerOption> Options { get; set; } = new List<AnswerOption>();
    }
}