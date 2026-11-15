using EduTrack.ViewModels;

namespace EduTrack.Services
{
    /// <summary>
    /// Admin va Moderator uchun davomat tahlili: guruh → fan → o'quvchi
    /// kesimida kim darsga kelgani/kelmaganini ko'rsatadi. Bu servis faqat
    /// o'qish uchun ma'lumot tayyorlaydi; davomat belgilash teacher oqimida
    /// qoladi.
    /// </summary>
    public interface IAdminAttendanceService
    {
        /// <summary>Oylik KPI va fan-guruh juftliklari jadvali.</summary>
        Task<AdminAttendanceOverviewViewModel> GetOverviewAsync(
            AdminAttendanceFilterViewModel? filter,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Bitta (fan, guruh) juftligi uchun o'quvchi × kun matritsasi.
        /// Juftlik topilmasa <c>null</c>.
        /// </summary>
        Task<AdminAttendanceDetailViewModel?> GetDetailAsync(
            int subjectId,
            int groupId,
            DateTime? month,
            CancellationToken cancellationToken = default);
    }
}
