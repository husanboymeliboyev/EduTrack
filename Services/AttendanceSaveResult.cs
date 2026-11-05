namespace EduTrack.Services
{
    public enum AttendanceSaveStatus
    {
        Success = 0,
        GroupSubjectMismatch = 1,
        DateOutOfRange = 2,
        InvalidStatus = 3,
        StudentNotInGroup = 4
    }

    /// <summary>
    /// Davomatni saqlash natijasi. Xato bo'lsa hech qanday yozuv saqlanmaydi
    /// (butun amal rad etiladi), shuning uchun <see cref="CreatedCount"/> va
    /// <see cref="UpdatedCount"/> faqat muvaffaqiyatli holatda ma'noga ega.
    /// </summary>
    public sealed class AttendanceSaveResult
    {
        public AttendanceSaveStatus Status { get; private init; }
        public string? ErrorMessage { get; private init; }
        public int CreatedCount { get; private init; }
        public int UpdatedCount { get; private init; }

        public bool Succeeded => Status == AttendanceSaveStatus.Success;

        public static AttendanceSaveResult Success(int createdCount, int updatedCount) => new()
        {
            Status = AttendanceSaveStatus.Success,
            CreatedCount = createdCount,
            UpdatedCount = updatedCount
        };

        public static AttendanceSaveResult Fail(AttendanceSaveStatus status, string message) => new()
        {
            Status = status,
            ErrorMessage = message
        };
    }
}
