using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    /// <summary>
    /// Admin: o'quvchining fanlar bo'yicha guruhlarini boshqarish
    /// (aralash o'qish — masalan matematika 2-guruh, ingliz tili 4-guruh).
    /// Standart holat barcha fanlar uchun o'quvchining guruhi; bu yerda
    /// aniq bir fan boshqa guruhga biriktiriladi (override).
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class StudentSubjectsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IStudentSubjectGroupService _subjectGroups;

        public StudentSubjectsController(
            ApplicationDbContext context,
            IStudentSubjectGroupService subjectGroups)
        {
            _context = context;
            _subjectGroups = subjectGroups;
        }

        /// <summary>O'quvchilarni qidirish va fan bo'yicha guruhlar ro'yxati.</summary>
        public async Task<IActionResult> Index(string? q)
        {
            ViewBag.Query = q;

            var query = _context.Users
                .AsNoTracking()
                .Where(u => u.Memberships.Any(m => m.IsActive) || u.GroupId != null);

            var term = q?.Trim();
            if (!string.IsNullOrWhiteSpace(term))
            {
                var lower = term.ToLower();
                query = query.Where(u =>
                    (u.FullName != null && u.FullName.ToLower().Contains(lower))
                    || (u.LoginId != null && u.LoginId.ToLower().Contains(lower)));
            }

            var students = await query
                .OrderBy(u => u.FullName)
                .Take(50)
                .ToListAsync();

            var rows = new List<StudentSubjectsStudentRow>();
            foreach (var student in students)
            {
                var memberships = await _context.GroupMemberships
                    .AsNoTracking()
                    .Include(m => m.Group)
                    .FirstOrDefaultAsync(m => m.StudentId == student.Id
                                              && m.Scope == GroupType.Stream
                                              && m.IsActive);

                var groupName = memberships?.Group?.Name;
                if (groupName == null && student.GroupId != null)
                {
                    groupName = await _context.Groups
                        .AsNoTracking()
                        .Where(g => g.Id == student.GroupId)
                        .Select(g => g.Name)
                        .FirstOrDefaultAsync();
                }

                var activeOverrides = await _context.StudentGroupSubjects
                    .AsNoTracking()
                    .CountAsync(x => x.StudentId == student.Id && x.IsActive);

                rows.Add(new StudentSubjectsStudentRow(
                    student.Id,
                    student.FullName ?? student.LoginId,
                    student.LoginId,
                    groupName,
                    activeOverrides));
            }

            // So'nggi o'zgarishlar tarixi (umumiy, oxirgi 20 yozuv).
            var recentRows = await _context.StudentGroupSubjects
                .AsNoTracking()
                .Include(x => x.Subject)
                .Include(x => x.Group)
                .Include(x => x.Student)
                .OrderByDescending(x => x.StartDate)
                .ThenByDescending(x => x.Id)
                .Take(20)
                .Select(x => new StudentSubjectsRecentRow(
                    x.Id,
                    x.StudentId,
                    x.Student!.FullName ?? x.Student.LoginId,
                    x.Subject!.Name,
                    x.Group!.Name,
                    x.StartDate,
                    x.EndDate,
                    x.IsActive))
                .ToListAsync();

            var model = new StudentSubjectsIndexViewModel(rows, recentRows);

            return View(model);
        }

        /// <summary>Bitta o'quvchining barcha fanlari va guruhlari.</summary>
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            var memberships = await _context.GroupMemberships
                .AsNoTracking()
                .Include(m => m.Group)
                .Where(m => m.StudentId == id && m.IsActive)
                .ToListAsync();

            var classGroup = memberships.FirstOrDefault(m => m.Scope == GroupType.Class)?.Group;
            var defaultGroup = memberships.FirstOrDefault(m => m.Scope == GroupType.Stream)?.Group;

            var defaultGroupId = await _subjectGroups.GetDefaultGroupIdAsync(id);

            var assignments = await _subjectGroups.GetAssignmentsAsync(id);
            var activeAssignments = assignments.Where(a => a.IsActive).ToList();

            // Ro'yxat: standart guruhda o'qiydigan fanlar + tarixi bo'lgan fanlar.
            var subjectsForResult = new Dictionary<int, string>();

            if (defaultGroupId is int defId)
            {
                var defaultSubjects = await _context.GroupSubjects
                    .AsNoTracking()
                    .Where(gs => gs.GroupId == defId)
                    .Join(
                        _context.Subjects,
                        gs => gs.SubjectId,
                        s => s.Id,
                        (gs, s) => new { s.Id, s.Name })
                    .ToListAsync();

                foreach (var subject in defaultSubjects)
                {
                    subjectsForResult[subject.Id] = subject.Name;
                }
            }

            foreach (var assignment in assignments)
            {
                var subjectId = assignment.SubjectId;
                var subjectName = assignment.Subject?.Name ?? $"Fan #{subjectId}";
                subjectsForResult[subjectId] = subjectName;
            }

            var subjectIds = subjectsForResult.Keys.ToList();
            var groupMap = subjectIds.Count == 0
                ? new Dictionary<int, int>()
                : (await _subjectGroups.GetGroupMapAsync(id, subjectIds)).ToDictionary(kv => kv.Key, kv => kv.Value);

            var subjectIdList = subjectsForResult.Keys.ToList();
            var groupOptions = await _context.GroupSubjects
                .AsNoTracking()
                .Where(gs => subjectIdList.Contains(gs.SubjectId))
                .Join(
                    _context.Groups,
                    gs => gs.GroupId,
                    g => g.Id,
                    (gs, g) => new { gs.SubjectId, g.Id, g.Name, g.IsArchived })
                .ToListAsync();

            var rows = subjectIds
                .OrderBy(subjectId => subjectsForResult[subjectId])
                .Select(subjectId =>
                {
                    var options = groupOptions
                        .Where(g => g.SubjectId == subjectId)
                        .OrderBy(g => g.Name)
                        .Select(g => new SelectListItem
                        {
                            Value = g.Id.ToString(),
                            Text = g.Name
                        })
                        .ToList();

                    var hasOverride = activeAssignments.Any(a => a.SubjectId == subjectId);
                    var currentGroupId = groupMap.GetValueOrDefault(subjectId);

                    return new StudentSubjectRowViewModel
                    {
                        SubjectId = subjectId,
                        SubjectName = subjectsForResult[subjectId],
                        Groups = options,
                        CurrentGroupId = currentGroupId,
                        CurrentGroupName = currentGroupId > 0
                            ? options.FirstOrDefault(o => o.Value == currentGroupId.ToString())?.Text
                            : null,
                        HasOverride = hasOverride
                    };
                })
                .ToList();

            var model = new StudentSubjectDetailsViewModel
            {
                StudentId = user.Id,
                FullName = user.FullName ?? user.LoginId,
                LoginId = user.LoginId,
                ClassGroupName = classGroup?.Name,
                DefaultGroupName = defaultGroup?.Name,
                Rows = rows,
                History = assignments.ToList()
            };

            return View(model);
        }

        /// <summary>Fanni boshqa guruhga biriktirish (yoki override yangilash).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetGroup(
            string studentId,
            int subjectId,
            int groupId,
            DateTime date,
            string? reason)
        {
            try
            {
                var result = await _subjectGroups.SetGroupAsync(
                    studentId, subjectId, groupId, date, reason);

                TempData["Success"] = result == null
                    ? "Fan standart guruhga qaytarildi."
                    : "Fan yangi guruhga biriktirildi.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }
            catch (ArgumentException ex)
            {
                TempData["Error"] = ex.Message;
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Saqlashda xatolik: " + ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = studentId });
        }

        /// <summary>Override'ni yopish — fan standart guruhga qaytadi.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reset(
            string studentId,
            int subjectId,
            DateTime date,
            string? reason)
        {
            try
            {
                var changed = await _subjectGroups.ResetToDefaultAsync(
                    studentId, subjectId, date, reason);

                TempData["Success"] = changed
                    ? "Fan standart guruhga qaytarildi."
                    : "Bu fan bo'yicha o'zgartirilmagan guruh yo'q edi.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Qaytarishda xatolik: " + ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = studentId });
        }
    }
}

