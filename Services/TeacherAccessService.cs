using EduTrack.Data;
using EduTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    /// <summary>
    /// O'qituvchi ruxsatlari. Tizim bitta fanga bir nechta o'qituvchini qo'llaydi:
    /// <list type="bullet">
    /// <item><see cref="Models.Subject.TeacherId"/> — fan EGASI (asosiy o'qituvchi):
    /// savol banklari, topshiriqlar kabi fan darajasidagi resurslarga egalik.</item>
    /// <item><see cref="Models.GroupSubject.TeacherId"/> — guruh bo'yicha o'qituvchi:
    /// "(fan, guruh)" juftligini kim o'qitadi. null bo'lsa fan egasi bo'ladi.
    /// Shu juftlik orqali ikkala smenada bitta fanga har xil o'qituvchi biriktiriladi
    /// (masalan, Matematika — 1-smenada Turdaliyev, 2-smenada Meliboyev).</item>
    /// </list>
    /// "Efektiv o'qituvchi" juftlik uchun: GroupSubject.TeacherId ?? Subject.TeacherId —
    /// ScheduleService va MonthlyRatingService bilan bir xil qoida.
    /// </summary>
    public class TeacherAccessService : ITeacherAccessService
    {
        private readonly ApplicationDbContext _context;

        public TeacherAccessService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Subject>> GetTeacherSubjectsAsync(string teacherId)
        {
            return await _context.Subjects
                .Where(s => s.TeacherId == teacherId
                            || s.GroupSubjects.Any(gs => gs.TeacherId == teacherId))
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<List<int>> GetOwnedSubjectIdsAsync(string teacherId)
        {
            return await _context.Subjects
                .Where(s => s.TeacherId == teacherId
                            || s.GroupSubjects.Any(gs => gs.TeacherId == teacherId))
                .Select(s => s.Id)
                .ToListAsync();
        }

        public async Task<bool> OwnsSubjectAsync(string teacherId, int subjectId)
        {
            return await _context.Subjects
                .AnyAsync(s => s.Id == subjectId
                               && (s.TeacherId == teacherId
                                   || s.GroupSubjects.Any(gs => gs.TeacherId == teacherId)));
        }

        public async Task<Subject?> GetOwnedSubjectAsync(string teacherId, int subjectId)
        {
            return await _context.Subjects
                .FirstOrDefaultAsync(s => s.Id == subjectId
                                          && (s.TeacherId == teacherId
                                              || s.GroupSubjects.Any(gs => gs.TeacherId == teacherId)));
        }

        public async Task<List<TeacherSubjectPair>> GetOwnedPairsAsync(string teacherId)
        {
            // Efektiv o'qituvchi: guruhdagi override, bo'lmasa fan egasi.
            // Override qilingan juftlik fan egasiga tegishli emas.
            var rows = await _context.GroupSubjects
                .AsNoTracking()
                .Where(gs => (gs.TeacherId ?? gs.Subject!.TeacherId) == teacherId)
                .Select(gs => new { gs.SubjectId, gs.GroupId })
                .ToListAsync();

            return rows
                .Select(r => new TeacherSubjectPair(r.SubjectId, r.GroupId))
                .Distinct()
                .ToList();
        }

        public async Task<List<int>> GetOwnedQuestionBankIdsAsync(string teacherId)
        {
            return await _context.QuestionBanks
                .AsNoTracking()
                .Where(QuestionBankAccess.VisibleTo(teacherId))
                .Select(b => b.Id)
                .ToListAsync();
        }

        public async Task<bool> OwnsQuestionBankAsync(string teacherId, int questionBankId)
        {
            return await _context.QuestionBanks
                .AsNoTracking()
                .Where(QuestionBankAccess.VisibleTo(teacherId))
                .AnyAsync(b => b.Id == questionBankId);
        }

        public async Task<bool> OwnsPairAsync(string teacherId, int subjectId, int? groupId)
        {
            if (!groupId.HasValue)
            {
                return await OwnsSubjectAsync(teacherId, subjectId);
            }

            // Guruh bo'yicha efektiv o'qituvchi: juftlikdagi override, bo'lmasa fan egasi.
            return await _context.GroupSubjects
                .AnyAsync(gs => gs.SubjectId == subjectId
                                && gs.GroupId == groupId.Value
                                && (gs.TeacherId == teacherId
                                    || (gs.TeacherId == null && gs.Subject!.TeacherId == teacherId)));
        }
    }
}
