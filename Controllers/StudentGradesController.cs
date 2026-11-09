using System.Globalization;
using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentGradesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStudentSubjectGroupService _subjectGroups;
        private readonly ISubjectGradeService _subjectGrades;

        public StudentGradesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IStudentSubjectGroupService subjectGroups,
            ISubjectGradeService subjectGrades)
        {
            _context = context;
            _userManager = userManager;
            _subjectGroups = subjectGroups;
            _subjectGrades = subjectGrades;
        }

        // Talabaning o'z fanlari bo'yicha 100 ballik yakuniy baholari
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return View(new List<StudentSubjectGradeViewModel>());
            }

            var subjectIds = await _subjectGroups.GetSubjectIdsAsync(user.Id);
            var grades = await _subjectGrades.GetStudentGradesAsync(user.Id, subjectIds);

            var model = grades.Values
                .OrderBy(s => s.SubjectName, StringComparer.Ordinal)
                .ToList();

            ViewBag.AverageScore = model.Where(s => s.HasScore).Any()
                ? Math.Round(model.Where(s => s.HasScore).Average(s => s.Score!.Value), 1, MidpointRounding.AwayFromZero)
                : (double?)null;
            ViewBag.GradedCount = model.Count(s => s.HasScore);
            ViewBag.SubjectCount = model.Count;

            return View(model);
        }

        // Fanlar bo'yicha reyting (o'quvchi o'qiydigan har bir fan guruhi ichida)
        public async Task<IActionResult> Rating()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return View(new List<GroupRankingEntryViewModel>());
            }

            var subjectIds = await _subjectGroups.GetSubjectIdsAsync(user.Id);
            if (subjectIds.Count == 0)
            {
                return View(new List<GroupRankingEntryViewModel>());
            }

            var groupMap = await _subjectGroups.GetGroupMapAsync(user.Id, subjectIds);

            // Har bir fan uchun o'sha guruhdagi o'quvchilar ro'yxati. Aralash
            // o'qishda bir o'quvchi turli fanlarda turli guruhlarda bo'ladi.
            var rosters = new Dictionary<int, HashSet<string>>();
            foreach (var subjectId in subjectIds)
            {
                if (!groupMap.TryGetValue(subjectId, out var groupId))
                {
                    continue;
                }

                var ids = await _subjectGroups.GetStudentIdsAsync(groupId, subjectId);
                rosters[subjectId] = ids.ToHashSet(StringComparer.Ordinal);
            }

            var studentIds = rosters.Values
                .SelectMany(ids => ids)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var groupStudents = await _context.Users
                .AsNoTracking()
                .Where(u => studentIds.Contains(u.Id))
                .ToListAsync();

            var gradeMap = await _subjectGrades.GetGradesForStudentsAsync(studentIds, subjectIds);

            var ranking = groupStudents.Select(s =>
            {
                var scores = new List<double>();

                foreach (var subjectId in subjectIds)
                {
                    if (!rosters.TryGetValue(subjectId, out var roster) || !roster.Contains(s.Id))
                    {
                        continue;
                    }

                    if (gradeMap.TryGetValue(s.Id, out var grades)
                        && grades.TryGetValue(subjectId, out var grade)
                        && grade.HasScore)
                    {
                        scores.Add(grade.Score!.Value);
                    }
                }

                return new GroupRankingEntryViewModel
                {
                    StudentId = s.Id,
                    StudentName = string.IsNullOrWhiteSpace(s.FullName) ? s.UserName ?? "" : s.FullName!,
                    AveragePercentage = scores.Count > 0
                        ? Math.Round(scores.Average(), 1, MidpointRounding.AwayFromZero)
                        : 0
                };
            })
            .OrderByDescending(r => r.AveragePercentage)
            .ThenBy(r => r.StudentName, StringComparer.Ordinal)
            .ToList();

            for (int i = 0; i < ranking.Count; i++)
            {
                ranking[i].Rank = i + 1;
            }

            ViewBag.MyStudentId = user.Id;

            return View(ranking);
        }
    }
}
