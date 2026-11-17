using EduTrack.Services;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduTrack.Controllers
{
    // Admin va Moderator uchun davomat tahlili — faqat ko'rish va Excel
    // eksport. Davomatni belgilash (SaveCell) bu controllerda ataylab yo'q:
    // uni faqat dars o'tadigan o'qituvchi bajaradi.
    [Authorize(Roles = "Admin,Moderator")]
    public class AdminAttendanceController : Controller
    {
        private readonly IAdminAttendanceService _adminAttendanceService;
        private readonly IAttendanceJournalExcelExporter _journalExporter;
        private readonly IAppClock _appClock;

        public AdminAttendanceController(
            IAdminAttendanceService adminAttendanceService,
            IAttendanceJournalExcelExporter journalExporter,
            IAppClock appClock)
        {
            _adminAttendanceService = adminAttendanceService;
            _journalExporter = journalExporter;
            _appClock = appClock;
        }

        // Umumiy ko'rinish: oylik KPI va barcha fan-guruh juftliklari.
        [HttpGet]
        public async Task<IActionResult> Index(int? subjectId, int? groupId, DateTime? month)
        {
            var model = await _adminAttendanceService.GetOverviewAsync(
                new AdminAttendanceFilterViewModel
                {
                    SubjectId = subjectId,
                    GroupId = groupId,
                    Month = month
                });

            return View(model);
        }

        // Bitta (fan, guruh) bo'yicha o'quvchi × kun matritsasi.
        [HttpGet]
        public async Task<IActionResult> Detail(int subjectId, int groupId, DateTime? month)
        {
            var model = await _adminAttendanceService.GetDetailAsync(subjectId, groupId, month);
            if (model is null)
            {
                return NotFound();
            }

            return View(model);
        }

        // Bitta kun bo'yicha: qaysi darsda kim kelmagan / belgilanmagan.
        [HttpGet]
        public async Task<IActionResult> Daily(DateTime? date, int? subjectId, int? groupId)
        {
            var model = await _adminAttendanceService.GetDailyAsync(date, subjectId, groupId);
            return View(model);
        }

        // Excel eksport — GET, faqat o'qish amali. O'qituvchi jurnalining
        // mavjud eksportchisi qayta ishlatiladi.
        [HttpGet]
        public async Task<IActionResult> Export(int subjectId, int groupId, DateTime? month)
        {
            var model = await _adminAttendanceService.GetDetailAsync(subjectId, groupId, month);
            if (model is null)
            {
                return NotFound();
            }

            if (model.Journal.Rows.Count == 0 || model.Journal.Days.Count == 0)
            {
                TempData["Error"] = "Eksport qilish uchun davomat ma'lumotlari topilmadi.";
                return RedirectToAction(nameof(Detail), new
                {
                    subjectId,
                    groupId,
                    month = month?.ToString("yyyy-MM")
                });
            }

            var content = _journalExporter.Export(model.Journal, _appClock.Now);
            var fileName = _journalExporter.BuildFileName(model.Journal);

            return File(
                content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
    }
}
