using EduTrack.Data;
using EduTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace EduTrack.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public HomeController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Agar foydalanuvchi tizimga kirmagan bo'lsa - oddiy sahifa ko'rsatamiz
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return View();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return View();

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();

            if (role == "Admin")
            {
                ViewBag.TotalUsers = await _context.Users.CountAsync();
                ViewBag.TotalGroups = await _context.Groups.CountAsync();
                ViewBag.TotalSubjects = await _context.Subjects.CountAsync();
                ViewBag.TotalStudents = await _context.Users.CountAsync(u => u.GroupId != null);
                return View("AdminDashboard");
            }
            else if (role == "Teacher")
            {
                var mySubjects = await _context.Subjects
                    .Where(s => s.TeacherId == user.Id)
                    .ToListAsync();
                return View("TeacherDashboard", mySubjects);
            }
            else if (role == "Student")
            {
                var myGroup = await _context.Groups
                    .Include(g => g.Students)
                    .FirstOrDefaultAsync(g => g.Id == user.GroupId);
                return View("StudentDashboard", myGroup);
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}