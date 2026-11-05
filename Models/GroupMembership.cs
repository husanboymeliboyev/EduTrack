using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    /// <summary>
    /// O'quvchining sinf yoki guruhga a'zoligi. Mavjud
    /// <see cref="ApplicationUser.GroupId"/> o'tish davrida saqlanadi, lekin
    /// a'zolikning yagona manbai shu jadval hisoblanadi.
    /// Tarkib o'zgarganda eski yozuv o'chirilmaydi: <see cref="IsActive"/>
    /// false bo'ladi va <see cref="EndDate"/> to'ldiriladi (tarix saqlanadi).
    /// </summary>
    public class GroupMembership
    {
        public int Id { get; set; }

        [Required]
        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }

        public int GroupId { get; set; }
        public Group? Group { get; set; }

        /// <summary>
        /// <see cref="Group.Type"/> nusxasi. "Bitta o'quvchi — bitta aktiv sinf
        /// va bitta aktiv guruh" qoidasini DB darajasida (filtrlangan unique
        /// indeks orqali) kafolatlash uchun kerak.
        /// </summary>
        public GroupType Scope { get; set; }

        public DateTime StartDate { get; set; }

        /// <summary>
        /// A'zolik tugagan sana (eksklyuziv): shu sanadan boshlab o'quvchi
        /// bu guruhga kirmaydi. null = hali tugamagan.
        /// </summary>
        public DateTime? EndDate { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
