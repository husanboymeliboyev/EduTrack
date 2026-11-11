using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    public class UserDirectoryService : IUserDirectoryService
    {
        // LIKE uchun maxsus belgilarni escape qilishda ishlatiladigan belgi.
        private const string LikeEscape = "\\";

        private readonly ApplicationDbContext _context;

        public UserDirectoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<UsersPageViewModel> GetForAdminAsync(UsersFilterViewModel filter)
        {
            return BuildAsync(
                filter,
                allowedGroupIds: null,
                studentsOnly: false,
                isTeacherScope: false);
        }

        public async Task<UsersPageViewModel> GetForTeacherAsync(
            string teacherId,
            UsersFilterViewModel filter)
        {
            // O'qituvchining guruhlari uning fanlari orqali aniqlanadi
            // (Subject -> GroupSubject -> Group). Bu tizimdagi boshqa
            // teacher-scoped sahifalar bilan bir xil qoida.
            var teacherGroupIds = await _context.Subjects
                .AsNoTracking()
                .Where(s => s.TeacherId == teacherId)
                .SelectMany(s => s.GroupSubjects)
                .Select(gs => gs.GroupId)
                .Distinct()
                .ToListAsync();

            // Fan bo'yicha guruh (aralash o'qish): o'qituvchi shu (guruh, fan)
            // juftlikdagi darsga qatnashadigan o'quvchilar ham ro'yxatga kiradi
            // (juftlik int sifatida kodlanadi: GroupId * 1 000 000 + SubjectId).
            var teacherPairKeys = await _context.GroupSubjects
                .AsNoTracking()
                .Where(gs => gs.Subject!.TeacherId == teacherId)
                .Select(gs => gs.GroupId * 1000000 + gs.SubjectId)
                .ToListAsync();

            return await BuildAsync(
                filter,
                allowedGroupIds: teacherGroupIds,
                teacherPairKeys: teacherPairKeys,
                studentsOnly: true,
                isTeacherScope: true);
        }

        private async Task<UsersPageViewModel> BuildAsync(
            UsersFilterViewModel filter,
            IReadOnlyCollection<int>? allowedGroupIds,
            bool studentsOnly,
            bool isTeacherScope,
            IReadOnlyList<int>? teacherPairKeys = null)
        {
            filter ??= new UsersFilterViewModel();
            filter.Normalize();

            var query = BuildQuery(filter, allowedGroupIds, studentsOnly, teacherPairKeys);

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(totalCount / (double)filter.PageSize));

            // Filtr o'zgarganidan keyin eski sahifa raqami mavjud bo'lmasligi mumkin.
            if (filter.Page > totalPages)
            {
                filter.Page = totalPages;
            }

            var rows = await ApplySort(query, filter.Sort)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(u => new UserListItemViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    LoginId = u.LoginId,
                    Email = u.Email ?? string.Empty,
                    // Sinf va guruh alohida: avvalo aktiv a'zolik, bo'lmasa
                    // eski ApplicationUser.GroupId (o'tish davri).
                    ClassName = _context.GroupMemberships
                        .Where(m => m.StudentId == u.Id && m.IsActive && m.Scope == GroupType.Class)
                        .Select(m => m.Group!.Name)
                        .FirstOrDefault()
                        ?? (u.Group != null && u.Group.Type == GroupType.Class ? u.Group.Name : null),
                    StreamName = _context.GroupMemberships
                        .Where(m => m.StudentId == u.Id && m.IsActive && m.Scope == GroupType.Stream)
                        .Select(m => m.Group!.Name)
                        .FirstOrDefault()
                        ?? (u.Group != null && u.Group.Type == GroupType.Stream ? u.Group.Name : null),
                    Role = _context.UserRoles
                        .Where(ur => ur.UserId == u.Id)
                        .Join(
                            _context.Roles,
                            ur => ur.RoleId,
                            r => r.Id,
                            (ur, r) => r.Name)
                        .FirstOrDefault() ?? "Rolsiz"
                })
                .ToListAsync();

            var groupsQuery = _context.Groups.AsNoTracking();

            if (allowedGroupIds != null)
            {
                groupsQuery = groupsQuery.Where(g => allowedGroupIds.Contains(g.Id));
            }

            var groups = await groupsQuery
                .OrderBy(g => g.Type)
                .ThenBy(g => g.Name)
                .Select(g => new UsersGroupOptionViewModel
                {
                    Id = g.Id,
                    Name = g.Name,
                    Type = g.Type
                })
                .ToListAsync();

            // O'qituvchi sahifasida rol filtri yo'q — ro'yxat faqat talabalar.
            IReadOnlyList<string> roles = isTeacherScope
                ? Array.Empty<string>()
                : await _context.Roles
                    .AsNoTracking()
                    .OrderBy(r => r.Name)
                    .Select(r => r.Name!)
                    .ToListAsync();

            return new UsersPageViewModel
            {
                Filter = filter,
                Rows = rows,
                Groups = groups,
                Roles = roles,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize,
                IsTeacherScope = isTeacherScope
            };
        }

        private IQueryable<ApplicationUser> BuildQuery(
            UsersFilterViewModel filter,
            IReadOnlyCollection<int>? allowedGroupIds,
            bool studentsOnly,
            IReadOnlyList<int>? teacherPairKeys = null)
        {
            var query = _context.Users.AsNoTracking();

            if (allowedGroupIds != null)
            {
                // Standart holat: o'quvchining aktiv a'zoligi (sinf yoki guruh).
                // Eski yozuvlar uchun GroupId zaxira. Qo'shimcha: fan bo'yicha
                // aniq biriktirilgan uchrashuvlar (aralash o'qish).
                query = query.Where(u =>
                    _context.GroupMemberships.Any(m => m.StudentId == u.Id
                        && m.IsActive && allowedGroupIds.Contains(m.GroupId))
                    || (u.GroupId.HasValue && allowedGroupIds.Contains(u.GroupId.Value))
                    || u.StudentGroupSubjects.Any(x => x.IsActive
                        && teacherPairKeys!.Contains(x.GroupId * 1000000 + x.SubjectId)));
            }

            if (studentsOnly)
            {
                query = query.Where(u => _context.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(
                        _context.Roles,
                        ur => ur.RoleId,
                        r => r.Id,
                        (ur, r) => r.Name)
                    .Any(name => name == "Student"));
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var pattern = "%" + EscapeLikePattern(filter.Search) + "%";

                query = query.Where(u =>
                    EF.Functions.Like(u.FullName ?? string.Empty, pattern, LikeEscape)
                    || EF.Functions.Like(u.LoginId, pattern, LikeEscape)
                    || EF.Functions.Like(u.Email ?? string.Empty, pattern, LikeEscape));
            }

            if (!string.IsNullOrWhiteSpace(filter.Role))
            {
                var role = filter.Role;

                query = query.Where(u => _context.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(
                        _context.Roles,
                        ur => ur.RoleId,
                        r => r.Id,
                        (ur, r) => r.Name)
                    .Any(name => name == role));
            }

            if (filter.GroupId.HasValue)
            {
                // Sinf yoki guruh filtri: aktiv a'zolik (GroupMemberships)
                // asosiy manba, eski GroupId esa zaxira. Teacher scope'da
                // fan bo'yicha override a'zolari ham qamrab olinadi.
                var groupId = filter.GroupId.Value;
                if (teacherPairKeys != null)
                {
                    query = query.Where(u =>
                        _context.GroupMemberships.Any(m => m.StudentId == u.Id
                            && m.IsActive && m.GroupId == groupId)
                        || u.GroupId == groupId
                        || u.StudentGroupSubjects.Any(x => x.IsActive
                            && x.GroupId == groupId
                            && teacherPairKeys.Contains(x.GroupId * 1000000 + x.SubjectId)));
                }
                else
                {
                    query = query.Where(u =>
                        _context.GroupMemberships.Any(m => m.StudentId == u.Id
                            && m.IsActive && m.GroupId == groupId)
                        || u.GroupId == groupId);
                }
            }

            return query;
        }

        private static IOrderedQueryable<ApplicationUser> ApplySort(
            IQueryable<ApplicationUser> query,
            UsersSort sort)
        {
            // SQLite'da NOCASE collation faqat ASCII harflar uchun ishlaydi;
            // kirill harflari uchun tartib baza collation'iga bog'liq.
            return sort switch
            {
                UsersSort.NameDesc => query
                    .OrderByDescending(u => EF.Functions.Collate(
                        u.FullName ?? string.Empty, "NOCASE"))
                    .ThenByDescending(u => u.LoginId),
                UsersSort.LoginAsc => query
                    .OrderBy(u => u.LoginId),
                UsersSort.LoginDesc => query
                    .OrderByDescending(u => u.LoginId),
                _ => query
                    .OrderBy(u => EF.Functions.Collate(
                        u.FullName ?? string.Empty, "NOCASE"))
                    .ThenBy(u => u.LoginId)
            };
        }

        // LIKE naqshidagi % va _ ni oddiy belgi sifatida qidirish uchun escape qiladi.
        private static string EscapeLikePattern(string value)
        {
            return value
                .Replace(LikeEscape, LikeEscape + LikeEscape)
                .Replace("%", LikeEscape + "%")
                .Replace("_", LikeEscape + "_");
        }
    }
}
