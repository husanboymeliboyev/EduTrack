using EduTrack.Data;
using EduTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    public class GradeSyncService : IGradeSyncService
    {
        private readonly ApplicationDbContext _context;

        public GradeSyncService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SyncFromSubmissionAsync(int submissionId)
        {
            var submission = await _context.Submissions
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null || submission.Grade == null) return;

            var component = await _context.GradeComponents
                .FirstOrDefaultAsync(c => c.AssignmentId == submission.AssignmentId);

            if (component == null) return;

            var score = Math.Round(submission.Grade.Value / 100.0 * component.MaxScore, 1);
            await UpsertStudentGradeAsync(component.Id, submission.StudentId, score);
        }

        public async Task SyncFromExamResultAsync(int examResultId)
        {
            var result = await _context.ExamResults
                .FirstOrDefaultAsync(r => r.Id == examResultId);

            if (result == null || result.TotalQuestions == 0) return;

            var component = await _context.GradeComponents
                .FirstOrDefaultAsync(c => c.ExamId == result.ExamId);

            if (component == null) return;

            var percent = (double)result.CorrectAnswers / result.TotalQuestions;
            var score = Math.Round(percent * component.MaxScore, 1);
            await UpsertStudentGradeAsync(component.Id, result.StudentId, score);
        }

        public async Task SyncAllForAssignmentAsync(int assignmentId)
        {
            var submissionIds = await _context.Submissions
                .Where(s => s.AssignmentId == assignmentId && s.Grade != null)
                .Select(s => s.Id)
                .ToListAsync();

            foreach (var id in submissionIds)
                await SyncFromSubmissionAsync(id);
        }

        public async Task SyncAllForExamAsync(int examId)
        {
            var resultIds = await _context.ExamResults
                .Where(r => r.ExamId == examId)
                .Select(r => r.Id)
                .ToListAsync();

            foreach (var id in resultIds)
                await SyncFromExamResultAsync(id);
        }

        private async Task UpsertStudentGradeAsync(int componentId, string studentId, double score)
        {
            var existing = await _context.StudentGrades
                .FirstOrDefaultAsync(g => g.GradeComponentId == componentId && g.StudentId == studentId);

            if (existing != null)
            {
                existing.Score = score;
                existing.UpdatedDate = DateTime.Now;
            }
            else
            {
                _context.StudentGrades.Add(new StudentGrade
                {
                    GradeComponentId = componentId,
                    StudentId = studentId,
                    Score = score,
                    UpdatedDate = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
        }
    }
}