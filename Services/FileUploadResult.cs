namespace EduTrack.Services
{
    /// <summary>
    /// Fayl yuklash amalining natijasi. Muvaffaqiyatli bo'lsa RelativePath va OriginalFileName
    /// to'ldiriladi, muvaffaqiyatsiz bo'lsa ErrorMessage foydalanuvchiga ko'rsatish uchun tayyor bo'ladi.
    /// </summary>
    public class FileUploadResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RelativePath { get; set; }
        public string? OriginalFileName { get; set; }

        public static FileUploadResult Fail(string message) => new() { Success = false, ErrorMessage = message };

        public static FileUploadResult Ok(string relativePath, string originalFileName) => new()
        {
            Success = true,
            RelativePath = relativePath,
            OriginalFileName = originalFileName
        };
    }
}
