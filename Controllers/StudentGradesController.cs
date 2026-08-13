using EduTrack.Data;
using EduTrack.Models;
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

        public StudentGradesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Talabaning o'z fanlari bo'yicha komponent-komponent baho jadvali
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.GroupId == null)
            {
                return View(new List<StudentSubjectGradeViewModel>());
            }

            var subjectIds = await _context.GroupSubjects
                .Where(gs => gs.GroupId == user.GroupId)
                .Select(gs => gs.SubjectId)
                .ToListAsync();

            var components = await _context.GradeComponents
                .Where(c => subjectIds.Contains(c.SubjectId))
                .Include(c => c.Subject)
                .OrderBy(c => c.Order)
                .ToListAsync();

            var componentIds = components.Select(c => c.Id).ToList();
            var myGrades = await _context.StudentGrades
                .Where(g => g.StudentId == user.Id && componentIds.Contains(g.GradeComponentId))
                .ToListAsync();

            var subjectGrades = components
                .GroupBy(c => c.Subject?.Name ?? "")
                .Select(g => new StudentSubjectGradeViewModel
                {
                    SubjectName = g.Key,
                    Components = g.OrderBy(c => c.Order).Select(c => new ComponentScoreViewModel
                    {
                        ComponentName = c.Name,
                        MaxScore = c.MaxScore,
                        Score = myGrades.FirstOrDefault(mg => mg.GradeComponentId == c.Id)?.Score
                    }).ToList()
                })
                .ToList();

            return View(subjectGrades);
        }

        // Guruh ichida reyting (fanlar bo'yicha o'rtacha foizga qarab)
        public async Task<IActionResult> Rating()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.GroupId == null)
            {
                return View(new List<GroupRankingEntryViewModel>());
            }

            var subjectIds = await _context.GroupSubjects
                .Where(gs => gs.GroupId == user.GroupId)
                .Select(gs => gs.SubjectId)
                .ToListAsync();

            var components = await _context.GradeComponents
                .Where(c => subjectIds.Contains(c.SubjectId))
                .ToListAsync();

            var componentIds = components.Select(c => c.Id).ToList();
            var maxTotalPerSubject = components
                .GroupBy(c => c.SubjectId)
                .ToDictionary(g => g.Key, g => g.Sum(c => c.MaxScore));

            var groupStudents = await _context.Users
                .Where(u => u.GroupId == user.GroupId)
                .ToListAsync();

            var studentIds = groupStudents.Select(s => s.Id).ToList();

            var allGrades = await _context.StudentGrades
                .Where(g => componentIds.Contains(g.GradeComponentId) && studentIds.Contains(g.StudentId))
                .ToListAsync();

            var ranking = groupStudents.Select(s =>
            {
                var subjectPercentages = new List<double>();
                foreach (var subjectId in subjectIds)
                {
                    var subjectComponentIds = components.Where(c => c.SubjectId == subjectId).Select(c => c.Id).ToList();
                    if (!subjectComponentIds.Any()) continue;

                    var maxTotal = maxTotalPerSubject.GetValueOrDefault(subjectId, 0);
                    if (maxTotal == 0) continue;

                    var total = allGrades
                        .Where(g => g.StudentId == s.Id && subjectComponentIds.Contains(g.GradeComponentId))
                        .Sum(g => g.Score);

                    subjectPercentages.Add(total / maxTotal * 100);
                }

                return new GroupRankingEntryViewModel
                {
                    StudentId = s.Id,
                    StudentName = s.FullName ?? "",
                    AveragePercentage = subjectPercentages.Any() ? Math.Round(subjectPercentages.Average(), 1) : 0
                };
            })
            .OrderByDescending(r => r.AveragePercentage)
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