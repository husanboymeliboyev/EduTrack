using System.Globalization;
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
    /// <summary>
    /// O'qituvchi uchun 100 ballik yakuniy baho qo'yish. Baho ikki manbadan
    /// olinadi: o'qituvchi qo'lda qo'ygan ball (ustuvor) yoki fan bo'yicha
    /// tasdiqlangan imtihon natijalarining o'rtachasi. Komponent tizimi
    /// (Yakuniy / topshiriq / davomat) ishlatilmaydi.
    /// </summary>
    [Authorize(Roles = "Teacher")]
    public class GradingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITeacherAccessService _teacherAccessService;
        private readonly IStudentSubjectGroupService _subjectGroups;
        private readonly ISubjectGradeService _subjectGrades;

        public GradingController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITeacherAccessService teacherAccessService,
            IStudentSubjectGroupService subjectGroups,
            ISubjectGradeService subjectGrades)
        {
            _context = context;
            _userManager = userManager;
            _teacherAccessService = teacherAccessService;
            _subjectGroups = subjectGroups;
            _subjectGrades = subjectGrades;
        }

        // Fanlar ro'yxati + har biri bo'yicha baholash holati
        public async Task<IActionResult> Index()
        {
            var teacherId = _userManager.GetUserId(User)!;
            var mySubjects = await _teacherAccessService.GetTeacherSubjectsAsync(teacherId);
            var subjectIds = mySubjects.Select(s => s.Id).ToList();

            if (subjectIds.Count == 0)
            {
                return View(new List<SubjectGradingSummaryViewModel>());
            }

            var groupRows = await _context.GroupSubjects
                .AsNoTracking()
                .Where(gs => subjectIds.Contains(gs.SubjectId))
                .Select(gs => new { gs.SubjectId, gs.GroupId })
                .ToListAsync();

            var studentsBySubject = new Dictionary<int, HashSet<string>>();
            foreach (var subjectId in subjectIds)
            {
                studentsBySubject[subjectId] = new HashSet<string>(StringComparer.Ordinal);
            }

            foreach (var row in groupRows)
            {
                var ids = await _subjectGroups.GetStudentIdsAsync(row.GroupId, row.SubjectId);
                studentsBySubject[row.SubjectId].UnionWith(ids);
            }

            var allStudentIds = studentsBySubject.Values
                .SelectMany(ids => ids)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var gradeMap = await _subjectGrades.GetGradesForStudentsAsync(
                allStudentIds, subjectIds);

            var summary = mySubjects.Select(s =>
            {
                var studentIds = studentsBySubject.GetValueOrDefault(s.Id) ?? new HashSet<string>();
                var graded = 0;
                var manual = 0;
                var scores = new List<double>();

                foreach (var studentId in studentIds)
                {
                    if (!gradeMap.TryGetValue(studentId, out var grades)) continue;
                    if (!grades.TryGetValue(s.Id, out var grade) || !grade.HasScore) continue;

                    graded++;
                    if (grade.IsManual) manual++;
                    scores.Add(grade.Score!.Value);
                }

                return new SubjectGradingSummaryViewModel
                {
                    SubjectId = s.Id,
                    SubjectName = s.Name,
                    StudentCount = studentIds.Count,
                    GradedCount = graded,
                    ManualCount = manual,
                    AverageScore = scores.Count == 0
                        ? null
                        : Math.Round(scores.Average(), 1, MidpointRounding.AwayFromZero)
                };
            })
            .OrderBy(s => s.SubjectName, StringComparer.Ordinal)
            .ToList();

            return View(summary);
        }

        // Guruh tanlanmagan bo'lsa avval shuni so'raymiz, aks holda baho jadvali
        public async Task<IActionResult> Enter(int subjectId, int groupId = 0)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var subject = await _teacherAccessService.GetOwnedSubjectAsync(teacherId, subjectId);
            if (subject == null) return Forbid();

            if (groupId <= 0)
            {
                var groups = await _context.GroupSubjects
                    .AsNoTracking()
                    .Where(gs => gs.SubjectId == subjectId)
                    .Include(gs => gs.Group)
                    .Select(gs => gs.Group!)
                    .OrderBy(g => g.Name)
                    .ToListAsync();

                ViewBag.SubjectId = subjectId;
                ViewBag.SubjectName = subject.Name;
                ViewBag.Groups = new SelectList(groups, "Id", "Name");
                return View("SelectGroup");
            }

            var group = await _context.Groups
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null) return NotFound();

            var studentIdsInGroup = await _subjectGroups.GetStudentIdsAsync(groupId, subjectId);
            var students = await _context.Users
                .AsNoTracking()
                .Where(u => studentIdsInGroup.Contains(u.Id))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var gradeMap = await _subjectGrades.GetGradesForStudentsAsync(
                students.Select(s => s.Id).ToList(),
                new[] { subjectId });

            var rows = students.Select(s =>
            {
                StudentSubjectGradeViewModel? grade = null;
                if (gradeMap.TryGetValue(s.Id, out var grades))
                {
                    grades.TryGetValue(subjectId, out grade);
                }

                return new StudentGradeRowViewModel
                {
                    StudentId = s.Id,
                    StudentName = string.IsNullOrWhiteSpace(s.FullName) ? s.UserName ?? s.Id : s.FullName!,
                    ManualScore = grade?.ManualScore,
                    ExamAverage = grade?.ExamAverage,
                    ExamCount = grade?.ExamCount ?? 0
                };
            }).ToList();

            ViewBag.SubjectId = subjectId;
            ViewBag.SubjectName = subject.Name;
            ViewBag.GroupId = groupId;
            ViewBag.GroupName = group.Name;

            return View(rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveGrades(
            int subjectId,
            int groupId,
            Dictionary<string, string>? scores)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var ownsSubject = await _teacherAccessService.OwnsSubjectAsync(teacherId, subjectId);
            if (!ownsSubject) return Forbid();

            if (scores is null || scores.Count == 0)
            {
                TempData["Info"] = "O'zgartirish kiritilmadi.";
                return RedirectToAction(nameof(Enter), new { subjectId, groupId });
            }

            // Faqat shu fan + guruhdagi o'quvchilarga yozishga ruxsat.
            var allowedStudentIds = (await _subjectGroups.GetStudentIdsAsync(groupId, subjectId))
                .ToHashSet(StringComparer.Ordinal);

            var saved = 0;
            var skipped = 0;

            foreach (var entry in scores)
            {
                if (!allowedStudentIds.Contains(entry.Key)) continue;

                var raw = entry.Value?.Trim();
                if (string.IsNullOrWhiteSpace(raw)) continue;

                raw = raw.Replace(',', '.');

                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var score)
                    || score < 0 || score > 100)
                {
                    skipped++;
                    continue;
                }

                await _subjectGrades.SaveManualGradeAsync(
                    entry.Key, subjectId, score, teacherId);

                saved++;
            }

            TempData["Success"] = saved == 0
                ? "Saqlash uchun baho kiritilmadi."
                : $"{saved} ta baho saqlandi.";

            if (skipped > 0)
            {
                TempData["Error"] = $"{skipped} ta qiymat 0–100 oralig'ida emas va saqlanmadi.";
            }

            return RedirectToAction(nameof(Enter), new { subjectId, groupId });
        }
    }
}
