using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    // Fan uchun baholash tarkibiy qismi: masalan "1-nazorat" (15 ball),
    // "Oraliq nazorat" (20 ball), "Yakuniy nazorat" (50 ball).
    // Har bir o'qituvchi o'z fani uchun bu tuzilmani moslashtirib sozlaydi.
    public class GradeComponent
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Komponent nomini kiriting")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Range(1, 100, ErrorMessage = "1 dan 100 gacha bo'lishi kerak")]
        public int MaxScore { get; set; }

        public int Order { get; set; }

        public int SubjectId { get; set; }
        public Subject? Subject { get; set; }

        public int? AssignmentId { get; set; }
        public Assignment? Assignment { get; set; }

        public int? ExamId { get; set; }
        public Exam? Exam { get; set; }

        public bool IsAutoLinked => AssignmentId.HasValue || ExamId.HasValue;

        public ICollection<StudentGrade> Grades { get; set; } = new List<StudentGrade>();
    }
}