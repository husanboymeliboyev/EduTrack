using EduTrack.Models;
using EduTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EduTrack.Controllers
{
    /// <summary>
    /// Oylik reyting sahifalari: talaba o'z sinf/yo'nalish reytingini,
    /// o'qituvchi o'z guruhlarini, admin butun maktabni ko'radi.
    /// Hisoblash mantig'i <see cref="IMonthlyRatingService"/>da.
    /// </summary>
    [Authorize]
    public class MonthlyRatingController : Controller
    {
        private readonly IMonthlyRatingService _ratingService;
        private readonly UserManager<ApplicationUser> _userManager;

        public MonthlyRatingController(
            IMonthlyRatingService ratingService,
            UserManager<ApplicationUser> userManager)
        {
            _ratingService = ratingService;
            _userManager = userManager;
        }

        // GET: /MonthlyRating — talabaning shaxsiy reytingi
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Index(int? year, int? month)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var model = await _ratingService.GetStudentRatingAsync(user.Id, year, month);
            if (model == null) return NotFound();

            return View(model);
        }

        // GET: /MonthlyRating/Admin — maktab bo'yicha guruhlar kesimi
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Admin(int? groupId, int? year, int? month)
        {
            var model = await _ratingService.GetOverviewAsync(
                groupId, year, month, teacherId: null, isTeacherView: false);

            return View("Overview", model);
        }

        // GET: /MonthlyRating/Teacher — o'qituvchining guruhlari kesimi
        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> Teacher(int? groupId, int? year, int? month)
        {
            var teacherId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(teacherId)) return Forbid();

            var model = await _ratingService.GetOverviewAsync(
                groupId, year, month, teacherId, isTeacherView: true);

            return View("Overview", model);
        }

        // GET: /MonthlyRating/Student/{id} — admin uchun o'quvchi kartasi
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Student(string id, int? year, int? month)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var model = await _ratingService.GetStudentRatingAsync(id, year, month);
            if (model == null) return NotFound();

            ViewBag.BackUrl = Url.Action(nameof(Admin), new { year, month });
            ViewBag.BackText = "Oylik reyting";
            ViewBag.ReadOnly = true;

            return View("Index", model);
        }
    }
}
