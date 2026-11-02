using EduTrack.ViewModels;

namespace EduTrack.Services
{
    // Admin uchun barcha imtihonlar ro'yxatini qidiruv, filtr, sort va
    // sahifalash bilan o'qish (read-only).
    public interface IAdminExamDirectoryService
    {
        Task<AdminExamListPageViewModel> GetExamsAsync(
            AdminExamListFilterViewModel filter);
    }
}
