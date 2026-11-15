using EduTrack.Data;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    /// <summary>
    /// Dublikat fanlarni birlashtirish. Jadval importi paytida har bir
    /// o'qituvchi uchun alohida fan yaratilgandi (2× Matematika, 3× Ingliz tili...).
    /// Bu servis barcha bog'liq yozuvlarni (jadval, guruh-fan, topshiriq, imtihon,
    /// savollar, davomat, baholash) asosiy fanga ko'chiradi va dublikatni o'chiradi.
    ///
    /// Guruh-fan (GroupSubject) biriktirmalarida birlashma guruh bo'lsa, dublikat
    /// fanning o'qituvchisi asosiy fanning shu guruhdagi override'i
    /// (GroupSubject.TeacherId) sifatida saqlanadi — shu tufayli bitta fanda
    /// bir nechta o'qituvchi ishlay oladi.
    /// </summary>
    public interface ISubjectMergeService
    {
        /// <summary>Birlashtirish oldidan nima ko'chishini ko'rsatadi (tasdiqlash sahifasi uchun).</summary>
        Task<SubjectMergeSummary?> GetSummaryAsync(int sourceId, int targetId);

        /// <summary>source fanni target fanga birlashtiradi (source o'chadi).</summary>
        Task<SubjectMergeResult> MergeAsync(int sourceId, int targetId);
    }

    public sealed class SubjectMergeSummary
    {
        public int SourceId { get; set; }
        public int TargetId { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public string TargetName { get; set; } = string.Empty;
        public string? SourceTeacherName { get; set; }
        public string? TargetTeacherName { get; set; }

        public int Schedules { get; set; }
        public int GroupLinks { get; set; }
        public int DuplicateGroupLinks { get; set; }
        public int StudentOverrides { get; set; }
        public int DuplicateStudentOverrides { get; set; }
        public int Assignments { get; set; }
        public int Exams { get; set; }
        public int QuestionBanks { get; set; }
        public int Questions { get; set; }
        public int Attendances { get; set; }
        public int AttendanceConflicts { get; set; }
        public int GradeComponents { get; set; }
        public int SubjectGrades { get; set; }

        public bool HasAnything =>
            Schedules + GroupLinks + StudentOverrides + Assignments + Exams +
            QuestionBanks + Questions + Attendances + GradeComponents + SubjectGrades > 0;
    }

    public sealed class SubjectMergeResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public int MergedSubjectId { get; set; }
    }

    public sealed class SubjectMergeService : ISubjectMergeService
    {
        private readonly ApplicationDbContext _context;

        public SubjectMergeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<SubjectMergeSummary?> GetSummaryAsync(int sourceId, int targetId)
        {
            if (sourceId == targetId) return null;

            var subjects = await _context.Subjects
                .AsNoTracking()
                .Where(s => s.Id == sourceId || s.Id == targetId)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    TeacherName = s.Teacher != null
                        ? (s.Teacher.FullName ?? s.Teacher.Email)
                        : null
                })
                .ToListAsync();

            var source = subjects.FirstOrDefault(s => s.Id == sourceId);
            var target = subjects.FirstOrDefault(s => s.Id == targetId);
            if (source == null || target == null) return null;

            var assignmentIds = await _context.Assignments
                .Where(a => a.SubjectId == sourceId)
                .Select(a => a.Id)
                .ToListAsync();

            var examIds = await _context.Exams
                .Where(e => e.SubjectId == sourceId)
                .Select(e => e.Id)
                .ToListAsync();

            var componentIds = await _context.GradeComponents
                .Where(c => c.SubjectId == sourceId)
                .Select(c => c.Id)
                .ToListAsync();

            return new SubjectMergeSummary
            {
                SourceId = source.Id,
                TargetId = target.Id,
                SourceName = source.Name,
                TargetName = target.Name,
                SourceTeacherName = source.TeacherName,
                TargetTeacherName = target.TeacherName,
                Schedules = await _context.Schedules.CountAsync(x => x.SubjectId == sourceId),
                GroupLinks = await _context.GroupSubjects.CountAsync(x => x.SubjectId == sourceId),
                DuplicateGroupLinks = await CountDuplicateGroupLinksAsync(sourceId, targetId),
                StudentOverrides = await _context.StudentGroupSubjects.CountAsync(x => x.SubjectId == sourceId),
                DuplicateStudentOverrides = await CountDuplicateStudentOverridesAsync(sourceId, targetId),
                Assignments = assignmentIds.Count,
                Exams = examIds.Count,
                QuestionBanks = await _context.QuestionBanks.CountAsync(b => b.SubjectId == sourceId),
                Questions = await _context.Questions.CountAsync(q => q.SubjectId == sourceId),
                Attendances = await _context.Attendances.CountAsync(a => a.SubjectId == sourceId),
                AttendanceConflicts = await CountAttendanceConflictsAsync(sourceId, targetId),
                GradeComponents = componentIds.Count,
                SubjectGrades = await _context.SubjectGrades.CountAsync(g => g.SubjectId == sourceId)
            };
        }

        public async Task<SubjectMergeResult> MergeAsync(int sourceId, int targetId)
        {
            if (sourceId == targetId)
            {
                return new SubjectMergeResult { Error = "Fan o'ziga birlashtirilishi mumkin emas." };
            }

            var exists = await _context.Subjects
                .Where(s => s.Id == sourceId || s.Id == targetId)
                .Select(s => s.Id)
                .ToListAsync();
            if (exists.Count != 2)
            {
                return new SubjectMergeResult { Error = "Fanlardan biri topilmadi." };
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1) Guruh-fan biriktirmalar: birlashma guruhlarda o'qituvchi
                //    override'i asosiy fanga ko'chiriladi, qolganlari SubjectId
                //    almashtiriladi. Dublikat fanning o'qituvchisi shu orqali
                //    "shu guruhni o'qituvchi" sifatida saqlanadi.
                var sourceLinks = await _context.GroupSubjects
                    .Where(gs => gs.SubjectId == sourceId)
                    .Select(gs => new { gs.Id, gs.GroupId, gs.TeacherId })
                    .ToListAsync();

                var targetLinkTeachers = await _context.GroupSubjects
                    .Where(gs => gs.SubjectId == targetId)
                    .Select(gs => new { gs.GroupId, gs.TeacherId })
                    .ToDictionaryAsync(x => x.GroupId, x => x.TeacherId);

                var moveLinkIds = new List<int>();
                var duplicateLinkIds = new List<int>();
                foreach (var link in sourceLinks)
                {
                    if (targetLinkTeachers.TryGetValue(link.GroupId, out var targetTeacher))
                    {
                        // Birlashma guruh: o'qituvchi override'i bo'sh bo'lsa, dublikat
                        // fanning o'qituvchisini o'zlashtiramiz (ko'p o'qituvchi modeli).
                        if (targetTeacher == null && link.TeacherId != null)
                        {
                            await _context.GroupSubjects
                                .Where(gs => gs.SubjectId == targetId && gs.GroupId == link.GroupId)
                                .ExecuteUpdateAsync(u => u.SetProperty(gs => gs.TeacherId, link.TeacherId));
                        }

                        duplicateLinkIds.Add(link.Id);
                    }
                    else
                    {
                        moveLinkIds.Add(link.Id);
                    }
                }

                if (moveLinkIds.Count > 0)
                {
                    await _context.GroupSubjects
                        .Where(gs => moveLinkIds.Contains(gs.Id))
                        .ExecuteUpdateAsync(u => u.SetProperty(gs => gs.SubjectId, targetId));
                }

                if (duplicateLinkIds.Count > 0)
                {
                    await _context.GroupSubjects
                        .Where(gs => duplicateLinkIds.Contains(gs.Id))
                        .ExecuteDeleteAsync();
                }

                // 2) O'quvchi fan-guruh override'lari: (StudentId, SubjectId) unique
                //    faqat AKTIV yozuvlarda (filtered indeks) — shuning uchun
                //    konflikt faqat ikkala fanda ham aktiv yozuvi bor o'quvchida.
                //    Bunday yozuv o'chiriladi (asosiy fan yozuvi saqlanadi), qolganlari
                //    (shu jumladan arxiv yozuvlar) ko'chiriladi.
                var sourceOverrideStudents = await _context.StudentGroupSubjects
                    .Where(x => x.SubjectId == sourceId)
                    .Select(x => new { x.StudentId, x.IsActive })
                    .ToListAsync();

                var targetActiveOverrideStudentIds = (await _context.StudentGroupSubjects
                        .Where(x => x.SubjectId == targetId && x.IsActive
                                    && sourceOverrideStudents.Select(s => s.StudentId).Contains(x.StudentId))
                        .Select(x => x.StudentId)
                        .ToListAsync())
                    .ToHashSet();

                await _context.StudentGroupSubjects
                    .Where(x => x.SubjectId == sourceId
                                && x.IsActive
                                && targetActiveOverrideStudentIds.Contains(x.StudentId))
                    .ExecuteDeleteAsync();

                await _context.StudentGroupSubjects
                    .Where(x => x.SubjectId == sourceId)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.SubjectId, targetId));

                // 3) Davomat: (Sana, Talaba, Fan, Guruh) unique — birlashma
                //    yozuvlar o'chiriladi, qolganlari ko'chiriladi.
                var sourceAttendanceRows = await _context.Attendances
                    .Where(a => a.SubjectId == sourceId)
                    .Select(a => new { a.Id, a.Date, a.StudentId, a.GroupId })
                    .ToListAsync();

                var targetAttendanceKeys = (await _context.Attendances
                        .Where(a => a.SubjectId == targetId)
                        .Select(a => new { a.Date, a.StudentId, a.GroupId })
                        .ToListAsync())
                    .Select(a => (a.Date, a.StudentId, a.GroupId))
                    .ToHashSet();

                var conflictAttendanceIds = new List<int>();
                var moveAttendanceIds = new List<int>();
                foreach (var row in sourceAttendanceRows)
                {
                    if (targetAttendanceKeys.Contains((row.Date, row.StudentId, row.GroupId)))
                    {
                        conflictAttendanceIds.Add(row.Id);
                    }
                    else
                    {
                        moveAttendanceIds.Add(row.Id);
                    }
                }

                if (conflictAttendanceIds.Count > 0)
                {
                    await _context.Attendances
                        .Where(a => conflictAttendanceIds.Contains(a.Id))
                        .ExecuteDeleteAsync();
                }

                if (moveAttendanceIds.Count > 0)
                {
                    await _context.Attendances
                        .Where(a => moveAttendanceIds.Contains(a.Id))
                        .ExecuteUpdateAsync(u => u.SetProperty(a => a.SubjectId, targetId));
                }

                // 4) Qolgan yozuvlar oddiy FK ko'chirish — imtihonlar, natijalari,
                //    topshiriqlar, javoblar, savollar, banklar, jadval, baholash
                //    bilan birga ko'chadi.
                await _context.Schedules
                    .Where(x => x.SubjectId == sourceId)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.SubjectId, targetId));

                await _context.Assignments
                    .Where(x => x.SubjectId == sourceId)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.SubjectId, targetId));

                await _context.QuestionBanks
                    .Where(x => x.SubjectId == sourceId)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.SubjectId, targetId));

                await _context.Questions
                    .Where(x => x.SubjectId == sourceId)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.SubjectId, targetId));

                await _context.GradeComponents
                    .Where(x => x.SubjectId == sourceId)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.SubjectId, targetId));

                await _context.SubjectGrades
                    .Where(x => x.SubjectId == sourceId)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.SubjectId, targetId));

                // 5) Fan o'zi o'chadi (barcha bolalari allaqachon ko'chirilgan).
                var deleted = await _context.Subjects
                    .Where(s => s.Id == sourceId)
                    .ExecuteDeleteAsync();

                if (deleted == 0)
                {
                    await transaction.RollbackAsync();
                    return new SubjectMergeResult { Error = "Fan topilmadi." };
                }

                await transaction.CommitAsync();

                return new SubjectMergeResult { Success = true, MergedSubjectId = targetId };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new SubjectMergeResult
                {
                    Error = "Fanni birlashtirib bo'lmadi: " + ex.Message
                };
            }
        }

        private async Task<int> CountDuplicateGroupLinksAsync(int sourceId, int targetId)
        {
            var sourceGroupIds = await _context.GroupSubjects
                .Where(gs => gs.SubjectId == sourceId)
                .Select(gs => gs.GroupId)
                .ToListAsync();

            if (sourceGroupIds.Count == 0) return 0;

            var targetGroupIds = (await _context.GroupSubjects
                    .Where(gs => gs.SubjectId == targetId && sourceGroupIds.Contains(gs.GroupId))
                    .Select(gs => gs.GroupId)
                    .ToListAsync())
                .ToHashSet();

            return sourceGroupIds.Count(targetGroupIds.Contains);
        }

        private async Task<int> CountDuplicateStudentOverridesAsync(int sourceId, int targetId)
        {
            // Konflikt faqat aktiv yozuvlar orasida (filtered unique indeks).
            var sourceStudentIds = await _context.StudentGroupSubjects
                .Where(x => x.SubjectId == sourceId && x.IsActive)
                .Select(x => x.StudentId)
                .ToListAsync();

            if (sourceStudentIds.Count == 0) return 0;

            var targetStudentIds = (await _context.StudentGroupSubjects
                    .Where(x => x.SubjectId == targetId && x.IsActive && sourceStudentIds.Contains(x.StudentId))
                    .Select(x => x.StudentId)
                    .ToListAsync())
                .ToHashSet();

            return sourceStudentIds.Count(targetStudentIds.Contains);
        }

        private async Task<int> CountAttendanceConflictsAsync(int sourceId, int targetId)
        {
            var sourceRows = await _context.Attendances
                .Where(a => a.SubjectId == sourceId)
                .Select(a => new { a.Date, a.StudentId, a.GroupId })
                .ToListAsync();

            if (sourceRows.Count == 0) return 0;

            var targetKeys = (await _context.Attendances
                    .Where(a => a.SubjectId == targetId)
                    .Select(a => new { a.Date, a.StudentId, a.GroupId })
                    .ToListAsync())
                .Select(a => (a.Date, a.StudentId, a.GroupId))
                .ToHashSet();

            return sourceRows.Count(r => targetKeys.Contains((r.Date, r.StudentId, r.GroupId)));
        }
    }
}
