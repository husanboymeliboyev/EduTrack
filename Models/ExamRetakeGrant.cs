using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    // O'qituvchi talabaga imtihon uchun QO'SHIMCHA (2-) urinish ruxsatini beradi:
    // chetlatilgan, vaqti tugab yakunlay olmagan yoki umuman qatnashmagan talaba uchun.
    // Ruxsat faqat BIR marta ishlatiladi (IsUsed). Talaba imtihonni boshlaganda
    // (StudentExamsController.Take) ruxsat "ishlatilgan" deb belgilanadi.
    public class ExamRetakeGrant
    {
        public int Id { get; set; }

        public int ExamId { get; set; }
        public Exam? Exam { get; set; }

        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }

        // Ruxsat bergan o'qituvchi (ApplicationUser.Id) — audit uchun.
        public string GrantedByTeacherId { get; set; } = string.Empty;

        public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;

        [StringLength(300)]
        public string? Reason { get; set; }

        public bool IsUsed { get; set; } = false;
        public DateTime? UsedAtUtc { get; set; }

        // Eski urinish o'chirilishidan oldin uning holati shu yerga nusxalanadi (audit).
        public bool? PreviousWasForceFinished { get; set; }
        public int? PreviousViolationCount { get; set; }
        public string? PreviousLastViolationReason { get; set; }
    }
}
