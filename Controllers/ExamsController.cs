using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class ExamsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IGradeSyncService _gradeSyncService;

        public ExamsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IGradeSyncService gradeSyncService)
        {
            _context = context;
            _userManager = userManager;
            _gradeSyncService = gradeSyncService;
        }

        public async Task<IActionResult> Index()
        {
            var teacherId = _userManager.GetUserId(User);

            var exams = await _context.Exams
                .Include(e => e.Subject)
                .Include(e => e.Group)
                .Include(e => e.QuestionBank)
                .Include(e => e.Results)
                .Where(e => e.Subject!.TeacherId == teacherId)
                .OrderByDescending(e => e.CreatedDate)
                .ToListAsync();

            return View(exams);
        }

        public async Task<IActionResult> Create()
        {
            await LoadFormDataAsync();
            return View(new Exam());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,SubjectId,QuestionBankId,GroupId,QuestionCount,DurationMinutes,OpenAt,CloseAt")] Exam exam)
        {
            var teacherId = _userManager.GetUserId(User);
            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == exam.SubjectId && s.TeacherId == teacherId);

            if (subject == null)
            {
                ModelState.AddModelError(string.Empty, "Noto'g'ri fan tanlandi.");
            }
            else
            {
                var bank = await _context.QuestionBanks
                    .FirstOrDefaultAsync(b => b.Id == exam.QuestionBankId && b.SubjectId == exam.SubjectId);

                if (bank == null)
                {
                    ModelState.AddModelError(string.Empty, "Noto'g'ri savollar banki tanlandi.");
                }
                else
                {
                    var availableCount = await _context.Questions.CountAsync(q => q.QuestionBankId == exam.QuestionBankId);
                    if (exam.QuestionCount > availableCount)
                    {
                        ModelState.AddModelError(nameof(exam.QuestionCount),
                            $"Bu bankda jami {availableCount} ta savol bor. Savollar sonini kamaytiring yoki avval Savollar bankiga qo'shing.");
                    }
                }

                if (exam.GroupId.HasValue)
                {
                    var validGroup = await _context.GroupSubjects
                        .AnyAsync(gs => gs.SubjectId == exam.SubjectId && gs.GroupId == exam.GroupId);
                    if (!validGroup)
                    {
                        ModelState.AddModelError(string.Empty, "Tanlangan guruh bu fanga tegishli emas.");
                    }
                }
            }

            if (exam.OpenAt.HasValue && exam.CloseAt.HasValue && exam.CloseAt.Value <= exam.OpenAt.Value)
            {
                ModelState.AddModelError(nameof(exam.CloseAt), "Yopilish vaqti ochilish vaqtidan keyin bo'lishi kerak.");
            }

            if (ModelState.IsValid)
            {
                _context.Exams.Add(exam);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Imtihon yaratildi.";
                return RedirectToAction(nameof(Index));
            }

            await LoadFormDataAsync();
            return View(exam);
        }

        // Imtihonni tahrirlash sahifasi
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var teacherId = _userManager.GetUserId(User);
            var exam = await _context.Exams
                .Include(e => e.Subject)
                .Include(e => e.QuestionBank)
                .Include(e => e.Results)
                .FirstOrDefaultAsync(e => e.Id == id && e.Subject!.TeacherId == teacherId);

            if (exam == null) return NotFound();

            ViewBag.HasResults = exam.Results.Any();
            await LoadFormDataAsync();
            return View(exam);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,SubjectId,QuestionBankId,GroupId,QuestionCount,DurationMinutes,IsOpen,OpenAt,CloseAt")] Exam formExam)
        {
            if (id != formExam.Id) return NotFound();

            var teacherId = _userManager.GetUserId(User);
            var exam = await _context.Exams
                .Include(e => e.Subject)
                .Include(e => e.QuestionBank)
                .Include(e => e.Results)
                .FirstOrDefaultAsync(e => e.Id == id && e.Subject!.TeacherId == teacherId);

            if (exam == null) return NotFound();

            var hasResults = exam.Results.Any();
            ViewBag.HasResults = hasResults;

            // Xavfsizlik: agar imtihonda allaqachon talaba natijalari bo'lsa, savollar bankini,
            // savollar sonini, davomiylikni yoki fanni o'zgartirishga ruxsat berilmaydi —
            // aks holda mavjud natijalar bilan yangi sozlamalar mos kelmay qoladi.
            // Bunday holatda shu maydonlar e'tiborga olinmaydi, faqat quyidagilar o'zgaradi:
            // Title, GroupId, IsOpen, OpenAt, CloseAt.
            if (!hasResults)
            {
                var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == formExam.SubjectId && s.TeacherId == teacherId);
                if (subject == null)
                {
                    ModelState.AddModelError(string.Empty, "Noto'g'ri fan tanlandi.");
                }
                else
                {
                    var bank = await _context.QuestionBanks
                        .FirstOrDefaultAsync(b => b.Id == formExam.QuestionBankId && b.SubjectId == formExam.SubjectId);

                    if (bank == null)
                    {
                        ModelState.AddModelError(string.Empty, "Noto'g'ri savollar banki tanlandi.");
                    }
                    else
                    {
                        var availableCount = await _context.Questions.CountAsync(q => q.QuestionBankId == formExam.QuestionBankId);
                        if (formExam.QuestionCount > availableCount)
                        {
                            ModelState.AddModelError(nameof(formExam.QuestionCount),
                                $"Bu bankda jami {availableCount} ta savol bor. Savollar sonini kamaytiring yoki avval Savollar bankiga qo'shing.");
                        }
                    }
                }
            }

            if (formExam.GroupId.HasValue)
            {
                var validGroup = await _context.GroupSubjects
                    .AnyAsync(gs => gs.SubjectId == (hasResults ? exam.SubjectId : formExam.SubjectId) && gs.GroupId == formExam.GroupId);
                if (!validGroup)
                {
                    ModelState.AddModelError(string.Empty, "Tanlangan guruh bu fanga tegishli emas.");
                }
            }

            if (formExam.OpenAt.HasValue && formExam.CloseAt.HasValue && formExam.CloseAt.Value <= formExam.OpenAt.Value)
            {
                ModelState.AddModelError(nameof(formExam.CloseAt), "Yopilish vaqti ochilish vaqtidan keyin bo'lishi kerak.");
            }

            if (ModelState.IsValid)
            {
                exam.Title = formExam.Title;
                exam.GroupId = formExam.GroupId;
                exam.IsOpen = formExam.IsOpen;
                exam.OpenAt = formExam.OpenAt;
                exam.CloseAt = formExam.CloseAt;

                if (!hasResults)
                {
                    exam.SubjectId = formExam.SubjectId;
                    exam.QuestionBankId = formExam.QuestionBankId;
                    exam.QuestionCount = formExam.QuestionCount;
                    exam.DurationMinutes = formExam.DurationMinutes;
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Imtihon yangilandi.";
                return RedirectToAction(nameof(Index));
            }

            // Formani qayta ko'rsatishda, qulflangan maydonlar uchun asl (bazadagi) qiymatlarni
            // ko'rsatamiz, foydalanuvchi kiritgan (rad etilgan) qiymatlarni emas.
            if (hasResults)
            {
                formExam.SubjectId = exam.SubjectId;
                formExam.QuestionBankId = exam.QuestionBankId;
                formExam.QuestionCount = exam.QuestionCount;
                formExam.DurationMinutes = exam.DurationMinutes;
            }

            await LoadFormDataAsync();
            return View(formExam);
        }

        // Index sahifasidan bir bosishda Ochiq/Yopiq holatini almashtirish (tezkor tugma)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleOpen(int id)
        {
            var teacherId = _userManager.GetUserId(User);
            var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == id && e.Subject!.TeacherId == teacherId);

            if (exam == null) return NotFound();

            exam.IsOpen = !exam.IsOpen;
            await _context.SaveChangesAsync();

            TempData["Success"] = exam.IsOpen ? "Imtihon ochildi." : "Imtihon yopildi.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var teacherId = _userManager.GetUserId(User);
            var exam = await _context.Exams
                .Include(e => e.Subject)
                .Include(e => e.Group)
                .Include(e => e.Results)
                .FirstOrDefaultAsync(e => e.Id == id && e.Subject!.TeacherId == teacherId);

            if (exam == null) return NotFound();

            // Ma'lumot uchun: bu imtihon Baholash tarkibiga (GradeComponent) bog'langanmi —
            // bog'langan bo'lsa, o'chirilgach o'sha komponent "bog'lanmagan" holga o'tadi.
            var linkedComponent = await _context.GradeComponents.FirstOrDefaultAsync(c => c.ExamId == id);
            ViewBag.LinkedComponentName = linkedComponent?.Name;

            return View(exam);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var teacherId = _userManager.GetUserId(User);
            var exam = await _context.Exams
                .Include(e => e.Results)
                .FirstOrDefaultAsync(e => e.Id == id && e.Subject!.TeacherId == teacherId);

            if (exam != null)
            {
                if (exam.Results.Any())
                {
                    TempData["Error"] = "Bu imtihonda talabalar natijasi mavjud, shuning uchun o'chirib bo'lmaydi. " +
                        "Agar chindan ham o'chirmoqchi bo'lsangiz, pastdagi \"Majburiy o'chirish\" ni ishlating.";
                    return RedirectToAction(nameof(Delete), new { id });
                }

                _context.Exams.Remove(exam);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Imtihon o'chirildi.";
            }
            return RedirectToAction(nameof(Index));
        }

        // Talabalar natijasi bo'lsa ham majburan o'chirish — natijalar (ExamResult) va ularning
        // urinishlari (ExamAttempt) QAYTARIB BO'LMAYDIGAN tarzda o'chadi. Shuning uchun bu action
        // View'dagi qo'shimcha yozma tasdiqni (nomni qo'lda kiritish) talab qiladi.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceDeleteConfirmed(int id, string confirmTitle)
        {
            var teacherId = _userManager.GetUserId(User);
            var exam = await _context.Exams
                .Include(e => e.Results)
                .FirstOrDefaultAsync(e => e.Id == id && e.Subject!.TeacherId == teacherId);

            if (exam == null) return NotFound();

            if (!string.Equals(confirmTitle?.Trim(), exam.Title, StringComparison.Ordinal))
            {
                TempData["Error"] = "Tasdiqlash matni imtihon nomiga mos kelmadi. Hech narsa o'chirilmadi.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            var attempts = await _context.ExamAttempts.Where(a => a.ExamId == id).ToListAsync();

            // Bog'langan GradeComponent bo'lsa, u avtomatik "bog'lanmagan" holatga o'tadi
            // (SetNull), baholar o'zi o'chmaydi — faqat Exam, uning Result va Attempt'lari o'chadi.
            _context.ExamAttempts.RemoveRange(attempts);
            _context.ExamResults.RemoveRange(exam.Results);
            _context.Exams.Remove(exam);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"\"{exam.Title}\" imtihoni, shu jumladan {exam.Results.Count} ta talaba natijasi bilan birga o'chirildi.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Results(int? id)
        {
            if (id == null) return NotFound();

            var teacherId = _userManager.GetUserId(User);
            var exam = await _context.Exams
                .Include(e => e.Subject)
                .Include(e => e.Results)
                    .ThenInclude(r => r.Student)
                .FirstOrDefaultAsync(e => e.Id == id && e.Subject!.TeacherId == teacherId);

            if (exam == null) return NotFound();

            return View(exam);
        }

        // Talaba topshirgan imtihon natijasini o'qituvchi ko'rib chiqib tasdiqlaydi.
        // Tasdiqlangandan keyingina natija GradeSyncService orqali baholash jadvaliga sinxronlanadi.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveResult(int resultId, int examId)
        {
            var teacherId = _userManager.GetUserId(User);

            var result = await _context.ExamResults
                .Include(r => r.Exam)
                    .ThenInclude(e => e!.Subject)
                .FirstOrDefaultAsync(r => r.Id == resultId);

            if (result?.Exam?.Subject?.TeacherId != teacherId)
            {
                return Forbid();
            }

            if (!result.IsApproved)
            {
                result.IsApproved = true;
                await _context.SaveChangesAsync();
                await _gradeSyncService.SyncFromExamResultAsync(result.Id);
                TempData["Success"] = "Natija tasdiqlandi va baholash jadvaliga sinxronlandi.";
            }

            return RedirectToAction(nameof(Results), new { id = examId });
        }

        // Fan tanlanganda, shu fanga tegishli Guruhlar VA Savollar banklari JS orqali dropdown'larga yuklanadi
        private async Task LoadFormDataAsync()
        {
            var teacherId = _userManager.GetUserId(User);
            var mySubjects = await _context.Subjects
                .Where(s => s.TeacherId == teacherId)
                .ToListAsync();

            ViewBag.Subjects = new SelectList(mySubjects, "Id", "Name");

            var subjectIds = mySubjects.Select(s => s.Id).ToList();

            var subjectGroups = await _context.GroupSubjects
                .Where(gs => subjectIds.Contains(gs.SubjectId))
                .Include(gs => gs.Group)
                .Select(gs => new { gs.SubjectId, gs.GroupId, GroupName = gs.Group!.Name })
                .ToListAsync();

            ViewBag.SubjectGroupsJson = System.Text.Json.JsonSerializer.Serialize(subjectGroups);

            var bankLinks = await _context.QuestionBanks
                .Where(b => subjectIds.Contains(b.SubjectId))
                .Select(b => new { b.SubjectId, b.Id, b.Name })
                .ToListAsync();

            ViewBag.SubjectBanksJson = System.Text.Json.JsonSerializer.Serialize(
                bankLinks.GroupBy(b => b.SubjectId).ToDictionary(
                    g => g.Key,
                    g => g.Select(x => new { id = x.Id, name = x.Name }).ToList()
                )
            );
        }
    }
}
