using EduTrack.ViewModels;

namespace EduTrack.Services
{
    public interface IStudentShowcaseService
    {
        Task<StudentShowcaseViewModel> GetStudentShowcaseAsync(string studentId);
        Task<ClassOverviewViewModel> GetClassOverviewAsync(int groupId);
    }
}
