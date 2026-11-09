using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IStudentSubjectGroupService _subjectGroups;
        private readonly ISubjectGradeService _subjectGrades;

        public AnalyticsService(
            ApplicationDbContext context,
            IStudentSubjectGroupService subjectGroups,
            ISubjectGradeService subjectGrades)
        {
            _context = context;
            _subjectGroups = subjectGroups;
            _subjectGrades = subjectGrades;
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

            var subjectExamIds = await _context.Exams
                .Where(e => e.SubjectId == subjectId)
                .Select(e => e.Id)
                .ToListAsync();

            var result = new List<GroupAnalyticsSummaryViewModel>();

            foreach (var group in groups)
            {
                // Fan-guruh rosteri: aralash o'qishda group-fan juftligidagi
                // haqiqiy o'quvchilar (override + standart).
                var studentIds = (await _subjectGroups
                    .GetStudentIdsAsync(group.Id, subjectId))
                    .ToList();

                // Imtihon o'rtacha foizi (shu guruh talabalarining shu fandagi barcha imtihon natijalari bo'yicha)
                var examResults = await _context.ExamResults
                    .Where(r => subjectExamIds.Contains(r.ExamId) && studentIds.Contains(r.StudentId))
                    .ToListAsync();

                double? examAvg = examResults.Any()
                    ? Math.Round(examResults.Average(r => r.TotalQuestions > 0 ? 100.0 * r.CorrectAnswers / r.TotalQuestions : 0), 1)
                    : null;

                // Yakuniy 100 ballik baho o'rtachasi (qo'lda baho yoki
                // tasdiqlangan imtihon o'rtachasi).
                double? gradeAvg = null;
                if (studentIds.Count > 0)
                {
                    var gradeMap = await _subjectGrades.GetGradesForStudentsAsync(
                        studentIds, new[] { subjectId });

                    var scores = gradeMap.Values
                        .Select(m => m.TryGetValue(subjectId, out var g) && g.HasScore ? g.Score : null)
                        .Where(s => s.HasValue)
                        .Select(s => s!.Value)
                        .ToList();

                    if (scores.Count > 0)
                    {
                        gradeAvg = Math.Round(scores.Average(), 1, MidpointRounding.AwayFromZero);
                    }
                }

                result.Add(new GroupAnalyticsSummaryViewModel
                {
                    GroupId = group.Id,
                    GroupName = group.Name,
                    StudentCount = studentIds.Count,
                    AverageExamPercentage = examAvg,
                    AverageGradePercentage = gradeAvg
                });
            }

            return result.OrderBy(g => g.GroupName).ToList();
        }

        // Admin -> bitta fan + bitta guruh ichida, imtihonlar ro'yxati va har birining o'rtacha natijasi
        public async Task<List<ExamAnalyticsRowViewModel>> GetExamRowsAsync(int subjectId, int groupId)
        {
            // Fan-guruh rosteri (aralash o'qish hisobga olinadi).
            var studentIds = (await _subjectGroups.GetStudentIdsAsync(groupId, subjectId))
                .ToHashSet(StringComparer.Ordinal);

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

        // Admin -> bitta fan + bitta guruh ichida, talabalar reytingi (yakuniy baho bo'yicha)
        public async Task<List<AdminRankingEntryViewModel>> GetSubjectGroupRankingAsync(int subjectId, int groupId)
        {
            var studentIds = (await _subjectGroups.GetStudentIdsAsync(groupId, subjectId))
                .ToHashSet(StringComparer.Ordinal);

            var students = await _context.Users
                .AsNoTracking()
                .Where(u => studentIds.Contains(u.Id))
                .ToListAsync();

            var gradeMap = await _subjectGrades.GetGradesForStudentsAsync(
                studentIds.ToList(), new[] { subjectId });

            var ranking = students.Select(s =>
            {
                var score = gradeMap.TryGetValue(s.Id, out var grades)
                            && grades.TryGetValue(subjectId, out var grade)
                            && grade.HasScore
                    ? grade.Score!.Value
                    : 0;

                return new AdminRankingEntryViewModel
                {
                    StudentId = s.Id,
                    StudentName = s.FullName ?? "",
                    AveragePercentage = score
                };
            })
            .OrderByDescending(r => r.AveragePercentage)
            .ThenBy(r => r.StudentName, StringComparer.Ordinal)
            .ToList();

            for (int i = 0; i < ranking.Count; i++)
            {
                ranking[i].Rank = i + 1;
            }

            return ranking;
        }
    }
}