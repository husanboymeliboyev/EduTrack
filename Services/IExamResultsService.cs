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

        Task<ExamResultsPageViewModel?> GetForAdminAsync(
            int examId,
            ExamResultsFilterViewModel filter);

        // Excel eksport uchun filtrga mos barcha qatorlar (sahifalashsiz).
        Task<ExamResultsExportData?> GetExportForTeacherAsync(
            string teacherId,
            int examId,
            ExamResultsFilterViewModel filter);

        Task<ExamResultsExportData?> GetExportForAdminAsync(
            int examId,
            ExamResultsFilterViewModel filter);
    }
}
