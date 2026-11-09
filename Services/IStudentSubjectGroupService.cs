using EduTrack.Models;

namespace EduTrack.Services
{
    /// <summary>
    /// O'quvchining fan bo'yicha guruhini aniqlash uchun yagona kirish nuqtasi.
    /// Standart holat: barcha fanlar — o'quvchining aktiv Stream guruhida.
    /// Aniq bir fan boshqa guruhda o'qilsa, <see cref="StudentGroupSubject"/>
    /// yozuvi (override) ishlatiladi.
    /// </summary>
    public interface IStudentSubjectGroupService
    {
        /// <summary>O'quvchining standart (barcha fanlar uchun) guruhi.</summary>
        Task<int?> GetDefaultGroupIdAsync(string studentId, CancellationToken cancellationToken = default);

        /// <summary>Fan bo'yicha amaldagi guruh (override bo'lmasa — standart guruh).</summary>
        Task<int?> GetGroupIdAsync(string studentId, int subjectId, DateTime? onDate = null, CancellationToken cancellationToken = default);

        /// <summary>Bir nechta fan bo'yicha guruh xaritasi (subjectId → groupId).</summary>
        Task<IReadOnlyDictionary<int, int>> GetGroupMapAsync(
            string studentId,
            IEnumerable<int> subjectIds,
            DateTime? onDate = null,
            CancellationToken cancellationToken = default);

        /// <summary>O'quvchi haqiqatan o'qiydigan fanlar (guruh-fan bog'lanishi bilan mos).</summary>
        Task<IReadOnlyList<int>> GetSubjectIdsAsync(
            string studentId,
            DateTime? onDate = null,
            CancellationToken cancellationToken = default);

        /// <summary>O'quvchining fan bo'yicha guruh yozuvlari (tarix bilan).</summary>
        Task<IReadOnlyList<StudentGroupSubject>> GetAssignmentsAsync(
            string studentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Guruh-fan bo'yicha o'quvchilar ro'yxati: aniq biriktirilganlar va
        /// shu guruhda standart o'qiydiganlar (boshqa guruhga override qilinmaganlar).
        /// </summary>
        Task<IReadOnlyList<string>> GetStudentIdsAsync(
            int groupId,
            int subjectId,
            DateTime? onDate = null,
            CancellationToken cancellationToken = default);

        /// <summary>O'quvchi aynan shu guruhda shu fanni o'qiydimi.</summary>
        Task<bool> IsStudentInGroupSubjectAsync(
            string studentId,
            int groupId,
            int subjectId,
            DateTime? onDate = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Fanni boshqa guruhga biriktiradi (eski yozuv tarix sifatida yopiladi).
        /// Guruh o'quvchining standart guruhiga teng bo'lsa — override yopiladi
        /// va null qaytadi.
        /// </summary>
        Task<StudentGroupSubject?> SetGroupAsync(
            string studentId,
            int subjectId,
            int groupId,
            DateTime? startDate = null,
            string? reason = null,
            CancellationToken cancellationToken = default);

        /// <summary>Fan bo'yicha override'ni yopadi — o'quvchi standart guruhga qaytadi.</summary>
        Task<bool> ResetToDefaultAsync(
            string studentId,
            int subjectId,
            DateTime? startDate = null,
            string? reason = null,
            CancellationToken cancellationToken = default);
    }
}
