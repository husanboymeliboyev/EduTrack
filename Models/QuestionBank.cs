using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    /// <summary>
    /// Bitta fan ichidagi nomlangan savollar to'plami (masalan "1-oraliq", "2-oraliq").
    /// Har bir savol va imtihon endi aniq bitta Bankka bog'lanadi — shu orqali
    /// bir fan ichida bir nechta mustaqil mavzuviy to'plamni ajratib bo'ladi.
    /// </summary>
    public class QuestionBank
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Bank nomini kiriting")]
        [StringLength(150)]
        [Display(Name = "Bank nomi")]
        public string Name { get; set; } = string.Empty;

        public int SubjectId { get; set; }
        public Subject? Subject { get; set; }

        // Ixtiyoriy: belgilansa, bank faqat shu guruhga tegishli imtihonlarda ishlatiladi.
        // null = shu fanning barcha guruhlari uchun umumiy.
        public int? GroupId { get; set; }
        public Group? Group { get; set; }

        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<Exam> Exams { get; set; } = new List<Exam>();
    }
}
