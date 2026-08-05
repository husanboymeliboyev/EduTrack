using EduTrack.Data;
using EduTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SubjectsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SubjectsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Barcha fanlarni ko'rsatish
        public async Task<IActionResult> Index()
        {
            var subjects = await _context.Subjects
                .Include(s => s.Teacher)
                .ToListAsync();
            return View(subjects);
        }

        // O'qituvchilar ro'yxatini tayyorlash (dropdown uchun)
        private async Task<List<SelectListItem>> GetTeachersListAsync()
        {
            var teacherRoleId = await _context.Roles
                .Where(r => r.Name == "Teacher")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            var teachers = await _context.UserRoles
                .Where(ur => ur.RoleId == teacherRoleId)
                .Join(_context.Users, ur => ur.UserId, u => u.Id, (ur, u) => u)
                .ToListAsync();

            return teachers.Select(t => new SelectListItem
            {
                Value = t.Id,
                Text = string.IsNullOrEmpty(t.FullName) ? t.Email : t.FullName
            }).ToList();
        }

        // Yangi fan qo'shish sahifasi
        public async Task<IActionResult> Create()
        {
            ViewBag.Teachers = await GetTeachersListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,TeacherId")] Subject subject)
        {
            if (ModelState.IsValid)
            {
                _context.Add(subject);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Teachers = await GetTeachersListAsync();
            return View(subject);
        }

        // Fanni tahrirlash sahifasi
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null) return NotFound();

            ViewBag.Teachers = await GetTeachersListAsync();
            return View(subject);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,TeacherId")] Subject subject)
        {
            if (id != subject.Id) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(subject);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Teachers = await GetTeachersListAsync();
            return View(subject);
        }

        // Fanni o'chirish sahifasi
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var subject = await _context.Subjects
                .Include(s => s.Teacher)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (subject == null) return NotFound();

            return View(subject);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var subject = await _context.Subjects.FindAsync(id);
            if (subject != null)
            {
                _context.Subjects.Remove(subject);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}