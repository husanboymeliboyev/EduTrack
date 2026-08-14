using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class QuestionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileUploadService _fileUploadService;
        private readonly IQuestionImportService _questionImportService;

        private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB
        private const long MaxImportFileSizeBytes = 2 * 1024 * 1024; // 2 MB

        public QuestionsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IFileUploadService fileUploadService,
            IQuestionImportService questionImportService)
        {
            _context = context;
            _userManager = userManager;
            _fileUploadService = fileUploadService;
            _questionImportService = questionImportService;
        }

        // O'qituvchining barcha savollari (fani bo'yicha)
        public async Task<IActionResult> Index(int? subjectId)
        {
            var teacherId = _userManager.GetUserId(User);
            var mySubjects = await _context.Subjects.Where(s => s.TeacherId == teacherId).ToListAsync();
            ViewBag.Subjects = new SelectList(mySubjects, "Id", "Name", subjectId);

            var query = _context.Questions
                .Include(q => q.Subject)
                .Include(q => q.Options)
                .Where(q => q.Subject!.TeacherId == teacherId);

            if (subjectId.HasValue)
            {
                query = query.Where(q => q.SubjectId == subjectId);
            }

            var questions = await query.ToListAsync();
            return View(questions);
        }

        // Yangi savol qo'shish sahifasi
        public async Task<IActionResult> Create()
        {
            await LoadSubjectsAsync();
            return View(new QuestionFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(QuestionFormViewModel model)
        {
            var teacherId = _userManager.GetUserId(User);
            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == model.SubjectId && s.TeacherId == teacherId);

            if (subject == null)
            {
                ModelState.AddModelError(string.Empty, "Noto'g'ri fan tanlandi.");
            }

            var optionTexts = new[] { model.Option1, model.Option2, model.Option3, model.Option4 };
            var filledCount = optionTexts.Count(o => !string.IsNullOrWhiteSpace(o));

            if (model.CorrectOption < 1 || model.CorrectOption > filledCount)
            {
                ModelState.AddModelError(nameof(model.CorrectOption), "To'g'ri javob to'ldirilgan variantlar orasidan bo'lishi kerak.");
            }

            string? imagePath = null;
            if (model.ImageFile != null)
            {
                var uploadResult = await _fileUploadService.UploadAsync(model.ImageFile, MaxImageSizeBytes);
                if (!uploadResult.Success && !string.IsNullOrEmpty(uploadResult.ErrorMessage))
                {
                    ModelState.AddModelError(nameof(model.ImageFile), uploadResult.ErrorMessage);
                }
                else if (uploadResult.Success)
                {
                    imagePath = uploadResult.RelativePath;
                }
            }

            if (ModelState.IsValid)
            {
                var question = new Question
                {
                    Text = model.Text,
                    SubjectId = model.SubjectId,
                    GroupId = model.GroupId,
                    ImagePath = imagePath
                };

                for (int i = 0; i < optionTexts.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(optionTexts[i]))
                    {
                        question.Options.Add(new AnswerOption
                        {
                            Text = optionTexts[i]!,
                            IsCorrect = (i + 1) == model.CorrectOption
                        });
                    }
                }

                _context.Questions.Add(question);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Savol qo'shildi.";
                return RedirectToAction(nameof(Index));
            }

            await LoadSubjectsAsync();
            return View(model);
        }

        // Savolni tahrirlash sahifasi
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var teacherId = _userManager.GetUserId(User);
            var question = await _context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id && q.Subject!.TeacherId == teacherId);

            if (question == null) return NotFound();

            var options = question.Options.ToList();
            var model = new QuestionFormViewModel
            {
                Id = question.Id,
                SubjectId = question.SubjectId,
                GroupId = question.GroupId,
                Text = question.Text,
                ExistingImagePath = question.ImagePath,
                Option1 = options.ElementAtOrDefault(0)?.Text ?? string.Empty,
                Option2 = options.ElementAtOrDefault(1)?.Text ?? string.Empty,
                Option3 = options.ElementAtOrDefault(2)?.Text,
                Option4 = options.ElementAtOrDefault(3)?.Text,
                CorrectOption = options.FindIndex(o => o.IsCorrect) + 1
            };

            await LoadSubjectsAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, QuestionFormViewModel model)
        {
            if (id != model.Id) return NotFound();

            var teacherId = _userManager.GetUserId(User);
            var question = await _context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id && q.Subject!.TeacherId == teacherId);

            if (question == null) return NotFound();

            var optionTexts = new[] { model.Option1, model.Option2, model.Option3, model.Option4 };
            var filledCount = optionTexts.Count(o => !string.IsNullOrWhiteSpace(o));

            if (model.CorrectOption < 1 || model.CorrectOption > filledCount)
            {
                ModelState.AddModelError(nameof(model.CorrectOption), "To'g'ri javob to'ldirilgan variantlar orasidan bo'lishi kerak.");
            }

            string? newImagePath = question.ImagePath;
            if (model.ImageFile != null)
            {
                var uploadResult = await _fileUploadService.UploadAsync(model.ImageFile, MaxImageSizeBytes);
                if (!uploadResult.Success && !string.IsNullOrEmpty(uploadResult.ErrorMessage))
                {
                    ModelState.AddModelError(nameof(model.ImageFile), uploadResult.ErrorMessage);
                }
                else if (uploadResult.Success)
                {
                    newImagePath = uploadResult.RelativePath;
                }
            }

            if (ModelState.IsValid)
            {
               question.Text = model.Text;
question.SubjectId = model.SubjectId;
question.GroupId = model.GroupId;
question.ImagePath = newImagePath;

                _context.AnswerOptions.RemoveRange(question.Options);
                question.Options.Clear();

                for (int i = 0; i < optionTexts.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(optionTexts[i]))
                    {
                        question.Options.Add(new AnswerOption
                        {
                            Text = optionTexts[i]!,
                            IsCorrect = (i + 1) == model.CorrectOption
                        });
                    }
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Savol yangilandi.";
                return RedirectToAction(nameof(Index));
            }

            model.ExistingImagePath = question.ImagePath;
            await LoadSubjectsAsync();
            return View(model);
        }

        // Savolni o'chirish sahifasi
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var teacherId = _userManager.GetUserId(User);
            var question = await _context.Questions
                .Include(q => q.Subject)
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id && q.Subject!.TeacherId == teacherId);

            if (question == null) return NotFound();

            return View(question);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var teacherId = _userManager.GetUserId(User);
            var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == id && q.Subject!.TeacherId == teacherId);

            if (question != null)
            {
                _context.Questions.Remove(question);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // ===== LaTeX-shablon orqali ommaviy import =====

        // Shablon faylni yuklab berish
        public IActionResult DownloadTemplate()
        {
            var text = _questionImportService.CreateTemplateText();
            var bytes = Encoding.UTF8.GetBytes(text);
            return File(bytes, "text/plain", "savollar_shabloni.txt");
        }

        // Yuklash sahifasi
        public async Task<IActionResult> Import()
        {
            await LoadSubjectsAsync();
            return View(new QuestionImportUploadViewModel());
        }

        // Fayl yuklanadi, o'qiladi, oldindan ko'rish ko'rsatiladi (hali saqlanmaydi)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(QuestionImportUploadViewModel model)
        {
            var teacherId = _userManager.GetUserId(User);
            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == model.SubjectId && s.TeacherId == teacherId);

            if (subject == null)
            {
                ModelState.AddModelError(string.Empty, "Noto'g'ri fan tanlandi.");
            }

            if (model.File == null || model.File.Length == 0)
            {
                ModelState.AddModelError(nameof(model.File), "Fayl tanlanmadi.");
            }
            else if (Path.GetExtension(model.File.FileName).ToLowerInvariant() != ".txt")
            {
                ModelState.AddModelError(nameof(model.File), "Faqat .txt fayl qabul qilinadi.");
            }
            else if (model.File.Length > MaxImportFileSizeBytes)
            {
                ModelState.AddModelError(nameof(model.File), "Fayl hajmi 2 MB dan oshmasligi kerak.");
            }

            if (!ModelState.IsValid || subject == null)
            {
                await LoadSubjectsAsync();
                return View(model);
            }

            string rawText;
            using (var reader = new StreamReader(model.File!.OpenReadStream(), Encoding.UTF8))
            {
                rawText = await reader.ReadToEndAsync();
            }

            var parseResult = _questionImportService.Parse(rawText);

            string? groupName = null;
            if (model.GroupId.HasValue)
            {
                groupName = (await _context.Groups.FindAsync(model.GroupId.Value))?.Name;
            }

            var preview = new QuestionImportPreviewViewModel
            {
                SubjectId = subject.Id,
                SubjectName = subject.Name,
                GroupId = model.GroupId,
                GroupName = groupName,
                Questions = parseResult.Questions,
                Errors = parseResult.Errors,
                RawText = rawText
            };

            return View("ImportPreview", preview);
        }

        // Tasdiqlash — hammasi bir vaqtda bazaga saqlanadi
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportConfirm(int subjectId, int? groupId, string rawText)
        {
            var teacherId = _userManager.GetUserId(User);
            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId && s.TeacherId == teacherId);
            if (subject == null) return NotFound();

            var parseResult = _questionImportService.Parse(rawText);
            if (parseResult.HasErrors)
            {
                TempData["Error"] = "Faylda xatolar bor edi, saqlanmadi. Iltimos qaytadan yuklang.";
                return RedirectToAction(nameof(Import));
            }

            foreach (var pq in parseResult.Questions)
            {
                var question = new Question
                {
                    Text = pq.Text,
                    SubjectId = subject.Id,
                    GroupId = groupId
                };

                foreach (var po in pq.Options)
                {
                    question.Options.Add(new AnswerOption
                    {
                        Text = po.Text,
                        IsCorrect = po.IsCorrect
                    });
                }

                _context.Questions.Add(question);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"{parseResult.Questions.Count} ta savol muvaffaqiyatli qo'shildi.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadSubjectsAsync()
        {
            var teacherId = _userManager.GetUserId(User);
            var mySubjects = await _context.Subjects.Where(s => s.TeacherId == teacherId).ToListAsync();
            ViewBag.Subjects = new SelectList(mySubjects, "Id", "Name");

            var subjectIds = mySubjects.Select(s => s.Id).ToList();
            var groupLinks = await _context.GroupSubjects
                .Where(gs => subjectIds.Contains(gs.SubjectId))
                .Include(gs => gs.Group)
                .Select(gs => new { gs.SubjectId, GroupId = gs.Group!.Id, GroupName = gs.Group.Name })
                .ToListAsync();

            // Har bir fan uchun qaysi guruhlar tegishli ekanini JS'ga uzatamiz (fan tanlanganda dropdown yangilanadi)
            ViewBag.SubjectGroupsJson = System.Text.Json.JsonSerializer.Serialize(
                groupLinks.GroupBy(g => g.SubjectId).ToDictionary(
                    g => g.Key,
                    g => g.Select(x => new { id = x.GroupId, name = x.GroupName }).ToList()
                )
            );
        }
    }
}