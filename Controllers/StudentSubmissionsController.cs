using EduTrack.Data;
using EduTrack.Models;
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
        private readonly IWebHostEnvironment _environment;

        public StudentSubmissionsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        // Talabaning barcha topshiriqlari (o'z guruhi fanlari bo'yicha)
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.GroupId == null)
            {
                return View(new List<Assignment>());
            }

            // Talaba guruhi qaysi fanlarga tegishli ekanini bilmaymiz hozircha (guruh-fan bog'lanishi yo'q),
            // shuning uchun barcha fanlardagi topshiriqlarni ko'rsatamiz
            var assignments = await _context.Assignments
                .Include(a => a.Subject)
                .Include(a => a.Submissions.Where(s => s.StudentId == user.Id))
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

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Fayl tanlanmagan.";
                return RedirectToAction(nameof(Index));
            }

            // Fayl hajmini cheklash (10 MB)
            if (file.Length > 10 * 1024 * 1024)
            {
                TempData["Error"] = "Fayl hajmi 10 MB dan oshmasligi kerak.";
                return RedirectToAction(nameof(Index));
            }

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

            var existing = await _context.Submissions
                .FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == user.Id);

            if (existing != null)
            {
                existing.FilePath = $"uploads/{uniqueFileName}";
                existing.FileName = file.FileName;
                existing.SubmittedDate = DateTime.Now;
            }
            else
            {
                _context.Submissions.Add(new Submission
                {
                    AssignmentId = assignmentId,
                    StudentId = user.Id,
                    FilePath = $"uploads/{uniqueFileName}",
                    FileName = file.FileName,
                    SubmittedDate = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Topshiriq muvaffaqiyatli yuklandi.";
            return RedirectToAction(nameof(Index));
        }
    }
}