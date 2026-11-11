using EduTrack.ViewModels;

namespace EduTrack.Services
{
    // Admin uchun barcha imtihonlar ro'yxatini qidiruv, filtr, sort va
    // sahifalash bilan o'qish (read-only).
    // approvedOnly = true bo'lsa faqat kamida bitta tasdiqlangan natijasi bor
    // imtihonlar qaytadi (moderator/zauch uchun).
    public interface IAdminExamDirectoryService
    {
        Task<AdminExamListPageViewModel> GetExamsAsync(
            AdminExamListFilterViewModel filter,
            bool approvedOnly = false);
    }
}
