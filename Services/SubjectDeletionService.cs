using EduTrack.Data;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    /// <summary>
    /// Fanni o'chirishdan oldin unga bog'langan yozuvlar haqida ma'lumot va
    /// fanni xavfsiz (tranzaksiya ichida) o'chirish.
    ///
    /// Nega alohida servis? <see cref="Models.Subject"/> ga bevosita bog'langan
    /// jadvallar kaskad bilan o'chadi, lekin ularning ostidagi ba'zi jadvallar
    /// RESTRICT bilan bog'langan (imtihon natijalari, savol banklari va baholash
    /// shkalasi). Shuning uchun o'chirish tartibi muhim — aks holda SQLite
    /// "FOREIGN KEY constraint failed" xatosini beradi.
    /// </summary>
    public interface ISubjectDeletionService
    {
        Task<SubjectDeletionSummary?> GetSummaryAsync(int subjectId);
        Task<SubjectDeletionResult> DeleteAsync(int subjectId);
    }

    public sealed class SubjectDeletionSummary
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public string? TeacherName { get; set; }

        public int Schedules { get; set; }
        public int GroupSubjects { get; set; }
        public int StudentOverrides { get; set; }
        public int Assignments { get; set; }
        public int Submissions { get; set; }
        public int Attendances { get; set; }
        public int Exams { get; set; }
        public int ExamResults { get; set; }
        public int GradeComponents { get; set; }
        public int StudentGrades { get; set; }
        public int SubjectGrades { get; set; }
        public int QuestionBanks { get; set; }
        public int Questions { get; set; }

        /// <summary>O'chirishni to'sib qo'yadigan holatlar (bo'sh bo'lsa — xavfsiz).</summary>
        public List<string> Blockers { get; set; } = new();

        public bool HasRelatedData =>
            Schedules + GroupSubjects + StudentOverrides + Assignments + Submissions +
            Attendances + Exams + ExamResults + GradeComponents + StudentGrades +
            SubjectGrades + QuestionBanks + Questions > 0;
    }

    public sealed class SubjectDeletionResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    public sealed class SubjectDeletionService : ISubjectDeletionService
    {
        private readonly ApplicationDbContext _context;

        public SubjectDeletionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<SubjectDeletionSummary?> GetSummaryAsync(int subjectId)
        {
            var subject = await _context.Subjects
                .AsNoTracking()
                .Where(s => s.Id == subjectId)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    TeacherName = s.Teacher != null
                        ? (s.Teacher.FullName ?? s.Teacher.Email)
                        : null
                })
                .FirstOrDefaultAsync();

            if (subject == null)
            {
                return null;
            }

            var groupSubjectIds = await _context.GroupSubjects
                .Where(gs => gs.SubjectId == subjectId)
                .Select(gs => gs.GroupId)
                .ToListAsync();

            var assignmentIds = await _context.Assignments
                .Where(a => a.SubjectId == subjectId)
                .Select(a => a.Id)
                .ToListAsync();

            var examIds = await _context.Exams
                .Where(e => e.SubjectId == subjectId)
                .Select(e => e.Id)
                .ToListAsync();

            var componentIds = await _context.GradeComponents
                .Where(c => c.SubjectId == subjectId)
                .Select(c => c.Id)
                .ToListAsync();

            var bankIds = await _context.QuestionBanks
                .Where(b => b.SubjectId == subjectId)
                .Select(b => b.Id)
                .ToListAsync();

            var summary = new SubjectDeletionSummary
            {
                SubjectId = subject.Id,
                SubjectName = subject.Name,
                TeacherName = subject.TeacherName,
                Schedules = await _context.Schedules.CountAsync(s => s.SubjectId == subjectId),
                GroupSubjects = groupSubjectIds.Count,
                StudentOverrides = await _context.StudentGroupSubjects
                    .CountAsync(x => x.SubjectId == subjectId),
                Assignments = assignmentIds.Count,
                Submissions = assignmentIds.Count == 0
                    ? 0
                    : await _context.Submissions.CountAsync(s => assignmentIds.Contains(s.AssignmentId)),
                Attendances = await _context.Attendances.CountAsync(a => a.SubjectId == subjectId),
                Exams = examIds.Count,
                ExamResults = examIds.Count == 0
                    ? 0
                    : await _context.ExamResults.CountAsync(r => examIds.Contains(r.ExamId)),
                GradeComponents = componentIds.Count,
                StudentGrades = componentIds.Count == 0
                    ? 0
                    : await _context.StudentGrades.CountAsync(g => componentIds.Contains(g.GradeComponentId)),
                SubjectGrades = await _context.SubjectGrades.CountAsync(g => g.SubjectId == subjectId),
                QuestionBanks = bankIds.Count,
                Questions = await _context.Questions
                    .CountAsync(q => q.SubjectId == subjectId
                                     || (bankIds.Contains(q.QuestionBankId))),
            };

            var blockers = summary.Blockers;

            if (bankIds.Count > 0)
            {
                var foreignExamCount = await _context.Exams
                    .CountAsync(e => bankIds.Contains(e.QuestionBankId)
                                     && e.SubjectId != subjectId);

                if (foreignExamCount > 0)
                {
                    blockers.Add($"{foreignExamCount} ta boshqa fan imtihoni shu fanning savol bankidan foydalanmoqda. " +
                                 "Avval o'sha imtihonlardan savol bankini uzib qo'ying.");
                }
            }

            return summary;
        }

        public async Task<SubjectDeletionResult> DeleteAsync(int subjectId)
        {
            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId);
            if (subject == null)
            {
                return new SubjectDeletionResult { Error = "Fan topilmadi." };
            }

            var bankIds = await _context.QuestionBanks
                .Where(b => b.SubjectId == subjectId)
                .Select(b => b.Id)
                .ToListAsync();

            if (bankIds.Count > 0)
            {
                var foreignExamCount = await _context.Exams
                    .CountAsync(e => bankIds.Contains(e.QuestionBankId)
                                     && e.SubjectId != subjectId);

                if (foreignExamCount > 0)
                {
                    return new SubjectDeletionResult
                    {
                        Error = $"{foreignExamCount} ta boshqa fan imtihoni shu fanning savol bankidan foydalanmoqda. " +
                                "O'chirish uchun avval o'sha imtihonlardan savol bankini uzib qo'ying."
                    };
                }
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var examIds = await _context.Exams
                    .Where(e => e.SubjectId == subjectId)
                    .Select(e => e.Id)
                    .ToListAsync();

                if (examIds.Count > 0)
                {
                    // Baholash shkalasi RESTRICT bilan bog'langan: avval bandlar va
                    // versiyalar, keyin shkalalar o'chiriladi.
                    var scaleIds = await _context.GradingScales
                        .Where(s => examIds.Contains(s.ExamId))
                        .Select(s => s.Id)
                        .ToListAsync();

                    if (scaleIds.Count > 0)
                    {
                        var versionIds = await _context.GradingScaleVersions
                            .Where(v => scaleIds.Contains(v.GradingScaleId))
                            .Select(v => v.Id)
                            .ToListAsync();

                        if (versionIds.Count > 0)
                        {
                            await _context.GradingScaleBands
                                .Where(b => versionIds.Contains(b.GradingScaleVersionId))
                                .ExecuteDeleteAsync();
                            await _context.GradingScaleVersions
                                .Where(v => versionIds.Contains(v.Id))
                                .ExecuteDeleteAsync();
                        }

                        await _context.GradingScales
                            .Where(s => scaleIds.Contains(s.Id))
                            .ExecuteDeleteAsync();
                    }

                    // Imtihon natijalari RESTRICT — avval o'chiriladi (tarix ham
                    // shu fan bilan birga o'chiriladi; admin buni tasdiqlagan).
                    await _context.ExamResults
                        .Where(r => examIds.Contains(r.ExamId))
                        .ExecuteDeleteAsync();

                    await _context.Exams
                        .Where(e => examIds.Contains(e.Id))
                        .ExecuteDeleteAsync();
                }

                // Savol banki RESTRICT: avval savollar va variantlar, keyin banklar.
                var questionIds = await _context.Questions
                    .Where(q => q.SubjectId == subjectId
                                || (bankIds.Contains(q.QuestionBankId)))
                    .Select(q => q.Id)
                    .ToListAsync();

                if (questionIds.Count > 0)
                {
                    await _context.AnswerOptions
                        .Where(o => questionIds.Contains(o.QuestionId))
                        .ExecuteDeleteAsync();
                    await _context.Questions
                        .Where(q => questionIds.Contains(q.Id))
                        .ExecuteDeleteAsync();
                }

                if (bankIds.Count > 0)
                {
                    await _context.QuestionBanks
                        .Where(b => bankIds.Contains(b.Id))
                        .ExecuteDeleteAsync();
                }

                // Qolganlari Subject bilan kaskad bo'yicha o'chadi:
                // jadval, guruh-fan, o'quvchi override'i, topshiriq va topshirilgan
                // ishlar, davomat, baholash komponentlari va baholar, yakuniy baholar.
                // ExecuteDelete ishlatiladi — change tracker'dagi eski bog'lanishlar
                // "association severed" xatosini bermasligi uchun.
                var deleted = await _context.Subjects
                    .Where(s => s.Id == subjectId)
                    .ExecuteDeleteAsync();

                if (deleted == 0)
                {
                    await transaction.RollbackAsync();
                    return new SubjectDeletionResult { Error = "Fan topilmadi." };
                }

                await transaction.CommitAsync();
                return new SubjectDeletionResult { Success = true };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new SubjectDeletionResult
                {
                    Error = "Fanni o'chirib bo'lmadi: " + ex.Message
                };
            }
        }
    }
}
