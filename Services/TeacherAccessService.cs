using EduTrack.Data;
using EduTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
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
                .Where(s => s.TeacherId == teacherId)
                .ToListAsync();
        }

        public async Task<bool> OwnsSubjectAsync(string teacherId, int subjectId)
        {
            return await _context.Subjects
                .AnyAsync(s => s.Id == subjectId && s.TeacherId == teacherId);
        }

        public async Task<Subject?> GetOwnedSubjectAsync(string teacherId, int subjectId)
        {
            return await _context.Subjects
                .FirstOrDefaultAsync(s => s.Id == subjectId && s.TeacherId == teacherId);
        }
    }
}
