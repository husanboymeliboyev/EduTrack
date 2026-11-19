using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace EduTrack.Services
{
    public class FileUploadService : IFileUploadService
    {
        // Ruxsat etilgan fayl turlari — tizimdagi barcha fayl yuklash joylarida (topshiriq,
        // talaba ishi va h.k.) shu bitta ro'yxat ishlatiladi.
        // Hujjat/rasm/arxiv bilan birga dasturlash fayllari ham qo'llab-quvvatlanadi:
        // Python, C/C++, Java, JavaScript/TypeScript, C#, Go, Rust, SQL va boshqalar.
        public static readonly string[] AllowedExtensions =
        {
            // Hujjatlar
            ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx",
            ".odt", ".ods", ".odp", ".rtf", ".txt", ".md", ".csv",
            // Rasmlar
            ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp",
            // Arxivlar
            ".zip", ".rar", ".7z",
            // Dasturlash va texnik fayllar
            ".py", ".ipynb", ".cpp", ".cc", ".cxx", ".c", ".h", ".hpp",
            ".java", ".kt", ".kts", ".js", ".jsx", ".ts", ".tsx", ".cs",
            ".go", ".rs", ".rb", ".php", ".swift", ".lua", ".r", ".m",
            ".sql", ".json", ".xml", ".yml", ".yaml", ".html", ".css",
            ".scss", ".sh", ".bat", ".ps1", ".asm", ".pas"
        };

        // <input type="file" accept="..."> uchun tayyor ro'yxat.
        public static string AcceptAttribute => string.Join(",", AllowedExtensions);

        // Foydalanuvchiga ko'rsatiladigan qisqa tavsif (fayl maydoni tagida).
        public const string AllowedTypesHint =
            "PDF, Word, PowerPoint, Excel, rasm, arxiv hamda kod fayllari (.py, .cpp, .c, .cs, .java, .js, .ts, .go, .rs, .sql va boshqalar)";

        /// <summary>Fayl kengaytmasi ruxsat etilganlar ro'yxatida bor-yo'qligini tekshiradi.</summary>
        public static bool IsAllowedExtension(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension.Length > 0 && AllowedExtensions.Contains(extension);
        }

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
                    "Ruxsat etilmagan fayl turi. " + AllowedTypesHint + " fayllarini yuklash mumkin.");
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

        public Task DeleteAsync(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return Task.CompletedTask;

            var uploadsFolder = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads"));
            var normalized = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, normalized));

            // Xavfsizlik: faqat uploads papkasi ichidagi fayllarni o'chirishga ruxsat.
            if (!fullPath.StartsWith(uploadsFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            try
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch (IOException)
            {
                // Fayl band bo'lishi mumkin — bu holda keyingi urinishda tozalanadi.
            }
            catch (UnauthorizedAccessException)
            {
                // Ruxsat yo'q — yuklash allaqachon yakunlangan, foydalanuvchini xato bilan bezovta qilmaymiz.
            }

            return Task.CompletedTask;
        }

        public string? ResolvePath(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;

            var uploadsFolder = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads"));
            var normalized = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, normalized));

            // Xavfsizlik: faqat uploads papkasi ichidagi fayllarga ruxsat (path-traversal himoyasi).
            if (!fullPath.StartsWith(uploadsFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return File.Exists(fullPath) ? fullPath : null;
        }

        public string GetContentType(string fileName)
        {
            var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(fileName, out var contentType))
            {
                contentType = "application/octet-stream";
            }
            return contentType;
        }
    }
}
