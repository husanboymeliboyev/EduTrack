using EduTrack.Models;
using EduTrack.ViewModels;

namespace EduTrack.Services
{
    public interface IStudentShowcaseService
    {
        Task<StudentShowcaseViewModel> GetStudentShowcaseAsync(string studentId);
        Task<ClassOverviewViewModel> GetClassOverviewAsync(int groupId);
        Task<List<PerformanceCriteria>> GetCriteriaAsync();
        Task<bool> UpdateCriteriaAsync(Dictionary<string, double> weightsByKey); // yig'indi 100% ga tengligini tekshiradi, aks holda false qaytaradi
    }
}