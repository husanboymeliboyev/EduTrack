using EduTrack.Data;
using EduTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Admin")]
    public class GroupsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GroupsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Barcha guruhlarni ko'rsatish
        public async Task<IActionResult> Index()
        {
            var groups = await _context.Groups
                .AsNoTracking()
                .OrderByDescending(g => g.Type)
                .ThenBy(g => g.Name)
                .ToListAsync();

            // Talabalar soni haqiqiy a'zolikdan (GroupMemberships) olinadi.
            // Eski ApplicationUser.GroupId faqat bitta guruhni saqlaydi, shuning
            // uchun yo'nalish guruhlari uchun 0 ko'rsatib qo'yardi.
            var counts = await _context.GroupMemberships
                .AsNoTracking()
                .Where(m => m.IsActive)
                .GroupBy(m => m.GroupId)
                .Select(g => new { GroupId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Total);

            ViewBag.MemberCounts = counts;
            return View(groups);
        }

        // Yangi guruh qo'shish sahifasi
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name")] Group group)
        {
            if (ModelState.IsValid)
            {
                _context.Add(group);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(group);
        }

        // Guruhni tahrirlash sahifasi
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var group = await _context.Groups.FindAsync(id);
            if (group == null) return NotFound();

            return View(group);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] Group group)
        {
            if (id != group.Id) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(group);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(group);
        }

        // Guruhni o'chirish sahifasi
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var group = await _context.Groups.FirstOrDefaultAsync(g => g.Id == id);
            if (group == null) return NotFound();

            return View(group);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var group = await _context.Groups.FindAsync(id);
            if (group != null)
            {
                _context.Groups.Remove(group);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}