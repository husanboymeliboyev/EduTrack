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
    // Fanlar: Admin to'liq boshqaradi; Moderator faqat ko'ra oladi.
    [Authorize(Roles = "Admin,Moderator")]
    public class SubjectsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISubjectDeletionService _subjectDeletion;

        public SubjectsController(ApplicationDbContext context, ISubjectDeletionService subjectDeletion)
        {
            _context = context;
            _subjectDeletion = subjectDeletion;
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Teachers = await GetTeachersListAsync();
            ViewBag.Groups = await GetGroupsListAsync();
            return View(new SubjectFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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

                // Faqat o'zgargan bog'lanishlar yangilanadi: mavjud qatorlar
                // (jumladan GroupSubject.TeacherId — guruh bo'yicha o'qituvchi
                // override'i) saqlanadi, faqat olib tashlangan guruhlar
                // o'chiriladi va yangilari qo'shiladi.
                var desiredGroupIds = model.SelectedGroupIds.Distinct().ToHashSet();
                var existingGroupIds = subject.GroupSubjects
                    .Select(gs => gs.GroupId)
                    .ToHashSet();

                foreach (var link in subject.GroupSubjects
                             .Where(gs => !desiredGroupIds.Contains(gs.GroupId))
                             .ToList())
                {
                    subject.GroupSubjects.Remove(link);
                    _context.GroupSubjects.Remove(link);
                }

                foreach (var groupId in desiredGroupIds.Where(gid => !existingGroupIds.Contains(gid)))
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var summary = await _subjectDeletion.GetSummaryAsync(id.Value);
            if (summary == null) return NotFound();

            return View(summary);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var result = await _subjectDeletion.DeleteAsync(id);
            if (result.Success)
            {
                TempData["Success"] = "Fan o'chirildi.";
            }
            else
            {
                TempData["Error"] = result.Error ?? "Fanni o'chirib bo'lmadi.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}