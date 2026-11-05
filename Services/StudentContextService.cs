using EduTrack.Data;
using EduTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    /// <inheritdoc />
    public sealed class StudentContextService : IStudentContextService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAppClock _clock;

        public StudentContextService(ApplicationDbContext db, IAppClock clock)
        {
            _db = db;
            _clock = clock;
        }

        public async Task<StudentContext> GetContextAsync(string studentId, CancellationToken cancellationToken = default)
        {
            var memberships = await _db.GroupMemberships
                .AsNoTracking()
                .Include(m => m.Group)
                .Where(m => m.StudentId == studentId && m.IsActive)
                .ToListAsync(cancellationToken);

            var classGroup = memberships.FirstOrDefault(m => m.Scope == GroupType.Class)?.Group;
            var streamGroup = memberships.FirstOrDefault(m => m.Scope == GroupType.Stream)?.Group;

            // O'tish davri: a'zolik yozuvi hali yo'q o'quvchilar uchun eski maydon.
            if (streamGroup is null)
            {
                var legacyGroupId = await _db.Users
                    .Where(u => u.Id == studentId)
                    .Select(u => u.GroupId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (legacyGroupId is int id)
                {
                    streamGroup = await _db.Groups
                        .AsNoTracking()
                        .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
                }
            }

            return new StudentContext(classGroup, streamGroup);
        }

        public async Task<IReadOnlyList<Group>> GetGroupsAsync(string studentId, GroupType type, CancellationToken cancellationToken = default)
        {
            var context = await GetContextAsync(studentId, cancellationToken);
            var group = type == GroupType.Class ? context.ClassGroup : context.StreamGroup;

            return group is null ? Array.Empty<Group>() : new[] { group };
        }

        public Task<GroupMembership?> GetActiveMembershipAsync(string studentId, GroupType type, CancellationToken cancellationToken = default)
        {
            return _db.GroupMemberships
                .AsNoTracking()
                .Include(m => m.Group)
                .Where(m => m.StudentId == studentId && m.Scope == type && m.IsActive)
                .OrderByDescending(m => m.StartDate)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<string>> GetStudentIdsAsync(int groupId, DateTime? onDate = null, CancellationToken cancellationToken = default)
        {
            var day = (onDate ?? _clock.Today).Date;

            // Tarix uchun IsActive'ga tayanmaymiz: sana oralig'i haqiqat manbai.
            var ids = await _db.GroupMemberships
                .AsNoTracking()
                .Where(m => m.GroupId == groupId)
                .Where(m => m.StartDate <= day && (m.EndDate == null || m.EndDate > day))
                .Select(m => m.StudentId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var group = await _db.Groups
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

            // Faqat guruh (stream) uchun legacy zaxira: a'zolik yozuvi umuman
            // bo'lmagan o'quvchilar eski GroupId orqali topiladi.
            if (group?.Type == GroupType.Stream)
            {
                var legacyIds = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.GroupId == groupId)
                    .Where(u => !_db.GroupMemberships.Any(m => m.StudentId == u.Id && m.Scope == GroupType.Stream))
                    .Select(u => u.Id)
                    .ToListAsync(cancellationToken);

                if (legacyIds.Count > 0)
                {
                    ids = ids.Concat(legacyIds).Distinct().ToList();
                }
            }

            return ids;
        }

        public async Task<bool> IsActiveMemberAsync(string studentId, int groupId, DateTime? onDate = null, CancellationToken cancellationToken = default)
        {
            var day = (onDate ?? _clock.Today).Date;

            var isMember = await _db.GroupMemberships
                .AsNoTracking()
                .AnyAsync(
                    m => m.StudentId == studentId
                         && m.GroupId == groupId
                         && m.StartDate <= day
                         && (m.EndDate == null || m.EndDate > day),
                    cancellationToken);

            if (isMember) return true;

            var group = await _db.Groups
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

            if (group?.Type != GroupType.Stream) return false;

            var hasMembershipHistory = await _db.GroupMemberships
                .AsNoTracking()
                .AnyAsync(m => m.StudentId == studentId && m.Scope == GroupType.Stream, cancellationToken);

            if (hasMembershipHistory) return false;

            return await _db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == studentId && u.GroupId == groupId, cancellationToken);
        }

        public async Task<GroupMembership> AssignAsync(string studentId, int groupId, DateTime? startDate = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(studentId))
                throw new ArgumentException("O'quvchi ID si bo'sh bo'lishi mumkin emas.", nameof(studentId));

            var group = await _db.Groups
                .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
                ?? throw new InvalidOperationException($"Guruh topilmadi: {groupId}");

            if (group.IsArchived)
                throw new InvalidOperationException("Arxivlangan guruhga a'zolik qo'shib bo'lmaydi.");

            var studentExists = await _db.Users.AnyAsync(u => u.Id == studentId, cancellationToken);
            if (!studentExists)
                throw new InvalidOperationException($"O'quvchi topilmadi: {studentId}");

            var start = (startDate ?? _clock.Today).Date;

            // Idempotentlik: shu guruhga aktiv a'zolik allaqachon bo'lsa, qaytaramiz.
            var active = await _db.GroupMemberships
                .Where(m => m.StudentId == studentId && m.Scope == group.Type && m.IsActive)
                .ToListAsync(cancellationToken);

            var same = active.FirstOrDefault(m => m.GroupId == groupId);
            if (same is not null) return same;

            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            // Avval eskisini yopamiz va saqlaymiz: filtrlangan unique indeks
            // bir vaqtda ikkita aktiv yozuvni ko'rmasligi kerak.
            foreach (var old in active)
            {
                old.IsActive = false;
                old.EndDate = start > old.StartDate ? start : old.StartDate;
            }

            if (active.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            var membership = new GroupMembership
            {
                StudentId = studentId,
                GroupId = groupId,
                Scope = group.Type,
                StartDate = start,
                IsActive = true
            };

            _db.GroupMemberships.Add(membership);

            // Dual-write: o'tish davrida eski kod ApplicationUser.GroupId ni o'qiydi.
            if (group.Type == GroupType.Stream)
            {
                var user = await _db.Users.FirstAsync(u => u.Id == studentId, cancellationToken);
                user.GroupId = groupId;
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return membership;
        }

        public async Task<bool> EndAsync(string studentId, GroupType type, DateTime? endDate = null, CancellationToken cancellationToken = default)
        {
            var end = (endDate ?? _clock.Today).Date;

            var memberships = await _db.GroupMemberships
                .Where(m => m.StudentId == studentId && m.Scope == type && m.IsActive)
                .ToListAsync(cancellationToken);

            if (memberships.Count == 0) return false;

            foreach (var membership in memberships)
            {
                membership.IsActive = false;
                membership.EndDate = end > membership.StartDate ? end : membership.StartDate;
            }

            if (type == GroupType.Stream)
            {
                var user = await _db.Users
                    .FirstOrDefaultAsync(u => u.Id == studentId, cancellationToken);

                if (user is not null && memberships.Any(m => m.GroupId == user.GroupId))
                {
                    user.GroupId = null;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
