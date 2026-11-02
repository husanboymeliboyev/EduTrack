using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    public class ExamResultsService : IExamResultsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppClock _appClock;

        public ExamResultsService(ApplicationDbContext context, IAppClock appClock)
        {
            _context = context;
            _appClock = appClock;
        }

        public Task<ExamResultsPageViewModel?> GetForTeacherAsync(
            string teacherId,
            int examId,
            ExamResultsFilterViewModel filter)
        {
            return BuildAsync(examId, filter, canApprove: true, teacherId: teacherId);
        }

        public Task<ExamResultsPageViewModel?> GetForAdminAsync(
            int examId,
            ExamResultsFilterViewModel filter)
        {
            return BuildAsync(examId, filter, canApprove: false, teacherId: null);
        }

        public Task<ExamResultsExportData?> GetExportForTeacherAsync(
            string teacherId,
            int examId,
            ExamResultsFilterViewModel filter)
        {
            return BuildExportAsync(examId, filter, teacherId);
        }

        public Task<ExamResultsExportData?> GetExportForAdminAsync(
            int examId,
            ExamResultsFilterViewModel filter)
        {
            return BuildExportAsync(examId, filter, teacherId: null);
        }

        private async Task<ExamResultsPageViewModel?> BuildAsync(
            int examId,
            ExamResultsFilterViewModel filter,
            bool canApprove,
            string? teacherId)
        {
            filter ??= new ExamResultsFilterViewModel();
            filter.Normalize();

            var exam = await FindExamAsync(examId, teacherId);
            if (exam == null)
            {
                return null;
            }

            var baseQuery = ApplyFilters(
                _context.ExamResults.AsNoTracking().Where(r => r.ExamId == examId),
                filter);

            var totalCount = await baseQuery.CountAsync();
            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)filter.PageSize);
            var page = totalPages == 0 ? 1 : Math.Min(filter.Page, totalPages);

            var stats = await GetStatsAsync(baseQuery);

            var rows = await ProjectRows(ApplySort(baseQuery, filter.Sort))
                .Skip((page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            var groupRows = await _context.ExamResults
                .AsNoTracking()
                .Where(r => r.ExamId == examId
                    && r.Student != null
                    && r.Student.GroupId != null)
                .Select(r => new
                {
                    Id = r.Student!.GroupId!.Value,
                    Name = r.Student.Group != null ? r.Student.Group.Name : string.Empty
                })
                .Distinct()
                .OrderBy(g => g.Name)
                .ToListAsync();

            return new ExamResultsPageViewModel
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                SubjectName = exam.SubjectName,
                ExamGroupName = exam.GroupName,
                Filter = filter,
                Rows = rows,
                Groups = groupRows
                    .Select(g => new ExamResultsGroupOptionViewModel { Id = g.Id, Name = g.Name })
                    .ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = filter.PageSize,
                TotalPages = totalPages,
                AveragePercent = stats?.Average ?? 0,
                HighestPercent = stats?.Highest ?? 0,
                LowestPercent = stats?.Lowest ?? 0,
                CanApprove = canApprove
            };
        }

        private async Task<ExamResultsExportData?> BuildExportAsync(
            int examId,
            ExamResultsFilterViewModel filter,
            string? teacherId)
        {
            filter ??= new ExamResultsFilterViewModel();
            filter.Normalize();

            var exam = await FindExamAsync(examId, teacherId);
            if (exam == null)
            {
                return null;
            }

            var baseQuery = ApplyFilters(
                _context.ExamResults.AsNoTracking().Where(r => r.ExamId == examId),
                filter);

            var totalCount = await baseQuery.CountAsync();
            var stats = await GetStatsAsync(baseQuery);

            // 5000 dan oshsa qatorlarni umuman yuklamaymiz — foydalanuvchiga
            // filtrlarni toraytirish haqidagi xabar ko'rsatiladi.
            IReadOnlyList<ExamResultRowViewModel> rows = totalCount > ExamResultsExportData.MaxRows
                ? Array.Empty<ExamResultRowViewModel>()
                : await ProjectRows(ApplySort(baseQuery, filter.Sort)).ToListAsync();

            string? filterGroupName = null;
            if (filter.GroupId.HasValue)
            {
                filterGroupName = await _context.Groups
                    .AsNoTracking()
                    .Where(g => g.Id == filter.GroupId.Value)
                    .Select(g => g.Name)
                    .FirstOrDefaultAsync();
            }

            return new ExamResultsExportData
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                SubjectName = exam.SubjectName,
                ExamGroupName = exam.GroupName,
                FilterGroupName = filterGroupName,
                Filter = filter,
                Rows = rows,
                TotalCount = totalCount,
                AveragePercent = stats?.Average ?? 0,
                HighestPercent = stats?.Highest ?? 0,
                LowestPercent = stats?.Lowest ?? 0,
                GeneratedAt = _appClock.Now
            };
        }

        private async Task<ExamHeader?> FindExamAsync(int examId, string? teacherId)
        {
            var query = _context.Exams
                .AsNoTracking()
                .Where(e => e.Id == examId);

            if (teacherId != null)
            {
                query = query.Where(e => e.Subject!.TeacherId == teacherId);
            }

            var exam = await query
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    SubjectName = e.Subject != null ? e.Subject.Name : null,
                    GroupName = e.Group != null ? e.Group.Name : null
                })
                .FirstOrDefaultAsync();

            return exam == null
                ? null
                : new ExamHeader(exam.Id, exam.Title, exam.SubjectName, exam.GroupName);
        }

        private IQueryable<ExamResultRowViewModel> ProjectRows(IQueryable<ExamResult> query)
        {
            return query.Select(r => new ExamResultRowViewModel
            {
                Id = r.Id,
                StudentName = r.Student != null
                    ? (r.Student.FullName ?? r.Student.UserName ?? string.Empty)
                    : string.Empty,
                GroupName = r.Student != null && r.Student.Group != null
                    ? r.Student.Group.Name
                    : null,
                LoginId = r.Student != null ? r.Student.LoginId : string.Empty,
                CorrectAnswers = r.CorrectAnswers,
                TotalQuestions = r.TotalQuestions,
                Percent = r.TotalQuestions > 0
                    ? 100.0 * r.CorrectAnswers / r.TotalQuestions
                    : 0.0,
                CompletedDate = r.CompletedDate,
                IsApproved = r.IsApproved,
                IsSuspicious = r.TabSwitchCount > 0
                    || r.ExitedFullscreen
                    || _context.ExamAttempts.Any(a =>
                        a.ExamId == r.ExamId
                        && a.StudentId == r.StudentId
                        && a.ViolationCount > 0)
            });
        }

        private static async Task<ExamStats?> GetStatsAsync(IQueryable<ExamResult> query)
        {
            var stats = await query
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Average = g.Average(r => r.TotalQuestions > 0
                        ? 100.0 * r.CorrectAnswers / r.TotalQuestions
                        : 0.0),
                    Highest = g.Max(r => r.TotalQuestions > 0
                        ? 100.0 * r.CorrectAnswers / r.TotalQuestions
                        : 0.0),
                    Lowest = g.Min(r => r.TotalQuestions > 0
                        ? 100.0 * r.CorrectAnswers / r.TotalQuestions
                        : 0.0)
                })
                .FirstOrDefaultAsync();

            return stats == null
                ? null
                : new ExamStats(stats.Average, stats.Highest, stats.Lowest);
        }

        private IQueryable<ExamResult> ApplyFilters(
            IQueryable<ExamResult> query,
            ExamResultsFilterViewModel filter)
        {
            if (!string.IsNullOrEmpty(filter.Search))
            {
                // LIKE'dagi % va _ maxsus belgilarini ekranlaymiz; aks holda
                // foydalanuvchi kiritgan qidiruv "hamma narsani" topib qo'yadi.
                var pattern = "%" + EscapeLike(filter.Search) + "%";

                query = query.Where(r => r.Student != null
                    && (EF.Functions.Like(
                            r.Student.FullName ?? string.Empty,
                            pattern,
                            "\\")
                        || EF.Functions.Like(
                            r.Student.LoginId,
                            pattern,
                            "\\")));
            }

            if (filter.GroupId.HasValue)
            {
                query = query.Where(r =>
                    r.Student != null && r.Student.GroupId == filter.GroupId.Value);
            }

            if (filter.PercentFrom.HasValue)
            {
                query = query.Where(r =>
                    (r.TotalQuestions > 0
                        ? 100.0 * r.CorrectAnswers / r.TotalQuestions
                        : 0.0) >= filter.PercentFrom.Value);
            }

            if (filter.PercentTo.HasValue)
            {
                query = query.Where(r =>
                    (r.TotalQuestions > 0
                        ? 100.0 * r.CorrectAnswers / r.TotalQuestions
                        : 0.0) <= filter.PercentTo.Value);
            }

            if (filter.DateFrom.HasValue)
            {
                query = query.Where(r => r.CompletedDate >= filter.DateFrom.Value.Date);
            }

            if (filter.DateTo.HasValue)
            {
                var exclusiveEnd = filter.DateTo.Value.Date.AddDays(1);
                query = query.Where(r => r.CompletedDate < exclusiveEnd);
            }

            if (filter.Status == ExamResultStatusFilter.Approved)
            {
                query = query.Where(r => r.IsApproved);
            }
            else if (filter.Status == ExamResultStatusFilter.Pending)
            {
                query = query.Where(r => !r.IsApproved);
            }

            if (filter.Reliability == ExamResultReliabilityFilter.Clean)
            {
                query = query.Where(r =>
                    r.TabSwitchCount == 0
                    && !r.ExitedFullscreen
                    && !_context.ExamAttempts.Any(a =>
                        a.ExamId == r.ExamId
                        && a.StudentId == r.StudentId
                        && a.ViolationCount > 0));
            }
            else if (filter.Reliability == ExamResultReliabilityFilter.Suspicious)
            {
                query = query.Where(r =>
                    r.TabSwitchCount > 0
                    || r.ExitedFullscreen
                    || _context.ExamAttempts.Any(a =>
                        a.ExamId == r.ExamId
                        && a.StudentId == r.StudentId
                        && a.ViolationCount > 0));
            }

            return query;
        }

        private static IQueryable<ExamResult> ApplySort(
            IQueryable<ExamResult> query,
            ExamResultsSort sort)
        {
            return sort switch
            {
                ExamResultsSort.DateAsc => query
                    .OrderBy(r => r.CompletedDate)
                    .ThenBy(r => r.Id),
                ExamResultsSort.NameAsc => query
                    .OrderBy(r => EF.Functions.Collate(
                        r.Student!.FullName ?? string.Empty,
                        "NOCASE"))
                    .ThenBy(r => r.Id),
                ExamResultsSort.NameDesc => query
                    .OrderByDescending(r => EF.Functions.Collate(
                        r.Student!.FullName ?? string.Empty,
                        "NOCASE"))
                    .ThenByDescending(r => r.Id),
                ExamResultsSort.PercentAsc => query
                    .OrderBy(r => r.TotalQuestions > 0
                        ? 100.0 * r.CorrectAnswers / r.TotalQuestions
                        : 0.0)
                    .ThenBy(r => r.Id),
                ExamResultsSort.PercentDesc => query
                    .OrderByDescending(r => r.TotalQuestions > 0
                        ? 100.0 * r.CorrectAnswers / r.TotalQuestions
                        : 0.0)
                    .ThenByDescending(r => r.Id),
                _ => query
                    .OrderByDescending(r => r.CompletedDate)
                    .ThenByDescending(r => r.Id)
            };
        }

        private static string EscapeLike(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
        }

        private sealed record ExamHeader(
            int Id,
            string Title,
            string? SubjectName,
            string? GroupName);

        private sealed record ExamStats(
            double Average,
            double Highest,
            double Lowest);
    }
}
