using EduTrack.Data;
using EduTrack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EduTrack.Services
{
    public sealed class AttendanceService : IAttendanceService
    {
        private const int MaxNamesInError = 5;

        private readonly ApplicationDbContext _context;
        private readonly IAppClock _appClock;
        private readonly AttendanceOptions _options;
        private readonly IStudentSubjectGroupService _subjectGroups;

        public AttendanceService(
            ApplicationDbContext context,
            IAppClock appClock,
            IOptions<AttendanceOptions> options,
            IStudentSubjectGroupService subjectGroups)
        {
            _context = context;
            _appClock = appClock;
            _options = options.Value;
            _subjectGroups = subjectGroups;
        }

        public int BackfillDays => Math.Max(0, _options.BackfillDays);

        public Task<bool> IsGroupSubjectAsync(int groupId, int subjectId) =>
            _context.GroupSubjects
                .AnyAsync(gs => gs.GroupId == groupId && gs.SubjectId == subjectId);

        public string? ValidateDate(DateTime date)
        {
            var day = date.Date;
            var today = _appClock.Today;

            if (day > today)
            {
                return "Kelajak sanaga davomat belgilab bo'lmaydi.";
            }

            var earliest = today.AddDays(-BackfillDays);
            if (day < earliest)
            {
                return BackfillDays == 0
                    ? "Faqat bugungi kun uchun davomat belgilash mumkin."
                    : $"Faqat oxirgi {BackfillDays} kun uchun davomat belgilash mumkin.";
            }

            return null;
        }

        public async Task<AttendanceSaveResult> SaveAsync(
            int subjectId,
            int groupId,
            DateTime date,
            IReadOnlyDictionary<string, string?>? statuses)
        {
            var day = date.Date;

            var dateError = ValidateDate(day);
            if (dateError != null)
            {
                return AttendanceSaveResult.Fail(AttendanceSaveStatus.DateOutOfRange, dateError);
            }

            if (!await IsGroupSubjectAsync(groupId, subjectId))
            {
                return AttendanceSaveResult.Fail(
                    AttendanceSaveStatus.GroupSubjectMismatch,
                    "Tanlangan guruh bu fanni o'qimaydi.");
            }

            var studentIdsInGroup = await _subjectGroups
                .GetStudentIdsAsync(groupId, subjectId, day);
            var students = await _context.Users
                .Where(u => studentIdsInGroup.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToListAsync();

            if (students.Count == 0)
            {
                return AttendanceSaveResult.Success(0, 0);
            }

            // 1) Holatlarni tekshirish — hali hech narsa yozilmaydi.
            var resolved = new Dictionary<string, AttendanceStatus>(StringComparer.Ordinal);
            var invalidStudents = new List<string>();

            foreach (var student in students)
            {
                // Holat umuman yuborilmagan bo'lsa, mavjud xatti-harakat saqlanadi:
                // "Kelmadi". (Forma buni oldindan tasdiqlab so'raydi.)
                if (statuses == null
                    || !statuses.TryGetValue(student.Id, out var raw)
                    || string.IsNullOrWhiteSpace(raw))
                {
                    resolved[student.Id] = AttendanceStatus.Kelmadi;
                    continue;
                }

                if (!TryParseStatus(raw, out var parsed))
                {
                    invalidStudents.Add(student.FullName ?? student.Id);
                    continue;
                }

                resolved[student.Id] = parsed;
            }

            if (invalidStudents.Count > 0)
            {
                var names = string.Join(", ", invalidStudents.Take(MaxNamesInError));
                if (invalidStudents.Count > MaxNamesInError)
                {
                    names += $" va yana {invalidStudents.Count - MaxNamesInError} ta";
                }

                return AttendanceSaveResult.Fail(
                    AttendanceSaveStatus.InvalidStatus,
                    $"Noto'g'ri davomat holati: {names}. Sahifa yangilanib, qaytadan belgilang.");
            }

            // 2) Mavjud yozuvlarni bitta so'rovda olish (talaba boshiga so'rov yo'q).
            var studentIds = students.Select(s => s.Id).ToList();
            var existing = await _context.Attendances
                .Where(a => a.SubjectId == subjectId && a.Date == day && studentIds.Contains(a.StudentId))
                .ToDictionaryAsync(a => a.StudentId, StringComparer.Ordinal);

            var created = 0;
            var updated = 0;

            foreach (var student in students)
            {
                var status = resolved[student.Id];

                if (existing.TryGetValue(student.Id, out var record))
                {
                    if (record.Status != status)
                    {
                        record.Status = status;
                        updated++;
                    }

                    continue;
                }

                _context.Attendances.Add(new Attendance
                {
                    SubjectId = subjectId,
                    StudentId = student.Id,
                    Date = day,
                    Status = status
                });
                created++;
            }

            // 3) Bitta SaveChanges — butun amal atomar.
            await _context.SaveChangesAsync();

            return AttendanceSaveResult.Success(created, updated);
        }

        public async Task<AttendanceSaveResult> SaveCellAsync(
            int subjectId,
            int groupId,
            string studentId,
            DateTime date,
            string? status)
        {
            var day = date.Date;

            var dateError = ValidateDate(day);
            if (dateError != null)
            {
                return AttendanceSaveResult.Fail(AttendanceSaveStatus.DateOutOfRange, dateError);
            }

            if (!await IsGroupSubjectAsync(groupId, subjectId))
            {
                return AttendanceSaveResult.Fail(
                    AttendanceSaveStatus.GroupSubjectMismatch,
                    "Tanlangan guruh bu fanni o'qimaydi.");
            }

            var belongsToGroup = await _subjectGroups
                .IsStudentInGroupSubjectAsync(studentId, groupId, subjectId, day);
            if (!belongsToGroup)
            {
                return AttendanceSaveResult.Fail(
                    AttendanceSaveStatus.StudentNotInGroup,
                    "Talaba bu guruhda topilmadi.");
            }

            if (!TryParseStatus(status, out var parsed))
            {
                return AttendanceSaveResult.Fail(
                    AttendanceSaveStatus.InvalidStatus,
                    "Noto'g'ri davomat holati.");
            }

            var existing = await _context.Attendances
                .FirstOrDefaultAsync(a => a.SubjectId == subjectId
                                          && a.Date == day
                                          && a.StudentId == studentId);

            if (existing == null)
            {
                _context.Attendances.Add(new Attendance
                {
                    SubjectId = subjectId,
                    StudentId = studentId,
                    Date = day,
                    Status = parsed
                });

                await _context.SaveChangesAsync();
                return AttendanceSaveResult.Success(1, 0);
            }

            if (existing.Status == parsed)
            {
                return AttendanceSaveResult.Success(0, 0);
            }

            existing.Status = parsed;
            await _context.SaveChangesAsync();
            return AttendanceSaveResult.Success(0, 1);
        }

        private static bool TryParseStatus(string? raw, out AttendanceStatus status)
        {
            status = default;

            return !string.IsNullOrWhiteSpace(raw)
                   && Enum.TryParse(raw.Trim(), ignoreCase: true, out status)
                   && Enum.IsDefined(typeof(AttendanceStatus), status);
        }
    }
}
