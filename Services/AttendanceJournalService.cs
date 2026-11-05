using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EduTrack.Services
{
    public sealed class AttendanceJournalService : IAttendanceJournalService
    {
        private static readonly string[] WeekdayShort = { "Ya", "Du", "Se", "Ch", "Pa", "Ju", "Sh" };

        private static readonly string[] MonthNames =
        {
            "Yanvar", "Fevral", "Mart", "Aprel", "May", "Iyun",
            "Iyul", "Avgust", "Sentabr", "Oktabr", "Noyabr", "Dekabr"
        };

        private readonly ApplicationDbContext _context;
        private readonly IAppClock _appClock;
        private readonly AttendanceOptions _options;

        public AttendanceJournalService(
            ApplicationDbContext context,
            IAppClock appClock,
            IOptions<AttendanceOptions> options)
        {
            _context = context;
            _appClock = appClock;
            _options = options.Value;
        }

        public async Task<AttendanceJournalViewModel> BuildAsync(
            string teacherId,
            int? subjectId,
            int? groupId,
            DateTime? month)
        {
            var today = _appClock.Today;
            var selectedMonth = month ?? today;
            var monthStart = new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
            var nextMonth = monthStart.AddMonths(1);
            var previousMonth = monthStart.AddMonths(-1);
            var currentMonthStart = new DateTime(today.Year, today.Month, 1);
            var monthLabel = $"{MonthNames[monthStart.Month - 1]} {monthStart.Year}";

            var subjects = await _context.Subjects
                .Where(s => s.TeacherId == teacherId)
                .OrderBy(s => s.Name)
                .Select(s => new AttendanceJournalOption { Id = s.Id, Name = s.Name })
                .ToListAsync();

            var model = new AttendanceJournalViewModel
            {
                Subjects = subjects,
                Month = monthStart,
                MonthLabel = monthLabel,
                PreviousMonth = previousMonth,
                NextMonth = nextMonth,
                CanGoNext = monthStart < currentMonthStart
            };

            if (subjects.Count == 0)
            {
                return model;
            }

            var subject = subjects.FirstOrDefault(s => s.Id == subjectId) ?? subjects[0];

            var groupIds = await _context.GroupSubjects
                .Where(gs => gs.SubjectId == subject.Id)
                .Select(gs => gs.GroupId)
                .Distinct()
                .ToListAsync();

            var groups = await _context.Groups
                .Where(g => groupIds.Contains(g.Id))
                .OrderBy(g => g.Name)
                .Select(g => new AttendanceJournalOption { Id = g.Id, Name = g.Name })
                .ToListAsync();

            model = CloneWithSubject(model, subject, groups);

            if (groups.Count == 0)
            {
                return model;
            }

            var group = groups.FirstOrDefault(g => g.Id == groupId) ?? groups[0];

            var students = await _context.Users
                .Where(u => u.GroupId == group.Id)
                .OrderBy(u => u.FullName)
                .Select(u => new { u.Id, u.FullName })
                .ToListAsync();

            var scheduleDays = await _context.Schedules
                .Where(s => s.SubjectId == subject.Id && s.GroupId == group.Id)
                .Select(s => s.DayOfWeek)
                .Distinct()
                .ToListAsync();

            var earliestEditable = today.AddDays(-Math.Max(0, _options.BackfillDays));
            var hasSchedule = scheduleDays.Count > 0;

            var days = new List<AttendanceJournalDay>();
            for (var day = monthStart; day < nextMonth; day = day.AddDays(1))
            {
                if (hasSchedule && !scheduleDays.Contains(day.DayOfWeek))
                {
                    continue;
                }

                days.Add(new AttendanceJournalDay
                {
                    Date = day,
                    WeekdayShort = WeekdayShort[(int)day.DayOfWeek],
                    IsEditable = day >= earliestEditable && day <= today,
                    IsToday = day == today
                });
            }

            var statusesByStudent = new Dictionary<string, Dictionary<string, AttendanceStatus?>>();

            if (students.Count > 0)
            {
                var studentIds = students.Select(s => s.Id).ToList();
                var records = await _context.Attendances
                    .Where(a => a.SubjectId == subject.Id
                                && a.Date >= monthStart
                                && a.Date < nextMonth
                                && studentIds.Contains(a.StudentId))
                    .Select(a => new { a.StudentId, a.Date, a.Status })
                    .ToListAsync();

                statusesByStudent = records
                    .GroupBy(a => a.StudentId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.ToDictionary(
                            a => a.Date.ToString("yyyy-MM-dd"),
                            a => (AttendanceStatus?)a.Status));
            }

            var rows = students
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

            return new AttendanceJournalViewModel
            {
                Subjects = model.Subjects,
                Groups = model.Groups,
                SubjectId = subject.Id,
                SubjectName = subject.Name,
                GroupId = group.Id,
                GroupName = group.Name,
                Month = model.Month,
                MonthLabel = model.MonthLabel,
                PreviousMonth = model.PreviousMonth,
                NextMonth = model.NextMonth,
                CanGoNext = model.CanGoNext,
                HasSchedule = hasSchedule,
                Days = days,
                Rows = rows
            };
        }

        private static AttendanceJournalViewModel CloneWithSubject(
            AttendanceJournalViewModel source,
            AttendanceJournalOption subject,
            IReadOnlyList<AttendanceJournalOption> groups) => new()
            {
                Subjects = source.Subjects,
                Groups = groups,
                SubjectId = subject.Id,
                SubjectName = subject.Name,
                Month = source.Month,
                MonthLabel = source.MonthLabel,
                PreviousMonth = source.PreviousMonth,
                NextMonth = source.NextMonth,
                CanGoNext = source.CanGoNext
            };
    }
}
