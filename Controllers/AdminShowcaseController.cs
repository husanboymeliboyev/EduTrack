using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminShowcaseController : Controller
    {
        private readonly IStudentShowcaseService _service;
        private readonly ApplicationDbContext _context;

        public AdminShowcaseController(IStudentShowcaseService service, ApplicationDbContext context)
        {
            _service = service;
            _context = context;
        }

        // Guruhlar ro'yxati
        public async Task<IActionResult> Index()
        {
            var groups = await _context.Groups
                .Include(g => g.Students)
                .OrderBy(g => g.Name)
                .ToListAsync();

            return View(groups);
        }

        // Katta ekran — Sinf ko'rinishi
        public async Task<IActionResult> ClassOverview(int groupId)
        {
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null) return NotFound();

            var overview = await _service.GetClassOverviewAsync(groupId);
            return View(overview);
        }

        // Bitta talabaning to'liq hisobot/taqdimot sahifasi
        public async Task<IActionResult> StudentReport(string studentId)
        {
            if (string.IsNullOrEmpty(studentId)) return NotFound();

            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == studentId);
            if (student == null) return NotFound();

            var report = await _service.GetStudentShowcaseAsync(studentId);
            return View(report);
        }

        // Mezon og'irliklarini ko'rish
        public async Task<IActionResult> Criteria()
        {
            var criteria = await _service.GetCriteriaAsync();
            return View(criteria);
        }

        // Mezon og'irliklarini saqlash (yig'indi 100% bo'lishi shart — tekshiruv servis ichida)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Criteria(Dictionary<string, double> weights)
        {
            var success = await _service.UpdateCriteriaAsync(weights ?? new Dictionary<string, double>());

            if (!success)
            {
                TempData["Error"] = "Saqlanmadi: og'irliklar yig'indisi aniq 100% ga teng bo'lishi kerak.";
            }
            else
            {
                TempData["Success"] = "Mezonlar muvaffaqiyatli saqlandi.";
            }

            return RedirectToAction(nameof(Criteria));
        }
    }
}