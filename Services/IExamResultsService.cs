using EduTrack.ViewModels;

namespace EduTrack.Services
{
    // Imtihon natijalarini filtr/sort/sahifalash bilan o'qish.
    // O'qituvchi uchun egalik tekshiruvi servis ichida bajariladi; admin uchun ochiq.
    public interface IExamResultsService
    {
        Task<ExamResultsPageViewModel?> GetForTeacherAsync(
            string teacherId,
            int examId,
            ExamResultsFilterViewModel filter);

        // approvedOnly = true bo'lsa faqat o'qituvchi tasdiqlagan natijalar
        // qaytadi va tasdiqlangan natijasi yo'q imtihon uchun null (moderator).
        Task<ExamResultsPageViewModel?> GetForAdminAsync(
            int examId,
            ExamResultsFilterViewModel filter,
            bool approvedOnly = false);

        // Excel eksport uchun filtrga mos barcha qatorlar (sahifalashsiz).
        Task<ExamResultsExportData?> GetExportForTeacherAsync(
            string teacherId,
            int examId,
            ExamResultsFilterViewModel filter);

        Task<ExamResultsExportData?> GetExportForAdminAsync(
            int examId,
            ExamResultsFilterViewModel filter,
            bool approvedOnly = false);
    }
}
