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
    public class ExamsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ExamsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // O'qituvchining barcha imtihonlari
        public async Task<IActionResult> Index()
        {
            var teacherId = _userManager.GetUserId(User);

            var exams = await _context.Exams
                .Include(e => e.Subject)
                .Include(e => e.Results)
                .Where(e => e.Subject!.TeacherId == teacherId)
                .OrderByDescending(e => e.CreatedDate)
                .ToListAsync();

            return View(exams);
        }

        // Yangi imtihon yaratish sahifasi
        public async Task<IActionResult> Create()
        {
            await LoadSubjectsAsync();
            return View(new Exam());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,SubjectId,QuestionCount,DurationMinutes")] Exam exam)
        {
            var teacherId = _userManager.GetUserId(User);
            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == exam.SubjectId && s.TeacherId == teacherId);

            if (subject == null)
            {
                ModelState.AddModelError(string.Empty, "Noto'g'ri fan tanlandi.");
            }
            else
            {
                var availableCount = await _context.Questions.CountAsync(q => q.SubjectId == exam.SubjectId);
                if (exam.QuestionCount > availableCount)
                {
                    ModelState.AddModelError(nameof(exam.QuestionCount),
                        $"Bu fanda jami {availableCount} ta savol bor. Savollar sonini kamaytiring yoki avval Savollar bankiga qo'shing.");
                }
            }

            if (ModelState.IsValid)
            {
                _context.Exams.Add(exam);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Imtihon yaratildi.";
                return RedirectToAction(nameof(Index));
            }

            await LoadSubjectsAsync();
            return View(exam);
        }

        // Imtihonni o'chirish sahifasi
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var teacherId = _userManager.GetUserId(User);
            var exam = await _context.Exams
                .Include(e => e.Subject)
                .Include(e => e.Results)
                .FirstOrDefaultAsync(e => e.Id == id && e.Subject!.TeacherId == teacherId);

            if (exam == null) return NotFound();

            return View(exam);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var teacherId = _userManager.GetUserId(User);
            var exam = await _context.Exams
                .Include(e => e.Results)
                .FirstOrDefaultAsync(e => e.Id == id && e.Subject!.TeacherId == teacherId);

            if (exam != null)
            {
                if (exam.Results.Any())
                {
                    TempData["Error"] = "Bu imtihonda talabalar natijasi mavjud, shuning uchun o'chirib bo'lmaydi.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Exams.Remove(exam);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Imtihon o'chirildi.";
            }
            return RedirectToAction(nameof(Index));
        }

        // Imtihon bo'yicha barcha talabalar natijasi (o'qituvchi ko'rishi uchun)
        public async Task<IActionResult> Results(int? id)
        {
            if (id == null) return NotFound();

            var teacherId = _userManager.GetUserId(User);
            var exam = await _context.Exams
                .Include(e => e.Subject)
                .Include(e => e.Results)
                    .ThenInclude(r => r.Student)
                .FirstOrDefaultAsync(e => e.Id == id && e.Subject!.TeacherId == teacherId);

            if (exam == null) return NotFound();

            return View(exam);
        }

        private async Task LoadSubjectsAsync()
        {
            var teacherId = _userManager.GetUserId(User);
            var mySubjects = await _context.Subjects
                .Where(s => s.TeacherId == teacherId)
                .ToListAsync();

            var items = new List<object>();
            foreach (var s in mySubjects)
            {
                var count = await _context.Questions.CountAsync(q => q.SubjectId == s.Id);
                items.Add(new { Id = s.Id, Name = $"{s.Name} ({count} ta savol)" });
            }

            ViewBag.Subjects = new SelectList(items, "Id", "Name");
        }
    }
}