// ==================== UI modellari ====================

namespace EduTrack.Controllers
{
    public sealed record StudentSubjectsIndexViewModel(
        IReadOnlyList<StudentSubjectsStudentRow> Students,
        IReadOnlyList<StudentSubjectsRecentRow> RecentRows);

    public sealed record StudentSubjectsStudentRow(
        string StudentId,
        string FullName,
        string LoginId,
        string? CurrentGroupName,
        int ActiveOverrides);

    public sealed record StudentSubjectsRecentRow(
        int Id,
        string StudentId,
        string FullName,
        string Subject,
        string Group,
        DateTime StartDate,
        DateTime? EndDate,
        bool IsActive);

    public sealed class StudentSubjectDetailsViewModel
    {
        public required string StudentId { get; init; }
        public required string FullName { get; init; }
        public required string LoginId { get; init; }
        public string? ClassGroupName { get; init; }
        public string? DefaultGroupName { get; init; }
        public required List<StudentSubjectRowViewModel> Rows { get; init; }
        public required List<StudentGroupSubject> History { get; init; }
    }

    public sealed class StudentSubjectRowViewModel
    {
        public int SubjectId { get; init; }
        public required string SubjectName { get; init; }
        public required List<SelectListItem> Groups { get; init; }
        public int? CurrentGroupId { get; init; }
        public string? CurrentGroupName { get; init; }
        public bool HasOverride { get; init; }
    }
}
