using EduTrack.ViewModels;

namespace EduTrack.Services
{
    public interface IAnalyticsService
    {
        Task<List<SubjectAnalyticsSummaryViewModel>> GetSubjectSummariesAsync();
        Task<List<GroupAnalyticsSummaryViewModel>> GetGroupSummariesForSubjectAsync(int subjectId);
        Task<List<ExamAnalyticsRowViewModel>> GetExamRowsAsync(int subjectId, int groupId);
        Task<List<AdminRankingEntryViewModel>> GetSubjectGroupRankingAsync(int subjectId, int groupId);
    }
}