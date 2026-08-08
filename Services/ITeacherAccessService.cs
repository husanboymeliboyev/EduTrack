using EduTrack.Models;

namespace EduTrack.Services
{
    /// <summary>
    /// "Bu fan aynan shu o'qituvchiga tegishlimi?" tekshiruvi tizimda 20 dan ortiq joyda
    /// deyarli bir xil kod bilan takrorlangan edi. Bu xizmat o'sha mantiqni bitta joyga
    /// jamlaydi — kelajakda tekshiruv qoidasi o'zgarsa (masalan, bitta fanga bir nechta
    /// o'qituvchi biriktirilishi kerak bo'lsa), faqat shu faylni o'zgartirish kifoya bo'ladi.
    /// </summary>
    public interface ITeacherAccessService
    {
        /// <summary>O'qituvchiga biriktirilgan barcha fanlarni qaytaradi (dropdown ro'yxatlar uchun).</summary>
        Task<List<Subject>> GetTeacherSubjectsAsync(string teacherId);

        /// <summary>Berilgan fan aynan shu o'qituvchiga tegishli ekanini tekshiradi.</summary>
        Task<bool> OwnsSubjectAsync(string teacherId, int subjectId);

        /// <summary>
        /// Fan shu o'qituvchiga tegishli bo'lsa, uni qaytaradi; aks holda null.
        /// Bir vaqtning o'zida ham tekshiruv, ham ma'lumot (masalan fan nomi) kerak bo'lgan joylar uchun qulay.
        /// </summary>
        Task<Subject?> GetOwnedSubjectAsync(string teacherId, int subjectId);
    }
}
