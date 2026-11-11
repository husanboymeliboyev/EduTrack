using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    // Guruhlar: Admin to'liq boshqaradi; Moderator ko'radi, yaratadi va
    // nomini tahrirlaydi, lekin o'chira olmaydi.
    [Authorize(Roles = "Admin,Moderator")]
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

            // Migratsiyadan oldingi o'quvchilarda a'zolik yozuvi yo'q — tizim
            // ularni eski GroupId orqali topadi (StudentContextService bilan bir xil).
            var legacyCounts = await _context.Groups
                .AsNoTracking()
                .Where(g => g.Type == GroupType.Stream)
                .Select(g => new
                {
                    g.Id,
                    Total = _context.Users.Count(u => u.GroupId == g.Id
                        && !_context.GroupMemberships.Any(m => m.StudentId == u.Id
                                                              && m.Scope == GroupType.Stream))
                })
                .ToDictionaryAsync(x => x.Id, x => x.Total);

            foreach (var pair in legacyCounts)
            {
                counts[pair.Key] = counts.GetValueOrDefault(pair.Key) + pair.Value;
            }

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

        // Guruh tarkibidagi o'quvchilar ro'yxati
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var group = await _context.Groups
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null) return NotFound();

            var members = await _context.Users
                .AsNoTracking()
                .Where(u =>
                    _context.GroupMemberships.Any(m => m.StudentId == u.Id
                        && m.IsActive && m.GroupId == group.Id)
                    || (u.GroupId == group.Id
                        && !_context.GroupMemberships.Any(m => m.StudentId == u.Id
                            && m.IsActive && m.Scope == group.Type)))
                .OrderBy(u => EF.Functions.Collate(u.FullName ?? string.Empty, "NOCASE"))
                .ThenBy(u => u.LoginId)
                .Select(u => new GroupMemberRowViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    LoginId = u.LoginId,
                    ClassName = _context.GroupMemberships
                        .Where(m => m.StudentId == u.Id && m.IsActive && m.Scope == GroupType.Class)
                        .Select(m => m.Group!.Name)
                        .FirstOrDefault()
                        ?? (u.Group != null && u.Group.Type == GroupType.Class ? u.Group.Name : null),
                    StreamName = _context.GroupMemberships
                        .Where(m => m.StudentId == u.Id && m.IsActive && m.Scope == GroupType.Stream)
                        .Select(m => m.Group!.Name)
                        .FirstOrDefault()
                        ?? (u.Group != null && u.Group.Type == GroupType.Stream ? u.Group.Name : null)
                })
                .ToListAsync();

            return View(new GroupDetailsViewModel
            {
                Group = group,
                Members = members
            });
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
                // Faqat nomni yangilaymiz: Type/Code/IsArchived kabi maydonlar
                // qisman bind qilingan modelda yo'q — ularni saqlab qolish shart.
                var existing = await _context.Groups.FindAsync(id);
                if (existing == null) return NotFound();

                existing.Name = group.Name;
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(group);
        }

        // Guruhni o'chirish sahifasi
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var group = await _context.Groups.FirstOrDefaultAsync(g => g.Id == id);
            if (group == null) return NotFound();

            return View(group);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
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