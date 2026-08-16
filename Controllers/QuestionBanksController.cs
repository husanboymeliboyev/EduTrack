using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class QuestionBanksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITeacherAccessService _teacherAccess;

        public QuestionBanksController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITeacherAccessService teacherAccess)
        {
            _context = context;
            _userManager = userManager;
            _teacherAccess = teacherAccess;
        }

        // O'qituvchining o'z fanlaridagi barcha banklar ro'yxati
        public async Task<IActionResult> Index(int? subjectId)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var mySubjects = await _teacherAccess.GetTeacherSubjectsAsync(teacherId);
            var mySubjectIds = mySubjects.Select(s => s.Id).ToList();

            ViewBag.Subjects = new SelectList(mySubjects, "Id", "Name", subjectId);

            var query = _context.QuestionBanks
                .Include(b => b.Subject)
                .Include(b => b.Group)
                .Include(b => b.Questions)
                .Where(b => mySubjectIds.Contains(b.SubjectId));

            if (subjectId.HasValue)
            {
                query = query.Where(b => b.SubjectId == subjectId);
            }

            var banks = await query
                .OrderBy(b => b.Subject!.Name)
                .ThenBy(b => b.Name)
                .ToListAsync();

            return View(banks);
        }

        // Yangi bank yaratish sahifasi
        public async Task<IActionResult> Create()
        {
            await LoadDropdownsAsync();
            return View(new QuestionBankFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(QuestionBankFormViewModel model)
        {
            var teacherId = _userManager.GetUserId(User)!;
            var subject = await _teacherAccess.GetOwnedSubjectAsync(teacherId, model.SubjectId);

            if (subject == null)
            {
                ModelState.AddModelError(string.Empty, "Noto'g'ri fan tanlandi.");
            }
            else if (model.GroupId.HasValue)
            {
                var validGroup = await _context.GroupSubjects
                    .AnyAsync(gs => gs.SubjectId == model.SubjectId && gs.GroupId == model.GroupId);
                if (!validGroup)
                {
                    ModelState.AddModelError(string.Empty, "Tanlangan guruh bu fanga tegishli emas.");
                }
            }

            if (ModelState.IsValid)
            {
                var bank = new QuestionBank
                {
                    Name = model.Name,
                    SubjectId = model.SubjectId,
                    GroupId = model.GroupId
                };

                _context.QuestionBanks.Add(bank);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Savollar banki yaratildi.";
                return RedirectToAction(nameof(Index));
            }

            await LoadDropdownsAsync();
            return View(model);
        }

        // Bankni tahrirlash sahifasi
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var bank = await _context.QuestionBanks.FirstOrDefaultAsync(b => b.Id == id);
            if (bank == null) return NotFound();

            var teacherId = _userManager.GetUserId(User)!;
            var owns = await _teacherAccess.OwnsSubjectAsync(teacherId, bank.SubjectId);
            if (!owns) return Forbid();

            var model = new QuestionBankFormViewModel
            {
                Id = bank.Id,
                Name = bank.Name,
                SubjectId = bank.SubjectId,
                GroupId = bank.GroupId
            };

            await LoadDropdownsAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, QuestionBankFormViewModel model)
        {
            if (id != model.Id) return NotFound();

            var bank = await _context.QuestionBanks.FirstOrDefaultAsync(b => b.Id == id);
            if (bank == null) return NotFound();

            var teacherId = _userManager.GetUserId(User)!;
            var owns = await _teacherAccess.OwnsSubjectAsync(teacherId, bank.SubjectId);
            if (!owns) return Forbid();

            var subject = await _teacherAccess.GetOwnedSubjectAsync(teacherId, model.SubjectId);
            if (subject == null)
            {
                ModelState.AddModelError(string.Empty, "Noto'g'ri fan tanlandi.");
            }
            else if (model.GroupId.HasValue)
            {
                var validGroup = await _context.GroupSubjects
                    .AnyAsync(gs => gs.SubjectId == model.SubjectId && gs.GroupId == model.GroupId);
                if (!validGroup)
                {
                    ModelState.AddModelError(string.Empty, "Tanlangan guruh bu fanga tegishli emas.");
                }
            }

            if (ModelState.IsValid)
            {
                bank.Name = model.Name;
                bank.SubjectId = model.SubjectId;
                bank.GroupId = model.GroupId;

                await _context.SaveChangesAsync();
                TempData["Success"] = "Bank yangilandi.";
                return RedirectToAction(nameof(Index));
            }

            await LoadDropdownsAsync();
            return View(model);
        }

        // Bankni o'chirish sahifasi
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var bank = await _context.QuestionBanks
                .Include(b => b.Subject)
                .Include(b => b.Group)
                .Include(b => b.Questions)
                .Include(b => b.Exams)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bank == null) return NotFound();

            var teacherId = _userManager.GetUserId(User)!;
            var owns = await _teacherAccess.OwnsSubjectAsync(teacherId, bank.SubjectId);
            if (!owns) return Forbid();

            return View(bank);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bank = await _context.QuestionBanks
                .Include(b => b.Questions)
                .Include(b => b.Exams)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bank == null) return NotFound();

            var teacherId = _userManager.GetUserId(User)!;
            var owns = await _teacherAccess.OwnsSubjectAsync(teacherId, bank.SubjectId);
            if (!owns) return Forbid();

            // Ma'lumot yo'qolishining oldini olish: bankda savol yoki unga bog'langan
            // imtihon bo'lsa, o'chirishga ruxsat berilmaydi.
            if (bank.Questions.Any())
            {
                TempData["Error"] = $"Bu bankda {bank.Questions.Count} ta savol mavjud, shuning uchun o'chirib bo'lmaydi. " +
                    "Avval savollarni boshqa bankka ko'chiring yoki ularni o'chiring.";
                return RedirectToAction(nameof(Index));
            }

            if (bank.Exams.Any())
            {
                TempData["Error"] = $"Bu bankka bog'langan {bank.Exams.Count} ta imtihon mavjud, shuning uchun o'chirib bo'lmaydi.";
                return RedirectToAction(nameof(Index));
            }

            _context.QuestionBanks.Remove(bank);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Bank o'chirildi.";
            return RedirectToAction(nameof(Index));
        }

        // Bankni savollari bilan birga majburiy o'chirish. Faqat bog'langan imtihon
        // bo'lmagan hollarda ishlaydi (imtihon bo'lsa, avval Exam boshqaruvi orqali
        // hal qilinishi kerak — natijalar/baholarga ta'sir qilishi mumkin).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceDeleteConfirmed(int id)
        {
            var bank = await _context.QuestionBanks
                .Include(b => b.Questions)
                    .ThenInclude(q => q.Options)
                .Include(b => b.Exams)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bank == null) return NotFound();

            var teacherId = _userManager.GetUserId(User)!;
            var owns = await _teacherAccess.OwnsSubjectAsync(teacherId, bank.SubjectId);
            if (!owns) return Forbid();

            if (bank.Exams.Any())
            {
                TempData["Error"] = $"Bu bankka {bank.Exams.Count} ta imtihon bog'langan, shuning uchun o'chirib bo'lmaydi. " +
                    "Avval imtihonlarni arxivlang yoki o'chiring.";
                return RedirectToAction(nameof(Index));
            }

            var questionCount = bank.Questions.Count;

            foreach (var question in bank.Questions)
            {
                _context.AnswerOptions.RemoveRange(question.Options);
            }
            _context.Questions.RemoveRange(bank.Questions);
            _context.QuestionBanks.Remove(bank);

            await _context.SaveChangesAsync();
            TempData["Success"] = $"\"{bank.Name}\" banki va uning {questionCount} ta savoli butunlay o'chirildi.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadDropdownsAsync()
        {
            var teacherId = _userManager.GetUserId(User)!;
            var mySubjects = await _teacherAccess.GetTeacherSubjectsAsync(teacherId);
            ViewBag.Subjects = new SelectList(mySubjects, "Id", "Name");

            var subjectIds = mySubjects.Select(s => s.Id).ToList();
            var groupLinks = await _context.GroupSubjects
                .Where(gs => subjectIds.Contains(gs.SubjectId))
                .Include(gs => gs.Group)
                .Select(gs => new { gs.SubjectId, GroupId = gs.Group!.Id, GroupName = gs.Group.Name })
                .ToListAsync();

            // Fan tanlanganda, shu fanga tegishli guruhlar dropdown'ga JS orqali yuklanadi
            // (mavjud Fan -> Guruh dinamik yuklash naqshiga mos)
            ViewBag.SubjectGroupsJson = System.Text.Json.JsonSerializer.Serialize(
                groupLinks.GroupBy(g => g.SubjectId).ToDictionary(
                    g => g.Key,
                    g => g.Select(x => new { id = x.GroupId, name = x.GroupName }).ToList()
                )
            );
        }
    }
}
