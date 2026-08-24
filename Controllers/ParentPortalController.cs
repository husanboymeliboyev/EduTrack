using EduTrack.Models;
using EduTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EduTrack.Controllers
{
    // Ota-onalar uchun alohida kirish nuqtasi. Alohida "ota-ona hisobi" yaratilmaydi —
    // talabaning mavjud LoginId + paroli orqali kiriladi (Sodiq School'dagi "Natijaga kirish"
    // sahifasiga o'xshash). Muvaffaqiyatli kirishdan so'ng faqat o'sha talabaning ko'rgazma
    // hisoboti (StudentReport) ko'rsatiladi — boshqa hech qanday admin/o'qituvchi funksiyasiga
    // kirish berilmaydi.
    [AllowAnonymous]
    public class ParentPortalController : Controller

    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStudentShowcaseService _service;

        public ParentPortalController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IStudentShowcaseService service)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _service = service;
        }

        // GET: /ParentPortal/Login
        [HttpGet]
        public async Task<IActionResult> Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var current = await _userManager.GetUserAsync(User);
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
            if (user == null || !await _userManager.IsInRoleAsync(user, "Student"))
            {
                // Bu portal faqat talaba hisoblari uchun — boshqa rol bo'lsa, kirishga ruxsat berilmaydi.
                await _signInManager.SignOutAsync();
                ModelState.AddModelError(string.Empty, "Bu kirish kodi orqali natijalarni ko'rish mumkin emas.");
                return View();
            }

            return RedirectToAction(nameof(Welcome));
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