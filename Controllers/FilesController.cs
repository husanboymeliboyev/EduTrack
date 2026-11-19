using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    /// <summary>
    /// Topshiriq va talaba ishlari fayllari uchun xavfsiz yuklab olish nuqtasi.
    /// Fayllar baza yonidagi "uploads" papkasida GUID nom bilan saqlanadi va to'g'ridan-to'g'ri
    /// statik fayl sifatida ochilmaydi — har bir so'rov uchun ruxsat tekshiriladi:
    ///   * topshiriq fayli: shu fanni o'qitadigan har qanday o'qituvchi, Admin,
    ///     yoki shu fanni o'qiydigan talaba;
    ///   * talaba ishi: o'sha talaba, shu fanni o'qitadigan o'qituvchi yoki Admin.
    /// </summary>
    [Authorize]
    public class FilesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileUploadService _fileUploadService;
        private readonly IStudentSubjectGroupService _subjectGroups;
        private readonly ITeacherAccessService _teacherAccess;

        public FilesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IFileUploadService fileUploadService,
            IStudentSubjectGroupService subjectGroups,
            ITeacherAccessService teacherAccess)
        {
            _context = context;
            _userManager = userManager;
            _fileUploadService = fileUploadService;
            _subjectGroups = subjectGroups;
            _teacherAccess = teacherAccess;
        }

        // Topshiriq fayli (o'qituvchi biriktirgan material).
        public async Task<IActionResult> Assignment(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Forbid();

            var assignment = await _context.Assignments
                .Include(a => a.Subject)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (assignment == null || string.IsNullOrWhiteSpace(assignment.FilePath))
            {
                return NotFound();
            }

            var canDownload = await CanAccessAssignmentAsync(userId, assignment);
            if (!canDownload) return Forbid();

            var physicalPath = _fileUploadService.ResolvePath(assignment.FilePath);
            if (physicalPath == null) return NotFound();

            return PhysicalFile(physicalPath,
                _fileUploadService.GetContentType(assignment.FileName ?? assignment.FilePath),
                assignment.FileName ?? "fayl");
        }

        // Talabaning yuborgan ishi.
        public async Task<IActionResult> Submission(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Forbid();

            var submission = await _context.Submissions
                .Include(s => s.Assignment)
                    .ThenInclude(a => a!.Subject)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (submission == null || string.IsNullOrWhiteSpace(submission.FilePath))
            {
                return NotFound();
            }

            var isOwner = string.Equals(submission.StudentId, userId, StringComparison.Ordinal);
            var isAdmin = User.IsInRole("Admin");

            var isSubjectTeacher = false;
            if (!isOwner && !isAdmin && User.IsInRole("Teacher") && submission.Assignment != null)
            {
                var teacherSubjectIds = await _teacherAccess.GetOwnedSubjectIdsAsync(userId);
                isSubjectTeacher = teacherSubjectIds.Contains(submission.Assignment.SubjectId);
            }

            if (!isOwner && !isSubjectTeacher && !isAdmin) return Forbid();

            var physicalPath = _fileUploadService.ResolvePath(submission.FilePath);
            if (physicalPath == null) return NotFound();

            return PhysicalFile(physicalPath,
                _fileUploadService.GetContentType(submission.FileName ?? submission.FilePath),
                submission.FileName ?? "fayl");
        }

        private async Task<bool> CanAccessAssignmentAsync(string userId, Assignment assignment)
        {
            if (User.IsInRole("Admin")) return true;

            // Fanga bir nechta o'qituvchi biriktirilgan bo'lishi mumkin
            // (Subject.TeacherId faqat bittasi). Shu fanni o'qitadigan har bir
            // o'qituvchi materialni yuklab olishi kerak.
            if (User.IsInRole("Teacher"))
            {
                var teacherSubjectIds = await _teacherAccess.GetOwnedSubjectIdsAsync(userId);
                return teacherSubjectIds.Contains(assignment.SubjectId);
            }

            if (!User.IsInRole("Student"))
            {
                return false;
            }

            // Talaba shu fanni o'qiydimi va (guruh bo'yicha cheklangan bo'lsa) shu guruhdami.
            var subjectIds = await _subjectGroups.GetSubjectIdsAsync(userId);
            if (!subjectIds.Contains(assignment.SubjectId)) return false;

            if (assignment.GroupId == null) return true;

            var groupId = await _subjectGroups.GetGroupIdAsync(userId, assignment.SubjectId);
            return groupId == assignment.GroupId;
        }
    }
}
