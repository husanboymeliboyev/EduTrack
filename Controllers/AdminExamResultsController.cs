using EduTrack.Services;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduTrack.Controllers
{
    // Admin uchun imtihon natijalari — faqat ko'rish va Excel eksport.
    // Tasdiqlash (ApproveResult) bu controllerda ataylab yo'q: natijani faqat
    // fan o'qituvchisi tasdiqlay oladi.
    [Authorize(Roles = "Admin")]
    public class AdminExamResultsController : Controller
    {
        private readonly IExamResultsService _examResultsService;
        private readonly IExamResultsExcelExporter _examResultsExcelExporter;
        private readonly IAdminExamDirectoryService _adminExamDirectoryService;

        public AdminExamResultsController(
            IExamResultsService examResultsService,
            IExamResultsExcelExporter examResultsExcelExporter,
            IAdminExamDirectoryService adminExamDirectoryService)
        {
            _examResultsService = examResultsService;
            _examResultsExcelExporter = examResultsExcelExporter;
            _adminExamDirectoryService = adminExamDirectoryService;
        }

        // Barcha imtihonlar ro'yxati (arxivlanganlar ham) — qidiruv, filtr,
        // sort va sahifalash bilan. Natijalar sahifasiga o'tish uchun manba.
        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] AdminExamListFilterViewModel filter)
        {
            var model = await _adminExamDirectoryService.GetExamsAsync(
                filter ?? new AdminExamListFilterViewModel());

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Results(
            int? id,
            [FromQuery] ExamResultsFilterViewModel filter)
        {
            if (id == null) return NotFound();

            var model = await _examResultsService.GetForAdminAsync(
                id.Value,
                filter ?? new ExamResultsFilterViewModel());

            if (model == null) return NotFound();

            return View(model);
        }

        // Excel eksport — GET orqali, anti-forgery token talab qilmaydi.
        [HttpGet]
        public async Task<IActionResult> ExportResults(
            int id,
            [FromQuery] ExamResultsFilterViewModel filter)
        {
            filter ??= new ExamResultsFilterViewModel();

            var data = await _examResultsService.GetExportForAdminAsync(id, filter);
            if (data == null) return NotFound();

            var export = _examResultsExcelExporter.Export(data);
            if (!export.Succeeded)
            {
                TempData["Error"] = export.ErrorMessage;
                return RedirectToAction(
                    nameof(Results),
                    BuildResultsRouteValues(id, filter));
            }

            return File(
                export.Content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                export.FileName);
        }

        // Natijalar sahifasiga qaytishda filtr holatini saqlash uchun route qiymatlari.
        private static RouteValueDictionary BuildResultsRouteValues(
            int examId,
            ExamResultsFilterViewModel filter)
        {
            var values = new RouteValueDictionary();

            foreach (var pair in filter.ToRouteValuesWithExamId(examId))
            {
                values[pair.Key] = pair.Value;
            }

            return values;
        }
    }
}
