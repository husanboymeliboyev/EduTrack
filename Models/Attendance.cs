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

        // Davomat qaysi guruh (smena) darsiga tegishli — 1-smena va 2-smena
        // davomatlari bir-biriga aralashmasligi uchun. Bir o'quvchi bir fan
        // bo'yicha ikkala smenada o'qigan bo'lishi mumkin (ertalab sinf,
        // kechqurun kurs), shuning uchun yozuv (Talaba, Fan, Guruh, Sana)
        // kesmasi bilan saqlanadi.
        public int? GroupId { get; set; }

        public AttendanceStatus Status { get; set; } = AttendanceStatus.Keldi;
    }
}
