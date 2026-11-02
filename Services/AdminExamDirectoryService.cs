using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    public class AdminExamDirectoryService : IAdminExamDirectoryService
    {
        private const string LikeEscape = "\\";

        private readonly ApplicationDbContext _context;

        public AdminExamDirectoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AdminExamListPageViewModel> GetExamsAsync(
            AdminExamListFilterViewModel filter)
        {
            filter ??= new AdminExamListFilterViewModel();
            filter.Normalize();

            var query = BuildQuery(filter);

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
                .Select(e => new AdminExamListItemViewModel
                {
                    Id = e.Id,
                    Title = e.Title,
                    SubjectName = e.Subject != null ? e.Subject.Name : null,
                    TeacherName = e.Subject != null && e.Subject.Teacher != null
                        ? e.Subject.Teacher.FullName
                        : null,
                    GroupName = e.Group != null ? e.Group.Name : null,
                    QuestionCount = e.QuestionCount,
                    ResultsCount = e.Results.Count,
                    CreatedDate = e.CreatedDate,
                    IsArchived = e.IsArchived
                })
                .ToListAsync();

            var subjects = await _context.Subjects
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .Select(s => new AdminExamListSubjectOptionViewModel
                {
                    Id = s.Id,
                    Name = s.Name
                })
                .ToListAsync();

            var teachers = await _context.Users
                .AsNoTracking()
                .Where(u => _context.Subjects.Any(s => s.TeacherId == u.Id))
                .OrderBy(u => u.FullName)
                .Select(u => new AdminExamListTeacherOptionViewModel
                {
                    Id = u.Id,
                    Name = u.FullName ?? u.LoginId
                })
                .ToListAsync();

            var groups = await _context.Groups
                .AsNoTracking()
                .OrderBy(g => g.Name)
                .Select(g => new AdminExamListGroupOptionViewModel
                {
                    Id = g.Id,
                    Name = g.Name
                })
                .ToListAsync();

            return new AdminExamListPageViewModel
            {
                Filter = filter,
                Rows = rows,
                Subjects = subjects,
                Teachers = teachers,
                Groups = groups,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        private IQueryable<Exam> BuildQuery(AdminExamListFilterViewModel filter)
        {
            var query = _context.Exams.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var pattern = "%" + EscapeLikePattern(filter.Search) + "%";

                query = query.Where(e =>
                    EF.Functions.Like(e.Title, pattern, LikeEscape)
                    || (e.Subject != null
                        && EF.Functions.Like(e.Subject.Name, pattern, LikeEscape))
                    || (e.Subject != null
                        && e.Subject.Teacher != null
                        && EF.Functions.Like(
                            e.Subject.Teacher.FullName ?? string.Empty,
                            pattern,
                            LikeEscape)));
            }

            if (filter.SubjectId.HasValue)
            {
                query = query.Where(e => e.SubjectId == filter.SubjectId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.TeacherId))
            {
                query = query.Where(e =>
                    e.Subject != null && e.Subject.TeacherId == filter.TeacherId);
            }

            if (filter.GroupId.HasValue)
            {
                query = query.Where(e => e.GroupId == filter.GroupId.Value);
            }

            if (filter.Status.HasValue)
            {
                var archived = filter.Status.Value == AdminExamListStatus.Archived;
                query = query.Where(e => e.IsArchived == archived);
            }

            return query;
        }

        private static IOrderedQueryable<Exam> ApplySort(
            IQueryable<Exam> query,
            AdminExamListSort sort)
        {
            // SQLite'da NOCASE collation faqat ASCII harflar uchun ishlaydi;
            // kirill harflari uchun tartib baza collation'iga bog'liq.
            return sort switch
            {
                AdminExamListSort.DateAsc => query
                    .OrderBy(e => e.CreatedDate)
                    .ThenBy(e => e.Id),
                AdminExamListSort.TitleAsc => query
                    .OrderBy(e => EF.Functions.Collate(e.Title, "NOCASE"))
                    .ThenBy(e => e.Id),
                AdminExamListSort.TitleDesc => query
                    .OrderByDescending(e => EF.Functions.Collate(e.Title, "NOCASE"))
                    .ThenByDescending(e => e.Id),
                AdminExamListSort.ResultsDesc => query
                    .OrderByDescending(e => e.Results.Count)
                    .ThenByDescending(e => e.CreatedDate),
                _ => query
                    .OrderByDescending(e => e.CreatedDate)
                    .ThenByDescending(e => e.Id)
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
