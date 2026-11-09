using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    /// <inheritdoc />
    public sealed class MonthlyRatingService : IMonthlyRatingService
    {
        private const int TrendMonths = 6;

        private readonly ApplicationDbContext _db;
        private readonly IAppClock _clock;

        public MonthlyRatingService(ApplicationDbContext db, IAppClock clock)
        {
            _db = db;
            _clock = clock;
        }

        // ==================== Davrlar ====================

        public async Task<IReadOnlyList<MonthlyPeriodOptionViewModel>> GetPeriodsAsync(
            int? selectedYear = null,
            int? selectedMonth = null,
            CancellationToken cancellationToken = default)
        {
            var (year, month) = ResolvePeriod(selectedYear, selectedMonth);

            var rows = await _db.Exams
                .AsNoTracking()
                .Where(e => e.Kind == ExamKind.Monthly
                            && e.PeriodYear != null
                            && e.PeriodMonth != null)
                .Select(e => new { Year = e.PeriodYear!.Value, Month = e.PeriodMonth!.Value })
                .Distinct()
                .ToListAsync(cancellationToken);

            var periods = rows
                .Select(r => (r.Year, r.Month))
                .ToList();

            periods.Add((_clock.Today.Year, _clock.Today.Month));
            periods.Add((year, month));

            var result = periods
                .Distinct()
                .OrderByDescending(p => p.Item1)
                .ThenByDescending(p => p.Item2)
                .Take(36)
                .Select(p => new MonthlyPeriodOptionViewModel
                {
                    Year = p.Item1,
                    Month = p.Item2,
                    Label = MonthlyPeriod.Label(p.Item1, p.Item2),
                    IsSelected = p.Item1 == year && p.Item2 == month,
                    IsCurrent = p.Item1 == _clock.Today.Year && p.Item2 == _clock.Today.Month
                })
                .ToList();

            // Tanlangan davr ro'yxatdan tushib qolmasin (masalan, 36 oydan eski).
            if (result.All(p => p.Year != year || p.Month != month))
            {
                result.Insert(0, new MonthlyPeriodOptionViewModel
                {
                    Year = year,
                    Month = month,
                    Label = MonthlyPeriod.Label(year, month),
                    IsSelected = true,
                    IsCurrent = year == _clock.Today.Year && month == _clock.Today.Month
                });
            }

            return result;
        }

        // ==================== Talaba reytingi ====================

        public async Task<StudentMonthlyRatingViewModel?> GetStudentRatingAsync(
            string studentId,
            int? year = null,
            int? month = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(studentId)) return null;

            var student = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == studentId)
                .Select(u => new { u.Id, Name = u.FullName ?? u.UserName })
                .FirstOrDefaultAsync(cancellationToken);

            if (student is null) return null;

            var (periodYear, periodMonth) = ResolvePeriod(year, month);
            var rosterDate = ResolveRosterDate(periodYear, periodMonth);
            var groups = await ResolveGroupsAtAsync(studentId, rosterDate, cancellationToken);

            var groupIds = new List<int>();
            if (groups.ClassGroup is not null) groupIds.Add(groups.ClassGroup.Id);
            if (groups.StreamGroup is not null) groupIds.Add(groups.StreamGroup.Id);

            var datasets = await LoadGroupDatasetsAsync(
                groupIds, periodYear, periodMonth, rosterDate,
                includeAllStudents: true,
                focusStudentId: studentId,
                cancellationToken);

            var model = new StudentMonthlyRatingViewModel
            {
                StudentId = student.Id,
                StudentName = string.IsNullOrWhiteSpace(student.Name) ? student.Id : student.Name!,
                ClassName = groups.ClassGroup?.Name ?? string.Empty,
                StreamName = groups.StreamGroup?.Name ?? string.Empty,
                Year = periodYear,
                Month = periodMonth,
                PeriodLabel = MonthlyPeriod.Label(periodYear, periodMonth),
                Periods = (await GetPeriodsAsync(periodYear, periodMonth, cancellationToken)).ToList()
            };

            if (groups.ClassGroup is not null && datasets.TryGetValue(groups.ClassGroup.Id, out var classData))
            {
                model.ClassRating = BuildScope(classData, GroupType.Class, studentId);
            }

            if (groups.StreamGroup is not null && datasets.TryGetValue(groups.StreamGroup.Id, out var streamData))
            {
                model.StreamRating = BuildScope(streamData, GroupType.Stream, studentId);
            }

            model.Trend = await BuildTrendAsync(studentId, periodYear, periodMonth, cancellationToken);
            return model;
        }

        // ==================== Admin / o'qituvchi ====================

        public async Task<MonthlyRatingOverviewViewModel> GetOverviewAsync(
            int? groupId = null,
            int? year = null,
            int? month = null,
            string? teacherId = null,
            bool isTeacherView = false,
            CancellationToken cancellationToken = default)
        {
            var (periodYear, periodMonth) = ResolvePeriod(year, month);
            var rosterDate = ResolveRosterDate(periodYear, periodMonth);

            var groupsQuery = _db.Groups
                .AsNoTracking()
                .Where(g => !g.IsArchived);

            if (!string.IsNullOrWhiteSpace(teacherId))
            {
                var teacherGroupIds = await _db.GroupSubjects
                    .AsNoTracking()
                    .Where(gs => gs.TeacherId == teacherId
                                 || (gs.TeacherId == null && gs.Subject != null && gs.Subject.TeacherId == teacherId))
                    .Select(gs => gs.GroupId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                groupsQuery = groupsQuery.Where(g => teacherGroupIds.Contains(g.Id));
            }

            var allGroups = await groupsQuery
                .OrderBy(g => g.Type)
                .ThenBy(g => g.Name)
                .ToListAsync(cancellationToken);

            var allGroupIds = allGroups.Select(g => g.Id).ToList();

            var subjectCounts = allGroupIds.Count == 0
                ? new Dictionary<int, int>()
                : await _db.GroupSubjects
                    .AsNoTracking()
                    .Where(gs => allGroupIds.Contains(gs.GroupId))
                    .GroupBy(gs => gs.GroupId)
                    .Select(g => new { GroupId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.GroupId, x => x.Count, cancellationToken);

            var groupOptions = allGroups
                .Select(g => new MonthlyRatingGroupOptionViewModel
                {
                    GroupId = g.Id,
                    Name = g.Name,
                    Type = g.Type,
                    SubjectCount = subjectCounts.GetValueOrDefault(g.Id)
                })
                .ToList();

            var filteredGroups = groupId.HasValue
                ? allGroups.Where(g => g.Id == groupId.Value).ToList()
                : allGroups;

            var filteredGroupIds = filteredGroups.Select(g => g.Id).ToList();

            var datasets = await LoadGroupDatasetsAsync(
                filteredGroupIds, periodYear, periodMonth, rosterDate,
                includeAllStudents: true,
                focusStudentId: null,
                cancellationToken);

            var ratings = filteredGroups
                .Where(g => datasets.ContainsKey(g.Id))
                .Select(g => BuildGroup(datasets[g.Id]))
                .ToList();

            // Guruh tanlanmagan bo'lsa, faqat shu oyda haqiqiy reytingi bor
            // guruhlarni ko'rsatamiz — bo'sh kartalar ro'yxatni chalkashtirmasin.
            if (!groupId.HasValue)
            {
                ratings = ratings.Where(r => r.RatedSubjectCount > 0).ToList();
            }

            var distinctStudentCount = ratings
                .SelectMany(r => r.Students)
                .Select(s => s.StudentId)
                .Distinct(StringComparer.Ordinal)
                .Count();

            var averages = ratings
                .Where(r => r.AverageScore.HasValue)
                .Select(r => r.AverageScore!.Value)
                .ToList();

            var examCount = datasets.Values
                .SelectMany(d => d.Exams)
                .Select(e => e.ExamId)
                .Distinct()
                .Count();

            return new MonthlyRatingOverviewViewModel
            {
                Title = isTeacherView ? "Oylik reyting — mening guruhlarim" : "Oylik reyting — maktab",
                Subtitle = isTeacherView
                    ? "Faqat siz dars beradigan guruhlar. Reyting tasdiqlangan oylik imtihon natijalari asosida."
                    : "Sinf va yo'nalish guruhlari bo'yicha 100 ballik oylik reyting.",
                IsTeacherView = isTeacherView,
                Year = periodYear,
                Month = periodMonth,
                PeriodLabel = MonthlyPeriod.Label(periodYear, periodMonth),
                Periods = (await GetPeriodsAsync(periodYear, periodMonth, cancellationToken)).ToList(),
                Groups = groupOptions,
                SelectedGroupId = groupId,
                GroupRatings = ratings,
                GroupCount = ratings.Count,
                StudentCount = distinctStudentCount,
                ExamCount = examCount,
                OverallAverage = averages.Count == 0
                    ? null
                    : Math.Round(averages.Average(), 1, MidpointRounding.AwayFromZero)
            };
        }

        // ==================== Guruh ma'lumotlari ====================

        private async Task<Dictionary<int, GroupDataset>> LoadGroupDatasetsAsync(
            IReadOnlyCollection<int> groupIds,
            int year,
            int month,
            DateTime rosterDate,
            bool includeAllStudents,
            string? focusStudentId,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<int, GroupDataset>();
            if (groupIds.Count == 0) return result;

            var ids = groupIds.Distinct().ToList();

            var groups = await _db.Groups
                .AsNoTracking()
                .Where(g => ids.Contains(g.Id))
                .ToListAsync(cancellationToken);

            var subjectRows = await _db.GroupSubjects
                .AsNoTracking()
                .Where(gs => ids.Contains(gs.GroupId))
                .Select(gs => new
                {
                    gs.GroupId,
                    gs.SubjectId,
                    SubjectName = gs.Subject != null ? gs.Subject.Name : "Noma'lum fan"
                })
                .ToListAsync(cancellationToken);

            var rosters = ids.ToDictionary(id => id, _ => new List<string>());

            if (includeAllStudents)
            {
                var membershipRows = await _db.GroupMemberships
                    .AsNoTracking()
                    .Where(m => ids.Contains(m.GroupId)
                                && m.StartDate <= rosterDate
                                && (m.EndDate == null || m.EndDate > rosterDate))
                    .Select(m => new { m.GroupId, m.StudentId })
                    .ToListAsync(cancellationToken);

                foreach (var row in membershipRows)
                {
                    if (rosters.TryGetValue(row.GroupId, out var roster))
                    {
                        roster.Add(row.StudentId);
                    }
                }

                // O'tish davri: a'zolik yozuvi umuman bo'lmagan stream
                // o'quvchilari eski ApplicationUser.GroupId orqali topiladi
                // (StudentContextService bilan bir xil qoida).
                var streamGroupIds = groups
                    .Where(g => g.Type == GroupType.Stream)
                    .Select(g => g.Id)
                    .ToList();

                if (streamGroupIds.Count > 0)
                {
                    var legacyRows = await _db.Users
                        .AsNoTracking()
                        .Where(u => u.GroupId != null && streamGroupIds.Contains(u.GroupId.Value))
                        .Where(u => !_db.GroupMemberships.Any(
                            m => m.StudentId == u.Id && m.Scope == GroupType.Stream))
                        .Select(u => new { u.Id, GroupId = u.GroupId!.Value })
                        .ToListAsync(cancellationToken);

                    foreach (var row in legacyRows)
                    {
                        if (rosters.TryGetValue(row.GroupId, out var roster))
                        {
                            roster.Add(row.Id);
                        }
                    }
                }

                foreach (var id in ids)
                {
                    rosters[id] = rosters[id]
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                }
            }
            else if (!string.IsNullOrWhiteSpace(focusStudentId))
            {
                // Trend uchun faqat kerakli o'quvchi yetarli.
                foreach (var id in ids)
                {
                    rosters[id] = new List<string> { focusStudentId };
                }
            }

            var allStudentIds = rosters.Values
                .SelectMany(x => x)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var names = allStudentIds.Count == 0
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : await _db.Users
                    .AsNoTracking()
                    .Where(u => allStudentIds.Contains(u.Id))
                    .Select(u => new { u.Id, Name = u.FullName ?? u.UserName })
                    .ToDictionaryAsync(
                        x => x.Id,
                        x => string.IsNullOrWhiteSpace(x.Name) ? x.Id : x.Name!,
                        cancellationToken);

            var examRows = await _db.Exams
                .AsNoTracking()
                .Where(e => e.Kind == ExamKind.Monthly
                            && e.PeriodYear == year
                            && e.PeriodMonth == month)
                .Select(e => new { e.Id, e.SubjectId, e.GroupId, e.Title })
                .ToListAsync(cancellationToken);

            var exams = examRows
                .Select(e => new ExamInfo(e.Id, e.SubjectId, e.GroupId, e.Title))
                .ToList();

            var examIds = exams.Select(e => e.ExamId).ToList();

            List<ResultInfo> results;
            if (examIds.Count == 0 || allStudentIds.Count == 0)
            {
                results = new List<ResultInfo>();
            }
            else
            {
                var resultRows = await _db.ExamResults
                    .AsNoTracking()
                    .Where(r => r.IsApproved
                                && examIds.Contains(r.ExamId)
                                && allStudentIds.Contains(r.StudentId))
                    .Select(r => new
                    {
                        r.StudentId,
                        r.ExamId,
                        r.TotalQuestions,
                        r.CorrectAnswers,
                        r.CompletedDate
                    })
                    .ToListAsync(cancellationToken);

                results = resultRows
                    .Select(r => new ResultInfo(
                        r.StudentId, r.ExamId, r.TotalQuestions, r.CorrectAnswers, r.CompletedDate))
                    .ToList();
            }

            foreach (var group in groups)
            {
                var subjectList = subjectRows
                    .Where(s => s.GroupId == group.Id)
                    .Select(s => new SubjectInfo(s.SubjectId, s.SubjectName))
                    .OrderBy(s => s.SubjectName, StringComparer.Ordinal)
                    .ToList();

                var groupSubjectIds = subjectList
                    .Select(s => s.SubjectId)
                    .ToHashSet();

                result[group.Id] = new GroupDataset
                {
                    Group = group,
                    Subjects = subjectList,
                    StudentIds = rosters[group.Id],
                    StudentNames = names,
                    Exams = exams
                        .Where(e => groupSubjectIds.Contains(e.SubjectId)
                                    && (e.GroupId == null || e.GroupId == group.Id))
                        .ToList(),
                    Results = results
                };
            }

            return result;
        }

        // ==================== Hisoblash ====================

        private static MonthlyRatingScopeViewModel BuildScope(
            GroupDataset dataset,
            GroupType scope,
            string focusStudentId)
        {
            var computation = ComputeRating(dataset);
            var focusRow = computation.Rows
                .FirstOrDefault(r => string.Equals(r.StudentId, focusStudentId, StringComparison.Ordinal));

            var model = new MonthlyRatingScopeViewModel
            {
                Scope = scope,
                GroupId = dataset.Group.Id,
                GroupName = dataset.Group.Name,
                SubjectCount = dataset.Subjects.Count,
                RatedSubjectCount = computation.MonthlySubjectIds.Count,
                ParticipantCount = computation.Rows.Count(r => r.AverageScore.HasValue),
                AverageScore = focusRow?.AverageScore,
                Rank = focusRow?.Rank,
                ScoredSubjectCount = focusRow?.ScoredSubjectCount ?? 0
            };

            if (dataset.Subjects.Count == 0)
            {
                model.EmptyReason = "Bu guruhga hali fan biriktirilmagan.";
            }
            else if (dataset.StudentIds.Count == 0)
            {
                model.EmptyReason = "Bu guruhda o'quvchilar yo'q.";
            }
            else if (computation.MonthlySubjectIds.Count == 0)
            {
                model.EmptyReason = "Bu oy uchun oylik imtihonlar belgilanmagan.";
            }
            else if (focusRow is null)
            {
                model.EmptyReason = "O'quvchi bu davrda guruh a'zosi emas.";
            }

            foreach (var subject in dataset.Subjects)
            {
                var isRated = computation.MonthlySubjectIds.Contains(subject.SubjectId);

                MonthlySubjectScore? detail = null;
                if (isRated && focusRow is not null
                    && focusRow.SubjectScores.TryGetValue(subject.SubjectId, out var found))
                {
                    detail = found;
                }

                int? subjectRank = null;
                if (detail is not null
                    && computation.SubjectRanks.TryGetValue(subject.SubjectId, out var ranks)
                    && ranks.TryGetValue(focusStudentId, out var rank))
                {
                    subjectRank = rank;
                }

                model.Subjects.Add(new MonthlyRatingSubjectViewModel
                {
                    SubjectId = subject.SubjectId,
                    SubjectName = subject.SubjectName,
                    HasMonthlyExam = isRated,
                    Score = detail?.Score,
                    Rank = subjectRank,
                    ExamTitle = detail?.ExamTitle,
                    CompletedDate = detail?.CompletedDate
                });
            }

            model.Leaderboard = computation.Rows
                .OrderBy(r => r.Rank ?? int.MaxValue)
                .ThenBy(r => r.StudentName, StringComparer.Ordinal)
                .Select(r => new MonthlyRatingLeaderboardRowViewModel
                {
                    Rank = r.Rank,
                    StudentId = r.StudentId,
                    StudentName = r.StudentName,
                    AverageScore = r.AverageScore,
                    ScoredSubjectCount = r.ScoredSubjectCount,
                    SubjectCount = computation.MonthlySubjectIds.Count,
                    IsCurrentStudent = string.Equals(r.StudentId, focusStudentId, StringComparison.Ordinal),
                    SubjectScores = BuildMiniScores(dataset, computation, r)
                })
                .ToList();

            return model;
        }

        private static MonthlyRatingGroupViewModel BuildGroup(GroupDataset dataset)
        {
            var computation = ComputeRating(dataset);
            var ratedRows = computation.Rows
                .Where(r => r.AverageScore.HasValue)
                .ToList();

            return new MonthlyRatingGroupViewModel
            {
                GroupId = dataset.Group.Id,
                GroupName = dataset.Group.Name,
                Type = dataset.Group.Type,
                SubjectCount = dataset.Subjects.Count,
                RatedSubjectCount = computation.MonthlySubjectIds.Count,
                StudentCount = dataset.StudentIds.Count,
                RatedStudentCount = ratedRows.Count,
                AverageScore = ratedRows.Count == 0
                    ? null
                    : Math.Round(
                        ratedRows.Average(r => r.AverageScore!.Value),
                        1,
                        MidpointRounding.AwayFromZero),
                Students = computation.Rows
                    .OrderBy(r => r.Rank ?? int.MaxValue)
                    .ThenBy(r => r.StudentName, StringComparer.Ordinal)
                    .Select(r => new MonthlyRatingLeaderboardRowViewModel
                    {
                        Rank = r.Rank,
                        StudentId = r.StudentId,
                        StudentName = r.StudentName,
                        AverageScore = r.AverageScore,
                        ScoredSubjectCount = r.ScoredSubjectCount,
                        SubjectCount = computation.MonthlySubjectIds.Count,
                        SubjectScores = BuildMiniScores(dataset, computation, r)
                    })
                    .ToList()
            };
        }

        private static List<MonthlyRatingMiniScoreViewModel> BuildMiniScores(
            GroupDataset dataset,
            RatingComputation computation,
            StudentComputation row)
        {
            return dataset.Subjects
                .Where(s => computation.MonthlySubjectIds.Contains(s.SubjectId))
                .Select(s => new MonthlyRatingMiniScoreViewModel
                {
                    SubjectId = s.SubjectId,
                    SubjectName = s.SubjectName,
                    Score = row.SubjectScores.TryGetValue(s.SubjectId, out var score)
                        ? score.Score
                        : 0
                })
                .ToList();
        }

        private static RatingComputation ComputeRating(GroupDataset dataset)
        {
            var examLookup = dataset.Exams
                .GroupBy(e => e.ExamId)
                .ToDictionary(g => g.Key, g => g.First());

            var monthlySubjectIds = dataset.Exams
                .Select(e => e.SubjectId)
                .ToHashSet();

            // Bir fan bo'yicha bir nechta oylik imtihon bo'lsa — eng yuqori
            // tasdiqlangan natija olinadi (retake qoidasi).
            var bestScores = new Dictionary<(string StudentId, int SubjectId), MonthlySubjectScore>();
            foreach (var result in dataset.Results)
            {
                if (!examLookup.TryGetValue(result.ExamId, out var exam)) continue;
                if (!monthlySubjectIds.Contains(exam.SubjectId)) continue;

                var percentage = result.TotalQuestions > 0
                    ? 100.0 * result.CorrectAnswers / result.TotalQuestions
                    : 0;

                percentage = Math.Round(
                    Math.Clamp(percentage, 0, 100),
                    1,
                    MidpointRounding.AwayFromZero);

                var key = (result.StudentId, exam.SubjectId);
                if (!bestScores.TryGetValue(key, out var existing) || percentage > existing.Score)
                {
                    bestScores[key] = new MonthlySubjectScore
                    {
                        Score = percentage,
                        ExamTitle = exam.Title,
                        CompletedDate = result.CompletedDate
                    };
                }
            }

            var rows = new List<StudentComputation>();
            foreach (var studentId in dataset.StudentIds)
            {
                var row = new StudentComputation
                {
                    StudentId = studentId,
                    StudentName = dataset.StudentNames.GetValueOrDefault(studentId, studentId)
                };

                double sum = 0;
                foreach (var subjectId in monthlySubjectIds)
                {
                    if (bestScores.TryGetValue((studentId, subjectId), out var score))
                    {
                        row.SubjectScores[subjectId] = score;
                        sum += score.Score;
                        row.ScoredSubjectCount++;
                    }
                }

                row.AverageScore = monthlySubjectIds.Count == 0
                    ? null
                    : Math.Round(sum / monthlySubjectIds.Count, 1, MidpointRounding.AwayFromZero);

                rows.Add(row);
            }

            ApplyAverageRanks(rows);
            var subjectRanks = ApplySubjectRanks(rows, monthlySubjectIds);

            return new RatingComputation
            {
                Rows = rows,
                MonthlySubjectIds = monthlySubjectIds,
                SubjectRanks = subjectRanks
            };
        }

        private static void ApplyAverageRanks(List<StudentComputation> rows)
        {
            var ranked = rows
                .Where(r => r.AverageScore.HasValue)
                .OrderByDescending(r => r.AverageScore!.Value)
                .ThenBy(r => r.StudentName, StringComparer.Ordinal)
                .ToList();

            var currentRank = 0;
            double? previous = null;

            for (var i = 0; i < ranked.Count; i++)
            {
                var value = ranked[i].AverageScore!.Value;

                if (previous is null || Math.Abs(value - previous.Value) > 0.0001)
                {
                    currentRank = i + 1;
                    previous = value;
                }

                ranked[i].Rank = currentRank;
            }
        }

        private static Dictionary<int, Dictionary<string, int>> ApplySubjectRanks(
            List<StudentComputation> rows,
            HashSet<int> subjectIds)
        {
            var result = new Dictionary<int, Dictionary<string, int>>();

            foreach (var subjectId in subjectIds)
            {
                var ranked = rows
                    .Where(r => r.SubjectScores.ContainsKey(subjectId))
                    .OrderByDescending(r => r.SubjectScores[subjectId].Score)
                    .ThenBy(r => r.StudentName, StringComparer.Ordinal)
                    .ToList();

                var map = new Dictionary<string, int>(StringComparer.Ordinal);
                var currentRank = 0;
                double? previous = null;

                for (var i = 0; i < ranked.Count; i++)
                {
                    var value = ranked[i].SubjectScores[subjectId].Score;

                    if (previous is null || Math.Abs(value - previous.Value) > 0.0001)
                    {
                        currentRank = i + 1;
                        previous = value;
                    }

                    map[ranked[i].StudentId] = currentRank;
                }

                result[subjectId] = map;
            }

            return result;
        }

        // ==================== Trend ====================

        private async Task<List<MonthlyRatingTrendPointViewModel>> BuildTrendAsync(
            string studentId,
            int endYear,
            int endMonth,
            CancellationToken cancellationToken)
        {
            var points = new List<MonthlyRatingTrendPointViewModel>();

            for (var offset = TrendMonths - 1; offset >= 0; offset--)
            {
                var (year, month) = MonthlyPeriod.AddMonths(endYear, endMonth, -offset);
                var rosterDate = ResolveRosterDate(year, month);
                var groups = await ResolveGroupsAtAsync(studentId, rosterDate, cancellationToken);

                var ids = new List<int>();
                if (groups.ClassGroup is not null) ids.Add(groups.ClassGroup.Id);
                if (groups.StreamGroup is not null) ids.Add(groups.StreamGroup.Id);

                var datasets = await LoadGroupDatasetsAsync(
                    ids, year, month, rosterDate,
                    includeAllStudents: false,
                    focusStudentId: studentId,
                    cancellationToken);

                double? classAverage = null;
                if (groups.ClassGroup is not null
                    && datasets.TryGetValue(groups.ClassGroup.Id, out var classData))
                {
                    classAverage = ComputeFocusAverage(classData, studentId);
                }

                double? streamAverage = null;
                if (groups.StreamGroup is not null
                    && datasets.TryGetValue(groups.StreamGroup.Id, out var streamData))
                {
                    streamAverage = ComputeFocusAverage(streamData, studentId);
                }

                points.Add(new MonthlyRatingTrendPointViewModel
                {
                    Year = year,
                    Month = month,
                    Label = MonthlyPeriod.Label(year, month),
                    ShortLabel = MonthlyPeriod.ShortName(month),
                    ClassAverage = classAverage,
                    StreamAverage = streamAverage,
                    IsSelected = year == endYear && month == endMonth
                });
            }

            return points;
        }

        private static double? ComputeFocusAverage(GroupDataset dataset, string studentId)
        {
            var computation = ComputeRating(dataset);
            return computation.Rows
                .FirstOrDefault(r => string.Equals(r.StudentId, studentId, StringComparison.Ordinal))
                ?.AverageScore;
        }

        // ==================== Sana va guruh ====================

        private (int Year, int Month) ResolvePeriod(int? year, int? month)
        {
            var today = _clock.Today;

            var resolvedMonth = month is >= 1 and <= 12 ? month.Value : today.Month;
            var resolvedYear = year is >= 2000 and <= 2100 ? year.Value : today.Year;

            return (resolvedYear, resolvedMonth);
        }

        private DateTime ResolveRosterDate(int year, int month)
        {
            var today = _clock.Today;

            if (year == today.Year && month == today.Month)
            {
                return today;
            }

            var endOfMonth = MonthlyPeriod.EndOfMonth(year, month);

            // Kelajak oy uchun bugungi tarkib ishlatiladi (hali tarix yo'q).
            return endOfMonth > today ? today : endOfMonth;
        }

        private async Task<ResolvedGroups> ResolveGroupsAtAsync(
            string studentId,
            DateTime onDate,
            CancellationToken cancellationToken)
        {
            var memberships = await _db.GroupMemberships
                .AsNoTracking()
                .Include(m => m.Group)
                .Where(m => m.StudentId == studentId
                            && m.StartDate <= onDate
                            && (m.EndDate == null || m.EndDate > onDate))
                .ToListAsync(cancellationToken);

            var classGroup = memberships
                .FirstOrDefault(m => m.Scope == GroupType.Class)?.Group;

            var streamGroup = memberships
                .FirstOrDefault(m => m.Scope == GroupType.Stream)?.Group;

            // O'tish davri: a'zolik yozuvi umuman bo'lmagan o'quvchi uchun
            // eski ApplicationUser.GroupId o'qiladi.
            if (streamGroup is null)
            {
                var legacyGroupId = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.Id == studentId)
                    .Select(u => u.GroupId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (legacyGroupId is int id)
                {
                    var hasStreamHistory = await _db.GroupMemberships
                        .AsNoTracking()
                        .AnyAsync(m => m.StudentId == studentId && m.Scope == GroupType.Stream, cancellationToken);

                    if (!hasStreamHistory)
                    {
                        streamGroup = await _db.Groups
                            .AsNoTracking()
                            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
                    }
                }
            }

            return new ResolvedGroups(classGroup, streamGroup);
        }

        // ==================== Ichki turlar ====================

        private sealed record ResolvedGroups(Group? ClassGroup, Group? StreamGroup);

        private sealed record SubjectInfo(int SubjectId, string SubjectName);

        private sealed record ExamInfo(int ExamId, int SubjectId, int? GroupId, string Title);

        private sealed record ResultInfo(
            string StudentId,
            int ExamId,
            int TotalQuestions,
            int CorrectAnswers,
            DateTime CompletedDate);

        private sealed class GroupDataset
        {
            public Group Group { get; init; } = null!;
            public List<SubjectInfo> Subjects { get; init; } = new();
            public List<string> StudentIds { get; init; } = new();
            public Dictionary<string, string> StudentNames { get; init; } = new();
            public List<ExamInfo> Exams { get; init; } = new();
            public List<ResultInfo> Results { get; init; } = new();
        }

        private sealed class MonthlySubjectScore
        {
            public double Score { get; init; }
            public string? ExamTitle { get; init; }
            public DateTime? CompletedDate { get; init; }
        }

        private sealed class StudentComputation
        {
            public string StudentId { get; init; } = string.Empty;
            public string StudentName { get; init; } = string.Empty;
            public double? AverageScore { get; set; }
            public int? Rank { get; set; }
            public int ScoredSubjectCount { get; set; }
            public Dictionary<int, MonthlySubjectScore> SubjectScores { get; } = new();
        }

        private sealed class RatingComputation
        {
            public List<StudentComputation> Rows { get; init; } = new();
            public HashSet<int> MonthlySubjectIds { get; init; } = new();
            public Dictionary<int, Dictionary<string, int>> SubjectRanks { get; init; } = new();
        }
    }
}
