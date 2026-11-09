using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    /// <summary>
    /// O'quvchini bitta sinfdan/guruhdan boshqasiga o'tkazish.
    /// Eski a'zolik o'chirilmaydi: tarix saqlanadi (EndDate + EndReason).
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class TransferController : Controller
    {
        private readonly IStudentTransferService _transfers;
        private readonly ApplicationDbContext _context;

        public TransferController(IStudentTransferService transfers, ApplicationDbContext context)
        {
            _transfers = transfers;
            _context = context;
        }

        /// <summary>O'quvchilarni qidirish va ro'yxat.</summary>
        public async Task<IActionResult> Index(string? q)
        {
            ViewBag.Query = q;
            var candidates = await _transfers.SearchAsync(q);
            return View(candidates);
        }

        /// <summary>O'tkazish tafsilotlari va forma.</summary>
        public async Task<IActionResult> Details(string id)
        {
            var details = await _transfers.GetDetailsAsync(id);
            if (details == null) return NotFound();

            await FillDropdownsAsync();
            return View(details);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Execute(
            string studentId,
            int? newClassGroupId,
            int? newStreamGroupId,
            DateTime date,
            string? reason)
        {
            var result = await _transfers.TransferAsync(new TransferRequest
            {
                StudentId = studentId,
                NewClassGroupId = newClassGroupId,
                NewStreamGroupId = newStreamGroupId,
                Date = date,
                Reason = reason,
            });

            if (!result.Success)
            {
                TempData["Error"] = result.Error;
            }
            else
            {
                TempData["Success"] = "O'tkazildi: " + string.Join("; ", result.Changes);
            }

            return RedirectToAction(nameof(Details), new { id = studentId });
        }

        private async Task FillDropdownsAsync()
        {
            ViewBag.Classes = await _context.Groups
                .AsNoTracking()
                .Where(g => g.Type == GroupType.Class && !g.IsArchived)
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name })
                .ToListAsync();

            ViewBag.Streams = await _context.Groups
                .AsNoTracking()
                .Where(g => g.Type == GroupType.Stream && !g.IsArchived)
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name })
                .ToListAsync();
        }
    }
}
