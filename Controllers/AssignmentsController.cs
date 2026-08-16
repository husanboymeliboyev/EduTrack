using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class AssignmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileUploadService _fileUploadService;
        private readonly ITeacherAccessService _teacherAccessService;
        private readonly IGradeSyncService _gradeSyncService;

        private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

        public AssignmentsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IFileUploadService fileUploadService,
            ITeacherAccessService teacherAccessService,
            IGradeSyncService gradeSyncService)
        {
            _context = context;
            _userManager = userManager;
            _fileUploadService = fileUploadService;
            _teacherAccessService = teacherAccessService;
            _gradeSyncService = gradeSyncService;
        }
        public async Task<IActionResult> Index()
        {
            var teacherId = _userManager.GetUserId(User);

            var assignments = await _context.Assignments
                .Include(a => a.Subject)
                .Include(a => a.Group)
                .Include(a => a.Submissions)
                .Where(a => a.Subject!.TeacherId == teacherId)
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            return View(assignments);
        }

        public async Task<IActionResult> Create()
        {
            await LoadFormDataAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Description,DueDate,SubjectId,GroupId")] Assignment assignment, IFormFile? file)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var ownsSubject = await _teacherAccessService.OwnsSubjectAsync(teacherId, assignment.SubjectId);

            if (!ownsSubject)
            {
                ModelState.AddModelError(string.Empty, "Noto'g'ri fan tanlandi.");
            }
            else if (assignment.GroupId.HasValue)
            {
                var validGroup = await _context.GroupSubjects
                    .AnyAsync(gs => gs.SubjectId == assignment.SubjectId && gs.GroupId == assignment.GroupId);
                if (!validGroup)
                {
                    ModelState.AddModelError(string.Empty, "Tanlangan guruh bu fanga tegishli emas.");
                }
            }

            FileUploadResult? uploadResult = null;
            if (file != null && file.Length > 0)
            {
                uploadResult = await _fileUploadService.UploadAsync(file, MaxFileSizeBytes);
                if (!uploadResult.Success)
                {
                    ModelState.AddModelError(string.Empty, uploadResult.ErrorMessage!);
                }
            }

            if (ModelState.IsValid)
            {
                if (uploadResult is { Success: true })
                {
                    assignment.FilePath = uploadResult.RelativePath;
                    assignment.FileName = uploadResult.OriginalFileName;
                }

                _context.Add(assignment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Topshiriq yaratildi.";
                return RedirectToAction(nameof(Index));
            }

            await LoadFormDataAsync();
            return View(assignment);
        }

        // Topshiriqni tahrirlash sahifasi
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var teacherId = _userManager.GetUserId(User);
            var assignment = await _context.Assignments
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.Id == id && a.Subject!.TeacherId == teacherId);

            if (assignment == null) return NotFound();

            ViewBag.HasSubmissions = assignment.Submissions.Any();
            await LoadFormDataAsync();
            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,DueDate,SubjectId,GroupId")] Assignment formAssignment, IFormFile? file)
        {
            if (id != formAssignment.Id) return NotFound();

            var teacherId = _userManager.GetUserId(User)!;
            var assignment = await _context.Assignments
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.Id == id && a.Subject!.TeacherId == teacherId);

            if (assignment == null) return NotFound();

            var hasSubmissions = assignment.Submissions.Any();
            ViewBag.HasSubmissions = hasSubmissions;

            // Xavfsizlik: talabalar allaqachon topshirgan bo'lsa, Fanni o'zgartirishga
            // ruxsat berilmaydi (bu topshiriq egaligini va Baholash bilan bog'lanishini
            // buzishi mumkin). Guruh, Sarlavha, Tavsif, Muddat va faylni istalgan payt
            // o'zgartirish mumkin.
            var effectiveSubjectId = hasSubmissions ? assignment.SubjectId : formAssignment.SubjectId;

            if (!hasSubmissions)
            {
                var ownsSubject = await _teacherAccessService.OwnsSubjectAsync(teacherId, formAssignment.SubjectId);
                if (!ownsSubject)
                {
                    ModelState.AddModelError(string.Empty, "Noto'g'ri fan tanlandi.");
                }
            }

            if (formAssignment.GroupId.HasValue)
            {
                var validGroup = await _context.GroupSubjects
                    .AnyAsync(gs => gs.SubjectId == effectiveSubjectId && gs.GroupId == formAssignment.GroupId);
                if (!validGroup)
                {
                    ModelState.AddModelError(string.Empty, "Tanlangan guruh bu fanga tegishli emas.");
                }
            }

            FileUploadResult? uploadResult = null;
            if (file != null && file.Length > 0)
            {
                uploadResult = await _fileUploadService.UploadAsync(file, MaxFileSizeBytes);
                if (!uploadResult.Success)
                {
                    ModelState.AddModelError(string.Empty, uploadResult.ErrorMessage!);
                }
            }

            if (ModelState.IsValid)
            {
                assignment.Title = formAssignment.Title;
                assignment.Description = formAssignment.Description;
                assignment.DueDate = formAssignment.DueDate;
                assignment.GroupId = formAssignment.GroupId;

                if (!hasSubmissions)
                {
                    assignment.SubjectId = formAssignment.SubjectId;
                }

                if (uploadResult is { Success: true })
                {
                    assignment.FilePath = uploadResult.RelativePath;
                    assignment.FileName = uploadResult.OriginalFileName;
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Topshiriq yangilandi.";
                return RedirectToAction(nameof(Index));
            }

            if (hasSubmissions)
            {
                formAssignment.SubjectId = assignment.SubjectId;
            }

            await LoadFormDataAsync();
            return View(formAssignment);
        }

        // Topshiriqni o'chirish sahifasi
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var teacherId = _userManager.GetUserId(User);
            var assignment = await _context.Assignments
                .Include(a => a.Subject)
                .Include(a => a.Group)
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.Id == id && a.Subject!.TeacherId == teacherId);

            if (assignment == null) return NotFound();

            // Ma'lumot uchun: bu topshiriq Baholash tarkibiga (GradeComponent) bog'langanmi —
            // bog'langan bo'lsa, o'chirilgach o'sha komponent "bog'lanmagan" holga o'tadi
            // (baholar o'chmaydi, lekin komponent endi hech narsaga avtomatik ulanmaydi).
            var linkedComponent = await _context.GradeComponents
                .FirstOrDefaultAsync(c => c.AssignmentId == id);
            ViewBag.LinkedComponentName = linkedComponent?.Name;

            return View(assignment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var teacherId = _userManager.GetUserId(User);
            var assignment = await _context.Assignments
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.Id == id && a.Subject!.TeacherId == teacherId);

            if (assignment == null) return NotFound();

            if (assignment.Submissions.Any())
            {
                TempData["Error"] = "Bu topshiriqqa talabalar javob yuborgan, shuning uchun o'chirib bo'lmaydi. " +
                    "Agar chindan ham o'chirmoqchi bo'lsangiz, pastdagi \"Majburiy o'chirish\" ni ishlating.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            _context.Assignments.Remove(assignment);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Topshiriq o'chirildi.";
            return RedirectToAction(nameof(Index));
        }

        // Talabalar javobi bo'lsa ham majburan o'chirish — javoblar (Submission) va ular bilan
        // bog'liq baholar QAYTARIB BO'LMAYDIGAN tarzda o'chadi. Shuning uchun bu action
        // View'dagi qo'shimcha yozma tasdiqni (sarlavhani qo'lda kiritish) talab qiladi.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceDeleteConfirmed(int id, string confirmTitle)
        {
            var teacherId = _userManager.GetUserId(User);
            var assignment = await _context.Assignments
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.Id == id && a.Subject!.TeacherId == teacherId);

            if (assignment == null) return NotFound();

            if (!string.Equals(confirmTitle?.Trim(), assignment.Title, StringComparison.Ordinal))
            {
                TempData["Error"] = "Tasdiqlash matni topshiriq nomiga mos kelmadi. Hech narsa o'chirilmadi.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            // Bog'langan GradeComponent bo'lsa, u avtomatik "bog'lanmagan" holatga o'tadi
            // (SetNull), baholar o'zi o'chmaydi — faqat Assignment va uning Submission'lari o'chadi.
            _context.Submissions.RemoveRange(assignment.Submissions);
            _context.Assignments.Remove(assignment);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"\"{assignment.Title}\" topshirig'i, shu jumladan {assignment.Submissions.Count} ta talaba javobi bilan birga o'chirildi.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Submissions(int id)
        {
            var teacherId = _userManager.GetUserId(User);

            var assignment = await _context.Assignments
                .Include(a => a.Subject)
                .Include(a => a.Group)
                .Include(a => a.Submissions)
                    .ThenInclude(s => s.Student)
                .FirstOrDefaultAsync(a => a.Id == id && a.Subject!.TeacherId == teacherId);

            if (assignment == null) return NotFound();

            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Grade(int submissionId, int grade, string? comment)
        {
            var submission = await _context.Submissions
                .Include(s => s.Assignment)
                    .ThenInclude(a => a!.Subject)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null) return NotFound();

            var teacherId = _userManager.GetUserId(User);
            if (submission.Assignment?.Subject?.TeacherId != teacherId) return Forbid();

            if (grade < 0 || grade > 100)
            {
                TempData["Error"] = "Baho 0 dan 100 gacha bo'lishi kerak.";
                return RedirectToAction(nameof(Submissions), new { id = submission.AssignmentId });
            }

            submission.Grade = grade;
            submission.TeacherComment = comment;
            await _context.SaveChangesAsync();

            await _gradeSyncService.SyncFromSubmissionAsync(submission.Id);

            TempData["Success"] = "Baholandi.";
            return RedirectToAction(nameof(Submissions), new { id = submission.AssignmentId });
        }

        // Fanlar ro'yxati va "qaysi fan qaysi guruhlarga tegishli" xaritasini (JS uchun) tayyorlaydi
        private async Task LoadFormDataAsync()
        {
            var teacherId = _userManager.GetUserId(User)!;
            var mySubjects = await _teacherAccessService.GetTeacherSubjectsAsync(teacherId);
            ViewBag.Subjects = new SelectList(mySubjects, "Id", "Name");

            var subjectIds = mySubjects.Select(s => s.Id).ToList();
            var subjectGroups = await _context.GroupSubjects
                .Where(gs => subjectIds.Contains(gs.SubjectId))
                .Include(gs => gs.Group)
                .Select(gs => new { gs.SubjectId, gs.GroupId, GroupName = gs.Group!.Name })
                .ToListAsync();

            ViewBag.SubjectGroupsJson = System.Text.Json.JsonSerializer.Serialize(subjectGroups);
        }
    }
}