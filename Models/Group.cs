using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    public class Group
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Guruh nomini kiriting")]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Sinf yoki yo'nalish guruhi. Standart Stream — mavjud guruhlar
        /// shu turda qoladi.
        /// </summary>
        public GroupType Type { get; set; } = GroupType.Stream;

        /// <summary>
        /// Qisqa kod: "10A", "G3". Ko'rsatish va qidirish uchun; unikal emas
        /// (turli o'quv yillarida takrorlanishi mumkin).
        /// </summary>
        [StringLength(20)]
        public string? Code { get; set; }

        /// <summary>O'tgan o'quv yili guruhi — ro'yxatlarda yashiriladi.</summary>
        public bool IsArchived { get; set; }

        // Guruhga biriktirilgan talabalar (eski, faqat o'qish uchun maydon)
        public ICollection<ApplicationUser> Students { get; set; } = new List<ApplicationUser>();

        // A'zolik tarixi (sinf va guruh)
        public ICollection<GroupMembership> Memberships { get; set; } = new List<GroupMembership>();
    }
}
