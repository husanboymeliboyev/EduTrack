using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    /// <summary>
    /// O'quvchining aniq bir fanni qaysi yo'nalish guruhida o'qishi.
    /// Asosiy qoida: o'quvchi barcha fanlarni o'zining aktiv guruhi
    /// (<see cref="GroupMembership"/>, Scope = Stream)da o'qiydi. Agar biror
    /// fan boshqa guruhda o'qilsa — shu jadvalda aniq yozuv (override) bo'ladi.
    /// Yozuv o'chirilmaydi: guruh almashtirilganda <see cref="IsActive"/> false
    /// bo'ladi va <see cref="EndDate"/> to'ldiriladi (tarix saqlanadi).
    /// </summary>
    public class StudentGroupSubject
    {
        public int Id { get; set; }

        [Required]
        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }

        public int SubjectId { get; set; }
        public Subject? Subject { get; set; }

        public int GroupId { get; set; }
        public Group? Group { get; set; }

        public DateTime StartDate { get; set; }

        /// <summary>
        /// Yozuv tugagan sana (eksklyuziv): shu sanadan boshlab o'quvchi bu
        /// fanni yana o'zining standart guruhida o'qiydi. null = hali tugamagan.
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>O'zgarish sababi (masalan: "Matematika 2-guruhga o'tkazildi").</summary>
        public string? EndReason { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
