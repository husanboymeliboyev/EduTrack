using EduTrack.Data;
using EduTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    /// <inheritdoc />
    public sealed class StudentSubjectGroupService : IStudentSubjectGroupService
    {
        private readonly ApplicationDbContext _db;
        private readonly IStudentContextService _studentContext;
        private readonly IAppClock _clock;

        public StudentSubjectGroupService(
            ApplicationDbContext db,
            IStudentContextService studentContext,
            IAppClock clock)
        {
            _db = db;
            _studentContext = studentContext;
            _clock = clock;
        }

        public async Task<int?> GetDefaultGroupIdAsync(string studentId, CancellationToken cancellationToken = default)
        {
            var context = await _studentContext.GetContextAsync(studentId, cancellationToken);
            return context.StreamGroup?.Id;
        }

        public async Task<int?> GetGroupIdAsync(
            string studentId,
            int subjectId,
            DateTime? onDate = null,
            CancellationToken cancellationToken = default)
        {
            var day = (onDate ?? _clock.Today).Date;

            var overrideGroupId = await ActiveOverridesQuery(day)
                .Where(x => x.StudentId == studentId && x.SubjectId == subjectId)
                .Select(x => (int?)x.GroupId)
                .FirstOrDefaultAsync(cancellationToken);

            if (overrideGroupId is int id)
            {
                return id;
            }

            return await GetDefaultGroupIdAsync(studentId, cancellationToken);
        }

        public async Task<IReadOnlyDictionary<int, int>> GetGroupMapAsync(
            string studentId,
            IEnumerable<int> subjectIds,
            DateTime? onDate = null,
            CancellationToken cancellationToken = default)
        {
            var day = (onDate ?? _clock.Today).Date;
            var subjectList = subjectIds.Distinct().ToList();
            var map = new Dictionary<int, int>();

            if (subjectList.Count == 0)
            {
                return map;
            }

            var overrides = await ActiveOverridesQuery(day)
                .Where(x => x.StudentId == studentId && subjectList.Contains(x.SubjectId))
                .Select(x => new { x.SubjectId, x.GroupId })
                .ToListAsync(cancellationToken);

            var defaultGroupId = await GetDefaultGroupIdAsync(studentId, cancellationToken);

            foreach (var subjectId in subjectList)
            {
                var overrideRow = overrides.FirstOrDefault(x => x.SubjectId == subjectId);

                if (overrideRow is not null)
                {
                    map[subjectId] = overrideRow.GroupId;
                }
                else if (defaultGroupId is int def)
                {
                    map[subjectId] = def;
                }
            }

            return map;
        }

        public async Task<IReadOnlyList<int>> GetSubjectIdsAsync(
            string studentId,
            DateTime? onDate = null,
            CancellationToken cancellationToken = default)
        {
            var day = (onDate ?? _clock.Today).Date;

            var overrides = await ActiveOverridesQuery(day)
                .Where(x => x.StudentId == studentId)
                .Select(x => new { x.SubjectId, x.GroupId })
                .ToListAsync(cancellationToken);

            var defaultGroupId = await GetDefaultGroupIdAsync(studentId, cancellationToken);

            var groupIds = overrides
                .Select(x => x.GroupId)
                .Append(defaultGroupId ?? 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (groupIds.Count == 0)
            {
                return Array.Empty<int>();
            }

            var pairs = await _db.GroupSubjects
                .AsNoTracking()
                .Where(gs => groupIds.Contains(gs.GroupId))
                .Select(gs => new { gs.SubjectId, gs.GroupId })
                .ToListAsync(cancellationToken);

            var overrideBySubject = overrides
                .GroupBy(x => x.SubjectId)
                .ToDictionary(g => g.Key, g => g.First().GroupId);

            var subjectIds = new HashSet<int>();

            foreach (var pair in pairs)
            {
                var resolvedGroupId = overrideBySubject.TryGetValue(pair.SubjectId, out var overrideGroupId)
                    ? overrideGroupId
                    : defaultGroupId;

                if (resolvedGroupId == pair.GroupId)
                {
                    subjectIds.Add(pair.SubjectId);
                }
            }

            return subjectIds.OrderBy(x => x).ToList();
        }

        public async Task<IReadOnlyList<StudentGroupSubject>> GetAssignmentsAsync(
            string studentId,
            CancellationToken cancellationToken = default)
        {
            return await _db.StudentGroupSubjects
                .AsNoTracking()
                .Include(x => x.Subject)
                .Include(x => x.Group)
                .Where(x => x.StudentId == studentId)
                .OrderByDescending(x => x.StartDate)
                .ThenBy(x => x.Subject!.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<string>> GetStudentIdsAsync(
            int groupId,
            int subjectId,
            DateTime? onDate = null,
            CancellationToken cancellationToken = default)
        {
            var day = (onDate ?? _clock.Today).Date;

            var overrideRows = await ActiveOverridesQuery(day)
                .Where(x => x.SubjectId == subjectId)
                .Select(x => new { x.StudentId, x.GroupId })
                .Distinct()
                .ToListAsync(cancellationToken);

            var explicitIds = overrideRows
                .Where(x => x.GroupId == groupId)
                .Select(x => x.StudentId)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            // Shu fan bo'yicha boshqa guruhga override qilingan o'quvchilar
            // standart guruh ro'yxatidan chiqarib tashlanadi.
            var overriddenIds = overrideRows
                .Select(x => x.StudentId)
                .ToHashSet(StringComparer.Ordinal);

            var defaultIds = await _studentContext.GetStudentIdsAsync(groupId, day, cancellationToken);

            var result = new HashSet<string>(explicitIds, StringComparer.Ordinal);

            foreach (var id in defaultIds)
            {
                if (!overriddenIds.Contains(id))
                {
                    result.Add(id);
                }
            }

            return result.ToList();
        }

        public async Task<bool> IsStudentInGroupSubjectAsync(
            string studentId,
            int groupId,
            int subjectId,
            DateTime? onDate = null,
            CancellationToken cancellationToken = default)
        {
            var resolvedGroupId = await GetGroupIdAsync(studentId, subjectId, onDate, cancellationToken);
            if (resolvedGroupId != groupId)
            {
                return false;
            }

            return await _db.GroupSubjects
                .AsNoTracking()
                .AnyAsync(gs => gs.GroupId == groupId && gs.SubjectId == subjectId, cancellationToken);
        }

        public async Task<StudentGroupSubject?> SetGroupAsync(
            string studentId,
            int subjectId,
            int groupId,
            DateTime? startDate = null,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(studentId))
                throw new ArgumentException("O'quvchi ID si bo'sh bo'lishi mumkin emas.", nameof(studentId));

            var group = await _db.Groups
                .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
                ?? throw new InvalidOperationException($"Guruh topilmadi: {groupId}");

            if (group.IsArchived)
                throw new InvalidOperationException("Arxivlangan guruhga biriktirib bo'lmaydi.");

            if (group.Type != GroupType.Stream)
                throw new InvalidOperationException("Fan bo'yicha biriktirish faqat yo'nalish guruhi uchun mumkin.");

            var subjectExists = await _db.Subjects
                .AnyAsync(s => s.Id == subjectId, cancellationToken);
            if (!subjectExists)
                throw new InvalidOperationException($"Fan topilmadi: {subjectId}");

            var studentExists = await _db.Users
                .AnyAsync(u => u.Id == studentId, cancellationToken);
            if (!studentExists)
                throw new InvalidOperationException($"O'quvchi topilmadi: {studentId}");

            var groupTeachesSubject = await _db.GroupSubjects
                .AnyAsync(gs => gs.GroupId == groupId && gs.SubjectId == subjectId, cancellationToken);
            if (!groupTeachesSubject)
                throw new InvalidOperationException("Tanlangan guruh bu fanni o'qimaydi.");

            var day = (startDate ?? _clock.Today).Date;
            var defaultGroupId = await GetDefaultGroupIdAsync(studentId, cancellationToken);

            // Standart guruhga qaytarish = override'ni yopish.
            if (defaultGroupId == groupId)
            {
                await ResetToDefaultAsync(studentId, subjectId, day, reason, cancellationToken);
                return null;
            }

            var active = await _db.StudentGroupSubjects
                .Where(x => x.StudentId == studentId && x.SubjectId == subjectId && x.IsActive)
                .ToListAsync(cancellationToken);

            var same = active.FirstOrDefault(x => x.GroupId == groupId);
            if (same is not null)
            {
                return same;
            }

            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            foreach (var old in active)
            {
                old.IsActive = false;
                old.EndDate = day > old.StartDate ? day : old.StartDate;
                old.EndReason = string.IsNullOrWhiteSpace(reason) ? old.EndReason : reason;
            }

            if (active.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            var assignment = new StudentGroupSubject
            {
                StudentId = studentId,
                SubjectId = subjectId,
                GroupId = groupId,
                StartDate = day,
                IsActive = true
            };

            _db.StudentGroupSubjects.Add(assignment);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return assignment;
        }

        public async Task<bool> ResetToDefaultAsync(
            string studentId,
            int subjectId,
            DateTime? startDate = null,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            var day = (startDate ?? _clock.Today).Date;

            var active = await _db.StudentGroupSubjects
                .Where(x => x.StudentId == studentId && x.SubjectId == subjectId && x.IsActive)
                .ToListAsync(cancellationToken);

            if (active.Count == 0)
            {
                return false;
            }

            foreach (var assignment in active)
            {
                assignment.IsActive = false;
                assignment.EndDate = day > assignment.StartDate ? day : assignment.StartDate;
                assignment.EndReason = string.IsNullOrWhiteSpace(reason)
                    ? "Standart guruhga qaytarildi"
                    : reason;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>
        /// Berilgan sanada amal qiladigan override yozuvlari. Tarix uchun
        /// <see cref="StudentGroupSubject.IsActive"/>ga emas, sana oralig'iga
        /// tayanamiz (kelajak sanali yozuv ham to'g'ri ishlashi uchun).
        /// </summary>
        private IQueryable<StudentGroupSubject> ActiveOverridesQuery(DateTime day)
        {
            return _db.StudentGroupSubjects
                .AsNoTracking()
                .Where(x => x.StartDate <= day && (x.EndDate == null || x.EndDate > day));
        }
    }
}
