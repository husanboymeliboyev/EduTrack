namespace EduTrack.ViewModels
{
    // Excel eksport natijasi. Xatolik bo'lsa Content bo'sh, ErrorMessage to'ldiriladi.
    public sealed class ExamResultsExcelExportResult
    {
        public bool Succeeded { get; init; }
        public byte[] Content { get; init; } = Array.Empty<byte>();
        public string FileName { get; init; } = string.Empty;
        public string? ErrorMessage { get; init; }

        public static ExamResultsExcelExportResult Success(byte[] content, string fileName) => new()
        {
            Succeeded = true,
            Content = content,
            FileName = fileName
        };

        public static ExamResultsExcelExportResult Failure(string errorMessage, string fileName) => new()
        {
            Succeeded = false,
            FileName = fileName,
            ErrorMessage = errorMessage
        };
    }
}
