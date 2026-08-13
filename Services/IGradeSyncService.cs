namespace EduTrack.Services
{
    public interface IGradeSyncService
    {
        Task SyncFromSubmissionAsync(int submissionId);
        Task SyncFromExamResultAsync(int examResultId);
        Task SyncAllForAssignmentAsync(int assignmentId);
        Task SyncAllForExamAsync(int examId);
    }
}