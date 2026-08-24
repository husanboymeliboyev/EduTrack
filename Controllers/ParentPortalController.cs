using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.ViewModels;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EduTrack.Controllers
{
        // Parent uchun alohida, read-only dashboard. Legacy Student report actions
    // saqlanadi, lekin yangi Parent dashboard faqat linked children orqali ishlaydi.

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
                    return RedirectToAction(nameof(Dashboard));
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
                ModelState.AddModelError(string.Empty, "Kirish kodi va parolni kiriting.");
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
                ModelState.AddModelError(string.Empty, "Kirish kodi yoki parol noto'g'ri.");
                return View();
            }

            var user = await _userManager.FindByNameAsync(loginId.Trim());
            if (user == null || !await _userManager.IsInRoleAsync(user, "Parent"))
            {
                // Bu portal faqat alohida Parent hisoblari uchun ishlaydi.
                await _signInManager.SignOutAsync();
                ModelState.AddModelError(string.Empty, "Bu kirish kodi orqali ota-ona paneliga kirish mumkin emas.");
                return View();
            }

            return RedirectToAction(nameof(Dashboard));
        }

        // GET: /ParentPortal/Welcome — kirishdan keyingi tabrik ekrani
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

                // GET: /ParentPortal/Dashboard
        [Authorize(Roles = "Parent")]
        public async Task<IActionResult> Dashboard(string? childId)
        {
            var parentId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(parentId))
            {
                return Challenge();
            }

            var links = await _context.ParentStudentLinks
                .AsNoTracking()
                .Where(link => link.ParentUserId == parentId && link.IsActive)
                .Include(link => link.StudentUser)
                    .ThenInclude(student => student.Group)
                .OrderBy(link => link.StudentUser.FullName)
                .ToListAsync();

            var model = new ParentDashboardViewModel
            {
                Children = links.Select(link => new ParentChildOptionViewModel
                {
                    StudentId = link.StudentUserId,
                    StudentName = link.StudentUser.FullName ?? link.StudentUser.UserName ?? "Noma'lum o'quvchi",
                    GroupName = link.StudentUser.Group?.Name ?? string.Empty
                }).ToList()
            };

            if (model.Children.Count == 0)
            {
                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(childId) && !model.Children.Any(child => child.StudentId == childId))
            {
                return NotFound();
            }

            var selected = model.Children.FirstOrDefault(child => child.StudentId == childId)
                ?? model.Children[0];
            model.SelectedChildId = selected.StudentId;
            model.Report = await _service.GetStudentShowcaseAsync(selected.StudentId);

            return View(model);
        }

        // POST: /ParentPortal/Logout

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }
    }
}