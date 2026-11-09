using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    // Ota-onalar uchun alohida panel. Ota-ona o'z hisobi (LoginId + parol) bilan
    // tizimga kiradi va faqat o'ziga bog'langan farzand(lar)ining natijalarini ko'radi.
    // Boshqa talabalar ma'lumoti ota-onaga ko'rsatilmaydi (server tomonida tekshiriladi).
    public class ParentPortalController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStudentShowcaseService _showcaseService;
        private readonly IParentPortalService _parentPortal;
        private readonly ApplicationDbContext _context;

        public ParentPortalController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IStudentShowcaseService showcaseService,
            IParentPortalService parentPortal,
            ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _showcaseService = showcaseService;
            _parentPortal = parentPortal;
            _context = context;
        }

        // GET: /ParentPortal/Login
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var current = await _userManager.GetUserAsync(User);
                if (current != null && await _userManager.IsInRoleAsync(current, "Parent"))
                {
                    return RedirectToAction(nameof(Index));
                }

                if (current != null && await _userManager.IsInRoleAsync(current, "Student"))
                {
                    return RedirectToAction(nameof(Report));
                }

                // Boshqa rol (Admin/Teacher) bilan shu sahifaga kirsa — chalkashmasin deb chiqarib yuboramiz.
                await _signInManager.SignOutAsync();
            }

            return View();
        }

        // POST: /ParentPortal/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string loginId, string password)
        {
            if (string.IsNullOrWhiteSpace(loginId) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(string.Empty, "Login ID va parolni kiriting.");
                return View();
            }

            var signInResult = await _signInManager.PasswordSignInAsync(
                loginId.Trim(), password, isPersistent: true, lockoutOnFailure: true);

            if (signInResult.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Hisob vaqtincha bloklangan. Birozdan so'ng qayta urinib ko'ring.");
                return View();
            }

            if (!signInResult.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Login ID yoki parol noto'g'ri.");
                return View();
            }

            var user = await _userManager.FindByNameAsync(loginId.Trim());
            if (user == null)
            {
                await _signInManager.SignOutAsync();
                ModelState.AddModelError(string.Empty, "Hisob topilmadi.");
                return View();
            }

            // Ota-ona hisobi -> bog'langan farzandlar ro'yxatiga.
            if (await _userManager.IsInRoleAsync(user, "Parent"))
            {
                return RedirectToAction(nameof(Index));
            }

            // Talaba hisobi -> o'z natijasini ko'rish (mavjud xatti-harakat saqlanadi).
            if (await _userManager.IsInRoleAsync(user, "Student"))
            {
                return RedirectToAction(nameof(Welcome));
            }

            // Boshqa rol bu panelga kira olmaydi.
            await _signInManager.SignOutAsync();
            ModelState.AddModelError(string.Empty, "Bu hisob orqali natijalarni ko'rish mumkin emas.");
            return View();
        }

        // GET: /ParentPortal — ota-ona bosh sahifasi (farzandlar va umumiy holat)
        [Authorize(Roles = "Parent")]
        public async Task<IActionResult> Index()
        {
            var parentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(parentId)) return RedirectToAction(nameof(Login));

            var model = await _parentPortal.GetDashboardAsync(parentId);
            return View(model);
        }

        // GET: /ParentPortal/Attendance — davomat moduli
        [Authorize(Roles = "Parent")]
        public async Task<IActionResult> Attendance(string? studentId)
        {
            return await RenderChildModuleAsync(
                studentId,
                (parentId, id) => _parentPortal.GetAttendanceAsync(parentId, id));
        }

        // GET: /ParentPortal/Grades — baholar moduli
        [Authorize(Roles = "Parent")]
        public async Task<IActionResult> Grades(string? studentId)
        {
            return await RenderChildModuleAsync(
                studentId,
                (parentId, id) => _parentPortal.GetGradesAsync(parentId, id));
        }

        // GET: /ParentPortal/Rating — farzandning oylik reytingi (sinf va yo'nalish)
        [Authorize(Roles = "Parent")]
        public async Task<IActionResult> Rating(string? studentId, int? year, int? month)
        {
            return await RenderChildModuleAsync(
                studentId,
                (parentId, id) => _parentPortal.GetMonthlyRatingAsync(parentId, id, year, month));
        }

        // GET: /ParentPortal/Exams — imtihon natijalari moduli
        [Authorize(Roles = "Parent")]
        public async Task<IActionResult> Exams(string? studentId)
        {
            return await RenderChildModuleAsync(
                studentId,
                (parentId, id) => _parentPortal.GetExamsAsync(parentId, id));
        }

        // GET: /ParentPortal/Assignments — topshiriqlar moduli
        [Authorize(Roles = "Parent")]
        public async Task<IActionResult> Assignments(string? studentId)
        {
            return await RenderChildModuleAsync(
                studentId,
                (parentId, id) => _parentPortal.GetAssignmentsAsync(parentId, id));
        }

        // GET: /ParentPortal/Alerts — ogohlantirishlar moduli
        [Authorize(Roles = "Parent")]
        public async Task<IActionResult> Alerts(string? studentId)
        {
            return await RenderChildModuleAsync(
                studentId,
                (parentId, id) => _parentPortal.GetAlertsAsync(parentId, id));
        }

        // GET: /ParentPortal/Schedule — dars jadvali moduli
        [Authorize(Roles = "Parent")]
        public async Task<IActionResult> Schedule(string? studentId)
        {
            return await RenderChildModuleAsync(
                studentId,
                (parentId, id) => _parentPortal.GetScheduleAsync(parentId, id));
        }

        // GET: /ParentPortal/StudentReport/{studentId} (yoki ?studentId=...) — faqat
        // bog'langan farzand natijasi. Bog'lanmagan yoki mavjud bo'lmagan studentId
        // so'ralganda NotFound qaytariladi (boshqa talabalar ma'lumotini sizdirmaslik uchun).
        [Authorize(Roles = "Parent")]
        public async Task<IActionResult> StudentReport(string? studentId, string? id = null)
        {
            // Standart route `{id?}` bo'lgani uchun yo'l shakli `id`ga tushadi;
            // havolalar esa `studentId` query parametrini ishlatadi. Ikkalasini ham qabul qilamiz.
            var targetStudentId = string.IsNullOrWhiteSpace(studentId) ? id : studentId;

            var parentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(targetStudentId))
            {
                return NotFound();
            }

            studentId = targetStudentId;

            var isLinked = await _context.ParentStudentLinks
                .AnyAsync(l => l.ParentId == parentId && l.StudentId == studentId);

            if (!isLinked)
            {
                return NotFound();
            }

            var report = await _showcaseService.GetStudentShowcaseAsync(studentId);

            ViewBag.ParentExamResults = await _context.ExamResults
                .AsNoTracking()
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.CompletedDate)
                .Take(10)
                .Select(r => new ParentExamResultViewModel
                {
                    Id = r.Id,
                    ExamTitle = r.Exam != null ? r.Exam.Title : "Imtihon",
                    SubjectName = r.Exam != null && r.Exam.Subject != null ? r.Exam.Subject.Name : null,
                    CorrectAnswers = r.CorrectAnswers,
                    TotalQuestions = r.TotalQuestions,
                    CompletedDate = r.CompletedDate,
                    IsApproved = r.IsApproved
                })
                .ToListAsync();

            ViewBag.BackUrl = Url.Action(nameof(Index), "ParentPortal");
            ViewBag.BackText = "Farzandlarim";
            ViewBag.ChildStudentId = studentId;

            return View("~/Views/AdminShowcase/StudentReport.cshtml", report);
        }

        // GET: /ParentPortal/Welcome — kirishdan keyingi tabrik ekrani (talaba uchun)
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Welcome()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction(nameof(Login));

            var report = await _showcaseService.GetStudentShowcaseAsync(userId);
            return View(report);
        }

        // GET: /ParentPortal/Report — joriy tizimga kirgan talabaning o'z hisoboti
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Report()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction(nameof(Login));

            var report = await _showcaseService.GetStudentShowcaseAsync(userId);

            // Admin ko'rinishi bilan bir xil View'dan foydalanamiz — dizayn ikkalasida ham bir xil bo'lsin.
            return View("~/Views/AdminShowcase/StudentReport.cshtml", report);
        }

        // POST: /ParentPortal/Logout
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        // ==================== Ichki yordamchi ====================

        private async Task<IActionResult> RenderChildModuleAsync<T>(
            string? studentId,
            Func<string, string?, Task<T?>> loadAsync)
            where T : ParentPageViewModel
        {
            var parentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(parentId)) return RedirectToAction(nameof(Login));

            var model = await loadAsync(parentId, studentId);
            if (model != null)
            {
                return View(model);
            }

            // Farzand umuman bog'lanmagan bo'lsa — bosh sahifaga.
            if (!await _parentPortal.HasChildrenAsync(parentId))
            {
                return RedirectToAction(nameof(Index));
            }

            // Boshqa ota-onaning farzandi so'ralgan — ma'lumot oshkor qilinmaydi.
            return NotFound();
        }
    }
}
