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

        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        public StudentSubmissionsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IFileUploadService fileUploadService)
        {
            _context = context;
            _userManager = userManager;
            _fileUploadService = fileUploadService;
        }

        // Talabaning barcha topshiriqlari (o'z guruhi fanlari bo'yicha)
        // Talabaning barcha topshiriqlari (o'z guruhi fanlari bo'yicha)
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.GroupId == null)
            {
                return View(new List<Assignment>());
            }

            // Talaba guruhiga tegishli fanlar ID'larini olamiz
            var mySubjectIds = await _context.GroupSubjects
                .Where(gs => gs.GroupId == user.GroupId)
                .Select(gs => gs.SubjectId)
                .ToListAsync();

            var assignments = await _context.Assignments
                .Include(a => a.Subject)
                .Include(a => a.Submissions.Where(s => s.StudentId == user.Id))
                .Where(a => mySubjectIds.Contains(a.SubjectId))
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            return View(assignments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int assignmentId, IFormFile file)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            // Topshiriq haqiqatan ham mavjudligini tekshiramiz (noto'g'ri/soxta ID yuborilishining oldini olish)
            var assignmentExists = await _context.Assignments.AnyAsync(a => a.Id == assignmentId);
            if (!assignmentExists)
            {
                TempData["Error"] = "Topshiriq topilmadi.";
                return RedirectToAction(nameof(Index));
            }

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Fayl tanlanmagan.";
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

            if (existing != null)
            {
                existing.FilePath = uploadResult.RelativePath;
                existing.FileName = uploadResult.OriginalFileName;
                existing.SubmittedDate = DateTime.Now;
            }
            else
            {
                _context.Submissions.Add(new Submission
                {
                    AssignmentId = assignmentId,
                    StudentId = user.Id,
                    FilePath = uploadResult.RelativePath,
                    FileName = uploadResult.OriginalFileName,
                    SubmittedDate = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Topshiriq muvaffaqiyatli yuklandi.";
            return RedirectToAction(nameof(Index));
        }
    }
}
