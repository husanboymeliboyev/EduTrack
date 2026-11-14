using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    public class Submission
    {
        public int Id { get; set; }

        public int AssignmentId { get; set; }
        public Assignment? Assignment { get; set; }

        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }

        public string? FilePath { get; set; }
        public string? FileName { get; set; }

        // Topshirilgan vaqt — ilova vaqt mintaqasida (IAppClock.Now) to'ldiriladi.
        public DateTime SubmittedDate { get; set; }

        public int? Grade { get; set; }
        public string? TeacherComment { get; set; }

        // Ish muddatdan keyin topshirilganmi — yuklash paytida belgilanadi.
        public bool IsLate { get; set; }

        // Bahoga yangi ball qo'yilgan vaqt (baholash tarixi uchun).
        public DateTime? GradedDate { get; set; }

        // Talabaning topshirish paytidagi qisqa izohi (ixtiyoriy).
        [StringLength(500)]
        public string? StudentNote { get; set; }
    }
}