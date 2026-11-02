using EduTrack.Models;
using EduTrack.Services;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EduTrack.Controllers
{
    // O'qituvchi uchun «Talabalar» sahifasi: faqat o'z fanlari orqali
    // biriktirilgan guruhlardagi talabalar ko'rinadi. Tahrirlash/o'chirish
    // amallari yo'q — bu read-only qidiruv va filtr sahifasi.
    [Authorize(Roles = "Teacher")]
    public class StudentsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserDirectoryService _userDirectoryService;

        public StudentsController(
            UserManager<ApplicationUser> userManager,
            IUserDirectoryService userDirectoryService)
        {
            _userManager = userManager;
            _userDirectoryService = userDirectoryService;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] UsersFilterViewModel filter)
        {
            var teacherId = _userManager.GetUserId(User)!;

            var model = await _userDirectoryService.GetForTeacherAsync(
                teacherId,
                filter ?? new UsersFilterViewModel());

            return View(model);
        }
    }
}
