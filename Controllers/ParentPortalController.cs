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
        private readonly IStudentShowcaseService _service;
        private readonly ApplicationDbContext _context;

        public ParentPortalController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IStudentShowcaseService service,
            ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _service = service;
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

        // GET: /ParentPortal — joriy ota-onaga bog'langan farzandlar ro'yxati
        [Authorize(Roles = "Parent")]
        public async Task<IActionResult> Index()
        {
            var parentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(parentId)) return RedirectToAction(nameof(Login));

            var children = await _context.ParentStudentLinks
                .Where(l => l.ParentId == parentId)
                .Include(l => l.Student)
                .ThenInclude(s => s!.Group)
                .OrderBy(l => l.Student!.FullName)
                .Select(l => new ParentChildViewModel
                {
                    StudentId = l.StudentId,
                    FullName = l.Student!.FullName ?? l.Student.UserName ?? string.Empty,
                    GroupName = l.Student.Group != null ? l.Student.Group.Name : string.Empty
                })
                .ToListAsync();

            return View(children);
        }

        // GET: /ParentPortal/StudentReport/{studentId} — faqat bog'langan farzand natijasi.
        // Bog'lanmagan yoki mavjud bo'lmagan studentId so'ralganda NotFound qaytariladi
        // (boshqa talabalar ma'lumotini sizdirmaslik uchun).
        [Authorize(Roles = "Parent")]
        public async Task<IActionResult> StudentReport(string studentId)
        {
            var parentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(studentId))
            {
                return NotFound();
            }

            var isLinked = await _context.ParentStudentLinks
                .AnyAsync(l => l.ParentId == parentId && l.StudentId == studentId);

            if (!isLinked)
            {
                return NotFound();
            }

            var report = await _service.GetStudentShowcaseAsync(studentId);

            ViewBag.ParentExamResults = await _context.ExamResults
                .AsNoTracking()
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.CompletedDate)
                .Take(10)
                .Select(r => new ParentExamResultViewModel
                {
                    ExamTitle = r.Exam != null ? r.Exam.Title : "Imtihon",
                    SubjectName = r.Exam != null && r.Exam.Subject != null ? r.Exam.Subject.Name : null,
                    CorrectAnswers = r.CorrectAnswers,
                    TotalQuestions = r.TotalQuestions,
                    CompletedDate = r.CompletedDate
                })
                .ToListAsync();

            ViewBag.BackUrl = Url.Action(nameof(Index), "ParentPortal");
            ViewBag.BackText = "Farzandlarim";

            return View("~/Views/AdminShowcase/StudentReport.cshtml", report);

        }

        // GET: /ParentPortal/Welcome — kirishdan keyingi tabrik ekrani (talaba uchun)
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Welcome()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction(nameof(Login));

            var report = await _service.GetStudentShowcaseAsync(userId);
            return View(report);
        }

        // GET: /ParentPortal/Report — joriy tizimga kirgan talabaning o'z hisoboti
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Report()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction(nameof(Login));

            var report = await _service.GetStudentShowcaseAsync(userId);

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
    }
}
