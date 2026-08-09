using EduTrack.Data;
using EduTrack.Models;
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

        public HomeController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
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

            if (role == "Admin")
            {
                ViewBag.TotalUsers = await _context.Users.CountAsync();
                ViewBag.TotalGroups = await _context.Groups.CountAsync();
                ViewBag.TotalSubjects = await _context.Subjects.CountAsync();
                ViewBag.TotalStudents = await _context.Users.CountAsync(u => u.GroupId != null);

                var roleCounts = await (
                    from ur in _context.UserRoles
                    join r in _context.Roles on ur.RoleId equals r.Id
                    group ur by r.Name into g
                    select new { RoleName = g.Key, Count = g.Count() }
                ).ToListAsync();

                ViewBag.RoleLabels = roleCounts.Select(x => x.RoleName).ToList();
                ViewBag.RoleCounts = roleCounts.Select(x => x.Count).ToList();

                return View("AdminDashboard");
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

                var eightWeeksAgoT = DateTime.Today.AddDays(-7 * 8);
                ViewBag.WeeklyTrend = BuildWeeklyTrend(myAllAttendance.Where(a => a.Date >= eightWeeksAgoT));

                return View("TeacherDashboard", mySubjects);
            }
            else if (role == "Student")
            {
                var myGroup = await _context.Groups
                    .Include(g => g.Students)
                    .FirstOrDefaultAsync(g => g.Id == user.GroupId);

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

                var eightWeeksAgo = DateTime.Today.AddDays(-7 * 8);
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