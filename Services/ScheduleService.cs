using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    public class ScheduleConflictResult
    {
        public bool HasConflict { get; set; }
        public string? Message { get; set; }
    }

    public class ScheduleService
    {
        private readonly ApplicationDbContext _context;
        private readonly IStudentContextService _studentContext;

        public ScheduleService(ApplicationDbContext context, IStudentContextService studentContext)
        {
            _context = context;
            _studentContext = studentContext;
        }

        private static readonly Dictionary<DayOfWeek, string> DayNames = new()
        {
            { DayOfWeek.Monday, "dushanba" },
            { DayOfWeek.Tuesday, "seshanba" },
            { DayOfWeek.Wednesday, "chorshanba" },
            { DayOfWeek.Thursday, "payshanba" },
            { DayOfWeek.Friday, "juma" },
            { DayOfWeek.Saturday, "shanba" },
            { DayOfWeek.Sunday, "yakshanba" },
        };

        /// <summary>
        /// Darsning haqiqiy o'qituvchisini aniqlaydi:
        /// Schedule.TeacherId → GroupSubject.TeacherId → Subject.TeacherId.
        /// </summary>
        public static string? ResolveTeacherId(Schedule schedule, IReadOnlyCollection<GroupSubject> groupSubjects)
        {
            if (!string.IsNullOrWhiteSpace(schedule.TeacherId))
            {
                return schedule.TeacherId;
            }

            var overrideTeacherId = groupSubjects
                .FirstOrDefault(gs => gs.GroupId == schedule.GroupId && gs.SubjectId == schedule.SubjectId)
                ?.TeacherId;

            if (!string.IsNullOrWhiteSpace(overrideTeacherId))
            {
                return overrideTeacherId;
            }

            return schedule.Subject?.TeacherId;
        }

        private async Task<string?> ResolveTeacherIdAsync(Schedule schedule)
        {
            if (!string.IsNullOrWhiteSpace(schedule.TeacherId))
            {
                return schedule.TeacherId;
            }

            var overrideTeacherId = await _context.GroupSubjects
                .Where(gs => gs.GroupId == schedule.GroupId && gs.SubjectId == schedule.SubjectId)
                .Select(gs => gs.TeacherId)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(overrideTeacherId))
            {
                return overrideTeacherId;
            }

            return await _context.Subjects
                .Where(s => s.Id == schedule.SubjectId)
                .Select(s => s.TeacherId)
                .FirstOrDefaultAsync();
        }

        private static bool Overlaps(Schedule candidate, Schedule existing)
            => candidate.StartTime < existing.EndTime && existing.StartTime < candidate.EndTime;

        /// <summary>
        /// Yangi/tahrirlanayotgan darsni mavjud jadval bilan solishtirib,
        /// guruh, o'qituvchi va o'quvchi bo'yicha to'qnashuvni tekshiradi.
        /// Smena hisobga olinadi: ertalabki va tushdan keyingi darslar
        /// bir-biriga to'sqinlik qilmaydi.
        /// </summary>
        public async Task<ScheduleConflictResult> CheckConflictAsync(Schedule schedule, int? excludeId = null)
        {
            if (schedule.StartTime >= schedule.EndTime)
            {
                return new ScheduleConflictResult
                {
                    HasConflict = true,
                    Message = "Boshlanish vaqti tugash vaqtidan oldin bo'lishi kerak"
                };
            }

            // 1) GURUH to'qnashuvi (o'sha smena ichida)
            // Vaqt kesishishini xotirada tekshiramiz — SQLite TimeSpan solishtirishini
            // to'g'ridan-to'g'ri SQL'ga o'gira olmaydi.
            var sameGroupDay = await _context.Schedules
                .Include(s => s.Subject)
                .Where(s => s.GroupId == schedule.GroupId
                    && s.DayOfWeek == schedule.DayOfWeek
                    && s.Shift == schedule.Shift
                    && (excludeId == null || s.Id != excludeId))
                .ToListAsync();

            var groupConflict = sameGroupDay.FirstOrDefault(s => Overlaps(schedule, s));

            if (groupConflict != null)
            {
                var group = await _context.Groups.FindAsync(schedule.GroupId);
                return new ScheduleConflictResult
                {
                    HasConflict = true,
                    Message = $"\"{group?.Name}\" guruhida {DayNames[schedule.DayOfWeek]} kuni soat " +
                              $"{groupConflict.StartTime:hh\\:mm}-{groupConflict.EndTime:hh\\:mm} da " +
                              $"\"{groupConflict.Subject?.Name}\" darsi allaqachon bor"
                };
            }

            // 2) O'QITUVCHI to'qnashuvi
            var teacherId = await ResolveTeacherIdAsync(schedule);
            if (!string.IsNullOrWhiteSpace(teacherId))
            {
                var groupSubjects = await _context.GroupSubjects
                    .AsNoTracking()
                    .ToListAsync();

                var sameTeacherDay = await _context.Schedules
                    .Include(s => s.Subject)
                    .Include(s => s.Group)
                    .Where(s => s.DayOfWeek == schedule.DayOfWeek
                        && s.Shift == schedule.Shift
                        && (excludeId == null || s.Id != excludeId))
                    .ToListAsync();

                var teacherConflict = sameTeacherDay.FirstOrDefault(s =>
                    Overlaps(schedule, s) && ResolveTeacherId(s, groupSubjects) == teacherId);

                if (teacherConflict != null)
                {
                    var teacher = await _context.Users.FindAsync(teacherId);
                    return new ScheduleConflictResult
                    {
                        HasConflict = true,
                        Message = $"Bu o'qituvchi ({teacher?.FullName}) {DayNames[schedule.DayOfWeek]} kuni soat " +
                                  $"{teacherConflict.StartTime:hh\\:mm}-{teacherConflict.EndTime:hh\\:mm} da " +
                                  $"\"{teacherConflict.Group?.Name}\" guruhida band"
                    };
                }
            }

            // 3) O'QUVCHI to'qnashuvi: guruh a'zolari bir vaqtda boshqa
            // (sinf yoki guruh) darsida bo'lmasligi kerak.
            var studentIds = await _studentContext.GetStudentIdsAsync(schedule.GroupId);
            if (studentIds.Count > 0)
            {
                var memberships = await _context.GroupMemberships
                    .AsNoTracking()
                    .Where(m => studentIds.Contains(m.StudentId) && m.IsActive)
                    .Select(m => new { m.StudentId, m.GroupId, m.Scope })
                    .ToListAsync();

                // Legacy GroupId faqat "stream" ma'nosida ishlatiladi: o'quvchida
                // aktiv stream a'zoligi bo'lsa, eski maydon e'tiborga olinmaydi.
                var streamMemberIds = memberships
                    .Where(m => m.Scope == GroupType.Stream)
                    .Select(m => m.StudentId)
                    .ToHashSet();

                var legacy = await _context.Users
                    .AsNoTracking()
                    .Where(u => studentIds.Contains(u.Id) && u.GroupId != null)
                    .Select(u => new { u.Id, GroupId = u.GroupId!.Value })
                    .ToListAsync();

                var otherGroupIds = memberships
                    .Select(m => m.GroupId)
                    .Concat(legacy.Where(l => !streamMemberIds.Contains(l.Id)).Select(l => l.GroupId))
                    .Where(id => id != schedule.GroupId)
                    .Distinct()
                    .ToList();

                if (otherGroupIds.Count > 0)
                {
                    var otherSchedules = await _context.Schedules
                        .Include(s => s.Subject)
                        .Include(s => s.Group)
                        .Where(s => otherGroupIds.Contains(s.GroupId)
                            && s.DayOfWeek == schedule.DayOfWeek
                            && s.Shift == schedule.Shift
                            && (excludeId == null || s.Id != excludeId))
                        .ToListAsync();

                    var studentConflict = otherSchedules.FirstOrDefault(s => Overlaps(schedule, s));

                    if (studentConflict != null)
                    {
                        return new ScheduleConflictResult
                        {
                            HasConflict = true,
                            Message = $"Bu guruh o'quvchilarining {DayNames[schedule.DayOfWeek]} kuni soat " +
                                      $"{studentConflict.StartTime:hh\\:mm}-{studentConflict.EndTime:hh\\:mm} da " +
                                      $"\"{studentConflict.Group?.Name}\" guruhida \"{studentConflict.Subject?.Name}\" darsi bor"
                        };
                    }
                }
            }

            return new ScheduleConflictResult { HasConflict = false };
        }

        /// <summary>
        /// O'qituvchining haftalik umumiy yuklamasini va fanlar bo'yicha taqsimotini
        /// jadvaldagi darslar yig'indisidan hisoblaydi (alohida "Ish rejasi" jadvali yo'q).
        /// Guruh-fan biriktirmasidagi o'qituvchi ham hisobga olinadi.
        /// </summary>
        public async Task<TeacherWorkloadViewModel> GetTeacherWorkloadAsync(string teacherId)
        {
            var schedules = await _context.Schedules
                .Include(s => s.Subject)
                .ToListAsync();

            var groupSubjects = await _context.GroupSubjects
                .AsNoTracking()
                .ToListAsync();

            var mine = schedules
                .Where(s => ResolveTeacherId(s, groupSubjects) == teacherId)
                .ToList();

            var bySubject = mine
                .GroupBy(s => s.Subject!.Name)
                .Select(g => new SubjectWorkloadItem
                {
                    SubjectName = g.Key,
                    Hours = Math.Round(g.Sum(s => (s.EndTime - s.StartTime).TotalHours), 1)
                })
                .OrderByDescending(x => x.Hours)
                .ToList();

            return new TeacherWorkloadViewModel
            {
                TotalHoursPerWeek = Math.Round(mine.Sum(s => (s.EndTime - s.StartTime).TotalHours), 1),
                BySubject = bySubject
            };
        }

        /// <summary>
        /// Fan nomidan barqaror (har doim bir xil) rang hosil qiladi.
        /// string.GetHashCode() ishlatilmaydi, chunki .NET'da u process bo'yicha randomlashtirilgan.
        /// </summary>
        public static int GetHueForSubject(string key)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in key)
                {
                    hash = hash * 31 + c;
                }
                return Math.Abs(hash) % 360;
            }
        }
    }
}
