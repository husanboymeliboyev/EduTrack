using EduTrack.ViewModels;

namespace EduTrack.Services
{
    public interface IExcelExportService
    {
        byte[] ExportCredentials(List<UserCredentialsViewModel> credentials, string sheetName = "Hisoblar");

        byte[] CreateNamesTemplate();
    }
}