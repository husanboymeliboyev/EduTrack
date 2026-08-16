using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentExamsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IGradeSyncService _gradeSyncService;

        public StudentExamsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IGradeSyncService gradeSyncService)
        {
            _context = context;
            _userManager = userManager;
            _gradeSyncService = gradeSyncService;
        }

        // Talaba uchun barcha imtihonlar ro'yxati (topshirilgan/topshirilmagan holati bilan)
        public async Task<IActionResult> Index()
        {
            var studentId = _userManager.GetUserId(User);

            var user = await _userManager.GetUserAsync(User);
            if (user?.GroupId == null)
            {
                return View(new List<Exam>());
            }

            // Talaba guruhiga tegishli fanlar ID'larini olamiz
            var mySubjectIds = await _context.GroupSubjects
                .Where(gs => gs.GroupId == user.GroupId)
                .Select(gs => gs.SubjectId)
                .ToListAsync();

            var exams = await _context.Exams
                .Include(e => e.Subject)
                .Include(e => e.Results.Where(r => r.StudentId == studentId))
                .Where(e => mySubjectIds.Contains(e.SubjectId) && (e.GroupId == null || e.GroupId == user.GroupId))
                .OrderByDescending(e => e.CreatedDate)
                .ToListAsync();

            return View(exams);
        }

        // Imtihonni boshlash: tasodifiy savollarni tanlab, timer bilan sahifa ko'rsatiladi
        public async Task<IActionResult> Take(int id)
        {
            var studentId = _userManager.GetUserId(User);

            var exam = await _context.Exams
                .Include(e => e.Subject)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null) return NotFound();

            // Imtihon vaqtinchalik yopiq bo'lishi mumkin (qo'lda yopilgan yoki hali OpenAt
            // vaqti kelmagan yoki CloseAt vaqti allaqachon o'tgan) — bunday holatda talaba
            // imtihonni boshlay olmaydi, garchi u ro'yxatda ko'rinsa ham.
            if (!exam.IsCurrentlyOpen)
            {
                TempData["Error"] = "Bu imtihon hozir yopiq. Ochilish vaqtini o'qituvchingizdan so'rang.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            var subjectMatches = user?.GroupId != null && await _context.GroupSubjects
      .AnyAsync(gs => gs.GroupId == user.GroupId && gs.SubjectId == exam.SubjectId);
            var groupMatches = exam.GroupId == null || exam.GroupId == user?.GroupId;
            var belongsToStudent = subjectMatches && groupMatches;

            if (!belongsToStudent)
            {
                TempData["Error"] = "Bu imtihon sizning guruhingizga tegishli emas.";
                return RedirectToAction(nameof(Index));
            }
            var alreadyTaken = await _context.ExamResults
                .AnyAsync(r => r.ExamId == id && r.StudentId == studentId);
            if (alreadyTaken)
            {
                TempData["Error"] = "Siz bu imtihonni allaqachon topshirgansiz.";
                return RedirectToAction(nameof(Index));
            }

            // Talaba bu imtihonni ilgari boshlaganmi? Bo'lsa, o'sha boshlanish vaqtidan
            // qolgan vaqtni hisoblaymiz (sahifa yangilansa ham timer to'g'ri ishlashi uchun).
            var attempt = await _context.ExamAttempts
                .FirstOrDefaultAsync(a => a.ExamId == id && a.StudentId == studentId);

            if (attempt == null)
            {
                attempt = new ExamAttempt
                {
                    ExamId = id,
                    StudentId = studentId!,
                    StartedDate = DateTime.Now
                };
                _context.ExamAttempts.Add(attempt);
                await _context.SaveChangesAsync();
            }

            var elapsedSeconds = (int)(DateTime.Now - attempt.StartedDate).TotalSeconds;
            var totalSeconds = exam.DurationMinutes * 60;
            var remainingSeconds = Math.Max(0, totalSeconds - elapsedSeconds);

            if (remainingSeconds == 0)
            {
                // Vaqt allaqachon tugagan (masalan, talaba sahifani yopib, ancha vaqtdan keyin qaytgan)
                TempData["Error"] = "Bu imtihon uchun vaqt tugagan. Iltimos, o'qituvchingiz bilan bog'laning.";
                return RedirectToAction(nameof(Index));
            }

            // Savollar imtihon bog'langan aniq Savollar bankidan tanlanadi (masalan
            // "1-oraliq" savollari "2-oraliq" bilan aralashmasligi shu orqali kafolatlanadi).
            // Qo'shimcha ravishda, savol darajasidagi GroupId ham hisobga olinadi: agar savol
            // aniq bir guruhga belgilangan bo'lsa (masalan faqat 1-guruh uchun), boshqa
            // guruhdagi talaba uni ko'rmasligi kerak. null = barcha guruhlar uchun umumiy.
            var questions = await _context.Questions
                .Include(q => q.Options)
                .Where(q => q.QuestionBankId == exam.QuestionBankId
                         && (q.GroupId == null || q.GroupId == user!.GroupId))
                .ToListAsync();

            if (questions.Count < exam.QuestionCount)
            {
                TempData["Error"] = "Bu imtihon uchun savollar yetarli emas. O'qituvchi bilan bog'laning.";
                return RedirectToAction(nameof(Index));
            }

            var rnd = new Random();
            var selected = questions.OrderBy(q => rnd.Next()).Take(exam.QuestionCount).ToList();

            var model = new TakeExamViewModel
            {
                ExamId = exam.Id,
                Title = exam.Title,
                SubjectName = exam.Subject?.Name ?? string.Empty,
                DurationMinutes = exam.DurationMinutes,
                RemainingSeconds = remainingSeconds,
                Questions = selected.Select(q => new ExamQuestionItem
                {
                    QuestionId = q.Id,
                    Text = q.Text,
                    ImagePath = q.ImagePath,
                    Options = q.Options.OrderBy(o => rnd.Next())
                         .Select(o => new ExamOptionItem { Id = o.Id, Text = o.Text }).ToList()
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int examId, List<int> questionIds, Dictionary<int, int> answers,
            int tabSwitchCount = 0, bool exitedFullscreen = false)
        {
            var studentId = _userManager.GetUserId(User);
            if (studentId == null) return Forbid();

            var alreadyTaken = await _context.ExamResults
                .AnyAsync(r => r.ExamId == examId && r.StudentId == studentId);
            if (alreadyTaken)
            {
                TempData["Error"] = "Siz bu imtihonni allaqachon topshirgansiz.";
                return RedirectToAction(nameof(Index));
            }

            var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId);
            if (exam == null) return NotFound();

            // Siz tanlagan siyosat: agar imtihon talaba boshlagandan keyin yopilib qolsa,
            // Submit ham rad etiladi — talaba javoblari qabul qilinmaydi.
            if (!exam.IsCurrentlyOpen)
            {
                TempData["Error"] = "Bu imtihon yopib qo'yildi, javoblaringiz qabul qilinmadi. O'qituvchingiz bilan bog'laning.";
                return RedirectToAction(nameof(Index));
            }

            var attempt = await _context.ExamAttempts
                .FirstOrDefaultAsync(a => a.ExamId == examId && a.StudentId == studentId);

            if (attempt == null)
            {
                TempData["Error"] = "Imtihon to'g'ri boshlanmagan. Iltimos, imtihonni ro'yxatdan qaytadan boshlang.";
                return RedirectToAction(nameof(Index));
            }

            var elapsedSeconds = (DateTime.Now - attempt.StartedDate).TotalSeconds;
            var allowedSeconds = exam.DurationMinutes * 60 + 120;

            if (elapsedSeconds > allowedSeconds)
            {
                TempData["Error"] = "Imtihon uchun belgilangan vaqt tugagan. Javoblaringiz qabul qilinmadi. O'qituvchingiz bilan bog'laning.";
                return RedirectToAction(nameof(Index));
            }

            questionIds ??= new List<int>();
            answers ??= new Dictionary<int, int>();

            var correctOptionIds = await _context.AnswerOptions
                .Where(o => questionIds.Contains(o.QuestionId) && o.IsCorrect)
                .ToDictionaryAsync(o => o.QuestionId, o => o.Id);

            int correctCount = 0;
            foreach (var qid in questionIds)
            {
                if (answers.TryGetValue(qid, out var selectedOptionId)
                    && correctOptionIds.TryGetValue(qid, out var correctId)
                    && selectedOptionId == correctId)
                {
                    correctCount++;
                }
            }

            var result = new ExamResult
            {
                ExamId = examId,
                StudentId = studentId,
                TotalQuestions = questionIds.Count,
                CorrectAnswers = correctCount,
                CompletedDate = DateTime.Now,
                QuestionIdsJson = System.Text.Json.JsonSerializer.Serialize(questionIds),
                SelectedOptionIdsJson = System.Text.Json.JsonSerializer.Serialize(answers),
                TabSwitchCount = tabSwitchCount,
                ExitedFullscreen = exitedFullscreen
            };

            _context.ExamResults.Add(result);
            await _context.SaveChangesAsync();

            // Natija hozircha "tasdiq kutilmoqda" holatida saqlanadi.
            // Baholarga sinxronlash faqat o'qituvchi ApproveResult orqali tasdiqlagandan keyin sodir bo'ladi.

            return RedirectToAction(nameof(Result), new { id = result.Id });
        }

        // Talaba o'zining imtihon natijasini ko'radi
        public async Task<IActionResult> Result(int id)
        {
            var studentId = _userManager.GetUserId(User);

            var result = await _context.ExamResults
                .Include(r => r.Exam)
                    .ThenInclude(e => e!.Subject)
                .FirstOrDefaultAsync(r => r.Id == id && r.StudentId == studentId);

            if (result == null) return NotFound();

            return View(result);
        }
    }
}