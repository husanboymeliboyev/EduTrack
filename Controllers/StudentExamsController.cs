using EduTrack.Data;
using EduTrack.Models;
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

        public StudentExamsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Talaba uchun barcha imtihonlar ro'yxati (topshirilgan/topshirilmagan holati bilan)
        public async Task<IActionResult> Index()
        {
            var studentId = _userManager.GetUserId(User);

            var exams = await _context.Exams
                .Include(e => e.Subject)
                .Include(e => e.Results.Where(r => r.StudentId == studentId))
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

            var alreadyTaken = await _context.ExamResults
                .AnyAsync(r => r.ExamId == id && r.StudentId == studentId);
            if (alreadyTaken)
            {
                TempData["Error"] = "Siz bu imtihonni allaqachon topshirgansiz.";
                return RedirectToAction(nameof(Index));
            }

            var questions = await _context.Questions
                .Include(q => q.Options)
                .Where(q => q.SubjectId == exam.SubjectId)
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
                Questions = selected.Select(q => new ExamQuestionItem
                {
                    QuestionId = q.Id,
                    Text = q.Text,
                    // Variantlarni ham aralashtiramiz, to'g'ri javob doim bir joyda bo'lib qolmasin
                    Options = q.Options.OrderBy(o => rnd.Next())
                        .Select(o => new ExamOptionItem { Id = o.Id, Text = o.Text }).ToList()
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int examId, List<int> questionIds, Dictionary<int, int> answers)
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

            questionIds ??= new List<int>();
            answers ??= new Dictionary<int, int>();

            // Savollarga tegishli to'g'ri javoblarni bazadan olamiz (talaba tomonidan yuborilgan ma'lumotga ishonmaymiz)
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
                SelectedOptionIdsJson = System.Text.Json.JsonSerializer.Serialize(answers)
            };

            _context.ExamResults.Add(result);
            await _context.SaveChangesAsync();

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