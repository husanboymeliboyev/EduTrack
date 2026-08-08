using EduTrack.ViewModels;

namespace EduTrack.Services
{
    public interface IExcelExportService
    {
        // Login ID / parol ro'yxatini .xlsx fayl baytlariga aylantiradi
        byte[] ExportCredentials(List<UserCredentialsViewModel> credentials, string sheetName = "Hisoblar");
    }
}