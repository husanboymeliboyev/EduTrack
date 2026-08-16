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

        // Har bir savol albatta bitta Bankka (masalan "1-oraliq") tegishli bo'lishi kerak.
        public int QuestionBankId { get; set; }
        public QuestionBank? QuestionBank { get; set; }

        // Ixtiyoriy, qo'shimcha filtr: belgilansa, savol faqat shu guruhga tegishli
        // imtihonlarda ishlatiladi. null = umumiy. Bank o'zi ham GroupId'ga ega bo'lishi
        // mumkin — ikkalasi birgalikda ishlaydi (savol darajasidagi filtr Bank darajasidagi
        // filtrni qo'shimcha toraytiradi).
        public int? GroupId { get; set; }
        public Group? Group { get; set; }

        public ICollection<AnswerOption> Options { get; set; } = new List<AnswerOption>();
    }
}
