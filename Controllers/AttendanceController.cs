using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITeacherAccessService _teacherAccessService;
        private readonly IAttendanceService _attendanceService;
        private readonly IAttendanceJournalService _journalService;
        private readonly IAttendanceJournalExcelExporter _journalExporter;
        private readonly IAppClock _appClock;
        private readonly IStudentSubjectGroupService _subjectGroups;

        public AttendanceController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITeacherAccessService teacherAccessService,
            IAttendanceService attendanceService,
            IAttendanceJournalService journalService,
            IAttendanceJournalExcelExporter journalExporter,
            IAppClock appClock,
            IStudentSubjectGroupService subjectGroups)
        {
            _context = context;
            _userManager = userManager;
            _teacherAccessService = teacherAccessService;
            _attendanceService = attendanceService;
            _journalService = journalService;
            _journalExporter = journalExporter;
            _appClock = appClock;
            _subjectGroups = subjectGroups;
        }

        // Oylik davomat jurnali: talaba × kun matritsasi.
        public async Task<IActionResult> Index(int? subjectId, int? groupId, DateTime? month)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var model = await _journalService.BuildAsync(teacherId, subjectId, groupId, month);
            return View(model);
        }

        // Oylik jurnalni Excel (.xlsx) faylga chiqaradi. GET orqali ishlaydi,
        // anti-forgery token talab qilmaydi (faqat o'qish amali).
        [HttpGet]
        public async Task<IActionResult> ExportJournal(int? subjectId, int? groupId, DateTime? month)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var monthValue = month?.ToString("yyyy-MM");

            if (subjectId == null || groupId == null)
            {
                TempData["Error"] = "Excel eksport uchun fan va guruhni tanlang.";
                return RedirectToAction(nameof(Index), new { subjectId, groupId, month = monthValue });
            }

            if (!await _teacherAccessService.OwnsSubjectAsync(teacherId, subjectId.Value))
            {
                TempData["Error"] = "Bu fan sizga tegishli emas.";
                return RedirectToAction(nameof(Index));
            }

            if (!await _attendanceService.IsGroupSubjectAsync(groupId.Value, subjectId.Value))
            {
                TempData["Error"] = "Tanlangan guruh bu fanni o'qimaydi.";
                return RedirectToAction(nameof(Index), new { subjectId, month = monthValue });
            }

            var model = await _journalService.BuildAsync(teacherId, subjectId, groupId, month);

            if (model.Rows.Count == 0 || model.Days.Count == 0)
            {
                TempData["Error"] = "Eksport qilish uchun ma'lumot topilmadi.";
                return RedirectToAction(nameof(Index), new { subjectId, groupId, month = monthValue });
            }

            var content = _journalExporter.Export(model, _appClock.Now);
            var fileName = _journalExporter.BuildFileName(model);

            return File(
                content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // Davomat belgilash sahifasi: fan + guruh + sana tanlangandan keyin
        public async Task<IActionResult> Mark(int subjectId, int groupId, DateTime? date)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var subject = await _teacherAccessService.GetOwnedSubjectAsync(teacherId, subjectId);
            if (subject == null) { TempData["Error"] = "Iltimos, avval Fan va Guruhni tanlang."; return RedirectToAction(nameof(Index)); }

            if (!await _attendanceService.IsGroupSubjectAsync(groupId, subjectId))
            {
                TempData["Error"] = "Tanlangan guruh bu fanni o'qimaydi.";
                return RedirectToAction(nameof(Index));
            }

            var selectedDate = (date ?? _appClock.Today).Date;

            var dateError = _attendanceService.ValidateDate(selectedDate);
            if (dateError != null)
            {
                TempData["Error"] = dateError;
                return RedirectToAction(nameof(Index));
            }

            var studentIdsInGroup = await _subjectGroups
                .GetStudentIdsAsync(groupId, subjectId, selectedDate);
            var students = await _context.Users
                .Where(u => studentIdsInGroup.Contains(u.Id))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var existingRecords = await _context.Attendances
                .Where(a => a.SubjectId == subjectId && a.Date == selectedDate && students.Select(s => s.Id).Contains(a.StudentId))
                .ToDictionaryAsync(a => a.StudentId, a => a.Status);

            ViewBag.SubjectId = subjectId;
            ViewBag.SubjectName = subject.Name;
            ViewBag.GroupId = groupId;
            ViewBag.GroupName = await _context.Groups
                .Where(g => g.Id == groupId)
                .Select(g => g.Name)
                .FirstOrDefaultAsync() ?? string.Empty;
            ViewBag.Date = selectedDate;
            ViewBag.ExistingRecords = existingRecords;

            return View(students);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCell(int subjectId, int groupId, string studentId, DateTime date, string? status)
        {
            var teacherId = _userManager.GetUserId(User)!;
            if (!await _teacherAccessService.OwnsSubjectAsync(teacherId, subjectId))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { ok = false, message = "Ruxsat yo'q." });
            }

            var result = await _attendanceService.SaveCellAsync(subjectId, groupId, studentId, date, status);
            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = result.ErrorMessage ?? "Saqlab bo'lmadi."
                });
            }

            return Json(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int subjectId, int groupId, DateTime date, Dictionary<string, string?>? statuses)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var ownsSubject = await _teacherAccessService.OwnsSubjectAsync(teacherId, subjectId);
            if (!ownsSubject) return Forbid();

            var result = await _attendanceService.SaveAsync(subjectId, groupId, date, statuses);

            if (!result.Succeeded)
            {
                TempData["Error"] = result.ErrorMessage ?? "Davomatni saqlab bo'lmadi.";
                return RedirectToAction(nameof(Mark), new { subjectId, groupId, date = date.ToString("yyyy-MM-dd") });
            }

            TempData["Success"] = "Davomat saqlandi.";

            return RedirectToAction(nameof(Mark), new { subjectId, groupId, date = date.ToString("yyyy-MM-dd") });
        }
    }
}