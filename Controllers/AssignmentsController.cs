using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class AssignmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileUploadService _fileUploadService;
        private readonly ITeacherAccessService _teacherAccessService;

        private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

        public AssignmentsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IFileUploadService fileUploadService,
            ITeacherAccessService teacherAccessService)
        {
            _context = context;
            _userManager = userManager;
            _fileUploadService = fileUploadService;
            _teacherAccessService = teacherAccessService;
        }

        // O'qituvchining barcha topshiriqlari
        public async Task<IActionResult> Index()
        {
            var teacherId = _userManager.GetUserId(User);

            var assignments = await _context.Assignments
                .Include(a => a.Subject)
                .Include(a => a.Submissions)
                .Where(a => a.Subject!.TeacherId == teacherId)
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            return View(assignments);
        }

        // Yangi topshiriq yaratish sahifasi
        public async Task<IActionResult> Create()
        {
            var teacherId = _userManager.GetUserId(User)!;
            var mySubjects = await _teacherAccessService.GetTeacherSubjectsAsync(teacherId);
            ViewBag.Subjects = new SelectList(mySubjects, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Description,DueDate,SubjectId")] Assignment assignment, IFormFile? file)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var ownsSubject = await _teacherAccessService.OwnsSubjectAsync(teacherId, assignment.SubjectId);

            if (!ownsSubject)
            {
                ModelState.AddModelError(string.Empty, "Noto'g'ri fan tanlandi.");
            }

            FileUploadResult? uploadResult = null;
            if (file != null && file.Length > 0)
            {
                uploadResult = await _fileUploadService.UploadAsync(file, MaxFileSizeBytes);
                if (!uploadResult.Success)
                {
                    ModelState.AddModelError(string.Empty, uploadResult.ErrorMessage!);
                }
            }

            if (ModelState.IsValid)
            {
                if (uploadResult is { Success: true })
                {
                    assignment.FilePath = uploadResult.RelativePath;
                    assignment.FileName = uploadResult.OriginalFileName;
                }

                _context.Add(assignment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Topshiriq yaratildi.";
                return RedirectToAction(nameof(Index));
            }

            var mySubjects = await _teacherAccessService.GetTeacherSubjectsAsync(teacherId);
            ViewBag.Subjects = new SelectList(mySubjects, "Id", "Name");
            return View(assignment);
        }

        // Topshirilgan ishlarni ko'rish va baholash
        public async Task<IActionResult> Submissions(int id)
        {
            var teacherId = _userManager.GetUserId(User);

            var assignment = await _context.Assignments
                .Include(a => a.Subject)
                .Include(a => a.Submissions)
                    .ThenInclude(s => s.Student)
                .FirstOrDefaultAsync(a => a.Id == id && a.Subject!.TeacherId == teacherId);

            if (assignment == null) return NotFound();

            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Grade(int submissionId, int grade, string? comment)
        {
            var submission = await _context.Submissions
                .Include(s => s.Assignment)
                    .ThenInclude(a => a!.Subject)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null) return NotFound();

            var teacherId = _userManager.GetUserId(User);
            if (submission.Assignment?.Subject?.TeacherId != teacherId) return Forbid();

            // Baho 0-100 oralig'ida bo'lishini ta'minlaymiz (tasodifiy xato qiymatlarning oldini olish)
            if (grade < 0 || grade > 100)
            {
                TempData["Error"] = "Baho 0 dan 100 gacha bo'lishi kerak.";
                return RedirectToAction(nameof(Submissions), new { id = submission.AssignmentId });
            }

            submission.Grade = grade;
            submission.TeacherComment = comment;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Baholandi.";
            return RedirectToAction(nameof(Submissions), new { id = submission.AssignmentId });
        }
    }
}