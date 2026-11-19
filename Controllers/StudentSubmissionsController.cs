using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentSubmissionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileUploadService _fileUploadService;
        private readonly IAppClock _appClock;
        private readonly IStudentSubjectGroupService _subjectGroups;

        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        public StudentSubmissionsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IFileUploadService fileUploadService,
            IAppClock appClock,
            IStudentSubjectGroupService subjectGroups)
        {
            _context = context;
            _userManager = userManager;
            _fileUploadService = fileUploadService;
            _appClock = appClock;
            _subjectGroups = subjectGroups;
        }

        // Talabaning barcha topshiriqlari (o'z guruhi fanlari bo'yicha).
        // status: all | pending | done | overdue | graded
        // Sahifa keshlanmasin: o'qituvchi muddatni o'zgartirsa, o'quvchi brauzer
        // "orqaga" tugmasi bilan eski "muddati o'tgan" holatini ko'rmasin.
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index(string? status)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return View(new List<Assignment>());
            }

            // O'quvchi o'qiydigan fanlar (fan bo'yicha guruhlari bilan).
            var mySubjectIds = await _subjectGroups.GetSubjectIdsAsync(user.Id);
            if (mySubjectIds.Count == 0)
            {
                return View(new List<Assignment>());
            }

            var groupMap = await _subjectGroups.GetGroupMapAsync(user.Id, mySubjectIds);

            var assignments = await _context.Assignments
                .Include(a => a.Subject)
                .Include(a => a.Submissions.Where(s => s.StudentId == user.Id))
                .Where(a => mySubjectIds.Contains(a.SubjectId))
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            assignments = assignments
                .Where(a => a.GroupId == null
                            || (groupMap.TryGetValue(a.SubjectId, out var groupId) && a.GroupId == groupId))
                .ToList();

            var statusFilter = string.IsNullOrWhiteSpace(status) ? "all" : status;
            var now = _appClock.Now;
            assignments = statusFilter switch
            {
                "pending" => assignments.Where(a => !a.Submissions.Any() && a.DueDate >= now).ToList(),
                "done" => assignments.Where(a => a.Submissions.Any()).ToList(),
                "overdue" => assignments.Where(a => !a.Submissions.Any() && a.DueDate < now).ToList(),
                "graded" => assignments.Where(a => a.Submissions.Any(s => s.Grade != null)).ToList(),
                _ => assignments,
            };

            ViewBag.Status = statusFilter;
            return View(assignments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int assignmentId, IFormFile? file, string? textAnswer, string? note)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            // Topshiriq haqiqatan ham mavjudligini va o'quvchining shu fandagi
            // guruhiga tegishli ekanini tekshiramiz.
            var assignment = await _context.Assignments
                .FirstOrDefaultAsync(a => a.Id == assignmentId);
            if (assignment == null)
            {
                TempData["Error"] = "Topshiriq topilmadi.";
                return RedirectToAction(nameof(Index));
            }

            var resolvedGroupId = await _subjectGroups.GetGroupIdAsync(user.Id, assignment.SubjectId);
            var canSubmit = resolvedGroupId is int groupId
                && await _subjectGroups.IsStudentInGroupSubjectAsync(user.Id, groupId, assignment.SubjectId)
                && (assignment.GroupId == null || assignment.GroupId == groupId);

            if (!canSubmit)
            {
                TempData["Error"] = "Bu topshiriq sizning guruhingizga tegishli emas.";
                return RedirectToAction(nameof(Index));
            }

            var hasFile = file != null && file.Length > 0;
            var answerText = string.IsNullOrWhiteSpace(textAnswer) ? null : textAnswer.Trim();

            if (!hasFile && answerText == null)
            {
                TempData["Error"] = assignment.SubmissionMode == SubmissionMode.TextOnly
                    ? "Javobni matn bilan yozing."
                    : "Fayl yuklang yoki javobni matn bilan yozing.";
                return RedirectToAction(nameof(Index));
            }

            // Topshiriqda tanlangan javob turiga rioya qilinadi.
            if (hasFile && !AssignmentUi.AllowsFile(assignment.SubmissionMode))
            {
                TempData["Error"] = "Bu topshiriq uchun faqat matnli javob qabul qilinadi.";
                return RedirectToAction(nameof(Index));
            }
            if (answerText != null && !AssignmentUi.AllowsText(assignment.SubmissionMode))
            {
                TempData["Error"] = "Bu topshiriq uchun faqat fayl yuklash mumkin.";
                return RedirectToAction(nameof(Index));
            }
            if (answerText != null && answerText.Length > 4000)
            {
                TempData["Error"] = "Matnli javob 4000 belgidan oshmasligi kerak.";
                return RedirectToAction(nameof(Index));
            }

            var now = _appClock.Now;
            var isLate = assignment.DueDate < now;
            if (isLate && !assignment.AllowLate)
            {
                TempData["Error"] = $"Topshirish muddati tugagan ({assignment.DueDate:dd.MM.yyyy HH:mm}). " +
                    "Kechikkan ishni yuklash uchun o'qituvchi bilan bog'laning.";
                return RedirectToAction(nameof(Index));
            }

            FileUploadResult? uploadResult = null;
            if (hasFile)
            {
                uploadResult = await _fileUploadService.UploadAsync(file, MaxFileSizeBytes);
                if (!uploadResult.Success)
                {
                    TempData["Error"] = uploadResult.ErrorMessage;
                    return RedirectToAction(nameof(Index));
                }
            }

            var existing = await _context.Submissions
                .FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == user.Id);

            string? previousFilePath = null;
            if (existing != null)
            {
                previousFilePath = existing.FilePath;
                if (uploadResult is { Success: true })
                {
                    existing.FilePath = uploadResult.RelativePath;
                    existing.FileName = uploadResult.OriginalFileName;
                }
                existing.TextAnswer = answerText;
                existing.SubmittedDate = now;
                existing.IsLate = isLate;
                existing.StudentNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
                // Qayta yuklanganda eski baho/izoh amal qilmaydi — o'qituvchi qaytadan baholaydi.
                existing.Grade = null;
                existing.GradedDate = null;
            }
            else
            {
                _context.Submissions.Add(new Submission
                {
                    AssignmentId = assignmentId,
                    StudentId = user.Id,
                    FilePath = uploadResult?.RelativePath,
                    FileName = uploadResult?.OriginalFileName,
                    TextAnswer = answerText,
                    SubmittedDate = now,
                    IsLate = isLate,
                    StudentNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                });
            }

            await _context.SaveChangesAsync();

            // Eski fayl endi bazada ishlatilmaydi — diskda qoldirib yubormaymiz.
            if (uploadResult is { Success: true }
                && !string.IsNullOrWhiteSpace(previousFilePath)
                && !string.Equals(previousFilePath, uploadResult.RelativePath, StringComparison.OrdinalIgnoreCase))
            {
                await _fileUploadService.DeleteAsync(previousFilePath);
            }

            TempData["Success"] = isLate
                ? "Ish kechikkan sifatida qabul qilindi."
                : "Topshiriq muvaffaqiyatli yuklandi.";
            return RedirectToAction(nameof(Index));
        }
    }
}
