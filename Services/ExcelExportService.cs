using ClosedXML.Excel;
using EduTrack.ViewModels;

namespace EduTrack.Services
{
    /// <summary>
    /// Yaratilgan Login ID / parol ro'yxatlarini Excel (.xlsx) faylga aylantiradi —
    /// Admin buni chop etib yoki saqlab, talabalarga tarqatishi uchun.
    /// </summary>
    public class ExcelExportService : IExcelExportService
    {
        public byte[] ExportCredentials(List<UserCredentialsViewModel> credentials, string sheetName = "Hisoblar")
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add(sheetName);

            ws.Cell(1, 1).Value = "Ism-familiya";
            ws.Cell(1, 2).Value = "Login ID";
            ws.Cell(1, 3).Value = "Parol";
            ws.Cell(1, 4).Value = "Rol";

            var headerRow = ws.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#E6F4F1");

            int row = 2;
            foreach (var c in credentials)
            {
                ws.Cell(row, 1).Value = c.FullName;
                ws.Cell(row, 2).Value = c.LoginId;
                ws.Cell(row, 3).Value = c.Password;
                ws.Cell(row, 4).Value = c.Role;
                row++;
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}