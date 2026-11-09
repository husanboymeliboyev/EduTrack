using EduTrack.ViewModels;

namespace EduTrack.Services
{
    /// <summary>
    /// 100 ballik yakuniy baho uchun yagona o'qish/yozish nuqtasi.
    ///
    /// Qoidalar:
    /// • O'qituvchi qo'lda qo'ygan baho ustuvor.
    /// • Qo'lda baho bo'lmasa, fan bo'yicha tasdiqlangan imtihon
    ///   natijalarining o'rtachasi olinadi. Har bir imtihon uchun eng yaxshi
    ///   urinish hisobga olinadi.
    /// • Davomat va topshiriqlar bu bahoga umuman ta'sir qilmaydi.
    /// </summary>
    public interface ISubjectGradeService
    {
        /// <summary>Bitta o'quvchining fanlar bo'yicha yakuniy baholari.</summary>
        Task<IReadOnlyDictionary<int, StudentSubjectGradeViewModel>> GetStudentGradesAsync(
            string studentId,
            IReadOnlyCollection<int> subjectIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir nechta o'quvchi uchun fan baholari (reyting hisoblashda N+1
        /// so'rovlardan qochish uchun). Kalit — o'quvchi Id'si.
        /// </summary>
        Task<IReadOnlyDictionary<string, IReadOnlyDictionary<int, StudentSubjectGradeViewModel>>> GetGradesForStudentsAsync(
            IReadOnlyCollection<string> studentIds,
            IReadOnlyCollection<int> subjectIds,
            CancellationToken cancellationToken = default);

        /// <summary>Qo'lda 100 ballik bahoni saqlash (mavjud bo'lsa yangilanadi).</summary>
        Task SaveManualGradeAsync(
            string studentId,
            int subjectId,
            double score,
            string? teacherId,
            CancellationToken cancellationToken = default);
    }
}
