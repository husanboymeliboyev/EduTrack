using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    /// <summary>
    /// "O'quvchilar guruh ro'yxati sinflar bo'yicha" Excel faylini import qilish.
    /// Dry-run (hech narsa saqlanmaydi) va tasdiqlangan import bir xil kod yo'lini ishlatadi.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class StudentImportController : Controller
    {
        private readonly IStudentImportService _importService;
        private readonly ApplicationDbContext _context;

        public StudentImportController(IStudentImportService importService, ApplicationDbContext context)
        {
            _importService = importService;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        /// <summary>Tekshirish (dry-run): nima o'zgarishini ko'rsatadi, bazaga hech narsa yozmaydi.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Preview(IFormFile? ExcelFile)
        {
            if (ExcelFile == null || ExcelFile.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Excel faylni tanlang.");
                return View("Index");
            }
            if (ExcelFile.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(string.Empty, "Fayl 5 MB dan oshmasligi kerak.");
                return View("Index");
            }

            using var stream = new MemoryStream();
            await ExcelFile.CopyToAsync(stream);
            stream.Position = 0;

            var preview = _importService.Preview(stream);
            return View(preview);
        }

        /// <summary>Import: faylni qayta o'qib, o'quvchilarni yaratadi (tranzaksiyada).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile? ExcelFile)
        {
            if (ExcelFile == null || ExcelFile.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Excel faylni tanlang.");
                return View("Index");
            }

            using var stream = new MemoryStream();
            await ExcelFile.CopyToAsync(stream);
            stream.Position = 0;

            var result = await _importService.ImportAsync(stream);
            return View("Result", result);
        }

        /// <summary>
        /// Chop etiladigan kirish varaqasi: sinf bo'yicha ro'yxat, LoginId va
        /// vaqtincha parol ko'rsatmasidan qolgan barcha ma'lumotlar.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EntrySheet(int? classGroupId)
        {
            var classesQuery = _context.Groups
                .AsNoTracking()
                .Where(g => g.Type == GroupType.Class && !g.IsArchived
                    && (!classGroupId.HasValue || g.Id == classGroupId.Value))
                .OrderBy(g => g.Name);

            var classes = await classesQuery.ToListAsync();
            var sheet = new List<(Group Class, List<ApplicationUser> Students)>();

            foreach (var cls in classes)
            {
                var students = await _context.GroupMemberships
                    .AsNoTracking()
                    .Where(m => m.GroupId == cls.Id && m.Scope == GroupType.Class && m.IsActive)
                    .Join(_context.Users.AsNoTracking(), m => m.StudentId, u => u.Id, (m, u) => u)
                    .OrderBy(u => u.LoginId)
                    .ToListAsync();

                sheet.Add((cls, students));
            }

            ViewBag.AllClasses = await _context.Groups
                .AsNoTracking()
                .Where(g => g.Type == GroupType.Class && !g.IsArchived)
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name })
                .ToListAsync();

            return View(sheet);
        }
    }
}
