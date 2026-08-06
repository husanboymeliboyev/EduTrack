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
    public class AssignmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public AssignmentsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
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
            var teacherId = _userManager.GetUserId(User);
            var mySubjects = await _context.Subjects.Where(s => s.TeacherId == teacherId).ToListAsync();
            ViewBag.Subjects = new SelectList(mySubjects, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Description,DueDate,SubjectId")] Assignment assignment, IFormFile? file)
        {
            var teacherId = _userManager.GetUserId(User);
            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == assignment.SubjectId && s.TeacherId == teacherId);

            if (subject == null)
            {
                ModelState.AddModelError(string.Empty, "Noto'g'ri fan tanlandi.");
            }

            // Fayl bo'lsa, turi va hajmini tekshiramiz
            string[] allowedExtensions = { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".zip", ".rar" };
            if (file != null && file.Length > 0)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(string.Empty, "Ruxsat etilmagan fayl turi. Faqat PDF, Word, PowerPoint, Excel, rasm yoki arxiv fayllarini yuklash mumkin.");
                }
                else if (file.Length > 20 * 1024 * 1024) // 20 MB
                {
                    ModelState.AddModelError(string.Empty, "Fayl hajmi 20 MB dan oshmasligi kerak.");
                }
            }

            if (ModelState.IsValid)
            {
                if (file != null && file.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    assignment.FilePath = $"uploads/{uniqueFileName}";
                    assignment.FileName = file.FileName;
                }

                _context.Add(assignment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Topshiriq yaratildi.";
                return RedirectToAction(nameof(Index));
            }

            var mySubjects = await _context.Subjects.Where(s => s.TeacherId == teacherId).ToListAsync();
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

            submission.Grade = grade;
            submission.TeacherComment = comment;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Baholandi.";
            return RedirectToAction(nameof(Submissions), new { id = submission.AssignmentId });
        }
    }
}