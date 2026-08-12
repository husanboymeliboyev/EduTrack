using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class GradingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITeacherAccessService _teacherAccessService;

        public GradingController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITeacherAccessService teacherAccessService)
        {
            _context = context;
            _userManager = userManager;
            _teacherAccessService = teacherAccessService;
        }

        // Fanlar ro'yxati + har biri uchun komponentlar holati
        public async Task<IActionResult> Index()
        {
            var teacherId = _userManager.GetUserId(User)!;
            var mySubjects = await _teacherAccessService.GetTeacherSubjectsAsync(teacherId);
            var subjectIds = mySubjects.Select(s => s.Id).ToList();

            var components = await _context.GradeComponents
                .Where(c => subjectIds.Contains(c.SubjectId))
                .ToListAsync();

            var summary = mySubjects.Select(s => new SubjectGradingSummaryViewModel
            {
                SubjectId = s.Id,
                SubjectName = s.Name,
                ComponentCount = components.Count(c => c.SubjectId == s.Id),
                TotalMaxScore = components.Where(c => c.SubjectId == s.Id).Sum(c => c.MaxScore)
            }).ToList();

            return View(summary);
        }

        // Fan uchun baholash komponentlarini boshqarish
        public async Task<IActionResult> Components(int subjectId)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var subject = await _teacherAccessService.GetOwnedSubjectAsync(teacherId, subjectId);
            if (subject == null) return Forbid();

            ViewBag.SubjectId = subjectId;
            ViewBag.SubjectName = subject.Name;

            var components = await _context.GradeComponents
                .Where(c => c.SubjectId == subjectId)
                .OrderBy(c => c.Order)
                .ToListAsync();

            return View(components);
        }

        // Standart O'zbekiston OTM shablonini bir zumda qo'shish: 15+15+20+50 = 100
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStandardTemplate(int subjectId)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var subject = await _teacherAccessService.GetOwnedSubjectAsync(teacherId, subjectId);
            if (subject == null) return Forbid();

            var alreadyHasComponents = await _context.GradeComponents.AnyAsync(c => c.SubjectId == subjectId);
            if (alreadyHasComponents)
            {
                TempData["Error"] = "Bu fan uchun komponentlar allaqachon mavjud. Avval eskilarini o'chiring.";
                return RedirectToAction(nameof(Components), new { subjectId });
            }

            _context.GradeComponents.AddRange(
                new GradeComponent { SubjectId = subjectId, Name = "1-nazorat", MaxScore = 15, Order = 1 },
                new GradeComponent { SubjectId = subjectId, Name = "2-nazorat", MaxScore = 15, Order = 2 },
                new GradeComponent { SubjectId = subjectId, Name = "Oraliq nazorat", MaxScore = 20, Order = 3 },
                new GradeComponent { SubjectId = subjectId, Name = "Yakuniy nazorat", MaxScore = 50, Order = 4 }
            );

            await _context.SaveChangesAsync();
            TempData["Success"] = "Standart shablon qo'shildi (jami 100 ball).";
            return RedirectToAction(nameof(Components), new { subjectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComponent(int subjectId, string name, int maxScore)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var subject = await _teacherAccessService.GetOwnedSubjectAsync(teacherId, subjectId);
            if (subject == null) return Forbid();

            if (string.IsNullOrWhiteSpace(name) || maxScore < 1 || maxScore > 100)
            {
                TempData["Error"] = "Komponent nomi va ball (1-100) to'g'ri kiritilishi kerak.";
                return RedirectToAction(nameof(Components), new { subjectId });
            }

            var maxOrder = await _context.GradeComponents
                .Where(c => c.SubjectId == subjectId)
                .Select(c => (int?)c.Order)
                .MaxAsync() ?? 0;

            _context.GradeComponents.Add(new GradeComponent
            {
                SubjectId = subjectId,
                Name = name.Trim(),
                MaxScore = maxScore,
                Order = maxOrder + 1
            });

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Components), new { subjectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComponent(int id, int subjectId)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var subject = await _teacherAccessService.GetOwnedSubjectAsync(teacherId, subjectId);
            if (subject == null) return Forbid();

            var component = await _context.GradeComponents
                .FirstOrDefaultAsync(c => c.Id == id && c.SubjectId == subjectId);

            if (component != null)
            {
                _context.GradeComponents.Remove(component);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Components), new { subjectId });
        }

        // Guruh tanlanmagan bo'lsa avval shuni so'raymiz, tanlangan bo'lsa baho kiritish jadvalini ko'rsatamiz
        public async Task<IActionResult> Enter(int subjectId, int groupId = 0)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var subject = await _teacherAccessService.GetOwnedSubjectAsync(teacherId, subjectId);
            if (subject == null) return Forbid();

            var components = await _context.GradeComponents
                .Where(c => c.SubjectId == subjectId)
                .OrderBy(c => c.Order)
                .ToListAsync();

            if (!components.Any())
            {
                TempData["Error"] = "Avval shu fan uchun baholash komponentlarini sozlang.";
                return RedirectToAction(nameof(Components), new { subjectId });
            }

            if (groupId <= 0)
            {
                var groups = await _context.GroupSubjects
                    .Where(gs => gs.SubjectId == subjectId)
                    .Include(gs => gs.Group)
                    .Select(gs => gs.Group!)
                    .ToListAsync();

                ViewBag.SubjectId = subjectId;
                ViewBag.SubjectName = subject.Name;
                ViewBag.Groups = new SelectList(groups, "Id", "Name");
                return View("SelectGroup");
            }

            var students = await _context.Users
                .Where(u => u.GroupId == groupId)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var componentIds = components.Select(c => c.Id).ToList();
            var studentIds = students.Select(s => s.Id).ToList();

            var existingGrades = await _context.StudentGrades
                .Where(g => componentIds.Contains(g.GradeComponentId) && studentIds.Contains(g.StudentId))
                .ToListAsync();

            var rows = students.Select(s => new StudentGradeRowViewModel
            {
                StudentId = s.Id,
                StudentName = s.FullName ?? "",
                Scores = components.ToDictionary(
                    c => c.Id,
                    c => (double?)existingGrades.FirstOrDefault(g => g.StudentId == s.Id && g.GradeComponentId == c.Id)?.Score
                )
            }).ToList();

            ViewBag.SubjectId = subjectId;
            ViewBag.SubjectName = subject.Name;
            ViewBag.GroupId = groupId;
            ViewBag.Components = components;

            return View(rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveGrades(int subjectId, int groupId, Dictionary<string, Dictionary<int, string>> grades)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var ownsSubject = await _teacherAccessService.OwnsSubjectAsync(teacherId, subjectId);
            if (!ownsSubject) return Forbid();

            var componentIds = await _context.GradeComponents
                .Where(c => c.SubjectId == subjectId)
                .Select(c => c.Id)
                .ToListAsync();

            if (grades != null)
            {
                foreach (var studentEntry in grades)
                {
                    var studentId = studentEntry.Key;
                    foreach (var componentEntry in studentEntry.Value)
                    {
                        var componentId = componentEntry.Key;
                        // Xavfsizlik: faqat shu fanga tegishli komponentlarga yozishga ruxsat
                        if (!componentIds.Contains(componentId)) continue;
                        if (!double.TryParse(componentEntry.Value, out var score)) continue;

                        var existing = await _context.StudentGrades
                            .FirstOrDefaultAsync(g => g.StudentId == studentId && g.GradeComponentId == componentId);

                        if (existing != null)
                        {
                            existing.Score = score;
                            existing.UpdatedDate = DateTime.Now;
                        }
                        else
                        {
                            _context.StudentGrades.Add(new StudentGrade
                            {
                                StudentId = studentId,
                                GradeComponentId = componentId,
                                Score = score,
                                UpdatedDate = DateTime.Now
                            });
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Baholar saqlandi.";
            return RedirectToAction(nameof(Enter), new { subjectId, groupId });
        }
    }
}