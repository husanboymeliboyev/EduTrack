using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    /// <summary>
    /// Admin/Moderator davomat tahlili. O'qituvchi jurnalida ma'lumot faqat
    /// shu o'qituvchining juftliklari bilan cheklanadi; bu servis esa barcha
    /// (fan, guruh) juftliklarini ko'radi.
    /// </summary>
    public sealed class AdminAttendanceService : IAdminAttendanceService
    {
        private static readonly string[] WeekdayShort = { "Ya", "Du", "Se", "Ch", "Pa", "Ju", "Sh" };

        private static readonly string[] WeekdayFull =
        {
            "Yakshanba", "Dushanba", "Seshanba", "Chorshanba", "Payshanba", "Juma", "Shanba"
        };

        private static readonly string[] MonthNames =
        {
            "Yanvar", "Fevral", "Mart", "Aprel", "May", "Iyun",
            "Iyul", "Avgust", "Sentabr", "Oktabr", "Noyabr", "Dekabr"
        };

        private readonly ApplicationDbContext _context;
        private readonly IAppClock _appClock;
        private readonly IStudentSubjectGroupService _subjectGroups;

        public AdminAttendanceService(
            ApplicationDbContext context,
            IAppClock appClock,
            IStudentSubjectGroupService subjectGroups)
        {
            _context = context;
            _appClock = appClock;
            _subjectGroups = subjectGroups;
        }

        public async Task<AdminAttendanceOverviewViewModel> GetOverviewAsync(
            AdminAttendanceFilterViewModel? filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminAttendanceFilterViewModel();
            var range = ResolveMonth(filter.Month);

            var subjects = await _context.Subjects
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .Select(s => new AttendanceJournalOption { Id = s.Id, Name = s.Name })
                .ToListAsync(cancellationToken);

            var groups = await _context.Groups
                .AsNoTracking()
                .Where(g => !g.IsArchived)
                .OrderBy(g => g.Name)
                .Select(g => new AttendanceJournalOption { Id = g.Id, Name = g.Name })
                .ToListAsync(cancellationToken);

            var subjectId = filter.SubjectId;
            if (subjectId is int requestedSubject && subjects.All(s => s.Id != requestedSubject))
            {
                subjectId = null;
            }

            var groupId = filter.GroupId;
            if (groupId is int requestedGroup && groups.All(g => g.Id != requestedGroup))
            {
                groupId = null;
            }

            var pairQuery = _context.GroupSubjects
                .AsNoTracking()
                .Where(gs => !gs.Group!.IsArchived);

            if (subjectId is int selectedSubjectId)
            {
                pairQuery = pairQuery.Where(gs => gs.SubjectId == selectedSubjectId);
            }

            if (groupId is int selectedGroupId)
            {
                pairQuery = pairQuery.Where(gs => gs.GroupId == selectedGroupId);
            }

            var pairRows = await pairQuery
                .Select(gs => new
                {
                    gs.SubjectId,
                    gs.GroupId,
                    SubjectName = gs.Subject!.Name,
                    GroupName = gs.Group!.Name,
                    GroupType = gs.Group!.Type,
                    OverrideTeacherName = gs.Teacher != null ? gs.Teacher.FullName : null,
                    OwnerTeacherName = gs.Subject!.Teacher != null ? gs.Subject!.Teacher.FullName : null
                })
                .OrderBy(x => x.SubjectName)
                .ThenBy(x => x.GroupName)
                .ToListAsync(cancellationToken);

            var pairs = pairRows
                .Select(p => new PairInfo(
                    p.SubjectId,
                    p.GroupId,
                    p.SubjectName,
                    p.GroupName,
                    p.GroupType,
                    p.OverrideTeacherName ?? p.OwnerTeacherName))
                .ToList();

            var today = _appClock.Today;
            var pairStudents = await BuildPairStudentsAsync(pairs, today, cancellationToken);

            var records = await LoadPairRecordsAsync(pairs, range.Start, range.Next, cancellationToken);
            var recordsByPair = records
                .GroupBy(r => (r.SubjectId, r.GroupId))
                .ToDictionary(g => g.Key, g => g.ToList());

            var rows = new List<AdminAttendancePairRowViewModel>(pairs.Count);

            foreach (var pair in pairs)
            {
                recordsByPair.TryGetValue((pair.SubjectId, pair.GroupId), out var pairRecords);
                pairRecords ??= new List<AttendanceEntry>();
                pairStudents.TryGetValue((pair.SubjectId, pair.GroupId), out var studentIds);

                rows.Add(new AdminAttendancePairRowViewModel
                {
                    SubjectId = pair.SubjectId,
                    GroupId = pair.GroupId,
                    SubjectName = pair.SubjectName,
                    GroupName = pair.GroupName,
                    TeacherName = pair.TeacherName,
                    StudentCount = studentIds?.Count ?? 0,
                    Keldi = pairRecords.Count(r => r.Status == AttendanceStatus.Keldi),
                    Kelmadi = pairRecords.Count(r => r.Status == AttendanceStatus.Kelmadi),
                    KechQoldi = pairRecords.Count(r => r.Status == AttendanceStatus.KechQoldi),
                    Sababli = pairRecords.Count(r => r.Status == AttendanceStatus.Sababli),
                    LastRecordDate = pairRecords.Count == 0
                        ? null
                        : pairRecords.Max(r => r.Date).Date
                });
            }

            var uniqueStudentCount = pairStudents.Values
                .SelectMany(ids => ids)
                .Distinct(StringComparer.Ordinal)
                .Count();

            return new AdminAttendanceOverviewViewModel
            {
                Subjects = subjects,
                Groups = groups,
                SubjectId = subjectId,
                GroupId = groupId,
                Month = range.Start,
                MonthLabel = range.Label,
                PreviousMonth = range.Previous,
                NextMonth = range.Next,
                CanGoNext = range.CanGoNext,
                PairCount = pairs.Count,
                StudentCount = uniqueStudentCount,
                Keldi = records.Count(r => r.Status == AttendanceStatus.Keldi),
                Kelmadi = records.Count(r => r.Status == AttendanceStatus.Kelmadi),
                KechQoldi = records.Count(r => r.Status == AttendanceStatus.KechQoldi),
                Sababli = records.Count(r => r.Status == AttendanceStatus.Sababli),
                Pairs = rows
            };
        }

        public async Task<AdminAttendanceDetailViewModel?> GetDetailAsync(
            int subjectId,
            int groupId,
            DateTime? month,
            CancellationToken cancellationToken = default)
        {
            var range = ResolveMonth(month);
            var today = _appClock.Today;

            var pair = await _context.GroupSubjects
                .AsNoTracking()
                .Where(gs => gs.GroupId == groupId && gs.SubjectId == subjectId)
                .Select(gs => new
                {
                    SubjectName = gs.Subject!.Name,
                    GroupName = gs.Group!.Name,
                    OverrideTeacherName = gs.Teacher != null ? gs.Teacher.FullName : null,
                    OwnerTeacherName = gs.Subject!.Teacher != null ? gs.Subject!.Teacher.FullName : null
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (pair is null)
            {
                return null;
            }

            var studentIds = await _subjectGroups.GetStudentIdsAsync(
                groupId,
                subjectId,
                today,
                cancellationToken);

            var students = new List<(string Id, string? FullName)>();

            if (studentIds.Count > 0)
            {
                var rawStudents = await _context.Users
                    .AsNoTracking()
                    .Where(u => studentIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.FullName })
                    .ToListAsync(cancellationToken);

                students = StudentNameOrder.SortBySurname(
                    rawStudents.Select(u => (Id: u.Id, FullName: u.FullName)),
                    u => u.FullName);
            }

            var scheduleDays = await _context.Schedules
                .AsNoTracking()
                .Where(s => s.SubjectId == subjectId && s.GroupId == groupId)
                .Select(s => s.DayOfWeek)
                .Distinct()
                .ToListAsync(cancellationToken);

            var records = new List<AttendanceEntry>();

            if (students.Count > 0)
            {
                var ids = students.Select(s => s.Id).ToList();

                var rawRecords = await _context.Attendances
                    .AsNoTracking()
                    .Where(a => a.SubjectId == subjectId
                                && a.GroupId == groupId
                                && a.Date >= range.Start
                                && a.Date < range.Next
                                && ids.Contains(a.StudentId))
                    .Select(a => new { a.StudentId, a.Date, a.Status })
                    .ToListAsync(cancellationToken);

                records = rawRecords
                    .Select(a => new AttendanceEntry(subjectId, groupId, a.StudentId, a.Date.Date, a.Status))
                    .ToList();
            }

            var hasSchedule = scheduleDays.Count > 0;

            var scheduledDates = new HashSet<DateTime>();
            if (hasSchedule)
            {
                for (var day = range.Start; day < range.Next; day = day.AddDays(1))
                {
                    if (scheduleDays.Contains(day.DayOfWeek))
                    {
                        scheduledDates.Add(day);
                    }
                }
            }

            var recordedDates = records
                .Select(r => r.Date)
                .Where(d => d >= range.Start && d < range.Next)
                .ToHashSet();

            // Tahlil uchun kunlar = dars jadvali kunlari ∪ davomat yozilgan kunlar.
            // Shu tarzda qo'shimcha (jadvaldan tashqari) darslar ham ko'rinadi.
            var daySet = new SortedSet<DateTime>(scheduledDates);
            daySet.UnionWith(recordedDates);

            var days = daySet
                .Select(day => new AttendanceJournalDay
                {
                    Date = day,
                    WeekdayShort = WeekdayShort[(int)day.DayOfWeek],
                    IsEditable = false,
                    IsToday = day == today
                })
                .ToList();

            var statusesByStudent = records
                .GroupBy(r => r.StudentId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .GroupBy(r => r.Date.ToString("yyyy-MM-dd"))
                        .ToDictionary(d => d.Key, d => (AttendanceStatus?)d.Last().Status));

            var journalRows = students
                .Select(student =>
                {
                    statusesByStudent.TryGetValue(student.Id, out var statuses);
                    statuses ??= new Dictionary<string, AttendanceStatus?>();

                    return new AttendanceJournalRow
                    {
                        StudentId = student.Id,
                        FullName = student.FullName ?? student.Id,
                        Statuses = statuses,
                        Keldi = statuses.Values.Count(s => s == AttendanceStatus.Keldi),
                        Kelmadi = statuses.Values.Count(s => s == AttendanceStatus.Kelmadi),
                        KechQoldi = statuses.Values.Count(s => s == AttendanceStatus.KechQoldi),
                        Sababli = statuses.Values.Count(s => s == AttendanceStatus.Sababli)
                    };
                })
                .ToList();

            var journal = new AttendanceJournalViewModel
            {
                SubjectId = subjectId,
                SubjectName = pair.SubjectName,
                GroupId = groupId,
                GroupName = pair.GroupName,
                Month = range.Start,
                MonthLabel = range.Label,
                PreviousMonth = range.Previous,
                NextMonth = range.Next,
                CanGoNext = range.CanGoNext,
                HasSchedule = hasSchedule,
                Days = days,
                Rows = journalRows
            };

            return new AdminAttendanceDetailViewModel
            {
                Journal = journal,
                TeacherName = pair.OverrideTeacherName ?? pair.OwnerTeacherName,
                ScheduledDayCount = scheduledDates.Count,
                RecordedDayCount = recordedDates.Count,
                UnmarkedDayCount = scheduledDates.Count(d => !recordedDates.Contains(d))
            };
        }

        public async Task<AdminAttendanceDailyViewModel> GetDailyAsync(
            DateTime? date,
            int? subjectId,
            int? groupId,
            CancellationToken cancellationToken = default)
        {
            var today = _appClock.Today;
            var day = (date ?? today).Date;
            if (day > today)
            {
                day = today;
            }

            var nextDay = day.AddDays(1);

            var subjects = await _context.Subjects
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .Select(s => new AttendanceJournalOption { Id = s.Id, Name = s.Name })
                .ToListAsync(cancellationToken);

            var groups = await _context.Groups
                .AsNoTracking()
                .Where(g => !g.IsArchived)
                .OrderBy(g => g.Name)
                .Select(g => new AttendanceJournalOption { Id = g.Id, Name = g.Name })
                .ToListAsync(cancellationToken);

            if (subjectId is int requestedSubject && subjects.All(s => s.Id != requestedSubject))
            {
                subjectId = null;
            }

            if (groupId is int requestedGroup && groups.All(g => g.Id != requestedGroup))
            {
                groupId = null;
            }

            var scheduleQuery = _context.Schedules
                .AsNoTracking()
                .Where(s => s.DayOfWeek == day.DayOfWeek);

            if (subjectId is int selectedSubjectId)
            {
                scheduleQuery = scheduleQuery.Where(s => s.SubjectId == selectedSubjectId);
            }

            if (groupId is int selectedGroupId)
            {
                scheduleQuery = scheduleQuery.Where(s => s.GroupId == selectedGroupId);
            }

            var scheduledPairs = await scheduleQuery
                .Select(s => new { s.SubjectId, s.GroupId })
                .Distinct()
                .ToListAsync(cancellationToken);

            var recordQuery = _context.Attendances
                .AsNoTracking()
                .Where(a => a.GroupId != null && a.Date >= day && a.Date < nextDay);

            if (subjectId is int recordSubjectId)
            {
                recordQuery = recordQuery.Where(a => a.SubjectId == recordSubjectId);
            }

            if (groupId is int recordGroupId)
            {
                recordQuery = recordQuery.Where(a => a.GroupId == recordGroupId);
            }

            var recordPairs = await recordQuery
                .Select(a => new { a.SubjectId, GroupId = a.GroupId.GetValueOrDefault() })
                .Distinct()
                .ToListAsync(cancellationToken);

            var scheduledPairKeys = scheduledPairs
                .Select(p => (p.SubjectId, p.GroupId))
                .ToHashSet();

            var pairKeys = scheduledPairKeys
                .Concat(recordPairs.Select(p => (p.SubjectId, p.GroupId)))
                .Distinct()
                .ToList();

            var pairKeySet = pairKeys.ToHashSet();
            var pairSubjectIds = pairKeys.Select(p => p.SubjectId).Distinct().ToList();
            var pairGroupIds = pairKeys.Select(p => p.GroupId).Distinct().ToList();

            var pairRows = new List<PairInfo>();

            if (pairKeys.Count > 0)
            {
                var rawPairs = await _context.GroupSubjects
                    .AsNoTracking()
                    .Where(gs => pairSubjectIds.Contains(gs.SubjectId)
                                 && pairGroupIds.Contains(gs.GroupId)
                                 && !gs.Group!.IsArchived)
                    .Select(gs => new
                    {
                        gs.SubjectId,
                        gs.GroupId,
                        SubjectName = gs.Subject!.Name,
                        GroupName = gs.Group!.Name,
                        GroupType = gs.Group!.Type,
                        OverrideTeacherName = gs.Teacher != null ? gs.Teacher.FullName : null,
                        OwnerTeacherName = gs.Subject!.Teacher != null ? gs.Subject!.Teacher.FullName : null
                    })
                    .ToListAsync(cancellationToken);

                pairRows = rawPairs
                    .Where(p => pairKeySet.Contains((p.SubjectId, p.GroupId)))
                    .Select(p => new PairInfo(
                        p.SubjectId,
                        p.GroupId,
                        p.SubjectName,
                        p.GroupName,
                        p.GroupType,
                        p.OverrideTeacherName ?? p.OwnerTeacherName))
                    .OrderBy(p => p.SubjectName)
                    .ThenBy(p => p.GroupName)
                    .ToList();
            }

            var pairStudents = await BuildPairStudentsAsync(pairRows, day, cancellationToken);

            var records = await LoadPairRecordsAsync(
                pairRows,
                day,
                nextDay,
                cancellationToken);

            var statusByPairStudent = records
                .GroupBy(r => (r.SubjectId, r.GroupId, r.StudentId))
                .ToDictionary(g => g.Key, g => g.Last().Status);

            var pairStudentIds = pairStudents
                .Values
                .SelectMany(ids => ids)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var studentNames = new Dictionary<string, string>(StringComparer.Ordinal);

            if (pairStudentIds.Count > 0)
            {
                var rawStudents = await _context.Users
                    .AsNoTracking()
                    .Where(u => pairStudentIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.FullName })
                    .ToListAsync(cancellationToken);

                studentNames = rawStudents.ToDictionary(
                    u => u.Id,
                    u => u.FullName ?? u.Id,
                    StringComparer.Ordinal);
            }

            var pairModels = new List<AdminAttendanceDailyPairViewModel>(pairRows.Count);

            foreach (var pair in pairRows)
            {
                pairStudents.TryGetValue((pair.SubjectId, pair.GroupId), out var studentIds);
                studentIds ??= new HashSet<string>(StringComparer.Ordinal);

                var orderedStudents = StudentNameOrder.SortBySurname(
                    studentIds.Select(id => new
                    {
                        Id = id,
                        FullName = studentNames.TryGetValue(id, out var name) ? name : id
                    }),
                    s => s.FullName);

                var absent = new List<AdminAttendanceDailyStudentViewModel>();
                var unmarked = new List<AdminAttendanceDailyStudentViewModel>();

                int keldi = 0, kelmadi = 0, kechQoldi = 0, sababli = 0;

                foreach (var student in orderedStudents)
                {
                    if (!statusByPairStudent.TryGetValue(
                            (pair.SubjectId, pair.GroupId, student.Id),
                            out var status))
                    {
                        unmarked.Add(new AdminAttendanceDailyStudentViewModel
                        {
                            StudentId = student.Id,
                            FullName = student.FullName
                        });
                        continue;
                    }

                    switch (status)
                    {
                        case AttendanceStatus.Keldi:
                            keldi++;
                            break;
                        case AttendanceStatus.Kelmadi:
                            kelmadi++;
                            absent.Add(new AdminAttendanceDailyStudentViewModel
                            {
                                StudentId = student.Id,
                                FullName = student.FullName
                            });
                            break;
                        case AttendanceStatus.KechQoldi:
                            kechQoldi++;
                            break;
                        case AttendanceStatus.Sababli:
                            sababli++;
                            break;
                    }
                }

                pairModels.Add(new AdminAttendanceDailyPairViewModel
                {
                    SubjectId = pair.SubjectId,
                    GroupId = pair.GroupId,
                    SubjectName = pair.SubjectName,
                    GroupName = pair.GroupName,
                    TeacherName = pair.TeacherName,
                    HasSchedule = scheduledPairKeys.Contains((pair.SubjectId, pair.GroupId)),
                    StudentCount = studentIds.Count,
                    Keldi = keldi,
                    Kelmadi = kelmadi,
                    KechQoldi = kechQoldi,
                    Sababli = sababli,
                    Unmarked = unmarked.Count,
                    AbsentStudents = absent,
                    UnmarkedStudents = unmarked
                });
            }

            var uniqueStudentCount = pairStudents
                .Where(p => pairKeySet.Contains((p.Key.SubjectId, p.Key.GroupId)))
                .SelectMany(p => p.Value)
                .Distinct(StringComparer.Ordinal)
                .Count();

            return new AdminAttendanceDailyViewModel
            {
                Subjects = subjects,
                Groups = groups,
                SubjectId = subjectId,
                GroupId = groupId,
                Date = day,
                DateLabel = $"{day.Day} {MonthNames[day.Month - 1]} {day.Year}, {WeekdayFull[(int)day.DayOfWeek]}",
                PreviousDate = day.AddDays(-1),
                NextDate = nextDay,
                CanGoNext = day < today,
                PairCount = pairModels.Count,
                StudentCount = uniqueStudentCount,
                ExpectedCount = pairModels.Sum(p => p.StudentCount),
                Keldi = pairModels.Sum(p => p.Keldi),
                Kelmadi = pairModels.Sum(p => p.Kelmadi),
                KechQoldi = pairModels.Sum(p => p.KechQoldi),
                Sababli = pairModels.Sum(p => p.Sababli),
                Unmarked = pairModels.Sum(p => p.Unmarked),
                Pairs = pairModels
            };
        }

        private async Task<List<AttendanceEntry>> LoadPairRecordsAsync(
            IReadOnlyList<PairInfo> pairs,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken)
        {
            if (pairs.Count == 0)
            {
                return new List<AttendanceEntry>();
            }

            var subjectIds = pairs.Select(p => p.SubjectId).Distinct().ToList();
            var validPairs = pairs
                .Select(p => (p.SubjectId, p.GroupId))
                .ToHashSet();

            var rawRecords = await _context.Attendances
                .AsNoTracking()
                .Where(a => a.GroupId != null
                            && a.Date >= from
                            && a.Date < to
                            && subjectIds.Contains(a.SubjectId))
                .Select(a => new
                {
                    a.SubjectId,
                    GroupId = a.GroupId.GetValueOrDefault(),
                    a.StudentId,
                    a.Date,
                    a.Status
                })
                .ToListAsync(cancellationToken);

            return rawRecords
                .Where(a => validPairs.Contains((a.SubjectId, a.GroupId)))
                .Select(a => new AttendanceEntry(
                    a.SubjectId,
                    a.GroupId,
                    a.StudentId,
                    a.Date.Date,
                    a.Status))
                .ToList();
        }

        /// <summary>
        /// Har bir (fan, guruh) juftligi uchun o'quvchilar to'plamini bir marta
        /// hisoblaydi. Mantiq <see cref="IStudentSubjectGroupService.GetStudentIdsAsync"/>
        /// bilan bir xil: standart guruh a'zolari + shu fan bo'yicha override'lar,
        /// override qilingan o'quvchi standart ro'yxatdan chiqariladi.
        /// </summary>
        private async Task<Dictionary<(int SubjectId, int GroupId), HashSet<string>>> BuildPairStudentsAsync(
            IReadOnlyList<PairInfo> pairs,
            DateTime today,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<(int SubjectId, int GroupId), HashSet<string>>();

            if (pairs.Count == 0)
            {
                return result;
            }

            var groupIds = pairs.Select(p => p.GroupId).Distinct().ToList();
            var subjectIds = pairs.Select(p => p.SubjectId).Distinct().ToList();

            var memberships = await _context.GroupMemberships
                .AsNoTracking()
                .Where(m => groupIds.Contains(m.GroupId)
                            && m.StartDate <= today
                            && (m.EndDate == null || m.EndDate > today))
                .Select(m => new { m.GroupId, m.StudentId })
                .ToListAsync(cancellationToken);

            var defaultByGroup = memberships
                .GroupBy(m => m.GroupId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(m => m.StudentId).ToHashSet(StringComparer.Ordinal));

            // Eski o'quvchilar uchun zaxira: stream guruhda GroupMembership
            // yozuvi umuman bo'lmasa, ApplicationUser.GroupId ishlatiladi.
            var streamGroupIds = pairs
                .Where(p => p.GroupType == GroupType.Stream)
                .Select(p => p.GroupId)
                .Distinct()
                .ToList();

            if (streamGroupIds.Count > 0)
            {
                var legacyStudents = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.GroupId != null)
                    .Where(u => !_context.GroupMemberships.Any(
                        m => m.StudentId == u.Id && m.Scope == GroupType.Stream))
                    .Select(u => new { GroupId = u.GroupId.GetValueOrDefault(), u.Id })
                    .ToListAsync(cancellationToken);

                foreach (var student in legacyStudents)
                {
                    if (!streamGroupIds.Contains(student.GroupId))
                    {
                        continue;
                    }

                    if (!defaultByGroup.TryGetValue(student.GroupId, out var set))
                    {
                        set = new HashSet<string>(StringComparer.Ordinal);
                        defaultByGroup[student.GroupId] = set;
                    }

                    set.Add(student.Id);
                }
            }

            var overrides = await _context.StudentGroupSubjects
                .AsNoTracking()
                .Where(x => subjectIds.Contains(x.SubjectId)
                            && x.StartDate <= today
                            && (x.EndDate == null || x.EndDate > today))
                .Select(x => new { x.SubjectId, x.StudentId, x.GroupId })
                .ToListAsync(cancellationToken);

            var overriddenBySubject = overrides
                .GroupBy(x => x.SubjectId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.StudentId).ToHashSet(StringComparer.Ordinal));

            var explicitByPair = overrides
                .GroupBy(x => (x.SubjectId, x.GroupId))
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.StudentId).ToHashSet(StringComparer.Ordinal));

            foreach (var pair in pairs.DistinctBy(p => (p.SubjectId, p.GroupId)))
            {
                var set = new HashSet<string>(StringComparer.Ordinal);

                if (explicitByPair.TryGetValue((pair.SubjectId, pair.GroupId), out var explicitIds))
                {
                    set.UnionWith(explicitIds);
                }

                if (defaultByGroup.TryGetValue(pair.GroupId, out var defaultIds))
                {
                    overriddenBySubject.TryGetValue(pair.SubjectId, out var overriddenIds);

                    foreach (var id in defaultIds)
                    {
                        if (overriddenIds is null || !overriddenIds.Contains(id))
                        {
                            set.Add(id);
                        }
                    }
                }

                result[(pair.SubjectId, pair.GroupId)] = set;
            }

            return result;
        }

        private MonthRange ResolveMonth(DateTime? month)
        {
            var today = _appClock.Today;
            var selected = month ?? today;
            var start = new DateTime(selected.Year, selected.Month, 1);
            var next = start.AddMonths(1);
            var currentMonthStart = new DateTime(today.Year, today.Month, 1);

            return new MonthRange(
                start,
                next,
                start.AddMonths(-1),
                $"{MonthNames[start.Month - 1]} {start.Year}",
                start < currentMonthStart);
        }

        private sealed record MonthRange(
            DateTime Start,
            DateTime Next,
            DateTime Previous,
            string Label,
            bool CanGoNext);

        private sealed record PairInfo(
            int SubjectId,
            int GroupId,
            string SubjectName,
            string GroupName,
            GroupType GroupType,
            string? TeacherName);

        private sealed record AttendanceEntry(
            int SubjectId,
            int GroupId,
            string StudentId,
            DateTime Date,
            AttendanceStatus Status);
    }
}
