using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    // Faqat Admin uchun: ota-ona hisoblarini talabalarga bog'lash boshqaruvi.
    // Ommaviy "ota-ona o'zi ro'yxatdan o'tadi" oqimi yo'q — bog'lashni faqat admin bajaradi.
    [Authorize(Roles = "Admin")]
    public class ParentLinksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ParentLinksController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Barcha bog'lanishlar ro'yxati + yangi bog'lash formasi
        public async Task<IActionResult> Index()
        {
            var parents = await _userManager.GetUsersInRoleAsync("Parent");
            var students = await _userManager.GetUsersInRoleAsync("Student");

            ViewBag.Parents = new SelectList(parents.OrderBy(p => p.FullName), "Id", "FullName");
            ViewBag.Students = new SelectList(students.OrderBy(s => s.FullName), "Id", "FullName");

            var links = await _context.ParentStudentLinks
                .Include(l => l.Parent)
                .Include(l => l.Student)
                .OrderBy(l => l.Parent!.FullName)
                .ThenBy(l => l.Student!.FullName)
                .ToListAsync();

            var model = links.Select(l => new ParentLinkListItemViewModel
            {
                Id = l.Id,
                ParentId = l.ParentId,
                ParentName = l.Parent?.FullName ?? l.Parent?.UserName ?? string.Empty,
                ParentLoginId = l.Parent?.LoginId ?? string.Empty,
                StudentId = l.StudentId,
                StudentName = l.Student?.FullName ?? l.Student?.UserName ?? string.Empty
            }).ToList();

            return View(model);
        }

        // Yangi bog'lanish qo'shish (faqat ota-ona + talaba roli, takroriy bog'lanish rad etiladi)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Link(string parentId, string studentId)
        {
            if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(studentId))
            {
                TempData["Error"] = "Ota-ona va talabani tanlang.";
                return RedirectToAction(nameof(Index));
            }

            var parent = await _userManager.FindByIdAsync(parentId);
            var student = await _userManager.FindByIdAsync(studentId);

            if (parent == null || !await _userManager.IsInRoleAsync(parent, "Parent"))
            {
                TempData["Error"] = "Tanlangan foydalanuvchi ota-ona hisobi emas.";
                return RedirectToAction(nameof(Index));
            }

            if (student == null || !await _userManager.IsInRoleAsync(student, "Student"))
            {
                TempData["Error"] = "Tanlangan foydalanuvchi talaba hisobi emas.";
                return RedirectToAction(nameof(Index));
            }

            var exists = await _context.ParentStudentLinks
                .AnyAsync(l => l.ParentId == parentId && l.StudentId == studentId);
            if (exists)
            {
                TempData["Error"] = "Bu bog'lanish allaqachon mavjud.";
                return RedirectToAction(nameof(Index));
            }

            _context.ParentStudentLinks.Add(new ParentStudentLink
            {
                ParentId = parentId,
                StudentId = studentId
            });

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Bog'lanish qo'shildi.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Bu bog'lanish allaqachon mavjud.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Bog'lanishni o'chirish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlink(int id)
        {
            var link = await _context.ParentStudentLinks.FindAsync(id);
            if (link != null)
            {
                _context.ParentStudentLinks.Remove(link);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Bog'lanish o'chirildi.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
