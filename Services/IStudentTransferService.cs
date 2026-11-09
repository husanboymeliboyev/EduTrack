using EduTrack.Models;

namespace EduTrack.Services
{
    public class TransferRequest
    {
        public string StudentId { get; set; } = string.Empty;

        /// <summary>Yangi sinf (Type=Class). null — sinfni o'zgartirmaslik.</summary>
        public int? NewClassGroupId { get; set; }

        /// <summary>Yangi yo'nalish guruhi (Type=Stream). null — guruhni o'zgartirmaslik.</summary>
        public int? NewStreamGroupId { get; set; }

        public DateTime Date { get; set; }
        public string? Reason { get; set; }
    }

    public class TransferResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public List<string> Changes { get; set; } = new();
    }

    public class StudentTransferDetails
    {
        public string StudentId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? LoginId { get; set; }
        public Group? CurrentClass { get; set; }
        public Group? CurrentStream { get; set; }
    }

    public class TransferCandidate
    {
        public string StudentId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? LoginId { get; set; }
        public string? CurrentClassName { get; set; }
        public string? CurrentStreamName { get; set; }
    }

    /// <summary>
    /// O'quvchini bitta sinfdan/guruhdan boshqasiga o'tkazish.
    /// Eski a'zolik o'chirilmaydi: EndDate va EndReason to'ldiriladi, tarix saqlanadi.
    /// </summary>
    public interface IStudentTransferService
    {
        Task<StudentTransferDetails?> GetDetailsAsync(string studentId);

        Task<List<TransferCandidate>> SearchAsync(string? term, int limit = 50);

        Task<TransferResult> TransferAsync(TransferRequest request);
    }
}
