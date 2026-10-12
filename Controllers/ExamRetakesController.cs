using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    // O'qituvchi uchun: chetlatilgan / qatnashmagan / vaqti tugagan talabaga
    // imtihonning 2-urinishini ochib berish.
    [Authorize(Roles = "Teacher")]
    public class ExamRetakesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ExamRetakesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Faqat shu o'qituvchining fani imtihonlari — boshqasiniki bo'lsa null (404).
        private async Task<Exam?> GetOwnedExamAsync(int examId)
        {
            var teacherId = _userManager.GetUserId(User);
            return await _context.Exams
                .Include(e => e.Subject)
                .FirstOrDefaultAsync(e => e.Id == examId && e.Subject!.TeacherId == teacherId);
        }

        // Imtihon qaysi guruhlar talabalariga tegishli (StudentExams.Take dagi qoida bilan bir xil).
        private async Task<List<int>> GetEligibleGroupIdsAsync(Exam exam)
        {
            var ids = await _context.GroupSubjects
                .Where(gs => gs.SubjectId == exam.SubjectId)
                .Select(gs => gs.GroupId)
                .ToListAsync();

            if (exam.GroupId.HasValue)
            {
                ids = ids.Where(g => g == exam.GroupId.Value).ToList();
            }
            return ids;
        }

        private static bool IsExpired(ExamAttempt attempt, Exam exam)
            => (DateTime.Now - attempt.StartedDate).TotalSeconds > exam.DurationMinutes * 60 + 120;

        public async Task<IActionResult> Index(int id)
        {
            var exam = await GetOwnedExamAsync(id);
            if (exam == null) return NotFound();

            var groupIds = await GetEligibleGroupIdsAsync(exam);

            var students = await _context.Users
                .AsNoTracking()
                .Include(u => u.Group)
                .Where(u => u.GroupId != null && groupIds.Contains(u.GroupId.Value))
                .OrderBy(u => u.Group!.Name)
                .ThenBy(u => u.FullName)
                .ToListAsync();

            var resultStudentIds = (await _context.ExamResults
                .AsNoTracking()
                .Where(r => r.ExamId == id)
                .Select(r => r.StudentId)
                .ToListAsync()).ToHashSet();

            var attempts = await _context.ExamAttempts
                .AsNoTracking()
                .Where(a => a.ExamId == id)
                .ToDictionaryAsync(a => a.StudentId);

            var grants = await _context.ExamRetakeGrants
                .AsNoTracking()
                .Where(g => g.ExamId == id)
                .ToListAsync();

            var vm = new ExamRetakeIndexViewModel { ExamId = exam.Id, ExamTitle = exam.Title };

            foreach (var s in students)
            {
                var row = new ExamRetakeRowViewModel
                {
                    StudentId = s.Id,
                    FullName = string.IsNullOrWhiteSpace(s.FullName) ? s.LoginId : s.FullName!,
                    GroupName = s.Group?.Name,
                    UsedRetakeCount = grants.Count(g => g.StudentId == s.Id && g.IsUsed)
                };

                var pending = grants.FirstOrDefault(g => g.StudentId == s.Id && !g.IsUsed);
                attempts.TryGetValue(s.Id, out var attempt);

                if (resultStudentIds.Contains(s.Id))
                {
                    row.StatusText = row.UsedRetakeCount > 0 ? "Topshirgan (2-urinishda)" : "Topshirgan";
                    row.BadgeClass = "bg-success";
                }
                else if (pending != null)
                {
                    row.StatusText = "2-urinish berilgan (kutilmoqda)";
                    row.BadgeClass = "bg-info text-dark";
                    row.PendingGrantId = pending.Id;
                    row.PendingReason = pending.Reason;
                }
                else if (attempt != null && attempt.IsForceFinished)
                {
                    row.StatusText = "Chetlatilgan";
                    row.BadgeClass = "bg-danger";
                    row.CanGrant = true;
                }
                else if (attempt != null && IsExpired(attempt, exam))
                {
                    row.StatusText = "Vaqti tugagan, yakunlamagan";
                    row.BadgeClass = "bg-warning text-dark";
                    row.CanGrant = true;
                }
                else if (attempt != null)
                {
                    row.StatusText = "Hozir topshiryapti";
                    row.BadgeClass = "bg-primary";
                }
                else
                {
                    row.StatusText = "Qatnashmagan";
                    row.BadgeClass = "bg-secondary";
                    row.CanGrant = true;
                }

                vm.Rows.Add(row);
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Grant(int examId, string studentId, string? reason)
        {
            var exam = await GetOwnedExamAsync(examId);
            if (exam == null) return NotFound();

            var groupIds = await GetEligibleGroupIdsAsync(exam);
            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == studentId);
            if (student == null || student.GroupId == null || !groupIds.Contains(student.GroupId.Value))
            {
                TempData["Error"] = "Talaba topilmadi yoki bu imtihon unga tegishli emas.";
                return RedirectToAction(nameof(Index), new { id = examId });
            }

            if (await _context.ExamResults.AnyAsync(r => r.ExamId == examId && r.StudentId == studentId))
            {
                TempData["Error"] = "Bu talaba imtihonni allaqachon topshirgan.";
                return RedirectToAction(nameof(Index), new { id = examId });
            }

            if (await _context.ExamRetakeGrants.AnyAsync(g => g.ExamId == examId && g.StudentId == studentId && !g.IsUsed))
            {
                TempData["Error"] = "Bu talabaga allaqachon qo'shimcha urinish berilgan.";
                return RedirectToAction(nameof(Index), new { id = examId });
            }

            var attempt = await _context.ExamAttempts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ExamId == examId && a.StudentId == studentId);

            if (attempt != null && !attempt.IsForceFinished && !IsExpired(attempt, exam))
            {
                TempData["Error"] = "Talaba hozir imtihon topshiryapti — urinish berib bo'lmaydi.";
                return RedirectToAction(nameof(Index), new { id = examId });
            }

            var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            if (trimmed != null && trimmed.Length > 300) trimmed = trimmed.Substring(0, 300);

            _context.ExamRetakeGrants.Add(new ExamRetakeGrant
            {
                ExamId = examId,
                StudentId = studentId,
                GrantedByTeacherId = _userManager.GetUserId(User)!,
                GrantedAtUtc = DateTime.UtcNow,
                Reason = trimmed
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "2-urinish ochildi. Talaba imtihonlar ro'yxatidan qayta boshlay oladi.";
            return RedirectToAction(nameof(Index), new { id = examId });
        }

        // Hali ishlatilmagan ruxsatni bekor qilish.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revoke(int grantId)
        {
            var teacherId = _userManager.GetUserId(User);
            var grant = await _context.ExamRetakeGrants
                .Include(g => g.Exam).ThenInclude(e => e!.Subject)
                .FirstOrDefaultAsync(g => g.Id == grantId);

            if (grant == null || grant.Exam?.Subject?.TeacherId != teacherId) return NotFound();

            if (grant.IsUsed)
            {
                TempData["Error"] = "Bu ruxsat allaqachon ishlatilgan, bekor qilib bo'lmaydi.";
                return RedirectToAction(nameof(Index), new { id = grant.ExamId });
            }

            var examId = grant.ExamId;
            _context.ExamRetakeGrants.Remove(grant);
            await _context.SaveChangesAsync();

            TempData["Success"] = "2-urinish ruxsati bekor qilindi.";
            return RedirectToAction(nameof(Index), new { id = examId });
        }
    }
}
