using EduTrack.ViewModels;

namespace EduTrack.Services
{
    // Filtrga mos natijalarni Excel (.xlsx) faylga aylantiradi.
    public interface IExamResultsExcelExporter
    {
        ExamResultsExcelExportResult Export(ExamResultsExportData data);

        string BuildFileName(string examTitle, DateTime generatedAt);
    }
}
