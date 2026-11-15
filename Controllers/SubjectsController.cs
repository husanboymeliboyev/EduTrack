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
        private readonly ISubjectMergeService _subjectMerge;

        public SubjectsController(
            ApplicationDbContext context,
            ISubjectDeletionService subjectDeletion,
            ISubjectMergeService subjectMerge)
        {
            _context = context;
            _subjectDeletion = subjectDeletion;
            _subjectMerge = subjectMerge;
        }

        // Barcha fanlarni ko'rsatish
        public async Task<IActionResult> Index()
        {
            var subjects = await _context.Subjects
                .Include(s => s.Teacher)
                .Include(s => s.GroupSubjects)
                    .ThenInclude(gs => gs.Group)
                .Include(s => s.GroupSubjects)
                    .ThenInclude(gs => gs.Teacher)
                .OrderBy(s => s.Name)
                .ToListAsync();

            ViewBag.DuplicateNames = subjects
                .GroupBy(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => new DuplicateSubjectViewModel(g.Key, g.Count()))
                .ToList();

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
                .OrderBy(u => u.FullName)
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
            return await _context.Groups.OrderBy(g => g.Type).ThenBy(g => g.Name).ToListAsync();
        }

        // Yangi fan qo'shish sahifasi
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            var model = new SubjectFormViewModel();
            await LoadFormDataAsync(model);
            return View(model);
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
                    subject.GroupSubjects.Add(new GroupSubject
                    {
                        GroupId = groupId,
                        // Guruh bo'yicha o'qituvchi (ko'p o'qituvchi modeli):
                        // fan egasidan farqli bo'lsa override sifatida saqlanadi.
                        TeacherId = ResolveGroupTeacher(model, groupId)
                    });
                }

                _context.Add(subject);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Fan qo'shildi.";
                return RedirectToAction(nameof(Index));
            }

            await LoadFormDataAsync(model);
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
                SelectedGroupIds = subject.GroupSubjects.Select(gs => gs.GroupId).ToList(),
                GroupTeacherIds = subject.GroupSubjects.ToDictionary(
                    gs => gs.GroupId, gs => gs.TeacherId)
            };

            await LoadFormDataAsync(model);
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
                // saqlanadi, faqat olib tashlangan guruhlar o'chiriladi va
                // yangilari qo'shiladi. Guruh bo'yicha o'qituvchi (ko'p
                // o'qituvchi modeli) tanlangan guruhlar bo'yicha yangilanadi.
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
                    subject.GroupSubjects.Add(new GroupSubject
                    {
                        GroupId = groupId,
                        TeacherId = ResolveGroupTeacher(model, groupId)
                    });
                }

                // Mavjud guruhlarning o'qituvchi override'i yangilanadi
                // (fan egasi bo'lsa ham guruhda boshqa o'qituvchi o'qitishi mumkin).
                foreach (var link in subject.GroupSubjects
                             .Where(gs => desiredGroupIds.Contains(gs.GroupId))
                             .ToList())
                {
                    link.TeacherId = ResolveGroupTeacher(model, link.GroupId);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Fan yangilandi.";
                return RedirectToAction(nameof(Index));
            }

            await LoadFormDataAsync(model);
            return View(model);
        }

        // Formadagi "guruh bo'yicha o'qituvchi" qiymati: bo'sh bo'lsa null
        // (fan egasi o'qitadi), aks holda tanlangan o'qituvchi Id'si.
        private static string? ResolveGroupTeacher(SubjectFormViewModel model, int groupId)
        {
            return model.GroupTeacherIds.TryGetValue(groupId, out var teacherId)
                && !string.IsNullOrWhiteSpace(teacherId)
                ? teacherId
                : null;
        }

        private async Task LoadFormDataAsync(SubjectFormViewModel model)
        {
            ViewBag.Teachers = await GetTeachersListAsync();
            ViewBag.Groups = await GetGroupsListAsync();

            // Har bir bog'langan guruh uchun joriy override o'qituvchi —
            // tahrirlash formasi joriy holatni ko'rsatishi uchun.
            ViewBag.CurrentGroupTeachers = model.GroupTeacherIds;
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

        // ===== Dublikat fanlarni birlashtirish =====

        // Birlashtirish sahifasi: sourceId (o'chadigan dublikat) berilsa,
        // summary ko'rsatiladi va tasdiqlash so'raladi.
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Merge(int? sourceId, int? targetId)
        {
            var subjects = await _context.Subjects
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name, TeacherName = s.Teacher != null ? (s.Teacher.FullName ?? s.Teacher.Email) : null })
                .ToListAsync();

            ViewBag.SubjectOptions = subjects.Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.TeacherName == null ? s.Name : $"{s.Name} — {s.TeacherName}"
            }).ToList();

            if (sourceId == null || targetId == null)
            {
                return View(null);
            }

            var summary = await _subjectMerge.GetSummaryAsync(sourceId.Value, targetId.Value);
            if (summary == null)
            {
                ModelState.AddModelError(string.Empty, "Fanlar topilmadi yoki bir xil tanlandi.");
                return View(null);
            }

            return View(summary);
        }

        [HttpPost, ActionName("Merge")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> MergeConfirmed(int sourceId, int targetId)
        {
            var result = await _subjectMerge.MergeAsync(sourceId, targetId);
            if (result.Success)
            {
                TempData["Success"] = "Fanlar birlashtirildi.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = result.Error ?? "Birlashtirib bo'lmadi.";
            return RedirectToAction(nameof(Merge), new { sourceId, targetId });
        }
    }
}
