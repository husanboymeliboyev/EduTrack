using EduTrack.ViewModels;

namespace EduTrack.Services
{
    // Foydalanuvchilar / talabalar ro'yxatini qidiruv, filtr, sort va
    // sahifalash bilan o'qish. O'qituvchi uchun faqat o'z guruhlaridagi
    // talabalar qaytariladi.
    public interface IUserDirectoryService
    {
        Task<UsersPageViewModel> GetForAdminAsync(UsersFilterViewModel filter);

        Task<UsersPageViewModel> GetForTeacherAsync(
            string teacherId,
            UsersFilterViewModel filter);
    }
}
