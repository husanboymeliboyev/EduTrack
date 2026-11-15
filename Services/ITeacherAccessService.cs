using EduTrack.Models;

namespace EduTrack.Services
{
    /// <summary>
    /// "(Fan, Guruh)" juftligi — o'qituvchi aniq shu juftlikda dars o'tadi.
    /// Bitta fanga bir nechta o'qituvchi biriktirilganda (masalan, Matematika
    /// 1-smena va 2-smenada har xil o'qituvchilar bilan) juftliklar
    /// <see cref="Models.GroupSubject.TeacherId"/> orqali ajratiladi.
    /// </summary>
    public sealed record TeacherSubjectPair(int SubjectId, int GroupId);

    /// <summary>
    /// O'qituvchining fanlarga egalik tekshiruvi. Ikki daraja bor:
    /// <list type="bullet">
    /// <item>Fan darajasi — <see cref="OwnsSubjectAsync"/>, <see cref="GetOwnedSubjectIdsAsync"/>:
    /// o'qituvchi fan egasi bo'lsa YOKI shu fanning istalgan guruhini o'qitsa, fan "unik"
    /// hisoblanadi (savol banklari, imtihonlar, topshiriqlar kabi fan darajasidagi
    /// resurslar ko'p o'qituvchi orasida umumiy bo'ladi).</item>
    /// <item>Juftlik darajasi — <see cref="OwnsPairAsync"/>, <see cref="GetOwnedPairsAsync"/>:
    /// aniq "(fan, guruh)" juftligini kim o'qitayotgani (davomat jurnali, jadval,
    /// oylik reyting kabi guruh-scoped amallar uchun).</item>
    /// </list>
    /// </summary>
    public interface ITeacherAccessService
    {
        /// <summary>O'qituvchiga tegishli (yoki o'qitayotgan) barcha fanlarni qaytaradi (dropdown ro'yxatlar uchun).</summary>
        Task<List<Subject>> GetTeacherSubjectsAsync(string teacherId);

        /// <summary>O'qituvchining fan Id'lari — so'rov filtrlari uchun (Contains).</summary>
        Task<List<int>> GetOwnedSubjectIdsAsync(string teacherId);

        /// <summary>Berilgan fan o'qituvchiga tegishli ekanini tekshiradi (fan darajasi).</summary>
        Task<bool> OwnsSubjectAsync(string teacherId, int subjectId);

        /// <summary>
        /// Fan o'qituvchiga tegishli bo'lsa, uni qaytaradi; aks holda null.
        /// Bir vaqtning o'zida ham tekshiruv, ham ma'lumot (masalan fan nomi) kerak bo'lgan joylar uchun qulay.
        /// </summary>
        Task<Subject?> GetOwnedSubjectAsync(string teacherId, int subjectId);

        /// <summary>O'qituvchining barcha "(fan, guruh)" juftliklari.</summary>
        Task<List<TeacherSubjectPair>> GetOwnedPairsAsync(string teacherId);

        /// <summary>
        /// O'qituvchi aniq shu "(fan, guruh)" juftligini o'qitayotganini tekshiradi.
        /// groupId null bo'lsa fan darajasidagi tekshiruv qilinadi.
        /// </summary>
        Task<bool> OwnsPairAsync(string teacherId, int subjectId, int? groupId);
    }
}
