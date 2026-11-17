using System.Text;
using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.ViewModels;
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
        private readonly IAppClock _appClock;
        private readonly IStudentSubjectGroupService _studentGroups;

        private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

        public AssignmentsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IFileUploadService fileUploadService,
            ITeacherAccessService teacherAccessService,
            IGradeSyncService gradeSyncService,
            IAppClock appClock,
            IStudentSubjectGroupService studentGroups)
        {
            _context = context;
            _userManager = userManager;
            _fileUploadService = fileUploadService;
            _teacherAccessService = teacherAccessService;
            _gradeSyncService = gradeSyncService;
            _appClock = appClock;
            _studentGroups = studentGroups;
        }

        // ===== Ro'yxat: qidiruv, fan va status bo'yicha filtr, KPI va progress =====
        public async Task<IActionResult> Index(string? q, int? subjectId, string? status)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var now = _appClock.Now;
            var ownedSubjectIds = await _teacherAccessService.GetOwnedSubjectIdsAsync(teacherId);

            var assignments = await _context.Assignments
                .Include(a => a.Subject)
                .Include(a => a.Group)
                .Include(a => a.Submissions)
                .Where(a => ownedSubjectIds.Contains(a.SubjectId))
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            // KPI — filtrga bog'liq emas
            var vm = new AssignmentsIndexViewModel
            {
                Search = q,
                SubjectId = subjectId,
                Status = string.IsNullOrWhiteSpace(status) ? "all" : status,
            };
            vm.TotalCount = assignments.Count;
            vm.ActiveCount = assignments.Count(a => a.DueDate >= now);
            vm.OverdueCount = assignments.Count(a => a.DueDate < now);
            vm.SubmissionsCount = assignments.Sum(a => a.Submissions.Count);
            vm.UngradedCount = assignments.Sum(a => a.Submissions.Count(s => s.Grade == null));

            // Fan filtri opsiyalari (o'qituvchining fanlari)
            var mySubjects = await _teacherAccessService.GetTeacherSubjectsAsync(teacherId);
            vm.SubjectOptions = mySubjects
                .Select(s => new SelectListItem(s.Name, s.Id.ToString()))
                .ToList();

            // Har bir topshiriq uchun: kutilayotgan o'quvchilar soni va statuslar
            var rows = new List<AssignmentRowViewModel>();
            foreach (var a in assignments)
            {
                var expected = await CountTargetStudentsAsync(a);
                rows.Add(new AssignmentRowViewModel
                {
                    Assignment = a,
                    Expected = expected,
                    Submitted = a.Submissions.Count,
                    Graded = a.Submissions.Count(s => s.Grade != null),
                    Late = a.Submissions.Count(s => s.IsLate),
                });
            }

            // Filtrlash
            var statusFilter = vm.Status;
            IEnumerable<AssignmentRowViewModel> filtered = rows;
            if (!string.IsNullOrWhiteSpace(q))
            {
                filtered = filtered.Where(r =>
                    r.Assignment.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (r.Assignment.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }
            if (subjectId.HasValue)
            {
                filtered = filtered.Where(r => r.Assignment.SubjectId == subjectId.Value);
            }
            filtered = statusFilter switch
            {
                "active" => filtered.Where(r => r.Assignment.DueDate >= now),
                "overdue" => filtered.Where(r => r.Assignment.DueDate < now),
                "ungraded" => filtered.Where(r => r.Submitted > r.Graded),
                "late" => filtered.Where(r => r.Late > 0),
                _ => filtered,
            };

            vm.Rows = filtered.ToList();
            return View(vm);
        }

        public async Task<IActionResult> Create()
        {
            await LoadFormDataAsync();
            return View(new Assignment { MaxPoints = 100 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Description,DueDate,SubjectId,GroupId,MaxPoints,AllowLate")] Assignment assignment, IFormFile? file)
        {
            var teacherId = _userManager.GetUserId(User)!;
            await ValidateAssignmentAsync(teacherId, assignment);

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
                assignment.CreatedDate = _appClock.Now;

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

            // Validatsiya xatosi bo'lsa, fayl bazaga yozilmaydi — diskda "yetim" qolmasin.
            if (uploadResult is { Success: true })
            {
                await _fileUploadService.DeleteAsync(uploadResult.RelativePath);
            }

            await LoadFormDataAsync();
            return View(assignment);
        }

        // Topshiriqni tahrirlash sahifasi
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var teacherId = _userManager.GetUserId(User);
            var ownedSubjectIds = await _teacherAccessService.GetOwnedSubjectIdsAsync(teacherId!);
            var assignment = await _context.Assignments
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.Id == id && ownedSubjectIds.Contains(a.SubjectId));

            if (assignment == null) return NotFound();

            ViewBag.HasSubmissions = assignment.Submissions.Any();
            await LoadFormDataAsync();
            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,DueDate,SubjectId,GroupId,MaxPoints,AllowLate")] Assignment formAssignment, IFormFile? file)
        {
            if (id != formAssignment.Id) return NotFound();

            var teacherId = _userManager.GetUserId(User)!;
            var ownedSubjectIds = await _teacherAccessService.GetOwnedSubjectIdsAsync(teacherId);
            var assignment = await _context.Assignments
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.Id == id && ownedSubjectIds.Contains(a.SubjectId));

            if (assignment == null) return NotFound();

            var hasSubmissions = assignment.Submissions.Any();
            ViewBag.HasSubmissions = hasSubmissions;

            // Xavfsizlik: talabalar allaqachon topshirgan bo'lsa, Fanni o'zgartirishga
            // ruxsat berilmaydi (bu topshiriq egaligini va Baholash bilan bog'lanishini
            // buzishi mumkin). Guruh, Sarlavha, Tavsif, Muddat, Maksimal ball va faylni istalgan payt
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
                var previousFilePath = assignment.FilePath;

                assignment.Title = formAssignment.Title;
                assignment.Description = formAssignment.Description;
                assignment.DueDate = formAssignment.DueDate;
                assignment.GroupId = formAssignment.GroupId;
                assignment.MaxPoints = formAssignment.MaxPoints;
                assignment.AllowLate = formAssignment.AllowLate;

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

                // Ball shkalasi o'zgarsa, allaqachon qo'yilgan baholarni yangi shkalaga moslashtiramiz.
                if (hasSubmissions)
                {
                    await _gradeSyncService.SyncAllForAssignmentAsync(assignment.Id);
                }

                if (uploadResult is { Success: true }
                    && !string.IsNullOrWhiteSpace(previousFilePath)
                    && !string.Equals(previousFilePath, assignment.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    await _fileUploadService.DeleteAsync(previousFilePath);
                }

                TempData["Success"] = "Topshiriq yangilandi.";
                return RedirectToAction(nameof(Index));
            }

            // Validatsiya xatosi bo'lsa, yangi fayl bazaga yozilmaydi — diskda "yetim" qolmasin.
            if (uploadResult is { Success: true })
            {
                await _fileUploadService.DeleteAsync(uploadResult.RelativePath);
            }

            if (hasSubmissions)
            {
                formAssignment.SubjectId = assignment.SubjectId;
            }

            await LoadFormDataAsync();
            return View(formAssignment);
        }

        // ===== Talabalar ro'yxati: topshirdi / topshirmadi + inline baholash + eksport =====
        public async Task<IActionResult> Submissions(int id, string? status)
        {
            var teacherId = _userManager.GetUserId(User);
            var ownedSubjectIds = await _teacherAccessService.GetOwnedSubjectIdsAsync(teacherId!);
            var assignment = await _context.Assignments
                .Include(a => a.Subject)
                .Include(a => a.Group)
                .Include(a => a.Submissions)
                    .ThenInclude(s => s.Student)
                .FirstOrDefaultAsync(a => a.Id == id && ownedSubjectIds.Contains(a.SubjectId));

            if (assignment == null) return NotFound();

            var vm = new AssignmentSubmissionsViewModel
            {
                Assignment = assignment,
                Status = string.IsNullOrWhiteSpace(status) ? "all" : status,
            };

            // Kutilayotgan o'quvchilar ro'yxati
            var studentIds = await GetTargetStudentIdsAsync(assignment);
            var users = await _context.Users
                .AsNoTracking()
                .Where(u => studentIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName, u.LoginId })
                .ToListAsync();

            var byStudent = assignment.Submissions.ToDictionary(s => s.StudentId, s => s);

            var rows = new List<SubmissionRosterRow>();
            var covered = new HashSet<string>();
            foreach (var u in users)
            {
                covered.Add(u.Id);
                byStudent.TryGetValue(u.Id, out var sub);
                rows.Add(new SubmissionRosterRow
                {
                    StudentId = u.Id,
                    FullName = u.FullName ?? "Noma'lum",
                    LoginId = u.LoginId,
                    Submission = sub,
                });
            }

            // Ro'yxatda bo'lmagan (masalan guruh almashgan) talabalar javoblari ham ko'rinsin.
            vm.HasExtraSubmissions = assignment.Submissions.Any(s => !covered.Contains(s.StudentId));
            foreach (var sub in assignment.Submissions.Where(s => !covered.Contains(s.StudentId)))
            {
                rows.Add(new SubmissionRosterRow
                {
                    StudentId = sub.StudentId,
                    FullName = sub.Student?.FullName ?? "Noma'lum talaba",
                    LoginId = sub.Student?.LoginId,
                    Submission = sub,
                });
            }

            vm.Total = rows.Count;
            vm.SubmittedCount = rows.Count(r => r.Submitted);
            vm.MissingCount = vm.Total - vm.SubmittedCount;
            vm.GradedCount = rows.Count(r => r.Submission?.Grade != null);
            vm.UngradedCount = vm.SubmittedCount - vm.GradedCount;
            vm.LateCount = rows.Count(r => r.Submission?.IsLate == true);
            var grades = rows.Where(r => r.Submission?.Grade != null).Select(r => r.Submission!.Grade!.Value).ToList();
            vm.AverageGrade = grades.Count > 0 ? Math.Round(grades.Average(), 1) : null;

            // Talabalar familiyasi bo'yicha tartib (barcha filtrlar uchun bir xil).
            rows = StudentNameOrder.SortBySurname(rows, r => r.FullName);

            vm.Rows = vm.Status switch
            {
                "submitted" => rows.Where(r => r.Submitted).ToList(),
                "missing" => rows.Where(r => !r.Submitted).ToList(),
                "graded" => rows.Where(r => r.Submission?.Grade != null).ToList(),
                "ungraded" => rows.Where(r => r.Submission != null && r.Submission.Grade == null).ToList(),
                "late" => rows.Where(r => r.Submission?.IsLate == true).ToList(),
                _ => rows,
            };

            return View(vm);
        }

        // Ro'yxatni Excel uchun CSV qilib eksport qilish (UTF-8 BOM, ';' ajratgich).
        public async Task<IActionResult> Export(int id)
        {
            var teacherId = _userManager.GetUserId(User);
            var ownedSubjectIds = await _teacherAccessService.GetOwnedSubjectIdsAsync(teacherId!);
            var assignment = await _context.Assignments
                .Include(a => a.Subject)
                .Include(a => a.Group)
                .Include(a => a.Submissions)
                    .ThenInclude(s => s.Student)
                .FirstOrDefaultAsync(a => a.Id == id && ownedSubjectIds.Contains(a.SubjectId));

            if (assignment == null) return NotFound();

            var studentIds = await GetTargetStudentIdsAsync(assignment);
            var users = await _context.Users
                .AsNoTracking()
                .Where(u => studentIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName, u.LoginId })
                .ToListAsync();
            var orderedUsers = StudentNameOrder.SortBySurname(users, u => u.FullName);
            var byStudent = assignment.Submissions.ToDictionary(s => s.StudentId, s => s);

            var sb = new StringBuilder();
            sb.AppendLine("Login ID;Talaba;Holat;Topshirilgan vaqt;Kechikkan;Ball;Maks ball;O'qituvchi izohi;Talaba izohi");
            foreach (var u in orderedUsers)
            {
                byStudent.TryGetValue(u.Id, out var sub);
                sb.Append(Esc(u.LoginId ?? "")).Append(';');
                sb.Append(Esc(u.FullName ?? "")).Append(';');
                sb.Append(sub == null ? "Topshirmagan" : (sub.Grade != null ? "Baholandi" : "Baholanmagan")).Append(';');
                sb.Append(sub == null ? "" : sub.SubmittedDate.ToString("dd.MM.yyyy HH:mm")).Append(';');
                sb.Append(sub == null ? "" : (sub.IsLate ? "Ha" : "Yo'q")).Append(';');
                sb.Append(sub?.Grade?.ToString() ?? "").Append(';');
                sb.Append(assignment.MaxPoints).Append(';');
                sb.Append(Esc(sub?.TeacherComment ?? "")).Append(';');
                sb.Append(Esc(sub?.StudentNote ?? ""));
                sb.AppendLine();
            }

            var fileName = $"topshiriq-{assignment.Id}-javoblar.csv";
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", fileName);
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
            var ownedSubjectIds = await _teacherAccessService.GetOwnedSubjectIdsAsync(teacherId!);
            if (submission.Assignment == null || !ownedSubjectIds.Contains(submission.Assignment.SubjectId)) return Forbid();

            if (grade < 0 || grade > submission.Assignment.MaxPoints)
            {
                TempData["Error"] = $"Baho 0 dan {submission.Assignment.MaxPoints} gacha bo'lishi kerak.";
                return RedirectToAction(nameof(Submissions), new { id = submission.AssignmentId });
            }

            submission.Grade = grade;
            submission.TeacherComment = comment;
            submission.GradedDate = _appClock.Now;
            await _context.SaveChangesAsync();

            await _gradeSyncService.SyncFromSubmissionAsync(submission.Id);

            TempData["Success"] = "Baholandi.";
            return RedirectToAction(nameof(Submissions), new { id = submission.AssignmentId });
        }

        // Bahoni tozalash (qayta baholash uchun)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearGrade(int submissionId)
        {
            var submission = await _context.Submissions
                .Include(s => s.Assignment)
                    .ThenInclude(a => a!.Subject)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null) return NotFound();

            var teacherId = _userManager.GetUserId(User);
            var ownedSubjectIds = await _teacherAccessService.GetOwnedSubjectIdsAsync(teacherId!);
            if (submission.Assignment == null || !ownedSubjectIds.Contains(submission.Assignment.SubjectId)) return Forbid();

            submission.Grade = null;
            submission.GradedDate = null;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Baho tozalandi.";
            return RedirectToAction(nameof(Submissions), new { id = submission.AssignmentId });
        }

        // Topshiriqni o'chirish sahifasi
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var teacherId = _userManager.GetUserId(User);
            var ownedSubjectIds = await _teacherAccessService.GetOwnedSubjectIdsAsync(teacherId!);
            var assignment = await _context.Assignments
                .Include(a => a.Subject)
                .Include(a => a.Group)
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.Id == id && ownedSubjectIds.Contains(a.SubjectId));

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
            var ownedSubjectIds = await _teacherAccessService.GetOwnedSubjectIdsAsync(teacherId!);
            var assignment = await _context.Assignments
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.Id == id && ownedSubjectIds.Contains(a.SubjectId));

            if (assignment == null) return NotFound();

            if (assignment.Submissions.Any())
            {
                TempData["Error"] = "Bu topshiriqqa talabalar javob yuborgan, shuning uchun o'chirib bo'lmaydi. " +
                    "Agar chindan ham o'chirmoqchi bo'lsangiz, pastdagi \"Majburiy o'chirish\" ni ishlating.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            _context.Assignments.Remove(assignment);
            await _context.SaveChangesAsync();
            await _fileUploadService.DeleteAsync(assignment.FilePath);
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
            var ownedSubjectIds = await _teacherAccessService.GetOwnedSubjectIdsAsync(teacherId!);
            var assignment = await _context.Assignments
                .Include(a => a.Submissions)
                .FirstOrDefaultAsync(a => a.Id == id && ownedSubjectIds.Contains(a.SubjectId));

            if (assignment == null) return NotFound();

            if (!string.Equals(confirmTitle?.Trim(), assignment.Title, StringComparison.Ordinal))
            {
                TempData["Error"] = "Tasdiqlash matni topshiriq nomiga mos kelmadi. Hech narsa o'chirilmadi.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            // Bog'langan GradeComponent bo'lsa, u avtomatik "bog'lanmagan" holatga o'tadi
            // (SetNull), baholar o'zi o'chmaydi — faqat Assignment va uning Submission'lari o'chadi.
            var filePaths = assignment.Submissions
                .Select(s => s.FilePath)
                .Append(assignment.FilePath)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            _context.Submissions.RemoveRange(assignment.Submissions);
            _context.Assignments.Remove(assignment);
            await _context.SaveChangesAsync();

            foreach (var path in filePaths)
            {
                await _fileUploadService.DeleteAsync(path);
            }

            TempData["Success"] = $"\"{assignment.Title}\" topshirig'i, shu jumladan {assignment.Submissions.Count} ta talaba javobi bilan birga o'chirildi.";
            return RedirectToAction(nameof(Index));
        }

        // ===== Yordamchilar =====

        private async Task ValidateAssignmentAsync(string teacherId, Assignment assignment)
        {
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
        }

        // Topshiriq mo'ljallangan guruhlar (GroupId null bo'lsa — fan biriktirilgan barcha guruhlar).
        private async Task<List<(int GroupId, int SubjectId)>> GetTargetPairsAsync(Assignment assignment)
        {
            var pairs = new List<(int, int)>();
            if (assignment.GroupId.HasValue)
            {
                pairs.Add((assignment.GroupId.Value, assignment.SubjectId));
            }
            else
            {
                var groupIds = await _context.GroupSubjects
                    .AsNoTracking()
                    .Where(gs => gs.SubjectId == assignment.SubjectId)
                    .Select(gs => gs.GroupId)
                    .ToListAsync();
                pairs.AddRange(groupIds.Select(g => (g, assignment.SubjectId)));
            }
            return pairs;
        }

        private async Task<int> CountTargetStudentsAsync(Assignment assignment)
        {
            var pairs = await GetTargetPairsAsync(assignment);
            var students = new HashSet<string>();
            foreach (var (groupId, subjectId) in pairs)
            {
                foreach (var sid in await _studentGroups.GetStudentIdsAsync(groupId, subjectId))
                {
                    students.Add(sid);
                }
            }
            return students.Count;
        }

        private async Task<List<string>> GetTargetStudentIdsAsync(Assignment assignment)
        {
            var pairs = await GetTargetPairsAsync(assignment);
            var students = new HashSet<string>();
            foreach (var (groupId, subjectId) in pairs)
            {
                foreach (var sid in await _studentGroups.GetStudentIdsAsync(groupId, subjectId))
                {
                    students.Add(sid);
                }
            }
            return students.ToList();
        }

        private static string Esc(string value) =>
            value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ");

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
