using EduTrack.ViewModels;

namespace EduTrack.Services
{
    /// <summary>
    /// Oylik davomat jurnalini Excel (.xlsx) faylga chiqaradi.
    /// </summary>
    public interface IAttendanceJournalExcelExporter
    {
        byte[] Export(AttendanceJournalViewModel journal, DateTime generatedAt);

        string BuildFileName(AttendanceJournalViewModel journal);
    }
}
