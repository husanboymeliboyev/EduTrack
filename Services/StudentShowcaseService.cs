using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    public class StudentShowcaseService : IStudentShowcaseService
    {
        private readonly ApplicationDbContext _context;

        // PerformanceCriteria jadvali hali seed qilinmagan yoki bo'sh bo'lsa ham
        // dastur qulamasligi uchun standart og'irliklar (50/25/15/10).
        private static readonly Dictionary<string, double> DefaultWeights = new()
        {
            ["OverallGrade"] = 50,
            ["Attendance"] = 25,
            ["AssignmentCompletion"] = 15,
            ["Trend"] = 10
        };

        private const int TrendWeeksCount = 4;
        private const int AttendanceWindowDays = 30;

        public StudentShowcaseService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ================== Ommaviy metodlar ==================

        public async Task<StudentShowcaseViewModel> GetStudentShowcaseAsync(string studentId)
        {
            var student = await _context.Users
                .Include(u => u.Group)
                .FirstOrDefaultAsync(u => u.Id == studentId);

            if (student == null)
            {
                // Talaba topilmasa ham xato tashlamaymiz — neytral bo'sh natija qaytaramiz
                return new StudentShowcaseViewModel
                {
                    StudentId = studentId,
                    StudentName = "Noma'lum talaba",
                    StatusZone = "Yellow"
                };
            }

            var weights = await GetWeightsAsync();
            var metrics = await ComputeMetricsAsync(student, weights);

            var vm = new StudentShowcaseViewModel
            {
                StudentId = student.Id,
                StudentName = student.FullName ?? student.UserName ?? "Noma'lum talaba",
                GroupName = student.Group?.Name ?? string.Empty,
                OverallScore = metrics.OverallScore,
                GradeScore = metrics.GradeScore,
                AttendancePercentage = metrics.AttendancePercentage,
                AssignmentCompletionPercentage = metrics.AssignmentCompletionPercentage,
                TrendScore = metrics.TrendScore,
                SubjectBreakdown = metrics.SubjectBreakdown,
                WeeklyTrend = metrics.WeeklyTrend,
                AttendanceCalendar = metrics.AttendanceCalendar
            };

            vm.StatusZone = GetStatusZone(vm.OverallScore);
            vm.StatusReasons = BuildStatusReasons(metrics);
            vm.Badges = BuildBadges(metrics);

            if (student.GroupId.HasValue)
            {
                var (rank, size) = await GetGroupRankAsync(student.GroupId.Value, studentId, weights);
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

            var students = await _context.Users
                .Where(u => u.GroupId == groupId)
                .ToListAsync();

            if (!students.Any())
                return overview;

            var weights = await GetWeightsAsync();
            var rows = new List<(StudentShowcaseSummary Summary, double Attendance)>();

            foreach (var student in students)
            {
                var metrics = await ComputeMetricsAsync(student, weights);
                rows.Add((new StudentShowcaseSummary
                {
                    StudentId = student.Id,
                    StudentName = student.FullName ?? student.UserName ?? "Noma'lum talaba",
                    OverallScore = metrics.OverallScore,
                    StatusZone = GetStatusZone(metrics.OverallScore)
                }, metrics.AttendancePercentage));
            }

            var ranked = rows.OrderByDescending(r => r.Summary.OverallScore).ToList();
            for (int i = 0; i < ranked.Count; i++)
                ranked[i].Summary.Rank = i + 1;

            overview.Students = ranked.Select(r => r.Summary).ToList();
            overview.ClassAverageScore = Math.Round(ranked.Average(r => r.Summary.OverallScore), 1);
            overview.ClassAverageAttendance = Math.Round(ranked.Average(r => r.Attendance), 1);

            return overview;
        }

        public async Task<List<PerformanceCriteria>> GetCriteriaAsync()
        {
            var criteria = await _context.PerformanceCriterias.OrderBy(c => c.Id).ToListAsync();

            if (criteria.Any())
                return criteria;

            // Seed hali bajarilmagan bo'lsa — standart qiymatlarni "virtual" qatorlar sifatida qaytaramiz
            return DefaultWeights.Select(kv => new PerformanceCriteria
            {
                Key = kv.Key,
                DisplayName = GetDefaultDisplayName(kv.Key),
                Weight = kv.Value
            }).ToList();
        }

        public async Task<bool> UpdateCriteriaAsync(Dictionary<string, double> weightsByKey)
        {
            if (weightsByKey == null || !weightsByKey.Any())
                return false;

            var sum = weightsByKey.Values.Sum();
            if (Math.Abs(sum - 100.0) > 0.01)
                return false;

            var criteria = await _context.PerformanceCriterias.ToListAsync();
            if (!criteria.Any())
                return false;

            var changed = false;
            foreach (var item in criteria)
            {
                if (weightsByKey.TryGetValue(item.Key, out var newWeight))
                {
                    item.Weight = newWeight;
                    changed = true;
                }
            }

            if (!changed)
                return false;

            await _context.SaveChangesAsync();
            return true;
        }

        // ================== Ichki hisoblash mantig'i ==================

        private record MetricsResult(
            double GradeScore,
            double AttendancePercentage,
            double AssignmentCompletionPercentage,
            double TrendScore,
            double OverallScore,
            List<SubjectPerformanceItem> SubjectBreakdown,
            List<WeeklyTrendPoint> WeeklyTrend,
            List<DailyAttendancePoint> AttendanceCalendar);

        private async Task<MetricsResult> ComputeMetricsAsync(ApplicationUser student, Dictionary<string, double> weights)
        {
            var (gradeScore, subjectBreakdown) = await GetGradeScoreAsync(student.Id, student.GroupId);
            var (attendancePercentage, attendanceCalendar) = await GetAttendanceAsync(student.Id);
            var assignmentCompletion = await GetAssignmentCompletionAsync(student.Id, student.GroupId);
            var weeklyTrend = await GetWeeklyTrendAsync(student.Id, TrendWeeksCount);
            var trendScore = CalculateTrendScore(weeklyTrend);

            var overall = CalculateOverallScore(gradeScore, attendancePercentage, assignmentCompletion, trendScore, weights);

            return new MetricsResult(
                Math.Round(gradeScore, 1),
                Math.Round(attendancePercentage, 1),
                Math.Round(assignmentCompletion, 1),
                Math.Round(trendScore, 1),
                Math.Round(overall, 1),
                subjectBreakdown,
                weeklyTrend,
                attendanceCalendar);
        }

        // 1) GradeScore — talaba guruhiga biriktirilgan fanlar bo'yicha barcha
        // GradeComponent'lar: (olingan ballar yig'indisi / maksimal ball yig'indisi) * 100
        private async Task<(double GradeScore, List<SubjectPerformanceItem> Breakdown)> GetGradeScoreAsync(string studentId, int? groupId)
        {
            if (!groupId.HasValue)
                return (0, new List<SubjectPerformanceItem>());

            var subjectIds = await _context.GroupSubjects
                .Where(gs => gs.GroupId == groupId.Value)
                .Select(gs => gs.SubjectId)
                .ToListAsync();

            if (!subjectIds.Any())
                return (0, new List<SubjectPerformanceItem>());

            var components = await _context.GradeComponents
                .Where(c => subjectIds.Contains(c.SubjectId))
                .Include(c => c.Subject)
                .ToListAsync();

            if (!components.Any())
                return (0, new List<SubjectPerformanceItem>());

            var componentIds = components.Select(c => c.Id).ToList();
            var grades = await _context.StudentGrades
                .Where(g => g.StudentId == studentId && componentIds.Contains(g.GradeComponentId))
                .ToListAsync();

            var breakdown = new List<SubjectPerformanceItem>();

            foreach (var subjectGroup in components.GroupBy(c => c.SubjectId))
            {
                var maxTotal = subjectGroup.Sum(c => c.MaxScore);
                if (maxTotal <= 0) continue;

                var scoreTotal = subjectGroup.Sum(c =>
                    grades.FirstOrDefault(g => g.GradeComponentId == c.Id)?.Score ?? 0);

                var subjectName = subjectGroup.First().Subject?.Name ?? "Noma'lum fan";
                breakdown.Add(new SubjectPerformanceItem
                {
                    SubjectName = subjectName,
                    Percentage = Math.Round(scoreTotal / maxTotal * 100.0, 1)
                });
            }

            var overallMax = components.Sum(c => c.MaxScore);
            if (overallMax <= 0)
                return (0, breakdown);

            var overallScoreSum = components.Sum(c =>
                grades.FirstOrDefault(g => g.GradeComponentId == c.Id)?.Score ?? 0);

            var gradeScore = overallScoreSum / overallMax * 100.0;
            return (gradeScore, breakdown);
        }

        // 2) AttendancePercentage — oxirgi 30 kunlik davomat: "Keldi" / jami dars kunlari * 100
        private async Task<(double Percentage, List<DailyAttendancePoint> Calendar)> GetAttendanceAsync(string studentId)
        {
            var since = DateTime.Now.Date.AddDays(-AttendanceWindowDays);

            var records = await _context.Attendances
                .Where(a => a.StudentId == studentId && a.Date >= since)
                .ToListAsync();

            if (!records.Any())
                return (0, new List<DailyAttendancePoint>());

            var cameCount = records.Count(a => a.Status == AttendanceStatus.Keldi);
            var percentage = (double)cameCount / records.Count * 100.0;

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

            return (percentage, calendar);
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

        // 3) AssignmentCompletionPercentage — talabaga tegishli topshiriqlardan nechtasiga
        // Submission topshirilgani / jami topshiriqlar soni * 100
        private async Task<double> GetAssignmentCompletionAsync(string studentId, int? groupId)
        {
            var assignmentIds = await _context.Assignments
                .Where(a => a.GroupId == null || a.GroupId == groupId)
                .Select(a => a.Id)
                .ToListAsync();

            if (!assignmentIds.Any())
                return 0;

            var submittedCount = await _context.Submissions
                .Where(s => s.StudentId == studentId && assignmentIds.Contains(s.AssignmentId))
                .Select(s => s.AssignmentId)
                .Distinct()
                .CountAsync();

            return (double)submittedCount / assignmentIds.Count * 100.0;
        }

        // 4) Haftalik o'rtacha ball trendi (eng eskisidan eng yangisiga qarab tartiblangan)
        private async Task<List<WeeklyTrendPoint>> GetWeeklyTrendAsync(string studentId, int weeksCount)
        {
            var grades = await _context.StudentGrades
                .Include(g => g.GradeComponent)
                .Where(g => g.StudentId == studentId)
                .ToListAsync();

            var today = DateTime.Now.Date;
            // Joriy haftaning dushanbasi
            var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            var currentWeekStart = today.AddDays(-daysSinceMonday);

            var points = new List<WeeklyTrendPoint>();

            for (int i = weeksCount - 1; i >= 0; i--)
            {
                var weekStart = currentWeekStart.AddDays(-7 * i);
                var weekEnd = weekStart.AddDays(7);

                var weekGrades = grades
                    .Where(g => g.UpdatedDate >= weekStart && g.UpdatedDate < weekEnd
                                && g.GradeComponent != null && g.GradeComponent.MaxScore > 0)
                    .ToList();

                double avg = 0;
                if (weekGrades.Any())
                    avg = weekGrades.Average(g => g.Score / g.GradeComponent!.MaxScore * 100.0);

                points.Add(new WeeklyTrendPoint
                {
                    WeekStart = weekStart,
                    AveragePercentage = Math.Round(avg, 1)
                });
            }

            return points;
        }

        private static double CalculateTrendScore(List<WeeklyTrendPoint> weeklyTrend)
        {
            if (weeklyTrend.Count < 2)
                return 0;

            var first = weeklyTrend.First().AveragePercentage;
            var last = weeklyTrend.Last().AveragePercentage;

            return Math.Clamp(last - first, -100, 100);
        }

        // 5) OverallScore — og'irlikli formula (Trend -100..100 dan 0..100 ga normalizatsiya qilinadi)
        private static double CalculateOverallScore(
            double gradeScore,
            double attendancePercentage,
            double assignmentCompletion,
            double trendScore,
            Dictionary<string, double> weights)
        {
            var normalizedTrend = (trendScore + 100) / 2.0;

            var overall =
                gradeScore * (GetWeight(weights, "OverallGrade") / 100.0) +
                attendancePercentage * (GetWeight(weights, "Attendance") / 100.0) +
                assignmentCompletion * (GetWeight(weights, "AssignmentCompletion") / 100.0) +
                normalizedTrend * (GetWeight(weights, "Trend") / 100.0);

            return Math.Clamp(overall, 0, 100);
        }

        private static double GetWeight(Dictionary<string, double> weights, string key)
            => weights.TryGetValue(key, out var w) ? w : DefaultWeights[key];

        // 6) Status zonasi va sabablar
        private static string GetStatusZone(double overallScore)
        {
            if (overallScore >= 75) return "Green";
            if (overallScore >= 50) return "Yellow";
            return "Red";
        }

        private static List<string> BuildStatusReasons(MetricsResult m)
        {
            var reasons = new List<string>();

            AddReason(reasons, "Ball", m.GradeScore);
            AddReason(reasons, "Davomat", m.AttendancePercentage);
            AddReason(reasons, "Topshiriqlar", m.AssignmentCompletionPercentage);

            if (m.TrendScore <= -10)
                reasons.Add($"Trend past — pasaymoqda ({m.TrendScore:0.#} foiz punkt)");
            else if (m.TrendScore >= 10)
                reasons.Add($"Trend yaxshi — o'smoqda (+{m.TrendScore:0.#} foiz punkt)");

            return reasons;
        }

        private static void AddReason(List<string> reasons, string label, double value)
        {
            if (value < 60)
                reasons.Add($"{label} past — {value:0.#}%");
            else if (value >= 80)
                reasons.Add($"{label} yaxshi — {value:0.#}%");
        }

        // 7) Yutuqlar — bazada saqlanmaydi, har chaqiriqda hisoblab chiqariladi
        private static List<string> BuildBadges(MetricsResult m)
        {
            var badges = new List<string>();

            if (m.WeeklyTrend.Count >= 3)
            {
                var lastThree = m.WeeklyTrend.Skip(Math.Max(0, m.WeeklyTrend.Count - 3)).Take(3).ToList();
                if (lastThree.Count == 3 && lastThree.All(w => w.AveragePercentage >= 90))
                    badges.Add("3 hafta ketma-ket 90%+");
            }

            if (m.AttendanceCalendar.Any() &&
                m.AttendanceCalendar.All(a => a.Status != AttendanceStatus.Kelmadi.ToString()
                                            && a.Status != AttendanceStatus.KechQoldi.ToString()))
            {
                badges.Add("Hech qachon kechikmagan");
            }

            if (m.AssignmentCompletionPercentage >= 100)
                badges.Add("Barcha topshiriqlar bajarilgan");

            if (m.OverallScore >= 90)
                badges.Add("Yuqori natija");

            if (m.TrendScore >= 15)
                badges.Add("Kuchli o'sish tendensiyasi");

            return badges;
        }

        // 8) Guruh ichidagi o'rin
        private async Task<(int Rank, int Size)> GetGroupRankAsync(int groupId, string studentId, Dictionary<string, double> weights)
        {
            var students = await _context.Users
                .Where(u => u.GroupId == groupId)
                .ToListAsync();

            if (!students.Any())
                return (1, 1);

            var scored = new List<(string StudentId, double Score)>();
            foreach (var s in students)
            {
                var metrics = await ComputeMetricsAsync(s, weights);
                scored.Add((s.Id, metrics.OverallScore));
            }

            var ranked = scored.OrderByDescending(s => s.Score).ToList();
            var rank = ranked.FindIndex(s => s.StudentId == studentId) + 1;

            return (rank <= 0 ? ranked.Count : rank, ranked.Count);
        }

        // ================== Og'irliklar (PerformanceCriteria) ==================

        private async Task<Dictionary<string, double>> GetWeightsAsync()
        {
            var weights = new Dictionary<string, double>(DefaultWeights);

            try
            {
                var criteria = await _context.PerformanceCriterias.ToListAsync();
                foreach (var c in criteria)
                {
                    if (!string.IsNullOrWhiteSpace(c.Key))
                        weights[c.Key] = c.Weight;
                }
            }
            catch
            {
                // Jadval hali migratsiya qilinmagan/bo'sh bo'lsa ham dastur qulamasin —
                // standart og'irliklar bilan davom etadi
            }

            return weights;
        }

        private static string GetDefaultDisplayName(string key) => key switch
        {
            "OverallGrade" => "Umumiy ball",
            "Attendance" => "Davomat",
            "AssignmentCompletion" => "Topshiriqlar",
            "Trend" => "Trend",
            _ => key
        };
    }
}