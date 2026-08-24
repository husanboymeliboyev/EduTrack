using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ParentLinksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ParentLinksController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var links = await _context.ParentStudentLinks
                .AsNoTracking()
                .Include(link => link.ParentUser)
                .Include(link => link.StudentUser)
                    .ThenInclude(student => student.Group)
                .OrderByDescending(link => link.IsActive)
                .ThenBy(link => link.ParentUser.FullName)
                .ThenBy(link => link.StudentUser.FullName)
                .Select(link => new ParentLinkRowViewModel
                {
                    Id = link.Id,
                    ParentName = link.ParentUser.FullName ?? link.ParentUser.UserName ?? "Noma'lum ota-ona",
                    ParentLoginId = link.ParentUser.LoginId,
                    StudentName = link.StudentUser.FullName ?? link.StudentUser.UserName ?? "Noma'lum o'quvchi",
                    GroupName = link.StudentUser.Group != null ? link.StudentUser.Group.Name : string.Empty,
                    IsActive = link.IsActive
                })
                .ToListAsync();

            return View(new ParentLinkIndexViewModel { Links = links });
        }

        public async Task<IActionResult> Create()
        {
            await LoadUsersAsync();
            return View(new ParentLinkCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ParentLinkCreateViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.ParentUserId) || string.IsNullOrWhiteSpace(model.StudentUserId))
            {
                ModelState.AddModelError(string.Empty, "Ota-ona va farzandni tanlang.");
            }

            var parent = await _userManager.FindByIdAsync(model.ParentUserId);
            var student = await _userManager.FindByIdAsync(model.StudentUserId);
            if (parent == null || !await _userManager.IsInRoleAsync(parent, "Parent"))
            {
                ModelState.AddModelError(nameof(model.ParentUserId), "Parent akkaunti topilmadi.");
            }

            if (student == null || !await _userManager.IsInRoleAsync(student, "Student"))
            {
                ModelState.AddModelError(nameof(model.StudentUserId), "O‘quvchi akkaunti topilmadi.");
            }

            if (parent != null && student != null)
            {
                var duplicate = await _context.ParentStudentLinks
                    .AnyAsync(link => link.ParentUserId == parent.Id && link.StudentUserId == student.Id && link.IsActive);
                if (duplicate)
                {
                    ModelState.AddModelError(string.Empty, "Bu farzand allaqachon ushbu ota-onaga bog‘langan.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadUsersAsync();
                return View(model);
            }

            var existingRevokedLink = await _context.ParentStudentLinks
                .FirstOrDefaultAsync(link => link.ParentUserId == parent!.Id && link.StudentUserId == student!.Id && !link.IsActive);

            if (existingRevokedLink != null)
            {
                existingRevokedLink.IsActive = true;
                existingRevokedLink.RevokedAtUtc = null;
            }
            else
            {
                _context.ParentStudentLinks.Add(new ParentStudentLink
                {
                    ParentUserId = parent!.Id,
                    StudentUserId = student!.Id,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Ota-ona va farzand muvaffaqiyatli bog‘landi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revoke(int id)
        {
            var link = await _context.ParentStudentLinks.FindAsync(id);
            if (link == null)
            {
                return NotFound();
            }

            if (link.IsActive)
            {
                link.IsActive = false;
                link.RevokedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Bog‘lanish bekor qilindi. Tarixiy yozuv saqlandi.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadUsersAsync()
        {
            var parents = await _userManager.GetUsersInRoleAsync("Parent");
            var students = await _userManager.GetUsersInRoleAsync("Student");
            var studentIds = students.Select(user => user.Id).ToList();
            var studentsWithGroups = await _context.Users
                .AsNoTracking()
                .Include(user => user.Group)
                .Where(user => studentIds.Contains(user.Id))
                .ToListAsync();

            ViewBag.Parents = new SelectList(
                parents.OrderBy(user => user.FullName).Select(user => new
                {
                    user.Id,
                    Display = $"{user.FullName ?? user.UserName} ({user.LoginId})"
                }), "Id", "Display");

            ViewBag.Students = new SelectList(
                studentsWithGroups.OrderBy(user => user.FullName).Select(user => new
                {
                    user.Id,
                    Display = $"{user.FullName ?? user.UserName}{(user.Group != null ? $" — {user.Group.Name}" : string.Empty)}"
                }), "Id", "Display");
        }
    }
}
