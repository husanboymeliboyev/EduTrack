using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly ApplicationDbContext _context;

        public AnalyticsService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Admin -> barcha fanlar ro'yxati, umumiy son ko'rsatkichlari bilan
        public async Task<List<SubjectAnalyticsSummaryViewModel>> GetSubjectSummariesAsync()
        {
            var subjects = await _context.Subjects
                .Include(s => s.Teacher)
                .OrderBy(s => s.Name)
                .ToListAsync();

            var groupCounts = await _context.GroupSubjects
                .GroupBy(gs => gs.SubjectId)
                .Select(g => new { SubjectId = g.Key, Count = g.Count() })
                .ToListAsync();

            var examCounts = await _context.Exams
                .GroupBy(e => e.SubjectId)
                .Select(g => new { SubjectId = g.Key, Count = g.Count() })
                .ToListAsync();

            return subjects.Select(s => new SubjectAnalyticsSummaryViewModel
            {
                SubjectId = s.Id,
                SubjectName = s.Name,
                TeacherName = s.Teacher?.FullName,
                GroupCount = groupCounts.FirstOrDefault(g => g.SubjectId == s.Id)?.Count ?? 0,
                ExamCount = examCounts.FirstOrDefault(g => g.SubjectId == s.Id)?.Count ?? 0
            }).ToList();
        }

        // Admin -> bitta fan ichida, guruhlar kesimida statistika
        public async Task<List<GroupAnalyticsSummaryViewModel>> GetGroupSummariesForSubjectAsync(int subjectId)
        {
            var groups = await _context.GroupSubjects
                .Where(gs => gs.SubjectId == subjectId)
                .Include(gs => gs.Group)
                .Select(gs => gs.Group!)
                .ToListAsync();

            var components = await _context.GradeComponents
                .Where(c => c.SubjectId == subjectId)
                .ToListAsync();
            var maxTotal = components.Sum(c => c.MaxScore);
            var componentIds = components.Select(c => c.Id).ToList();

            var subjectExamIds = await _context.Exams
                .Where(e => e.SubjectId == subjectId)
                .Select(e => e.Id)
                .ToListAsync();

            var result = new List<GroupAnalyticsSummaryViewModel>();

            foreach (var group in groups)
            {
                var students = await _context.Users
                    .Where(u => u.GroupId == group.Id)
                    .ToListAsync();
                var studentIds = students.Select(s => s.Id).ToList();

                // Imtihon o'rtacha foizi (shu guruh talabalarining shu fandagi barcha imtihon natijalari bo'yicha)
                var examResults = await _context.ExamResults
                    .Where(r => subjectExamIds.Contains(r.ExamId) && studentIds.Contains(r.StudentId))
                    .ToListAsync();

                double? examAvg = examResults.Any()
                    ? Math.Round(examResults.Average(r => r.TotalQuestions > 0 ? 100.0 * r.CorrectAnswers / r.TotalQuestions : 0), 1)
                    : null;

                // Baholash o'rtacha foizi (shu guruh talabalarining shu fandagi komponent ballari bo'yicha)
                double? gradeAvg = null;
                if (componentIds.Any() && maxTotal > 0)
                {
                    var grades = await _context.StudentGrades
                        .Where(g => componentIds.Contains(g.GradeComponentId) && studentIds.Contains(g.StudentId))
                        .ToListAsync();

                    var perStudentTotals = studentIds
                        .Select(sid => grades.Where(g => g.StudentId == sid).Sum(g => g.Score))
                        .Where(total => total > 0)
                        .ToList();

                    if (perStudentTotals.Any())
                    {
                        gradeAvg = Math.Round(perStudentTotals.Average() / maxTotal * 100, 1);
                    }
                }

                result.Add(new GroupAnalyticsSummaryViewModel
                {
                    GroupId = group.Id,
                    GroupName = group.Name,
                    StudentCount = students.Count,
                    AverageExamPercentage = examAvg,
                    AverageGradePercentage = gradeAvg
                });
            }

            return result.OrderBy(g => g.GroupName).ToList();
        }

        // Admin -> bitta fan + bitta guruh ichida, imtihonlar ro'yxati va har birining o'rtacha natijasi
        public async Task<List<ExamAnalyticsRowViewModel>> GetExamRowsAsync(int subjectId, int groupId)
        {
            var studentIds = await _context.Users
                .Where(u => u.GroupId == groupId)
                .Select(u => u.Id)
                .ToListAsync();

            var exams = await _context.Exams
                .Where(e => e.SubjectId == subjectId && (e.GroupId == null || e.GroupId == groupId))
                .Include(e => e.Results)
                .OrderByDescending(e => e.CreatedDate)
                .ToListAsync();

            return exams.Select(e =>
            {
                var groupResults = e.Results.Where(r => studentIds.Contains(r.StudentId)).ToList();
                return new ExamAnalyticsRowViewModel
                {
                    ExamId = e.Id,
                    ExamTitle = e.Title,
                    CreatedDate = e.CreatedDate,
                    ResultCount = groupResults.Count,
                    AveragePercentage = groupResults.Any()
                        ? Math.Round(groupResults.Average(r => r.TotalQuestions > 0 ? 100.0 * r.CorrectAnswers / r.TotalQuestions : 0), 1)
                        : null
                };
            }).ToList();
        }

        // Admin -> bitta fan + bitta guruh ichida, talabalar reytingi (baholash komponentlari bo'yicha)
        public async Task<List<AdminRankingEntryViewModel>> GetSubjectGroupRankingAsync(int subjectId, int groupId)
        {
            var components = await _context.GradeComponents
                .Where(c => c.SubjectId == subjectId)
                .ToListAsync();
            var maxTotal = components.Sum(c => c.MaxScore);
            var componentIds = components.Select(c => c.Id).ToList();

            var students = await _context.Users
                .Where(u => u.GroupId == groupId)
                .ToListAsync();
            var studentIds = students.Select(s => s.Id).ToList();

            var grades = await _context.StudentGrades
                .Where(g => componentIds.Contains(g.GradeComponentId) && studentIds.Contains(g.StudentId))
                .ToListAsync();

            var ranking = students.Select(s =>
            {
                var total = grades.Where(g => g.StudentId == s.Id).Sum(g => g.Score);
                var pct = maxTotal > 0 ? Math.Round(total / maxTotal * 100, 1) : 0;
                return new AdminRankingEntryViewModel
                {
                    StudentId = s.Id,
                    StudentName = s.FullName ?? "",
                    AveragePercentage = pct
                };
            })
            .OrderByDescending(r => r.AveragePercentage)
            .ToList();

            for (int i = 0; i < ranking.Count; i++)
            {
                ranking[i].Rank = i + 1;
            }

            return ranking;
        }
    }
}