using EduTrack.ViewModels;

namespace EduTrack.Services
{
    /// <summary>
    /// Ota-ona paneli uchun ma'lumot manbai. Har bir metod ota-ona va farzand
    /// bog'lanishini server tomonida tekshiradi: bog'lanmagan o'quvchi
    /// ma'lumoti hech qachon qaytarilmaydi (null).
    /// </summary>
    public interface IParentPortalService
    {
        Task<IReadOnlyList<ParentChildViewModel>> GetChildrenAsync(
            string parentId, CancellationToken cancellationToken = default);

        Task<bool> HasChildrenAsync(string parentId, CancellationToken cancellationToken = default);

        Task<ParentDashboardViewModel> GetDashboardAsync(
            string parentId, CancellationToken cancellationToken = default);

        Task<ParentAttendanceViewModel?> GetAttendanceAsync(
            string parentId, string? studentId, CancellationToken cancellationToken = default);

        Task<ParentGradesViewModel?> GetGradesAsync(
            string parentId, string? studentId, CancellationToken cancellationToken = default);

        Task<ParentMonthlyRatingViewModel?> GetMonthlyRatingAsync(
            string parentId,
            string? studentId,
            int? year = null,
            int? month = null,
            CancellationToken cancellationToken = default);

        Task<ParentExamsViewModel?> GetExamsAsync(
            string parentId, string? studentId, CancellationToken cancellationToken = default);

        Task<ParentAssignmentsViewModel?> GetAssignmentsAsync(
            string parentId, string? studentId, CancellationToken cancellationToken = default);

        Task<ParentAlertsViewModel?> GetAlertsAsync(
            string parentId, string? studentId, CancellationToken cancellationToken = default);

        Task<ParentScheduleViewModel?> GetScheduleAsync(
            string parentId, string? studentId, CancellationToken cancellationToken = default);
    }
}
