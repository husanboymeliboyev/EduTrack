using Microsoft.AspNetCore.Http;

namespace EduTrack.Services
{
    /// <summary>
    /// Fayl yuklashni markazlashtiruvchi xizmat. Bu interfeys tufayli fayl turi, hajmi kabi
    /// xavfsizlik tekshiruvlari BITTA joyda yoziladi va barcha kontrollerlarda bir xil qo'llaniladi —
    /// avval bo'lgani kabi bitta kontrollerda tekshiruv borligi-yu, boshqasida yo'qligi kabi
    /// nomuvofiqliklarning oldini oladi.
    /// </summary>
    public interface IFileUploadService
    {
        /// <summary>
        /// Faylni tekshiradi (turi va hajmi bo'yicha) va to'g'ri bo'lsa wwwroot/uploads papkasiga saqlaydi.
        /// </summary>
        /// <param name="file">Yuklanayotgan fayl (bo'sh bo'lishi ham mumkin)</param>
        /// <param name="maxSizeBytes">Ruxsat etilgan maksimal hajm (baytlarda)</param>
        Task<FileUploadResult> UploadAsync(IFormFile? file, long maxSizeBytes);

        /// <summary>
        /// Ilgari yuklangan faylni wwwroot/uploads papkasidan o'chiradi.
        /// Faqat shu papka ichidagi yo'llar qabul qilinadi — boshqa yo'llar e'tiborsiz qoldiriladi.
        /// Fayl topilmasa yoki o'chirib bo'lmasa, xato tashlanmaydi (yuklash oqimi buzilmasligi uchun).
        /// </summary>
        /// <param name="relativePath">UploadAsync qaytargan nisbiy yo'l (masalan "uploads/abc_file.pdf")</param>
        Task DeleteAsync(string? relativePath);
    }
}
