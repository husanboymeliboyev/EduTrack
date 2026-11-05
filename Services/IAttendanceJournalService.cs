using EduTrack.ViewModels;

namespace EduTrack.Services
{
    /// <summary>
    /// Oylik davomat jurnali (talaba × kun matritsasi) uchun ma'lumot tayyorlaydi.
    /// </summary>
    public interface IAttendanceJournalService
    {
        Task<AttendanceJournalViewModel> BuildAsync(
            string teacherId,
            int? subjectId,
            int? groupId,
            DateTime? month);
    }
}
