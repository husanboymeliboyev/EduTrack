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

        public ScheduleService(ApplicationDbContext context)
        {
            _context = context;
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
        /// Yangi/tahrirlanayotgan darsni mavjud jadval bilan solishtirib,
        /// guruh va o'qituvchi bo'yicha to'qnashuvni tekshiradi.
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

            // 1) GURUH to'qnashuvi
            // Avval SQL orqali faqat GroupId + DayOfWeek bo'yicha filtrlaymiz (bu SQLite'da tarjima bo'ladi),
            // vaqt kesishishini esa xotirada (C# tomonida) tekshiramiz — chunki SQLite TimeSpan solishtirishini
            // to'g'ridan-to'g'ri SQL'ga o'gira olmaydi.
            var sameGroupDay = await _context.Schedules
                .Include(s => s.Subject)
                .Where(s => s.GroupId == schedule.GroupId
                    && s.DayOfWeek == schedule.DayOfWeek
                    && (excludeId == null || s.Id != excludeId))
                .ToListAsync();

            var groupConflict = sameGroupDay.FirstOrDefault(s =>
                schedule.StartTime < s.EndTime && s.StartTime < schedule.EndTime);

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
            var subject = await _context.Subjects.FindAsync(schedule.SubjectId);
            if (subject?.TeacherId != null)
            {
                var sameTeacherDay = await _context.Schedules
                    .Include(s => s.Subject)
                    .Include(s => s.Group)
                    .Where(s => s.Subject!.TeacherId == subject.TeacherId
                        && s.DayOfWeek == schedule.DayOfWeek
                        && (excludeId == null || s.Id != excludeId))
                    .ToListAsync();

                var teacherConflict = sameTeacherDay.FirstOrDefault(s =>
                    schedule.StartTime < s.EndTime && s.StartTime < schedule.EndTime);

                if (teacherConflict != null)
                {
                    var teacher = await _context.Users.FindAsync(subject.TeacherId);
                    return new ScheduleConflictResult
                    {
                        HasConflict = true,
                        Message = $"Bu o'qituvchi ({teacher?.FullName}) {DayNames[schedule.DayOfWeek]} kuni soat " +
                                  $"{teacherConflict.StartTime:hh\\:mm}-{teacherConflict.EndTime:hh\\:mm} da " +
                                  $"\"{teacherConflict.Group?.Name}\" guruhida band"
                    };
                }
            }

            return new ScheduleConflictResult { HasConflict = false };
        }
        /// <summary>
        /// O'qituvchining haftalik umumiy yuklamasini va fanlar bo'yicha taqsimotini
        /// jadvaldagi darslar yig'indisidan hisoblaydi (alohida "Ish rejasi" jadvali yo'q).
        /// </summary>
        public async Task<TeacherWorkloadViewModel> GetTeacherWorkloadAsync(string teacherId)
        {
            var schedules = await _context.Schedules
                .Include(s => s.Subject)
                .Where(s => s.Subject!.TeacherId == teacherId)
                .ToListAsync();

            var bySubject = schedules
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
                TotalHoursPerWeek = Math.Round(bySubject.Sum(x => x.Hours), 1),
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