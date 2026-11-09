using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    /// <inheritdoc />
    public sealed class SubjectGradeService : ISubjectGradeService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAppClock _clock;

        public SubjectGradeService(ApplicationDbContext db, IAppClock clock)
        {
            _db = db;
            _clock = clock;
        }

        public async Task<IReadOnlyDictionary<int, StudentSubjectGradeViewModel>> GetStudentGradesAsync(
            string studentId,
            IReadOnlyCollection<int> subjectIds,
            CancellationToken cancellationToken = default)
        {
            var map = await GetGradesForStudentsAsync(
                new[] { studentId }, subjectIds, cancellationToken);

            return map.TryGetValue(studentId, out var grades)
                ? grades
                : new Dictionary<int, StudentSubjectGradeViewModel>();
        }

        public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<int, StudentSubjectGradeViewModel>>> GetGradesForStudentsAsync(
            IReadOnlyCollection<string> studentIds,
            IReadOnlyCollection<int> subjectIds,
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<string, IReadOnlyDictionary<int, StudentSubjectGradeViewModel>>(
                StringComparer.Ordinal);

            if (studentIds.Count == 0)
            {
                return result;
            }

            var ids = studentIds.Distinct(StringComparer.Ordinal).ToList();
            var subjectIdList = subjectIds.Distinct().ToList();

            var subjects = subjectIdList.Count == 0
                ? new List<(int Id, string Name)>()
                : (await _db.Subjects
                    .AsNoTracking()
                    .Where(s => subjectIdList.Contains(s.Id))
                    .Select(s => new { s.Id, s.Name })
                    .ToListAsync(cancellationToken))
                    .Select(s => (s.Id, s.Name))
                    .ToList();

            var manualGrades = subjectIdList.Count == 0
                ? new List<SubjectGrade>()
                : await _db.SubjectGrades
                    .AsNoTracking()
                    .Where(g => ids.Contains(g.StudentId) && subjectIdList.Contains(g.SubjectId))
                    .ToListAsync(cancellationToken);

            var manualLookup = manualGrades
                .GroupBy(g => (g.StudentId, g.SubjectId))
                .ToDictionary(g => g.Key, g => g.First());

            // Tasdiqlangan imtihon natijalari. Har bir imtihon uchun eng yaxshi
            // urinish olinadi, so'ng fan bo'yicha imtihonlar o'rtachasi hisoblanadi.
            var examAggregates = new Dictionary<(string StudentId, int SubjectId), (double Average, int Count)>();

            if (subjectIdList.Count > 0)
            {
                var rows = await _db.ExamResults
                    .AsNoTracking()
                    .Where(r => r.IsApproved
                                && ids.Contains(r.StudentId)
                                && r.Exam != null
                                && subjectIdList.Contains(r.Exam.SubjectId))
                    .Select(r => new
                    {
                        r.StudentId,
                        r.ExamId,
                        r.Exam!.SubjectId,
                        r.TotalQuestions,
                        r.CorrectAnswers
                    })
                    .ToListAsync(cancellationToken);

                examAggregates = rows
                    .Select(r => new
                    {
                        r.StudentId,
                        r.ExamId,
                        r.SubjectId,
                        Percentage = r.TotalQuestions > 0
                            ? Math.Clamp(100.0 * r.CorrectAnswers / r.TotalQuestions, 0, 100)
                            : 0
                    })
                    .GroupBy(r => (r.StudentId, r.ExamId))
                    .Select(g => new
                    {
                        g.Key.StudentId,
                        g.Key.ExamId,
                        SubjectId = g.First().SubjectId,
                        Percentage = g.Max(x => x.Percentage)
                    })
                    .GroupBy(x => (x.StudentId, x.SubjectId))
                    .ToDictionary(
                        g => g.Key,
                        g => (
                            Average: Math.Round(g.Average(x => x.Percentage), 1, MidpointRounding.AwayFromZero),
                            Count: g.Count()));
            }

            foreach (var studentId in ids)
            {
                var perSubject = new Dictionary<int, StudentSubjectGradeViewModel>();

                foreach (var subject in subjects)
                {
                    manualLookup.TryGetValue((studentId, subject.Id), out var manual);
                    examAggregates.TryGetValue((studentId, subject.Id), out var exam);

                    perSubject[subject.Id] = new StudentSubjectGradeViewModel
                    {
                        SubjectId = subject.Id,
                        SubjectName = subject.Name,
                        ManualScore = manual?.Score,
                        UpdatedDate = manual?.UpdatedDate,
                        ExamAverage = exam.Count > 0 ? exam.Average : null,
                        ExamCount = exam.Count
                    };
                }

                result[studentId] = perSubject;
            }

            return result;
        }

        public async Task SaveManualGradeAsync(
            string studentId,
            int subjectId,
            double score,
            string? teacherId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(studentId))
            {
                throw new ArgumentException("O'quvchi aniqlanmadi.", nameof(studentId));
            }

            if (score < 0 || score > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(score), "Ball 0 dan 100 gacha bo'lishi kerak.");
            }

            var normalized = Math.Round(score, 1, MidpointRounding.AwayFromZero);

            var existing = await _db.SubjectGrades
                .FirstOrDefaultAsync(
                    g => g.StudentId == studentId && g.SubjectId == subjectId,
                    cancellationToken);

            if (existing is null)
            {
                _db.SubjectGrades.Add(new SubjectGrade
                {
                    StudentId = studentId,
                    SubjectId = subjectId,
                    Score = normalized,
                    UpdatedDate = _clock.Now,
                    TeacherId = teacherId
                });
            }
            else
            {
                existing.Score = normalized;
                existing.UpdatedDate = _clock.Now;
                existing.TeacherId = teacherId;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
