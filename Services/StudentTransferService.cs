using EduTrack.Data;
using EduTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    public class StudentTransferService : IStudentTransferService
    {
        private readonly ApplicationDbContext _context;

        public StudentTransferService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<StudentTransferDetails?> GetDetailsAsync(string studentId)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == studentId);

            if (user == null)
            {
                return null;
            }

            var memberships = await _context.GroupMemberships
                .AsNoTracking()
                .Include(m => m.Group)
                .Where(m => m.StudentId == studentId && m.IsActive)
                .ToListAsync();

            return new StudentTransferDetails
            {
                StudentId = user.Id,
                FullName = user.FullName ?? "",
                LoginId = user.LoginId,
                CurrentClass = memberships.FirstOrDefault(m => m.Scope == GroupType.Class)?.Group,
                CurrentStream = memberships.FirstOrDefault(m => m.Scope == GroupType.Stream)?.Group,
            };
        }

        public async Task<List<TransferCandidate>> SearchAsync(string? term, int limit = 50)
        {
            term = term?.Trim();

            var query = _context.Users
                .AsNoTracking()
                .Where(u => u.Memberships.Any(m => m.IsActive));

            if (!string.IsNullOrWhiteSpace(term))
            {
                var lower = term.ToLower();
                query = query.Where(u =>
                    (u.FullName != null && u.FullName.ToLower().Contains(lower))
                    || (u.LoginId != null && u.LoginId.ToLower().Contains(lower)));
            }

            var users = await query
                .OrderBy(u => u.LoginId)
                .Take(limit)
                .Select(u => new { u.Id, u.FullName, u.LoginId })
                .ToListAsync();

            var userIds = users.Select(u => u.Id).ToList();
            var memberships = await _context.GroupMemberships
                .AsNoTracking()
                .Include(m => m.Group)
                .Where(m => userIds.Contains(m.StudentId) && m.IsActive)
                .ToListAsync();

            return users.Select(u => new TransferCandidate
            {
                StudentId = u.Id,
                FullName = u.FullName ?? "",
                LoginId = u.LoginId,
                CurrentClassName = memberships.FirstOrDefault(m => m.StudentId == u.Id && m.Scope == GroupType.Class)?.Group?.Name,
                CurrentStreamName = memberships.FirstOrDefault(m => m.StudentId == u.Id && m.Scope == GroupType.Stream)?.Group?.Name,
            }).ToList();
        }

        public async Task<TransferResult> TransferAsync(TransferRequest request)
        {
            var result = new TransferResult();

            if (request.NewClassGroupId == null && request.NewStreamGroupId == null)
            {
                result.Error = "Kamida yangi sinf yoki yangi guruh tanlang.";
                return result;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.StudentId);
            if (user == null)
            {
                result.Error = "O'quvchi topilmadi.";
                return result;
            }

            var currentMemberships = await _context.GroupMemberships
                .Include(m => m.Group)
                .Where(m => m.StudentId == user.Id && m.IsActive)
                .ToListAsync();

            var currentClass = currentMemberships.FirstOrDefault(m => m.Scope == GroupType.Class);
            var currentStream = currentMemberships.FirstOrDefault(m => m.Scope == GroupType.Stream);

            Group? targetClass = null;
            if (request.NewClassGroupId != null)
            {
                targetClass = await _context.Groups.FirstOrDefaultAsync(g => g.Id == request.NewClassGroupId);
                if (targetClass == null || targetClass.Type != GroupType.Class || targetClass.IsArchived)
                {
                    result.Error = "Tanlangan sinf topilmadi yoki sinf emas.";
                    return result;
                }
                if (currentClass?.GroupId == targetClass.Id)
                {
                    result.Error = $"O'quvchi allaqachon \"{targetClass.Name}\" sinfida.";
                    return result;
                }
            }

            Group? targetStream = null;
            if (request.NewStreamGroupId != null)
            {
                targetStream = await _context.Groups.FirstOrDefaultAsync(g => g.Id == request.NewStreamGroupId);
                if (targetStream == null || targetStream.Type != GroupType.Stream || targetStream.IsArchived)
                {
                    result.Error = "Tanlangan yo'nalish guruhi topilmadi yoki guruh emas.";
                    return result;
                }
                if (currentStream?.GroupId == targetStream.Id)
                {
                    result.Error = $"O'quvchi allaqachon \"{targetStream.Name}\" guruhida.";
                    return result;
                }
            }

            var date = request.Date.Date;
            var reason = string.IsNullOrWhiteSpace(request.Reason) ? "O'tkazish" : request.Reason.Trim();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (targetClass != null)
                {
                    // Faqat o'zgarayotgan scope yopiladi (ikkinchisi tegilmaydi)
                    CloseMembership(currentClass, date, reason);
                    result.Changes.Add($"Sinf: {currentClass?.Group?.Name ?? "—"} → {targetClass.Name}");

                    _context.GroupMemberships.Add(new GroupMembership
                    {
                        StudentId = user.Id,
                        GroupId = targetClass.Id,
                        Scope = GroupType.Class,
                        StartDate = date,
                        IsActive = true,
                    });
                    user.GroupId = targetClass.Id; // legacy maydon
                }

                if (targetStream != null)
                {
                    CloseMembership(currentStream, date, reason);
                    result.Changes.Add($"Guruh: {currentStream?.Group?.Name ?? "—"} → {targetStream.Name}");

                    _context.GroupMemberships.Add(new GroupMembership
                    {
                        StudentId = user.Id,
                        GroupId = targetStream.Id,
                        Scope = GroupType.Stream,
                        StartDate = date,
                        IsActive = true,
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                result.Error = "O'tkazish bekor qilindi: " + ex.Message;
                return result;
            }
        }

        private static void CloseMembership(GroupMembership? membership, DateTime date, string reason)
        {
            if (membership == null)
            {
                return;
            }

            membership.IsActive = false;
            membership.EndDate = date;
            membership.EndReason = reason;
        }
    }
}
