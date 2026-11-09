using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    /// <summary>
    /// Talaba ko'rgazmasi va diagnostik hisobot. Asosiy ko'rsatkich — faqat
    /// 100 ballik yakuniy baholar (qo'lda baho yoki tasdiqlangan imtihon
    /// o'rtachasi). Davomat va topshiriqlar umumiy bahoga kirmaydi.
    /// </summary>
    public class StudentShowcaseService : IStudentShowcaseService
    {
        private const int AttendanceWindowDays = 30;

        private readonly ApplicationDbContext _context;
        private readonly IAppClock _appClock;
        private readonly IStudentSubjectGroupService _subjectGroups;
        private readonly ISubjectGradeService _subjectGrades;

        public StudentShowcaseService(
            ApplicationDbContext context,
            IAppClock appClock,
            IStudentSubjectGroupService subjectGroups,
            ISubjectGradeService subjectGrades)
        {
            _context = context;
            _appClock = appClock;
            _subjectGroups = subjectGroups;
            _subjectGrades = subjectGrades;
        }

        // ================== Ommaviy metodlar ==================

        public async Task<StudentShowcaseViewModel> GetStudentShowcaseAsync(string studentId)
        {
            var student = await _context.Users
                .Include(u => u.Group)
                .FirstOrDefaultAsync(u => u.Id == studentId);

            if (student == null)
            {
                return new StudentShowcaseViewModel
                {
                    StudentId = studentId,
                    StudentName = "Noma'lum talaba",
                    StatusZone = "Muted"
                };
            }

            var subjectIds = await _subjectGroups.GetSubjectIdsAsync(student.Id);
            var grades = await _subjectGrades.GetStudentGradesAsync(student.Id, subjectIds);

            var breakdown = grades.Values
                .Where(g => g.HasScore)
                .OrderBy(g => g.SubjectName, StringComparer.Ordinal)
                .Select(g => new SubjectPerformanceItem
                {
                    SubjectName = g.SubjectName,
                    Percentage = g.Score!.Value
                })
                .ToList();

            var overall = breakdown.Count == 0
                ? 0
                : Math.Round(breakdown.Average(s => s.Percentage), 1, MidpointRounding.AwayFromZero);

            var (calendar, attendancePercentage) = await GetAttendanceAsync(student.Id);

            var vm = new StudentShowcaseViewModel
            {
                StudentId = student.Id,
                StudentName = student.FullName ?? student.UserName ?? "Noma'lum talaba",
                GroupName = student.Group?.Name ?? string.Empty,
                OverallScore = overall,
                SubjectBreakdown = breakdown,
                AttendanceCalendar = calendar,
                AttendancePercentage = attendancePercentage
            };

            vm.StatusZone = breakdown.Count == 0 ? "Muted" : GetStatusZone(overall);
            vm.StatusReasons = BuildStatusReasons(breakdown);
            vm.Badges = BuildBadges(breakdown, calendar, overall);

            if (student.GroupId.HasValue)
            {
                var (rank, size) = await GetGroupRankAsync(student.GroupId.Value, studentId);
                vm.GroupRank = rank;
                vm.GroupSize = size;
            }
            else
            {
                vm.GroupRank = 1;
                vm.GroupSize = 1;
            }

            return vm;
        }

        public async Task<ClassOverviewViewModel> GetClassOverviewAsync(int groupId)
        {
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.Id == groupId);

            var overview = new ClassOverviewViewModel
            {
                GroupName = group?.Name ?? string.Empty
            };

            var students = await GetGroupStudentsAsync(groupId);

            if (!students.Any())
                return overview;

            var subjectIds = await _context.GroupSubjects
                .AsNoTracking()
                .Where(gs => gs.GroupId == groupId)
                .Select(gs => gs.SubjectId)
                .Distinct()
                .ToListAsync();

            var studentIds = students.Select(s => s.Id).ToList();
            var grades = await _subjectGrades.GetGradesForStudentsAsync(studentIds, subjectIds);

            var since = _appClock.Today.AddDays(-AttendanceWindowDays);
            var attendanceRecords = await _context.Attendances
                .AsNoTracking()
                .Where(a => studentIds.Contains(a.StudentId) && a.Date >= since)
                .Select(a => new { a.StudentId, a.Status })
                .ToListAsync();

            var rows = students.Select(student =>
            {
                var score = AverageScore(grades, student.Id);
                var records = attendanceRecords.Where(a => a.StudentId == student.Id).ToList();
                var attendance = records.Count == 0
                    ? 0
                    : Math.Round(
                        100.0 * records.Count(a => a.Status == AttendanceStatus.Keldi) / records.Count,
                        1,
                        MidpointRounding.AwayFromZero);

                return new
                {
                    Summary = new StudentShowcaseSummary
                    {
                        StudentId = student.Id,
                        StudentName = student.FullName ?? student.UserName ?? "Noma'lum talaba",
                        OverallScore = score,
                        StatusZone = score == 0 ? "Muted" : GetStatusZone(score)
                    },
                    Attendance = attendance
                };
            }).ToList();

            var ranked = rows
                .OrderByDescending(r => r.Summary.OverallScore)
                .ThenBy(r => r.Summary.StudentName, StringComparer.Ordinal)
                .ToList();

            for (int i = 0; i < ranked.Count; i++)
                ranked[i].Summary.Rank = i + 1;

            overview.Students = ranked.Select(r => r.Summary).ToList();
            overview.ClassAverageScore = ranked.Count == 0
                ? 0
                : Math.Round(ranked.Average(r => r.Summary.OverallScore), 1, MidpointRounding.AwayFromZero);
            overview.ClassAverageAttendance = ranked.Count == 0
                ? 0
                : Math.Round(ranked.Average(r => r.Attendance), 1, MidpointRounding.AwayFromZero);

            return overview;
        }

        // ================== Ichki yordamchilar ==================

        private static double AverageScore(
            IReadOnlyDictionary<string, IReadOnlyDictionary<int, StudentSubjectGradeViewModel>> grades,
            string studentId)
        {
            if (!grades.TryGetValue(studentId, out var subjects))
            {
                return 0;
            }

            var scores = subjects.Values
                .Where(s => s.HasScore)
                .Select(s => s.Score!.Value)
                .ToList();

            return scores.Count == 0
                ? 0
                : Math.Round(scores.Average(), 1, MidpointRounding.AwayFromZero);
        }

        private async Task<(List<DailyAttendancePoint> Calendar, double Percentage)> GetAttendanceAsync(string studentId)
        {
            var since = _appClock.Today.AddDays(-AttendanceWindowDays);

            var records = await _context.Attendances
                .AsNoTracking()
                .Where(a => a.StudentId == studentId && a.Date >= since)
                .ToListAsync();

            if (!records.Any())
                return (new List<DailyAttendancePoint>(), 0);

            var cameCount = records.Count(a => a.Status == AttendanceStatus.Keldi);
            var percentage = Math.Round(100.0 * cameCount / records.Count, 1, MidpointRounding.AwayFromZero);

            // Bir kunda bir nechta fan bo'lsa, kalendarda eng "yomon" holatni ko'rsatamiz
            var calendar = records
                .GroupBy(a => a.Date.Date)
                .Select(g => new DailyAttendancePoint
                {
                    Date = g.Key,
                    Status = PickWorstStatus(g.Select(a => a.Status)).ToString()
                })
                .OrderBy(p => p.Date)
                .ToList();

            return (calendar, percentage);
        }

        private static AttendanceStatus PickWorstStatus(IEnumerable<AttendanceStatus> statuses)
        {
            var priority = new[] { AttendanceStatus.Kelmadi, AttendanceStatus.KechQoldi, AttendanceStatus.Sababli, AttendanceStatus.Keldi };
            foreach (var status in priority)
            {
                if (statuses.Contains(status))
                    return status;
            }
            return AttendanceStatus.Keldi;
        }

        private static string GetStatusZone(double overallScore)
        {
            if (overallScore >= 75) return "Green";
            if (overallScore >= 50) return "Yellow";
            return "Red";
        }

        private static List<string> BuildStatusReasons(List<SubjectPerformanceItem> breakdown)
        {
            if (breakdown.Count == 0)
            {
                return new List<string> { "Baholar hali kiritilmagan" };
            }

            var reasons = new List<string>();

            var weak = breakdown.Where(s => s.Percentage < 55).ToList();
            if (weak.Count > 0)
            {
                reasons.Add("Past baholi fanlar: " +
                            string.Join(", ", weak.Select(s => $"{s.SubjectName} — {s.Percentage:0.#}")));
            }

            var shaky = breakdown.Where(s => s.Percentage >= 55 && s.Percentage < 71).ToList();
            if (shaky.Count > 0)
            {
                reasons.Add("O'rtacha fanlar: " +
                            string.Join(", ", shaky.Select(s => $"{s.SubjectName} — {s.Percentage:0.#}")));
            }

            if (reasons.Count == 0)
            {
                reasons.Add("Barcha fanlardan qoniqarli va yuqori natija");
            }

            return reasons;
        }

        private static List<string> BuildBadges(
            List<SubjectPerformanceItem> breakdown,
            List<DailyAttendancePoint> calendar,
            double overall)
        {
            var badges = new List<string>();

            if (breakdown.Count >= 3 && breakdown.All(s => s.Percentage >= 90))
                badges.Add("Barcha fanlardan 90+");

            if (breakdown.Count > 0 && breakdown.All(s => s.Percentage >= 80))
                badges.Add("Barcha fanlardan 80+");

            if (breakdown.Count > 0 && breakdown.All(s => s.Percentage >= 55))
                badges.Add("Barcha fanlardan qoniqarli");

            if (overall >= 90)
                badges.Add("Yuqori natija");

            if (calendar.Any() &&
                calendar.All(a => a.Status != AttendanceStatus.Kelmadi.ToString()
                                  && a.Status != AttendanceStatus.KechQoldi.ToString()))
            {
                badges.Add("Hech qachon kechikmagan");
            }

            return badges;
        }

        // Guruh a'zolari — asosiy manba aktiv GroupMembership; eski hisoblar
        // uchun yangi a'zolik yozuvi bo'lmasa legacy GroupId zaxira.
        private async Task<List<ApplicationUser>> GetGroupStudentsAsync(int groupId)
        {
            var memberIds = (await _context.GroupMemberships
                    .AsNoTracking()
                    .Where(m => m.IsActive && m.GroupId == groupId)
                    .Select(m => m.StudentId)
                    .ToListAsync())
                .ToHashSet(StringComparer.Ordinal);

            return await _context.Users
                .AsNoTracking()
                .Where(u => u.GroupId == groupId || memberIds.Contains(u.Id))
                .ToListAsync();
        }

        private async Task<(int Rank, int Size)> GetGroupRankAsync(int groupId, string studentId)
        {
            var students = await GetGroupStudentsAsync(groupId);

            if (!students.Any())
                return (1, 1);

            var subjectIds = await _context.GroupSubjects
                .AsNoTracking()
                .Where(gs => gs.GroupId == groupId)
                .Select(gs => gs.SubjectId)
                .Distinct()
                .ToListAsync();

            var studentIds = students.Select(s => s.Id).ToList();
            var grades = await _subjectGrades.GetGradesForStudentsAsync(studentIds, subjectIds);

            var ranked = students
                .Select(s => new { s.Id, Score = AverageScore(grades, s.Id) })
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.Id, StringComparer.Ordinal)
                .ToList();

            var rank = ranked.FindIndex(s => s.Id == studentId) + 1;

            return (rank <= 0 ? ranked.Count : rank, ranked.Count);
        }
    }
}
