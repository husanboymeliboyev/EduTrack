using EduTrack.Data;
using EduTrack.Models;
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

        public AttendanceController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Fan va guruh tanlash sahifasi
        public async Task<IActionResult> Index()
        {
            var teacherId = _userManager.GetUserId(User);

            var mySubjects = await _context.Subjects
                .Where(s => s.TeacherId == teacherId)
                .ToListAsync();

            ViewBag.Subjects = new SelectList(mySubjects, "Id", "Name");
            ViewBag.Groups = new SelectList(await _context.Groups.ToListAsync(), "Id", "Name");

            return View();
        }

        // Davomat belgilash sahifasi: fan + guruh + sana tanlangandan keyin
        public async Task<IActionResult> Mark(int subjectId, int groupId, DateTime? date)
        {
            var teacherId = _userManager.GetUserId(User);
            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId && s.TeacherId == teacherId);
            if (subject == null) return Forbid();

            var selectedDate = (date ?? DateTime.Today).Date;

            var students = await _context.Users
                .Where(u => u.GroupId == groupId)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var existingRecords = await _context.Attendances
                .Where(a => a.SubjectId == subjectId && a.Date == selectedDate && students.Select(s => s.Id).Contains(a.StudentId))
                .ToDictionaryAsync(a => a.StudentId, a => a.IsPresent);

            ViewBag.SubjectId = subjectId;
            ViewBag.SubjectName = subject.Name;
            ViewBag.GroupId = groupId;
            ViewBag.Date = selectedDate;
            ViewBag.ExistingRecords = existingRecords;

            return View(students);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int subjectId, int groupId, DateTime date, List<string> presentStudentIds)
        {
            var teacherId = _userManager.GetUserId(User);
            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId && s.TeacherId == teacherId);
            if (subject == null) return Forbid();

            var selectedDate = date.Date;

            var students = await _context.Users
                .Where(u => u.GroupId == groupId)
                .ToListAsync();

            foreach (var student in students)
            {
                var existing = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.SubjectId == subjectId && a.Date == selectedDate && a.StudentId == student.Id);

                bool isPresent = presentStudentIds != null && presentStudentIds.Contains(student.Id);

                if (existing != null)
                {
                    existing.IsPresent = isPresent;
                }
                else
                {
                    _context.Attendances.Add(new Attendance
                    {
                        SubjectId = subjectId,
                        StudentId = student.Id,
                        Date = selectedDate,
                        IsPresent = isPresent
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Davomat saqlandi.";

            return RedirectToAction(nameof(Mark), new { subjectId, groupId, date = selectedDate.ToString("yyyy-MM-dd") });
        }
    }
}