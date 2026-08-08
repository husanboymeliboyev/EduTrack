using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace EduTrack.Services
{
    public class FileUploadService : IFileUploadService
    {
        // Ruxsat etilgan fayl turlari — tizimdagi barcha fayl yuklash joylarida (topshiriq,
        // talaba ishi va h.k.) shu bitta ro'yxat ishlatiladi.
        private static readonly string[] AllowedExtensions =
        {
            ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".zip", ".rar"
        };

        private readonly IWebHostEnvironment _environment;

        public FileUploadService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<FileUploadResult> UploadAsync(IFormFile? file, long maxSizeBytes)
        {
            if (file == null || file.Length == 0)
            {
                // Fayl umuman yuborilmagan — bu xato emas, chunki ba'zi joylarda fayl ixtiyoriy.
                // Chaqiruvchi kod buni o'zi hal qiladi (masalan "fayl tanlanmagan" xabarini ko'rsatib).
                return FileUploadResult.Fail(string.Empty);
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                return FileUploadResult.Fail(
                    "Ruxsat etilmagan fayl turi. Faqat PDF, Word, PowerPoint, Excel, rasm yoki arxiv fayllarini yuklash mumkin.");
            }

            if (file.Length > maxSizeBytes)
            {
                var maxMb = maxSizeBytes / (1024 * 1024);
                return FileUploadResult.Fail($"Fayl hajmi {maxMb} MB dan oshmasligi kerak.");
            }

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return FileUploadResult.Ok($"uploads/{uniqueFileName}", file.FileName);
        }
    }
}
