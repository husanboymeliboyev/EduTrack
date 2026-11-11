using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace EduTrack.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IAppClock _appClock;
        private readonly IStudentSubjectGroupService _subjectGroups;
        private readonly ISubjectGradeService _subjectGrades;

        public HomeController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IAppClock appClock,
            IStudentSubjectGroupService subjectGroups,
            ISubjectGradeService subjectGrades)
        {
            _userManager = userManager;
            _context = context;
            _appClock = appClock;
            _subjectGroups = subjectGroups;
            _subjectGrades = subjectGrades;
        }

        public async Task<IActionResult> Index()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return View();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return View();

            ViewBag.DisplayName = string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName;

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();

            if (role == "Parent")
            {
                // Ota-ona umumiy kirish sahifasidan kirgan bo'lsa ham o'z paneliga yo'naltiriladi.
                return RedirectToAction("Index", "ParentPortal");
            }

            if (role == "Admin")
            {
                ViewBag.TotalUsers = await _context.Users.CountAsync();
                ViewBag.TotalSubjects = await _context.Subjects.CountAsync();

                var roleCounts = await (
                    from ur in _context.UserRoles
                    join r in _context.Roles on ur.RoleId equals r.Id
                    group ur by r.Name into g
                    select new { RoleName = g.Key, Count = g.Count() }
                ).ToListAsync();

                ViewBag.RoleLabels = roleCounts.Select(x => x.RoleName).ToList();
                ViewBag.RoleCounts = roleCounts.Select(x => x.Count).ToList();

                // O'quvchilar — rol bo'yicha hisoblanadi (legacy GroupId'ga bog'liq emas).
                ViewBag.StudentCount = roleCounts
                    .FirstOrDefault(x => x.RoleName == "Student")?.Count ?? 0;

                // Guruhlar: sinf va yo'nalish bo'linishiligi (arxivdagi birga).
                ViewBag.ClassGroupCount = await _context.Groups
                    .CountAsync(g => g.Type == GroupType.Class);
                ViewBag.StreamGroupCount = await _context.Groups
                    .CountAsync(g => g.Type == GroupType.Stream);
                ViewBag.ArchivedGroupCount = await _context.Groups
                    .CountAsync(g => g.IsArchived);

                // Imtihonlar va natijalar;
                ViewBag.ExamCount = await _context.Exams.CountAsync();
                ViewBag.PendingExamResultCount = await _context.ExamResults
                    .CountAsync(r => !r.IsApproved);

                return View("AdminDashboard");
            }
            else if (role == "Moderator")
            {
                // Zauch uchun cheklangan panel: tahlil/imtihonlar va
                // tasdiqlanmagan natijalar ko'rsatilmaydi.
                ViewBag.TotalUsers = await _context.Users.CountAsync();
                ViewBag.TotalSubjects = await _context.Subjects.CountAsync();

                var studentRoleId = await _context.Roles
                    .Where(r => r.Name == "Student")
                    .Select(r => r.Id)
                    .FirstOrDefaultAsync();

                ViewBag.StudentCount = studentRoleId == null
                    ? 0
                    : await _context.UserRoles.CountAsync(ur => ur.RoleId == studentRoleId);

                ViewBag.ClassGroupCount = await _context.Groups
                    .CountAsync(g => g.Type == GroupType.Class);
                ViewBag.StreamGroupCount = await _context.Groups
                    .CountAsync(g => g.Type == GroupType.Stream);
                ViewBag.ArchivedGroupCount = await _context.Groups
                    .CountAsync(g => g.IsArchived);

                // Faqat o'qituvchi tasdiqlagan natijalar hisobga olinadi.
                ViewBag.ApprovedExamResultCount = await _context.ExamResults
                    .CountAsync(r => r.IsApproved);
                ViewBag.ApprovedExamCount = await _context.Exams
                    .CountAsync(e => e.Results.Any(r => r.IsApproved));

                return View("ModeratorDashboard");
            }
            else if (role == "Teacher")
            {
                var mySubjects = await _context.Subjects
                    .Where(s => s.TeacherId == user.Id)
                    .ToListAsync();

                var subjectIds = mySubjects.Select(s => s.Id).ToList();

                ViewBag.QuestionCount = await _context.Questions.CountAsync(q => subjectIds.Contains(q.SubjectId));

                var myExams = await _context.Exams
                    .Include(e => e.Results)
                    .Where(e => subjectIds.Contains(e.SubjectId))
                    .OrderBy(e => e.CreatedDate)
                    .ToListAsync();

                ViewBag.ExamCount = myExams.Count;
                ViewBag.ExamLabels = myExams.Select(e => e.Title).ToList();
                ViewBag.ExamAverages = myExams.Select(e =>
                    e.Results.Any()
                        ? Math.Round(e.Results.Average(r => r.TotalQuestions > 0 ? 100.0 * r.CorrectAnswers / r.TotalQuestions : 0), 1)
                        : 0
                ).ToList();

                // --- Davomat statistikasi (o'qituvchining barcha fanlari bo'yicha) ---
                var myAllAttendance = await _context.Attendances
                    .Where(a => subjectIds.Contains(a.SubjectId))
                    .Include(a => a.Subject)
                    .ToListAsync();

                var teacherAttendanceStats = myAllAttendance
                    .GroupBy(a => a.Subject?.Name ?? "")
                    .Select(g => new StudentAttendanceStatsViewModel
                    {
                        SubjectName = g.Key,
                        TotalLessons = g.Count(),
                        PresentCount = g.Count(a => a.Status == AttendanceStatus.Keldi)
                    })
                    .ToList();

                ViewBag.AttendanceStats = teacherAttendanceStats;

                var teacherTotalLessons = teacherAttendanceStats.Sum(s => s.TotalLessons);
                var teacherTotalPresent = teacherAttendanceStats.Sum(s => s.PresentCount);
                ViewBag.OverallAttendancePercentage = teacherTotalLessons == 0
                    ? 0
                    : Math.Round(100.0 * teacherTotalPresent / teacherTotalLessons, 1);

                var eightWeeksAgoT = _appClock.Today.AddDays(-7 * 8);
                ViewBag.WeeklyTrend = BuildWeeklyTrend(myAllAttendance.Where(a => a.Date >= eightWeeksAgoT));

                return View("TeacherDashboard", mySubjects);
            }
            else if (role == "Student")
            {
                // O'quvchining standart guruhi (fan bo'yicha guruhlar resolver).
                var defaultGroupId = await _subjectGroups.GetDefaultGroupIdAsync(user.Id);
                var myGroup = defaultGroupId is int defId
                    ? await _context.Groups
                        .AsNoTracking()
                        .FirstOrDefaultAsync(g => g.Id == defId)
                    : null;

                var myAllAttendance = await _context.Attendances
                    .Where(a => a.StudentId == user.Id)
                    .Include(a => a.Subject)
                    .ToListAsync();

                var attendanceStats = myAllAttendance
                    .GroupBy(a => a.Subject?.Name ?? "")
                    .Select(g => new StudentAttendanceStatsViewModel
                    {
                        SubjectName = g.Key,
                        TotalLessons = g.Count(),
                        PresentCount = g.Count(a => a.Status == AttendanceStatus.Keldi)
                    })
                    .ToList();

                ViewBag.AttendanceStats = attendanceStats;

                var totalLessons = attendanceStats.Sum(s => s.TotalLessons);
                var totalPresent = attendanceStats.Sum(s => s.PresentCount);
                ViewBag.OverallAttendancePercentage = totalLessons == 0
                    ? 0
                    : Math.Round(100.0 * totalPresent / totalLessons, 1);

                var eightWeeksAgo = _appClock.Today.AddDays(-7 * 8);
                ViewBag.WeeklyTrend = BuildWeeklyTrend(myAllAttendance.Where(a => a.Date >= eightWeeksAgo));

                var examResults = await _context.ExamResults
                    .Include(r => r.Exam)
                    .Where(r => r.StudentId == user.Id)
                    .OrderBy(r => r.CompletedDate)
                    .ToListAsync();

                ViewBag.ExamResultLabels = examResults.Select(r => r.Exam?.Title ?? "").ToList();
                ViewBag.ExamResultPercents = examResults.Select(r =>
                    r.TotalQuestions > 0 ? Math.Round(100.0 * r.CorrectAnswers / r.TotalQuestions, 1) : 0
                ).ToList();
                // --- Baholash: 100 ballik fan baholari o'rtachasi va guruh reytingi ---
                if (myGroup != null)
                {
                    var gradeSubjectIds = await _subjectGroups.GetSubjectIdsAsync(user.Id);

                    if (gradeSubjectIds.Count > 0)
                    {
                        var groupMap = await _subjectGroups.GetGroupMapAsync(user.Id, gradeSubjectIds);

                        // Har fan bo'yicha o'sha guruhdagi o'quvchilar ro'yxati.
                        var rosters = new Dictionary<int, HashSet<string>>();
                        foreach (var subjectId in gradeSubjectIds)
                        {
                            if (!groupMap.TryGetValue(subjectId, out var subjectGroupId))
                            {
                                continue;
                            }

                            var ids = await _subjectGroups.GetStudentIdsAsync(subjectGroupId, subjectId);
                            rosters[subjectId] = ids.ToHashSet(StringComparer.Ordinal);
                        }

                        var participantIds = rosters.Values
                            .SelectMany(ids => ids)
                            .Append(user.Id)
                            .Distinct(StringComparer.Ordinal)
                            .ToList();

                        var gradeMap = await _subjectGrades.GetGradesForStudentsAsync(
                            participantIds, gradeSubjectIds);

                        var participants = await _context.Users
                            .AsNoTracking()
                            .Where(u => participantIds.Contains(u.Id))
                            .ToListAsync();

                        double ComputeAverage(string studentId)
                        {
                            var scores = new List<double>();

                            foreach (var subjectId in gradeSubjectIds)
                            {
                                if (!rosters.TryGetValue(subjectId, out var roster) || !roster.Contains(studentId))
                                {
                                    continue;
                                }

                                if (gradeMap.TryGetValue(studentId, out var grades)
                                    && grades.TryGetValue(subjectId, out var grade)
                                    && grade.HasScore)
                                {
                                    scores.Add(grade.Score!.Value);
                                }
                            }

                            return scores.Count == 0
                                ? 0
                                : Math.Round(scores.Average(), 1, MidpointRounding.AwayFromZero);
                        }

                        var ranking = participants
                            .Select(s => new { s.Id, Avg = ComputeAverage(s.Id) })
                            .OrderByDescending(x => x.Avg)
                            .ThenBy(x => x.Id, StringComparer.Ordinal)
                            .ToList();

                        var myAverage = ranking.FirstOrDefault(x => x.Id == user.Id)?.Avg;

                        if (myAverage.HasValue)
                        {
                            ViewBag.GradeAveragePercentage = myAverage.Value;
                            ViewBag.GradeRank = ranking.FindIndex(x => x.Id == user.Id) + 1;
                            ViewBag.GradeRankTotal = ranking.Count;
                        }
                    }
                }
                return View("StudentDashboard", myGroup);
            }

            return View();
        }

        // Davomat yozuvlarini haftalar bo'yicha guruhlab, har hafta uchun foizni hisoblaydi
        // (oxirgi "weeks" ta haftani qaytaradi, chiziqli trend grafik uchun)
        private static List<WeeklyAttendanceTrendViewModel> BuildWeeklyTrend(IEnumerable<Attendance> records, int weeks = 8)
        {
            DateTime StartOfWeek(DateTime dt)
            {
                int diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
                return dt.AddDays(-diff).Date;
            }

            var grouped = records
                .GroupBy(a => StartOfWeek(a.Date))
                .Select(g => new WeeklyAttendanceTrendViewModel
                {
                    WeekStart = g.Key,
                    Percentage = g.Count() == 0
                        ? 0
                        : Math.Round(100.0 * g.Count(a => a.Status == AttendanceStatus.Keldi) / g.Count(), 1)
                })
                .OrderBy(w => w.WeekStart)
                .ToList();

            return grouped.Count > weeks ? grouped.Skip(grouped.Count - weeks).ToList() : grouped;
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}