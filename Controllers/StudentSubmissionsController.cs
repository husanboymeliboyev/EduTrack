using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentSubmissionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileUploadService _fileUploadService;
        private readonly IAppClock _appClock;
        private readonly IStudentSubjectGroupService _subjectGroups;

        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        public StudentSubmissionsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IFileUploadService fileUploadService,
            IAppClock appClock,
            IStudentSubjectGroupService subjectGroups)
        {
            _context = context;
            _userManager = userManager;
            _fileUploadService = fileUploadService;
            _appClock = appClock;
            _subjectGroups = subjectGroups;
        }

        // Talabaning barcha topshiriqlari (o'z guruhi fanlari bo'yicha)
        // Talabaning barcha topshiriqlari (o'z guruhi fanlari bo'yicha)
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return View(new List<Assignment>());
            }

            // O'quvchi o'qiydigan fanlar (fan bo'yicha guruhlari bilan).
            var mySubjectIds = await _subjectGroups.GetSubjectIdsAsync(user.Id);
            if (mySubjectIds.Count == 0)
            {
                return View(new List<Assignment>());
            }

            var groupMap = await _subjectGroups.GetGroupMapAsync(user.Id, mySubjectIds);

            var assignments = await _context.Assignments
                .Include(a => a.Subject)
                .Include(a => a.Submissions.Where(s => s.StudentId == user.Id))
                .Where(a => mySubjectIds.Contains(a.SubjectId))
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            assignments = assignments
                .Where(a => a.GroupId == null
                            || (groupMap.TryGetValue(a.SubjectId, out var groupId) && a.GroupId == groupId))
                .ToList();

            return View(assignments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int assignmentId, IFormFile file)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            // Topshiriq haqiqatan ham mavjudligini va o'quvchining shu fandagi
            // guruhiga tegishli ekanini tekshiramiz.
            var assignment = await _context.Assignments
                .FirstOrDefaultAsync(a => a.Id == assignmentId);
            if (assignment == null)
            {
                TempData["Error"] = "Topshiriq topilmadi.";
                return RedirectToAction(nameof(Index));
            }

            var resolvedGroupId = await _subjectGroups.GetGroupIdAsync(user.Id, assignment.SubjectId);
            var canSubmit = resolvedGroupId is int groupId
                && await _subjectGroups.IsStudentInGroupSubjectAsync(user.Id, groupId, assignment.SubjectId)
                && (assignment.GroupId == null || assignment.GroupId == groupId);

            if (!canSubmit)
            {
                TempData["Error"] = "Bu topshiriq sizning guruhingizga tegishli emas.";
                return RedirectToAction(nameof(Index));
            }

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Fayl tanlanmagan.";
                return RedirectToAction(nameof(Index));
            }

            if (assignment.DueDate < _appClock.Now)
            {
                TempData["Error"] = $"Topshirish muddati tugagan ({assignment.DueDate:dd.MM.yyyy HH:mm}). " +
                    "Kechikkan ishni yuklash uchun o'qituvchi bilan bog'laning.";
                return RedirectToAction(nameof(Index));
            }

            var uploadResult = await _fileUploadService.UploadAsync(file, MaxFileSizeBytes);
            if (!uploadResult.Success)
            {
                TempData["Error"] = uploadResult.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            var existing = await _context.Submissions
                .FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == user.Id);

            string? previousFilePath = null;
            if (existing != null)
            {
                previousFilePath = existing.FilePath;
                existing.FilePath = uploadResult.RelativePath;
                existing.FileName = uploadResult.OriginalFileName;
                existing.SubmittedDate = _appClock.Now;
            }
            else
            {
                _context.Submissions.Add(new Submission
                {
                    AssignmentId = assignmentId,
                    StudentId = user.Id,
                    FilePath = uploadResult.RelativePath,
                    FileName = uploadResult.OriginalFileName,
                    SubmittedDate = _appClock.Now
                });
            }

            await _context.SaveChangesAsync();

            // Eski fayl endi bazada ishlatilmaydi — diskda qoldirib yubormaymiz.
            if (!string.IsNullOrWhiteSpace(previousFilePath)
                && !string.Equals(previousFilePath, uploadResult.RelativePath, StringComparison.OrdinalIgnoreCase))
            {
                await _fileUploadService.DeleteAsync(previousFilePath);
            }

            TempData["Success"] = "Topshiriq muvaffaqiyatli yuklandi.";
            return RedirectToAction(nameof(Index));
        }
    }
}
