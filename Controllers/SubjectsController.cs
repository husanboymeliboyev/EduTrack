using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
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
                .Include(s => s.GroupSubjects)
                    .ThenInclude(gs => gs.Group)
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

        // Barcha guruhlar ro'yxatini tayyorlash (checkbox ro'yxati uchun)
        private async Task<List<Group>> GetGroupsListAsync()
        {
            return await _context.Groups.OrderBy(g => g.Name).ToListAsync();
        }

        // Yangi fan qo'shish sahifasi
        public async Task<IActionResult> Create()
        {
            ViewBag.Teachers = await GetTeachersListAsync();
            ViewBag.Groups = await GetGroupsListAsync();
            return View(new SubjectFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubjectFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                var subject = new Subject
                {
                    Name = model.Name,
                    TeacherId = model.TeacherId
                };

                foreach (var groupId in model.SelectedGroupIds)
                {
                    subject.GroupSubjects.Add(new GroupSubject { GroupId = groupId });
                }

                _context.Add(subject);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Fan qo'shildi.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Teachers = await GetTeachersListAsync();
            ViewBag.Groups = await GetGroupsListAsync();
            return View(model);
        }

        // Fanni tahrirlash sahifasi
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var subject = await _context.Subjects
                .Include(s => s.GroupSubjects)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (subject == null) return NotFound();

            var model = new SubjectFormViewModel
            {
                Id = subject.Id,
                Name = subject.Name,
                TeacherId = subject.TeacherId,
                SelectedGroupIds = subject.GroupSubjects.Select(gs => gs.GroupId).ToList()
            };

            ViewBag.Teachers = await GetTeachersListAsync();
            ViewBag.Groups = await GetGroupsListAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SubjectFormViewModel model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var subject = await _context.Subjects
                    .Include(s => s.GroupSubjects)
                    .FirstOrDefaultAsync(s => s.Id == id);
                if (subject == null) return NotFound();

                subject.Name = model.Name;
                subject.TeacherId = model.TeacherId;

                // Eski guruh bog'lanishlarini olib tashlab, yangilarini qo'shamiz
                _context.GroupSubjects.RemoveRange(subject.GroupSubjects);
                subject.GroupSubjects.Clear();

                foreach (var groupId in model.SelectedGroupIds)
                {
                    subject.GroupSubjects.Add(new GroupSubject { GroupId = groupId });
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Fan yangilandi.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Teachers = await GetTeachersListAsync();
            ViewBag.Groups = await GetGroupsListAsync();
            return View(model);
        }

        // Fanni o'chirish sahifasi
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var subject = await _context.Subjects
                .Include(s => s.Teacher)
                .Include(s => s.GroupSubjects)
                    .ThenInclude(gs => gs.Group)
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