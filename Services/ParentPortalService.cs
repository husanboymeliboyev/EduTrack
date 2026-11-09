using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    /// <summary>
    /// Ota-ona panelining yagona ma'lumot manbai. Barcha so'rovlar ota-ona
    /// bilan bog'langan farzand(lar) doirasida cheklanadi.
    /// </summary>
    public sealed class ParentPortalService : IParentPortalService
    {
        private const int AttendanceCalendarDays = 30;
        private const int RecentAttendanceCount = 20;

        private readonly ApplicationDbContext _db;
        private readonly IStudentSubjectGroupService _subjectGroups;
        private readonly IAppClock _clock;
        private readonly IMonthlyRatingService _monthlyRating;
        private readonly ISubjectGradeService _subjectGrades;

        public ParentPortalService(
            ApplicationDbContext db,
            IStudentSubjectGroupService subjectGroups,
            IAppClock clock,
            IMonthlyRatingService monthlyRating,
            ISubjectGradeService subjectGrades)
        {
            _db = db;
            _subjectGroups = subjectGroups;
            _clock = clock;
            _monthlyRating = monthlyRating;
            _subjectGrades = subjectGrades;
        }

        // ==================== Farzandlar ====================

        public async Task<IReadOnlyList<ParentChildViewModel>> GetChildrenAsync(
            string parentId, CancellationToken cancellationToken = default)
        {
            var links = await _db.ParentStudentLinks
                .AsNoTracking()
                .Where(l => l.ParentId == parentId)
                .Select(l => new
                {
                    l.StudentId,
                    FullName = l.Student != null
                        ? (l.Student.FullName ?? l.Student.UserName)
                        : null,
                    LegacyGroupName = l.Student != null && l.Student.Group != null
                        ? l.Student.Group.Name
                        : null
                })
                .ToListAsync(cancellationToken);

            if (links.Count == 0)
            {
                return Array.Empty<ParentChildViewModel>();
            }

            var studentIds = links.Select(l => l.StudentId).ToList();

            var memberships = await _db.GroupMemberships
                .AsNoTracking()
                .Where(m => m.IsActive && studentIds.Contains(m.StudentId))
                .Select(m => new
                {
                    m.StudentId,
                    m.Scope,
                    GroupName = m.Group != null ? m.Group.Name : null
                })
                .ToListAsync(cancellationToken);

            return links
                .Select(l =>
                {
                    var classGroup = memberships.FirstOrDefault(
                        m => m.StudentId == l.StudentId && m.Scope == GroupType.Class)?.GroupName;
                    var streamGroup = memberships.FirstOrDefault(
                        m => m.StudentId == l.StudentId && m.Scope == GroupType.Stream)?.GroupName;

                    var displayGroup = !string.IsNullOrWhiteSpace(classGroup)
                        ? classGroup!
                        : !string.IsNullOrWhiteSpace(streamGroup)
                            ? streamGroup!
                            : l.LegacyGroupName ?? string.Empty;

                    return new ParentChildViewModel
                    {
                        StudentId = l.StudentId,
                        FullName = string.IsNullOrWhiteSpace(l.FullName)
                            ? l.StudentId
                            : l.FullName!,
                        ClassName = classGroup ?? string.Empty,
                        StreamName = streamGroup ?? string.Empty,
                        GroupName = displayGroup
                    };
                })
                .OrderBy(c => c.FullName)
                .ToList();
        }

        public Task<bool> HasChildrenAsync(string parentId, CancellationToken cancellationToken = default)
            => _db.ParentStudentLinks
                .AsNoTracking()
                .AnyAsync(l => l.ParentId == parentId, cancellationToken);

        // ==================== Umumiy ko'rinish ====================

        public async Task<ParentDashboardViewModel> GetDashboardAsync(
            string parentId, CancellationToken cancellationToken = default)
        {
            var parentName = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == parentId)
                .Select(u => u.FullName ?? u.UserName)
                .FirstOrDefaultAsync(cancellationToken);

            var model = new ParentDashboardViewModel
            {
                ParentName = string.IsNullOrWhiteSpace(parentName) ? "Ota-ona" : parentName!
            };

            var children = await GetChildrenAsync(parentId, cancellationToken);

            foreach (var child in children)
            {
                var metrics = await ComputeMetricsAsync(child, cancellationToken);
                child.SubjectCount = metrics.SubjectIds.Count;

                var overall = CalculateOverallPercentage(metrics);

                var summary = new ParentChildSummaryViewModel
                {
                    Child = child,
                    AttendancePercentage = metrics.AttendancePercentage,
                    GradePercentage = metrics.GradePercentage,
                    AssignmentCompletionPercentage = metrics.AssignmentCompletionPercentage,
                    OverallPercentage = overall,
                    StatusZone = GetStatusZone(overall, metrics),
                    TotalLessons = metrics.TotalLessons,
                    AbsentCount = metrics.AbsentCount,
                    LateCount = metrics.LateCount,
                    SubjectCount = metrics.SubjectIds.Count,
                    AssignmentCount = metrics.Assignments.Count,
                    UnsubmittedAssignments = metrics.PendingCount,
                    OverdueAssignments = metrics.OverdueCount,
                    ExamCount = metrics.Exams.Count,
                    PendingExamResults = metrics.PendingExamCount,
                    AverageExamPercentage = metrics.AverageExamPercentage,
                    GradedSubjects = metrics.GradedSubjects,
                    TotalSubjects = metrics.TotalSubjects,
                    Alerts = metrics.Alerts,
                    AttendanceTrend = BuildWeeklyAttendanceTrend(metrics.Attendances, _clock.Today)
                };

                await FillMonthlyRatingAsync(summary, child.StudentId, cancellationToken);

                model.Children.Add(summary);
            }

            return model;
        }

        /// <summary>
        /// Dashboard uchun oylik reytingning qisqa ko'rinishi: eng yaxshi
        /// qamrovli (sinf yoki yo'nalish) kartasi va 6 oylik trend.
        /// </summary>
        private async Task FillMonthlyRatingAsync(
            ParentChildSummaryViewModel summary,
            string studentId,
            CancellationToken cancellationToken)
        {
            var rating = await _monthlyRating.GetStudentRatingAsync(
                studentId, null, null, cancellationToken);

            if (rating is null)
            {
                return;
            }

            summary.MonthlyRatingPeriodLabel = rating.PeriodLabel;
            summary.RatingTrend = rating.Trend;

            MonthlyRatingScopeViewModel? best = null;
            if (rating.ClassRating?.AverageScore.HasValue == true
                && (rating.StreamRating?.AverageScore == null
                    || rating.ClassRating.AverageScore >= rating.StreamRating.AverageScore))
            {
                best = rating.ClassRating;
            }
            else
            {
                best = rating.StreamRating;
            }

            if (best?.AverageScore is not double score)
            {
                return;
            }

            summary.MonthlyRatingScore = score;
            summary.MonthlyRatingRank = best.Rank;
            summary.MonthlyRatingParticipants = best.ParticipantCount;
            summary.MonthlyRatingScopeLabel = best.ScopeShortLabel;
            summary.MonthlyRatingCoverage = best.CoveragePercentage;
        }

        /// <summary>
        /// Oxirgi 8 hafta bo'yicha davomat foizi. Faqat ma'lumot mavjud
        /// haftalar qaytariladi (bo'sh haftalar chartni chalg'itmasin).
        /// </summary>
        private static List<ParentTrendPointViewModel> BuildWeeklyAttendanceTrend(
            IReadOnlyCollection<Attendance> attendances,
            DateTime today)
        {
            var points = new List<ParentTrendPointViewModel>();

            var currentWeekStart = today.Date.AddDays(-(((int)today.DayOfWeek + 6) % 7));

            for (var i = 7; i >= 0; i--)
            {
                var weekStart = currentWeekStart.AddDays(-7 * i);
                var weekEnd = weekStart.AddDays(7);

                var weekItems = attendances
                    .Where(a => a.Date >= weekStart && a.Date < weekEnd)
                    .ToList();

                if (weekItems.Count == 0)
                {
                    continue;
                }

                points.Add(new ParentTrendPointViewModel
                {
                    Label = weekStart.ToString("dd.MM"),
                    TotalCount = weekItems.Count,
                    Percentage = Math.Round(
                        100.0 * weekItems.Count(a => a.Status == AttendanceStatus.Keldi) / weekItems.Count, 1)
                });
            }

            return points;
        }

        // ==================== Davomat ====================

        public async Task<ParentAttendanceViewModel?> GetAttendanceAsync(
            string parentId, string? studentId, CancellationToken cancellationToken = default)
        {
            var resolved = await ResolveChildAsync(parentId, studentId, cancellationToken);
            if (resolved is null) return null;

            var (child, children) = resolved.Value;
            var metrics = await ComputeMetricsAsync(child, cancellationToken);

            var bySubject = metrics.Attendances
                .GroupBy(a => a.Subject?.Name ?? "Noma'lum fan")
                .Select(g => new ParentSubjectAttendanceViewModel
                {
                    SubjectName = g.Key,
                    TotalCount = g.Count(),
                    PresentCount = g.Count(a => a.Status == AttendanceStatus.Keldi),
                    AbsentCount = g.Count(a => a.Status == AttendanceStatus.Kelmadi),
                    LateCount = g.Count(a => a.Status == AttendanceStatus.KechQoldi),
                    ExcusedCount = g.Count(a => a.Status == AttendanceStatus.Sababli)
                })
                .OrderBy(s => s.SubjectName)
                .ToList();

            var since = _clock.Today.AddDays(-AttendanceCalendarDays);
            var calendar = metrics.Attendances
                .Where(a => a.Date >= since)
                .GroupBy(a => a.Date.Date)
                .Select(g => new DailyAttendancePoint
                {
                    Date = g.Key,
                    Status = PickWorstStatus(g.Select(a => a.Status)).ToString()
                })
                .OrderBy(p => p.Date)
                .ToList();

            return new ParentAttendanceViewModel
            {
                Child = child,
                Children = children,
                ActiveAction = "Attendance",
                TotalCount = metrics.TotalLessons,
                PresentCount = metrics.PresentCount,
                AbsentCount = metrics.AbsentCount,
                LateCount = metrics.LateCount,
                ExcusedCount = metrics.ExcusedCount,
                Percentage = metrics.AttendancePercentage,
                BySubject = bySubject,
                Calendar = calendar,
                RecentRecords = metrics.Attendances
                    .Take(RecentAttendanceCount)
                    .Select(a =>
                    {
                        var (label, badge) = DescribeStatus(a.Status);
                        return new ParentAttendanceRecordViewModel
                        {
                            Date = a.Date,
                            SubjectName = a.Subject?.Name ?? "Noma'lum fan",
                            Status = a.Status,
                            StatusLabel = label,
                            BadgeClass = badge
                        };
                    })
                    .ToList()
            };
        }

        // ==================== Baholar ====================

        public async Task<ParentGradesViewModel?> GetGradesAsync(
            string parentId, string? studentId, CancellationToken cancellationToken = default)
        {
            var resolved = await ResolveChildAsync(parentId, studentId, cancellationToken);
            if (resolved is null) return null;

            var (child, children) = resolved.Value;
            var metrics = await ComputeMetricsAsync(child, cancellationToken);

            return new ParentGradesViewModel
            {
                Child = child,
                Children = children,
                ActiveAction = "Grades",
                Subjects = metrics.GradeSubjects,
                OverallPercentage = metrics.GradePercentage,
                GradedSubjects = metrics.GradedSubjects,
                TotalSubjects = metrics.TotalSubjects
            };
        }

        // ==================== Oylik reyting ====================

        public async Task<ParentMonthlyRatingViewModel?> GetMonthlyRatingAsync(
            string parentId,
            string? studentId,
            int? year = null,
            int? month = null,
            CancellationToken cancellationToken = default)
        {
            var resolved = await ResolveChildAsync(parentId, studentId, cancellationToken);
            if (resolved is null) return null;

            var (child, children) = resolved.Value;
            var rating = await _monthlyRating.GetStudentRatingAsync(
                child.StudentId, year, month, cancellationToken);

            if (rating is null) return null;

            return new ParentMonthlyRatingViewModel
            {
                Child = child,
                Children = children,
                ActiveAction = "Rating",
                Rating = rating
            };
        }

        // ==================== Imtihon natijalari ====================

        public async Task<ParentExamsViewModel?> GetExamsAsync(
            string parentId, string? studentId, CancellationToken cancellationToken = default)
        {
            var resolved = await ResolveChildAsync(parentId, studentId, cancellationToken);
            if (resolved is null) return null;

            var (child, children) = resolved.Value;
            var metrics = await ComputeMetricsAsync(child, cancellationToken);

            return new ParentExamsViewModel
            {
                Child = child,
                Children = children,
                ActiveAction = "Exams",
                Results = metrics.Exams,
                PendingCount = metrics.PendingExamCount,
                AveragePercentage = metrics.AverageExamPercentage,
                BestPercentage = metrics.BestExamPercentage
            };
        }

        // ==================== Topshiriqlar ====================

        public async Task<ParentAssignmentsViewModel?> GetAssignmentsAsync(
            string parentId, string? studentId, CancellationToken cancellationToken = default)
        {
            var resolved = await ResolveChildAsync(parentId, studentId, cancellationToken);
            if (resolved is null) return null;

            var (child, children) = resolved.Value;
            var metrics = await ComputeMetricsAsync(child, cancellationToken);

            return new ParentAssignmentsViewModel
            {
                Child = child,
                Children = children,
                ActiveAction = "Assignments",
                Items = metrics.Assignments,
                SubmittedCount = metrics.SubmittedCount,
                PendingCount = metrics.PendingCount,
                OverdueCount = metrics.OverdueCount,
                CompletionPercentage = metrics.AssignmentCompletionPercentage
            };
        }

        // ==================== Ogohlantirishlar ====================

        public async Task<ParentAlertsViewModel?> GetAlertsAsync(
            string parentId, string? studentId, CancellationToken cancellationToken = default)
        {
            var resolved = await ResolveChildAsync(parentId, studentId, cancellationToken);
            if (resolved is null) return null;

            var (child, children) = resolved.Value;
            var metrics = await ComputeMetricsAsync(child, cancellationToken);

            return new ParentAlertsViewModel
            {
                Child = child,
                Children = children,
                ActiveAction = "Alerts",
                Alerts = metrics.Alerts
            };
        }

        // ==================== Dars jadvali ====================

        public async Task<ParentScheduleViewModel?> GetScheduleAsync(
            string parentId, string? studentId, CancellationToken cancellationToken = default)
        {
            var resolved = await ResolveChildAsync(parentId, studentId, cancellationToken);
            if (resolved is null) return null;

            var (child, children) = resolved.Value;
            var schedule = await BuildWeeklyScheduleAsync(child.StudentId, cancellationToken);

            return new ParentScheduleViewModel
            {
                Child = child,
                Children = children,
                ActiveAction = "Schedule",
                Schedule = schedule
            };
        }

        // ==================== Ichki yordamchilar ====================

        private async Task<(ParentChildViewModel Child, List<ParentChildViewModel> Children)?> ResolveChildAsync(
            string parentId, string? studentId, CancellationToken cancellationToken)
        {
            var children = await GetChildrenAsync(parentId, cancellationToken);
            if (children.Count == 0)
            {
                return null;
            }

            var child = string.IsNullOrWhiteSpace(studentId)
                ? children[0]
                : children.FirstOrDefault(c => string.Equals(c.StudentId, studentId, StringComparison.Ordinal));

            if (child is null)
            {
                // Boshqa ota-onaning farzandi yoki umuman bog'lanmagan o'quvchi.
                return null;
            }

            return (child, children.ToList());
        }

        private sealed class ChildMetrics
        {
            public IReadOnlyList<int> SubjectIds = Array.Empty<int>();

            public List<Attendance> Attendances = new();
            public int TotalLessons;
            public int PresentCount;
            public int AbsentCount;
            public int LateCount;
            public int ExcusedCount;
            public double AttendancePercentage;

            public List<StudentSubjectGradeViewModel> GradeSubjects = new();
            public double GradePercentage;
            public int GradedSubjects;
            public int TotalSubjects;

            public List<ParentAssignmentItemViewModel> Assignments = new();
            public int SubmittedCount;
            public int PendingCount;
            public int OverdueCount;
            public double AssignmentCompletionPercentage;

            public List<ParentExamResultViewModel> Exams = new();
            public int PendingExamCount;
            public double AverageExamPercentage;
            public double BestExamPercentage;

            public List<ParentAlertViewModel> Alerts = new();
        }

        private async Task<ChildMetrics> ComputeMetricsAsync(
            ParentChildViewModel child, CancellationToken cancellationToken)
        {
            var metrics = new ChildMetrics
            {
                SubjectIds = await _subjectGroups.GetSubjectIdsAsync(child.StudentId, null, cancellationToken)
            };

            var subjectIds = metrics.SubjectIds.ToList();

            // ---------- Davomat ----------
            var attendanceQuery = _db.Attendances
                .AsNoTracking()
                .Include(a => a.Subject)
                .Where(a => a.StudentId == child.StudentId);

            if (subjectIds.Count > 0)
            {
                attendanceQuery = attendanceQuery.Where(a => subjectIds.Contains(a.SubjectId));
            }

            metrics.Attendances = await attendanceQuery
                .OrderByDescending(a => a.Date)
                .ToListAsync(cancellationToken);

            metrics.TotalLessons = metrics.Attendances.Count;
            metrics.PresentCount = metrics.Attendances.Count(a => a.Status == AttendanceStatus.Keldi);
            metrics.AbsentCount = metrics.Attendances.Count(a => a.Status == AttendanceStatus.Kelmadi);
            metrics.LateCount = metrics.Attendances.Count(a => a.Status == AttendanceStatus.KechQoldi);
            metrics.ExcusedCount = metrics.Attendances.Count(a => a.Status == AttendanceStatus.Sababli);
            metrics.AttendancePercentage = metrics.TotalLessons == 0
                ? 0
                : Math.Round(100.0 * metrics.PresentCount / metrics.TotalLessons, 1);

            // ---------- Baholar (100 ballik yakuniy baho) ----------
            // Manba: ISubjectGradeService. Qo'lda baho ustuvor, bo'lmasa fan
            // bo'yicha tasdiqlangan imtihon natijalarining o'rtachasi.
            var grades = await _subjectGrades.GetStudentGradesAsync(
                child.StudentId, subjectIds, cancellationToken);

            metrics.GradeSubjects = grades.Values
                .OrderBy(s => s.SubjectName, StringComparer.Ordinal)
                .ToList();

            metrics.TotalSubjects = metrics.GradeSubjects.Count;
            metrics.GradedSubjects = metrics.GradeSubjects.Count(s => s.HasScore);

            if (metrics.GradedSubjects > 0)
            {
                metrics.GradePercentage = Math.Round(
                    metrics.GradeSubjects
                        .Where(s => s.HasScore)
                        .Average(s => s.Score!.Value),
                    1,
                    MidpointRounding.AwayFromZero);
            }

            // ---------- Topshiriqlar ----------
            IReadOnlyDictionary<int, int> groupMap = subjectIds.Count == 0
                ? new Dictionary<int, int>()
                : await _subjectGroups.GetGroupMapAsync(child.StudentId, subjectIds, null, cancellationToken);

            var assignments = await _db.Assignments
                .AsNoTracking()
                .Where(a => subjectIds.Contains(a.SubjectId))
                .Include(a => a.Subject)
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync(cancellationToken);

            assignments = assignments
                .Where(a => a.GroupId == null
                            || (groupMap.TryGetValue(a.SubjectId, out var groupId) && a.GroupId == groupId))
                .ToList();

            var assignmentIds = assignments.Select(a => a.Id).ToList();

            var submissions = await _db.Submissions
                .AsNoTracking()
                .Where(s => s.StudentId == child.StudentId && assignmentIds.Contains(s.AssignmentId))
                .ToListAsync(cancellationToken);

            var submissionLookup = submissions
                .GroupBy(s => s.AssignmentId)
                .ToDictionary(g => g.Key, g => g.First());

            var now = _clock.Now;

            metrics.Assignments = assignments
                .Select(a =>
                {
                    submissionLookup.TryGetValue(a.Id, out var submission);
                    var isSubmitted = submission is not null;

                    return new ParentAssignmentItemViewModel
                    {
                        Id = a.Id,
                        Title = a.Title,
                        Description = a.Description,
                        SubjectName = a.Subject?.Name ?? "Noma'lum fan",
                        CreatedDate = a.CreatedDate,
                        DueDate = a.DueDate,
                        IsSubmitted = isSubmitted,
                        SubmittedDate = submission?.SubmittedDate,
                        Grade = submission?.Grade,
                        TeacherComment = submission?.TeacherComment,
                        FileName = a.FileName,
                        IsOverdue = !isSubmitted && a.DueDate < now
                    };
                })
                .ToList();

            metrics.SubmittedCount = metrics.Assignments.Count(a => a.IsSubmitted);
            metrics.PendingCount = metrics.Assignments.Count(a => !a.IsSubmitted);
            metrics.OverdueCount = metrics.Assignments.Count(a => a.IsOverdue);
            metrics.AssignmentCompletionPercentage = metrics.Assignments.Count == 0
                ? 0
                : Math.Round(100.0 * metrics.SubmittedCount / metrics.Assignments.Count, 1);

            // ---------- Imtihon natijalari ----------
            var examResults = await _db.ExamResults
                .AsNoTracking()
                .Include(r => r.Exam)
                    .ThenInclude(e => e!.Subject)
                .Where(r => r.StudentId == child.StudentId)
                .OrderByDescending(r => r.CompletedDate)
                .ToListAsync(cancellationToken);

            metrics.Exams = examResults
                .Select(r => new ParentExamResultViewModel
                {
                    Id = r.Id,
                    ExamTitle = r.Exam?.Title ?? "Imtihon",
                    SubjectName = r.Exam?.Subject?.Name,
                    CorrectAnswers = r.CorrectAnswers,
                    TotalQuestions = r.TotalQuestions,
                    CompletedDate = r.CompletedDate,
                    IsApproved = r.IsApproved
                })
                .ToList();

            metrics.PendingExamCount = metrics.Exams.Count(e => !e.IsApproved);

            if (metrics.Exams.Count > 0)
            {
                metrics.AverageExamPercentage = Math.Round(metrics.Exams.Average(e => e.Percentage), 1);
                metrics.BestExamPercentage = Math.Round(metrics.Exams.Max(e => e.Percentage), 1);
            }

            // ---------- Ogohlantirishlar ----------
            metrics.Alerts = BuildAlerts(metrics, now);

            return metrics;
        }

        private static List<ParentAlertViewModel> BuildAlerts(ChildMetrics metrics, DateTime now)
        {
            var alerts = new List<ParentAlertViewModel>();

            if (metrics.SubjectIds.Count == 0
                && metrics.Attendances.Count == 0
                && metrics.Assignments.Count == 0
                && metrics.Exams.Count == 0)
            {
                alerts.Add(new ParentAlertViewModel
                {
                    Severity = "info",
                    Icon = "info-circle",
                    Title = "Ma'lumot hali kiritilmagan",
                    Message = "Farzandingiz uchun davomat, baho, topshiriq yoki imtihon ma'lumotlari hali mavjud emas."
                });
                return alerts;
            }

            // Davomat
            if (metrics.TotalLessons > 0)
            {
                if (metrics.AttendancePercentage < 75)
                {
                    alerts.Add(new ParentAlertViewModel
                    {
                        Severity = "danger",
                        Icon = "calendar-x",
                        Title = "Davomat past",
                        Message = $"Umumiy davomat {metrics.AttendancePercentage:0.#}%. " +
                                  "75% dan yuqori bo'lishi tavsiya etiladi.",
                        ActionName = "Attendance",
                        ActionText = "Davomatni ko'rish"
                    });
                }

                var since = now.AddDays(-30);
                var absentInMonth = metrics.Attendances
                    .Count(a => a.Date >= since && a.Status == AttendanceStatus.Kelmadi);

                if (absentInMonth >= 3)
                {
                    alerts.Add(new ParentAlertViewModel
                    {
                        Severity = "warning",
                        Icon = "exclamation-triangle",
                        Title = "So'nggi 30 kunda sababsiz kelmagan",
                        Message = $"Farzandingiz so'nggi 30 kunda {absentInMonth} marta darsga kelmagan.",
                        ActionName = "Attendance",
                        ActionText = "Batafsil"
                    });
                }

                var lateInMonth = metrics.Attendances
                    .Count(a => a.Date >= since && a.Status == AttendanceStatus.KechQoldi);

                if (lateInMonth >= 3)
                {
                    alerts.Add(new ParentAlertViewModel
                    {
                        Severity = "warning",
                        Icon = "clock-history",
                        Title = "Kechikishlar ko'p",
                        Message = $"So'nggi 30 kunda {lateInMonth} marta darsga kech qolingan.",
                        ActionName = "Attendance",
                        ActionText = "Batafsil"
                    });
                }
            }

            // Baholar
            if (metrics.GradedSubjects > 0)
            {
                var weakSubjects = metrics.GradeSubjects
                    .Where(s => s.HasScore && s.Score!.Value < 55)
                    .Select(s => s.SubjectName)
                    .ToList();

                if (weakSubjects.Count > 0)
                {
                    alerts.Add(new ParentAlertViewModel
                    {
                        Severity = "danger",
                        Icon = "journal-x",
                        Title = "Bahosi past fanlar",
                        Message = $"Qoniqarsiz darajada: {string.Join(", ", weakSubjects)}. " +
                                  "O'qituvchi bilan suhbatlashish tavsiya etiladi.",
                        ActionName = "Grades",
                        ActionText = "Baholarni ko'rish"
                    });
                }

                var shakySubjects = metrics.GradeSubjects
                    .Where(s => s.HasScore && s.Score!.Value >= 55 && s.Score!.Value < 70)
                    .Select(s => s.SubjectName)
                    .ToList();

                if (shakySubjects.Count > 0)
                {
                    alerts.Add(new ParentAlertViewModel
                    {
                        Severity = "warning",
                        Icon = "journal-check",
                        Title = "O'rtacha darajadagi fanlar",
                        Message = $"Diqqat talab qiladi: {string.Join(", ", shakySubjects)}.",
                        ActionName = "Grades",
                        ActionText = "Baholarni ko'rish"
                    });
                }
            }

            // Topshiriqlar
            if (metrics.OverdueCount > 0)
            {
                alerts.Add(new ParentAlertViewModel
                {
                    Severity = "danger",
                    Icon = "clipboard-x",
                    Title = "Muddati o'tgan topshiriqlar",
                    Message = $"{metrics.OverdueCount} ta topshiriq topshirilmagan va muddati o'tgan.",
                    ActionName = "Assignments",
                    ActionText = "Topshiriqlarni ko'rish"
                });
            }
            else if (metrics.PendingCount > 0)
            {
                alerts.Add(new ParentAlertViewModel
                {
                    Severity = "info",
                    Icon = "clipboard-check",
                    Title = "Topshirilmagan topshiriqlar",
                    Message = $"{metrics.PendingCount} ta topshiriq hali topshirilmagan.",
                    ActionName = "Assignments",
                    ActionText = "Topshiriqlarni ko'rish"
                });
            }

            // Imtihonlar
            if (metrics.PendingExamCount > 0)
            {
                alerts.Add(new ParentAlertViewModel
                {
                    Severity = "info",
                    Icon = "hourglass-split",
                    Title = "Natijalar tasdiqlanmoqda",
                    Message = $"{metrics.PendingExamCount} ta imtihon natijasi o'qituvchi tasdiqlashini kutmoqda.",
                    ActionName = "Exams",
                    ActionText = "Natijalarni ko'rish"
                });
            }

            var failedExam = metrics.Exams
                .Where(e => e.Percentage < 50)
                .OrderByDescending(e => e.CompletedDate)
                .FirstOrDefault();

            if (failedExam is not null)
            {
                alerts.Add(new ParentAlertViewModel
                {
                    Severity = "warning",
                    Icon = "clipboard-data",
                    Title = "Imtihon natijasi past",
                    Message = $"\"{failedExam.ExamTitle}\" natijasi {failedExam.Percentage:0.#}% " +
                              $"({failedExam.CorrectAnswers}/{failedExam.TotalQuestions}).",
                    ActionName = "Exams",
                    ActionText = "Natijalarni ko'rish"
                });
            }

            if (alerts.Count == 0)
            {
                alerts.Add(new ParentAlertViewModel
                {
                    Severity = "success",
                    Icon = "check-circle",
                    Title = "Hammasi joyida",
                    Message = "Hozircha e'tibor talab qiladigan holat aniqlanmadi."
                });
            }

            return alerts;
        }

        private static double CalculateOverallPercentage(ChildMetrics metrics)
        {
            // Umumiy o'zlashtirish faqat 100 ballik yakuniy baholardan
            // hisoblanadi: fanlar bo'yicha o'rtacha. Davomat va topshiriqlar
            // bu ko'rsatkichga kirmaydi.
            return metrics.GradedSubjects == 0
                ? 0
                : Math.Round(metrics.GradePercentage, 1, MidpointRounding.AwayFromZero);
        }

        private static string GetStatusZone(double overall, ChildMetrics metrics)
        {
            if (metrics.GradedSubjects == 0)
            {
                return "Muted";
            }

            if (overall >= 75) return "Green";
            if (overall >= 50) return "Yellow";
            return "Red";
        }

        private static (string Label, string BadgeClass) DescribeStatus(AttendanceStatus status) => status switch
        {
            AttendanceStatus.Keldi => ("Keldi", "bg-success-subtle text-success-emphasis"),
            AttendanceStatus.Kelmadi => ("Kelmadi", "bg-danger-subtle text-danger-emphasis"),
            AttendanceStatus.KechQoldi => ("Kech qoldi", "bg-warning-subtle text-warning-emphasis"),
            AttendanceStatus.Sababli => ("Sababli", "bg-info-subtle text-info-emphasis"),
            _ => ("Noma'lum", "bg-secondary-subtle text-secondary-emphasis")
        };

        private static AttendanceStatus PickWorstStatus(IEnumerable<AttendanceStatus> statuses)
        {
            var priority = new[]
            {
                AttendanceStatus.Kelmadi,
                AttendanceStatus.KechQoldi,
                AttendanceStatus.Sababli,
                AttendanceStatus.Keldi
            };

            foreach (var status in priority)
            {
                if (statuses.Contains(status))
                {
                    return status;
                }
            }

            return AttendanceStatus.Keldi;
        }

        private async Task<WeeklyScheduleViewModel> BuildWeeklyScheduleAsync(
            string studentId, CancellationToken cancellationToken)
        {
            var defaultGroupId = await _subjectGroups.GetDefaultGroupIdAsync(studentId, cancellationToken);
            var assignments = await _subjectGroups.GetAssignmentsAsync(studentId, cancellationToken);
            var activeOverrides = assignments.Where(a => a.IsActive).ToList();

            if (defaultGroupId == null && activeOverrides.Count == 0)
            {
                return new WeeklyScheduleViewModel
                {
                    Title = "Dars jadvali topilmadi",
                    Blocks = new List<ScheduleBlockViewModel>()
                };
            }

            var groupIds = activeOverrides
                .Select(a => a.GroupId)
                .Append(defaultGroupId ?? 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var schedules = await _db.Schedules
                .AsNoTracking()
                .Include(s => s.Subject)
                    .ThenInclude(s => s!.Teacher)
                .Include(s => s.Group)
                .Include(s => s.Teacher)
                .Where(s => groupIds.Contains(s.GroupId))
                .ToListAsync(cancellationToken);

            var overrides = await _db.GroupSubjects
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var teacherIds = schedules
                .Select(s => ScheduleService.ResolveTeacherId(s, overrides))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .Distinct()
                .ToList();

            var teacherNames = await _db.Users
                .AsNoTracking()
                .Where(u => teacherIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

            var targetGroupBySubject = activeOverrides
                .GroupBy(a => a.SubjectId)
                .ToDictionary(g => g.Key, g => g.First().GroupId);

            var mine = schedules
                .Where(s =>
                {
                    var target = targetGroupBySubject.TryGetValue(s.SubjectId, out var overrideGroupId)
                        ? overrideGroupId
                        : defaultGroupId;

                    return target != null && s.GroupId == target;
                })
                .ToList();

            string? defaultGroupName = null;
            if (defaultGroupId is int id)
            {
                defaultGroupName = await _db.Groups
                    .AsNoTracking()
                    .Where(g => g.Id == id)
                    .Select(g => g.Name)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            var title = activeOverrides.Count == 0 && !string.IsNullOrWhiteSpace(defaultGroupName)
                ? $"{defaultGroupName} guruh jadvali"
                : "Mening dars jadvalim";

            return new WeeklyScheduleViewModel
            {
                Title = title,
                Blocks = mine
                    .Select(s => MapToBlock(s, overrides, teacherNames))
                    .ToList()
            };
        }

        private static ScheduleBlockViewModel MapToBlock(
            Schedule schedule,
            IReadOnlyCollection<GroupSubject> overrides,
            IReadOnlyDictionary<string, string?> teacherNames)
        {
            var teacherId = ScheduleService.ResolveTeacherId(schedule, overrides);

            string? teacherName = null;
            if (!string.IsNullOrWhiteSpace(teacherId))
            {
                teacherNames.TryGetValue(teacherId, out teacherName);
            }

            return new ScheduleBlockViewModel
            {
                Id = schedule.Id,
                SubjectName = schedule.Subject?.Name ?? string.Empty,
                GroupName = schedule.Group?.Name ?? string.Empty,
                TeacherName = teacherName,
                DayOfWeek = schedule.DayOfWeek,
                StartTime = schedule.StartTime,
                EndTime = schedule.EndTime,
                Shift = schedule.Shift,
                Kind = schedule.Kind,
                Period = schedule.Period,
                HueDegree = ScheduleService.GetHueForSubject(
                    schedule.Subject?.Name ?? schedule.SubjectId.ToString())
            };
        }
    }
}
