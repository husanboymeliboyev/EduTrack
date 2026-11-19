namespace EduTrack.Models
{
    /// <summary>
    /// O'quvchi topshiriqni qanday topshirishi mumkinligi.
    /// Standart qiymat <see cref="FileOrText"/> bo'lgani uchun eski topshiriqlar
    /// avvalgidek ishlaydi, ustiga matnli javob imkoniyati ham qo'shiladi.
    /// </summary>
    public enum SubmissionMode
    {
        // Fayl ham, matn ham qabul qilinadi.
        FileOrText = 0,

        // Faqat fayl (masalan, dasturlash kodi yoki hisobot).
        FileOnly = 1,

        // Faqat matn (matematika, formulalar uchun tez va qulay).
        TextOnly = 2
    }
}
