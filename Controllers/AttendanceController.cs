using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITeacherAccessService _teacherAccessService;

        public AttendanceController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITeacherAccessService teacherAccessService)
        {
            _context = context;
            _userManager = userManager;
            _teacherAccessService = teacherAccessService;
        }

        // Fan va guruh tanlash sahifasi
        public async Task<IActionResult> Index()
        {
            var teacherId = _userManager.GetUserId(User)!;

            var mySubjects = await _teacherAccessService.GetTeacherSubjectsAsync(teacherId);
            var subjectIds = mySubjects.Select(s => s.Id).ToList();

            ViewBag.Subjects = new SelectList(mySubjects, "Id", "Name");
            ViewBag.Groups = new SelectList(await _context.Groups.ToListAsync(), "Id", "Name");

            // So'nggi belgilangan davomatlar (oxirgi 5 ta sessiya).
            // Bitta o'qituvchining o'z yozuvlari doirasida bo'lgani uchun,
            // guruhlashni xotirada (in-memory) bajarish xavfsiz va soddaroq.
            var flatRecords = await _context.Attendances
                .Where(a => subjectIds.Contains(a.SubjectId))
                .Include(a => a.Subject)
                .Include(a => a.Student)
                    .ThenInclude(s => s!.Group)
                .Where(a => a.Student != null && a.Student.GroupId != null)
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            var recentSessions = flatRecords
                .GroupBy(a => new { a.Date, a.SubjectId, GroupId = a.Student!.GroupId })
                .Select(g => new RecentAttendanceSessionViewModel
                {
                    Date = g.Key.Date,
                    SubjectId = g.Key.SubjectId,
                    SubjectName = g.First().Subject?.Name ?? "",
                    GroupId = g.Key.GroupId,
                    GroupName = g.First().Student?.Group?.Name ?? "—",
                    StudentCount = g.Count()
                })
                .OrderByDescending(s => s.Date)
                .Take(5)
                .ToList();

            ViewBag.RecentSessions = recentSessions;

            return View();
        }

        // Davomat belgilash sahifasi: fan + guruh + sana tanlangandan keyin
        public async Task<IActionResult> Mark(int subjectId, int groupId, DateTime? date)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var subject = await _teacherAccessService.GetOwnedSubjectAsync(teacherId, subjectId);
            if (subject == null) { TempData["Error"] = "Iltimos, avval Fan va Guruhni tanlang."; return RedirectToAction(nameof(Index)); }

            var selectedDate = (date ?? DateTime.Today).Date;

            var students = await _context.Users
                .Where(u => u.GroupId == groupId)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var existingRecords = await _context.Attendances
                .Where(a => a.SubjectId == subjectId && a.Date == selectedDate && students.Select(s => s.Id).Contains(a.StudentId))
                .ToDictionaryAsync(a => a.StudentId, a => a.Status);

            ViewBag.SubjectId = subjectId;
            ViewBag.SubjectName = subject.Name;
            ViewBag.GroupId = groupId;
            ViewBag.Date = selectedDate;
            ViewBag.ExistingRecords = existingRecords;

            return View(students);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int subjectId, int groupId, DateTime date, Dictionary<string, string> statuses)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var ownsSubject = await _teacherAccessService.OwnsSubjectAsync(teacherId, subjectId);
            if (!ownsSubject) return Forbid();

            var selectedDate = date.Date;

            var students = await _context.Users
                .Where(u => u.GroupId == groupId)
                .ToListAsync();

            foreach (var student in students)
            {
                // Agar biror sabab bilan holat yuborilmagan bo'lsa, xavfsizlik uchun "Kelmadi" deb olamiz —
                // o'qituvchi noto'g'ri "Keldi" bilan tasodifan saqlab qo'ymasligi uchun.
                var status = AttendanceStatus.Kelmadi;
                if (statuses != null && statuses.TryGetValue(student.Id, out var statusStr) && !string.IsNullOrEmpty(statusStr))
                {
                    Enum.TryParse(statusStr, out status);
                }

                var existing = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.SubjectId == subjectId && a.Date == selectedDate && a.StudentId == student.Id);

                if (existing != null)
                {
                    existing.Status = status;
                }
                else
                {
                    _context.Attendances.Add(new Attendance
                    {
                        SubjectId = subjectId,
                        StudentId = student.Id,
                        Date = selectedDate,
                        Status = status
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Davomat saqlandi.";

            return RedirectToAction(nameof(Mark), new { subjectId, groupId, date = selectedDate.ToString("yyyy-MM-dd") });
        }
    }
}