using EduTrack.Models;

namespace EduTrack.Services
{
    /// <summary>
    /// O'quvchining sinf va guruh konteksti uchun yagona kirish nuqtasi.
    /// A'zolikning manbai — <see cref="GroupMembership"/>; o'tish davrida
    /// <see cref="ApplicationUser.GroupId"/> faqat zaxira (legacy) sifatida
    /// o'qiladi, shuning uchun eski kod ham ishlashda davom etadi.
    /// </summary>
    public interface IStudentContextService
    {
        /// <summary>O'quvchining aktiv sinfi va guruhi (legacy zaxira bilan).</summary>
        Task<StudentContext> GetContextAsync(string studentId, CancellationToken cancellationToken = default);

        /// <summary>Berilgan kesimdagi aktiv guruh(lar).</summary>
        Task<IReadOnlyList<Group>> GetGroupsAsync(string studentId, GroupType type, CancellationToken cancellationToken = default);

        /// <summary>Aktiv a'zolik yozuvi (legacy bo'lsa null).</summary>
        Task<GroupMembership?> GetActiveMembershipAsync(string studentId, GroupType type, CancellationToken cancellationToken = default);

        /// <summary>
        /// Guruh a'zolarining ID'lari. <paramref name="onDate"/> berilsa —
        /// o'sha sanadagi tarkib (tarix), aks holda bugungi tarkib.
        /// </summary>
        Task<IReadOnlyList<string>> GetStudentIdsAsync(int groupId, DateTime? onDate = null, CancellationToken cancellationToken = default);

        /// <summary>O'quvchi shu guruhga (sanada) a'zomi.</summary>
        Task<bool> IsActiveMemberAsync(string studentId, int groupId, DateTime? onDate = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// O'quvchini guruhga biriktiradi. Shu kesimdagi oldingi aktiv a'zolik
        /// tarix sifatida yopiladi (o'chirilmaydi). Qayta biriktirish idempotent.
        /// </summary>
        Task<GroupMembership> AssignAsync(string studentId, int groupId, DateTime? startDate = null, CancellationToken cancellationToken = default);

        /// <summary>Kesimdagi aktiv a'zolikni yopadi. Aktiv a'zolik bo'lmasa false.</summary>
        Task<bool> EndAsync(string studentId, GroupType type, DateTime? endDate = null, CancellationToken cancellationToken = default);
    }

    /// <summary>O'quvchining joriy sinfi va yo'nalish guruhi.</summary>
    public sealed record StudentContext(Group? ClassGroup, Group? StreamGroup)
    {
        public bool HasClass => ClassGroup is not null;
        public bool HasStream => StreamGroup is not null;
    }
}
