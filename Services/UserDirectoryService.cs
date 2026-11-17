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
            // O'qituvchining guruhlari fanlari orqali aniqlanadi:
            // fan egasi bo'lgan fanlarning guruhlari + guruh bo'yicha
            // override (GroupSubject.TeacherId) orqali o'qitadigan guruhlari.
            // Efektiv o'qituvchi: override bo'lsa — override, aks holda fan egasi.
            var teacherGroupIds = await _context.GroupSubjects
                .AsNoTracking()
                .Where(gs => (gs.TeacherId ?? gs.Subject!.TeacherId) == teacherId)
                .Select(gs => gs.GroupId)
                .Distinct()
                .ToListAsync();

            // Fan bo'yicha guruh (aralash o'qish): o'qituvchi shu (guruh, fan)
            // juftlikdagi darsga qatnashadigan o'quvchilar ham ro'yxatga kiradi
            // (juftlik int sifatida kodlanadi: GroupId * 1 000 000 + SubjectId).
            var teacherPairKeys = await _context.GroupSubjects
                .AsNoTracking()
                .Where(gs => (gs.TeacherId ?? gs.Subject!.TeacherId) == teacherId)
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

            // Familiya bo'yicha tartiblash uchun avval faqat kerakli yengil
            // ustunlarni olamiz, tartiblaymiz, keyin faqat joriy sahifa
            // qatorlari uchun to'liq ma'lumotni yuklaymiz.
            var sortable = await query
                .Select(u => new UserSortRow(u.Id, u.FullName, u.LoginId))
                .ToListAsync();

            var orderedIds = ApplySort(sortable, filter.Sort)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(r => r.Id)
                .ToList();

            var pageRows = orderedIds.Count == 0
                ? new List<UserListItemViewModel>()
                : await _context.Users
                    .AsNoTracking()
                    .Where(u => orderedIds.Contains(u.Id))
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

            // Tartibni saqlash: Where(...) natijasi bazadan ixtiyoriy tartibda
            // qaytishi mumkin, shuning uchun sahifa Id'lar bo'yicha joylaymiz.
            var rowsById = pageRows.ToDictionary(r => r.Id, StringComparer.Ordinal);
            var rows = orderedIds
                .Where(id => rowsById.ContainsKey(id))
                .Select(id => rowsById[id])
                .ToList();

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

        private sealed record UserSortRow(string Id, string? FullName, string? LoginId);

        private static List<UserSortRow> ApplySort(
            IReadOnlyCollection<UserSortRow> rows,
            UsersSort sort)
        {
            return sort switch
            {
                // Familiya bo'yicha kamayish tartibi: ko'payish tartibini
                // teskari aylantiramiz (barqarorlik muhim emas).
                UsersSort.NameDesc => StudentNameOrder
                    .SortBySurname(rows, r => r.FullName)
                    .AsEnumerable()
                    .Reverse()
                    .ToList(),
                UsersSort.LoginAsc => rows
                    .OrderBy(r => r.LoginId, StringComparer.Ordinal)
                    .ToList(),
                UsersSort.LoginDesc => rows
                    .OrderByDescending(r => r.LoginId, StringComparer.Ordinal)
                    .ToList(),
                // Default: familiya, keyin to'liq ism bo'yicha (StudentNameOrder).
                _ => StudentNameOrder.SortBySurname(rows, r => r.FullName)
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
