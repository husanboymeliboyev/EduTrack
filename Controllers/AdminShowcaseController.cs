using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminShowcaseController : Controller
    {
        private readonly IStudentShowcaseService _service;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminShowcaseController(
            IStudentShowcaseService service,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _service = service;
            _context = context;
            _userManager = userManager;
        }

        // Guruhlar ro'yxati
        public async Task<IActionResult> Index()
        {
            var groups = await _context.Groups
                .Include(g => g.Students)
                .OrderBy(g => g.Name)
                .ToListAsync();

            return View(groups);
        }

        // Katta ekran — Sinf ko'rinishi
        public async Task<IActionResult> ClassOverview(int groupId)
        {
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null) return NotFound();

            var overview = await _service.GetClassOverviewAsync(groupId);
            return View(overview);
        }

        // Tasodifiy talaba navbati — Admin bir yoki bir nechta guruhni tanlab sessiya
        // boshlaydi. Server server tomonida Current va Next talabani zaxiralaydi hamda
        // mavjud sessiya holatini (Current/Next) ko'rsatadi.
        public async Task<IActionResult> RandomDraw()
        {
            var model = await BuildRandomDrawViewModelAsync(null);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RandomDraw(List<int> groupIds)
        {
            var currentAdminId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(currentAdminId))
                return Challenge();

            var selectedIds = (groupIds ?? new List<int>()).Distinct().ToList();
            var model = await BuildRandomDrawViewModelAsync(selectedIds);

            if (selectedIds.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Kamida bitta guruhni tanlang.");
                return View(model);
            }

            // Server tomonida barcha tanlangan guruh ID'lari tekshiriladi.
            // Ko'rsatiladigan nom yoki mijoz matniga hech qachon ishonmaymiz.
            var validGroupIds = await _context.Groups
                .Where(g => selectedIds.Contains(g.Id))
                .Select(g => g.Id)
                .ToListAsync();

            if (validGroupIds.Count != selectedIds.Count)
            {
                ModelState.AddModelError(string.Empty, "Tanlangan guruhlar topilmadi. Iltimos, qayta urinib ko'ring.");
                return View(model);
            }

            var hasOpenSession = await _context.PitchSessions
                .AnyAsync(s => s.CreatedById == currentAdminId && s.Status == PitchSessionStatus.Open);

            if (hasOpenSession)
            {
                ModelState.AddModelError(string.Empty, "Sizda allaqachon ochiq taqdimot sessiyasi mavjud. Avval uni yakunlang.");
                return View(model);
            }

            // Nomzodlar faqat Student rolidagi va tanlangan guruhlarga biriktirilgan
            // accountlardan olinadi. GroupIdning o'zi role kafolati emas.
            var studentUsers = await _userManager.GetUsersInRoleAsync("Student");
            var candidateIds = studentUsers
                .Where(u => u.GroupId.HasValue && validGroupIds.Contains(u.GroupId.Value))
                .Select(u => u.Id)
                .Distinct()
                .ToList();

            if (candidateIds.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Tanlangan guruhlarda talaba topilmadi.");
                return View(model);
            }

            var now = DateTime.UtcNow;

            // Current (Faol) — kriptografik xavfsiz va xolis tanlov.
            var currentIndex = RandomNumberGenerator.GetInt32(candidateIds.Count);
            var currentStudentId = candidateIds[currentIndex];
            candidateIds.RemoveAt(currentIndex);

            var session = new PitchSession
            {
                CreatedById = currentAdminId,
                SelectedGroupIds = string.Join(",", validGroupIds),
                Status = PitchSessionStatus.Open,
                CreatedDate = now
            };

            session.Participants.Add(new PitchParticipant
            {
                StudentId = currentStudentId,
                QueuePosition = 1,
                Status = PitchParticipantStatus.Active,
                DrawnDate = now,
                ActivatedDate = now
            });

            // Next (Navbatda) — ikkinchi talaba bo'lsa zaxiralanadi.
            if (candidateIds.Count > 0)
            {
                var nextIndex = RandomNumberGenerator.GetInt32(candidateIds.Count);
                var nextStudentId = candidateIds[nextIndex];

                session.Participants.Add(new PitchParticipant
                {
                    StudentId = nextStudentId,
                    QueuePosition = 2,
                    Status = PitchParticipantStatus.Queued,
                    DrawnDate = now
                });
            }

            _context.PitchSessions.Add(session);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(RandomDraw));
        }

        // Current talabani yakunlash: Completed qilib, Next'ni Current'ga ko'taradi va
        // qolgan talabalar ichidan yangi Next zaxiralaydi. Takroriy so'rovlar navbatni
        // ikki marta siljitmaydi.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Finalize(int sessionId, int participantId)
        {
            return await AdvanceQueueAsync(sessionId, participantId, reason: null, skip: false);
        }

        // Current talabani o'tkazib yuborish: sabab talab qilinadi va navbat xuddi
        // yakunlashdagi kabi siljitiladi.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Skip(int sessionId, int participantId, string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "O'tkazib yuborish uchun sabab ko'rsatilishi shart.";
                return RedirectToAction(nameof(RandomDraw));
            }

            return await AdvanceQueueAsync(sessionId, participantId, reason.Trim(), skip: true);
        }

        private async Task<IActionResult> AdvanceQueueAsync(int sessionId, int participantId, string? reason, bool skip)
        {
            var currentAdminId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(currentAdminId))
                return Challenge();

            using var transaction = await _context.Database.BeginTransactionAsync();

            var session = await _context.PitchSessions
                .Include(s => s.Participants)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
            {
                await transaction.RollbackAsync();
                return NotFound();
            }

            // Ownership: faqat sessiyani yaratgan Admin boshqarishi mumkin.
            if (session.CreatedById != currentAdminId)
            {
                await transaction.RollbackAsync();
                return Forbid();
            }

            // Client tomonidan kelgan participantId ham serverda tekshiriladi:
            // aynan shu sessiyaga tegishli va ayni paytda Active (Current) bo'lishi shart.
            var current = session.Participants.FirstOrDefault(p => p.Id == participantId);
            if (current == null || current.Status != PitchParticipantStatus.Active)
            {
                // Takroriy so'rov yoki allaqachon yakunlangan navbat — ikkinchi marta
                // siljitilmaydi, shunchaki sessiya sahifasiga qaytamiz.
                await transaction.RollbackAsync();
                return RedirectToAction(nameof(RandomDraw));
            }

            var now = DateTime.UtcNow;

            if (skip)
            {
                current.Status = PitchParticipantStatus.Skipped;
                current.SkippedDate = now;
                current.Reason = reason;
            }
            else
            {
                current.Status = PitchParticipantStatus.Completed;
                current.CompletedDate = now;
            }

            // Next (Navbatda) -> Current (Faol).
            var next = session.Participants
                .Where(p => p.Status == PitchParticipantStatus.Queued)
                .OrderBy(p => p.QueuePosition)
                .FirstOrDefault();

            if (next != null)
            {
                next.Status = PitchParticipantStatus.Active;
                next.ActivatedDate = now;
            }

            // Yangi Next zaxiralash: hali tanlanmagan, sessiyaga tegishli guruhlardagi talabalar.
            var groupIds = ParseGroupIds(session.SelectedGroupIds);
            var drawnStudentIds = session.Participants.Select(p => p.StudentId).Distinct().ToList();
            var studentUsers = await _userManager.GetUsersInRoleAsync("Student");
            var remainingIds = studentUsers
                .Where(u => u.GroupId.HasValue && groupIds.Contains(u.GroupId.Value) && !drawnStudentIds.Contains(u.Id))
                .Select(u => u.Id)
                .Distinct()
                .ToList();

            if (next != null && remainingIds.Count > 0)
            {
                var index = RandomNumberGenerator.GetInt32(remainingIds.Count);
                var newStudentId = remainingIds[index];
                var maxQueue = session.Participants.Max(p => p.QueuePosition);

                session.Participants.Add(new PitchParticipant
                {
                    PitchSessionId = session.Id,
                    StudentId = newStudentId,
                    QueuePosition = maxQueue + 1,
                    Status = PitchParticipantStatus.Queued,
                    DrawnDate = now
                });
            }

            // Active talaba qolmagan bo'lsa sessiya yopiladi.
            var hasActive = session.Participants.Any(p => p.Status == PitchParticipantStatus.Active);
            if (!hasActive)
            {
                session.Status = PitchSessionStatus.Closed;
                session.ClosedDate = now;
            }

            session.Version++;

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Bir vaqtning o'zida kelgan raqobatlashuvchi so'rov — navbat bir marta siljiydi.
                await transaction.RollbackAsync();
                return RedirectToAction(nameof(RandomDraw));
            }

            return RedirectToAction(nameof(RandomDraw));
        }

        private async Task<RandomDrawViewModel> BuildRandomDrawViewModelAsync(List<int>? selectedGroupIds)
        {
            var model = new RandomDrawViewModel
            {
                SelectedGroupIds = selectedGroupIds ?? new List<int>(),
                Groups = await _context.Groups
                    .Include(g => g.Students)
                    .OrderBy(g => g.Name)
                    .ToListAsync()
            };

            var currentAdminId = _userManager.GetUserId(User);
            if (!string.IsNullOrEmpty(currentAdminId))
            {
                var session = await _context.PitchSessions
                    .Include(s => s.Participants)
                        .ThenInclude(p => p.Student)
                    .Where(s => s.CreatedById == currentAdminId)
                    .OrderByDescending(s => s.Status == PitchSessionStatus.Open)
                    .ThenByDescending(s => s.CreatedDate)
                    .FirstOrDefaultAsync();

                if (session != null)
                {
                    model.ActiveSession = await BuildSessionViewModelAsync(session);
                }
            }

            return model;
        }

        private async Task<PitchSessionViewModel> BuildSessionViewModelAsync(PitchSession session)
        {
            var groupIds = ParseGroupIds(session.SelectedGroupIds);
            var groupNames = await _context.Groups
                .Where(g => groupIds.Contains(g.Id))
                .Select(g => g.Name)
                .ToListAsync();

            var drawnStudentIds = session.Participants.Select(p => p.StudentId).Distinct().ToList();
            var studentUsers = await _userManager.GetUsersInRoleAsync("Student");
            var remainingEligible = studentUsers
                .Count(u => u.GroupId.HasValue && groupIds.Contains(u.GroupId.Value) && !drawnStudentIds.Contains(u.Id));

            var current = session.Participants.FirstOrDefault(p => p.Status == PitchParticipantStatus.Active);
            var next = session.Participants
                .Where(p => p.Status == PitchParticipantStatus.Queued)
                .OrderBy(p => p.QueuePosition)
                .FirstOrDefault();

            var vm = new PitchSessionViewModel
            {
                SessionId = session.Id,
                Status = session.Status.ToString(),
                StatusLabel = session.Status == PitchSessionStatus.Open ? "Ochiq" : "Yakunlangan",
                GroupNames = groupNames,
                RemainingEligibleCount = remainingEligible,
                TotalParticipants = session.Participants.Count
            };

            if (current != null)
            {
                vm.Current = new PitchParticipantViewModel
                {
                    ParticipantId = current.Id,
                    StudentId = current.StudentId,
                    StudentName = current.Student?.FullName ?? current.Student?.UserName ?? "Noma'lum talaba",
                    Status = current.Status.ToString(),
                    StatusLabel = "Faol",
                    Reason = current.Reason
                };
            }

            if (next != null)
            {
                vm.Next = new PitchParticipantViewModel
                {
                    ParticipantId = next.Id,
                    StudentId = next.StudentId,
                    StudentName = next.Student?.FullName ?? next.Student?.UserName ?? "Noma'lum talaba",
                    Status = next.Status.ToString(),
                    StatusLabel = "Navbatda",
                    Reason = next.Reason
                };
            }

            return vm;
        }

        private static List<int> ParseGroupIds(string selectedGroupIds)
        {
            return (selectedGroupIds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => int.TryParse(s, out _))
                .Select(int.Parse)
                .Distinct()
                .ToList();
        }

        // Bitta talabaning to'liq hisobot/taqdimot sahifasi
        public async Task<IActionResult> StudentReport(string studentId)
        {
            if (string.IsNullOrEmpty(studentId)) return NotFound();

            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == studentId);
            if (student == null) return NotFound();

            var report = await _service.GetStudentShowcaseAsync(studentId);
            return View(report);
        }

        // Mezon og'irliklarini ko'rish
        public async Task<IActionResult> Criteria()
        {
            var criteria = await _service.GetCriteriaAsync();
            return View(criteria);
        }

        // Mezon og'irliklarini saqlash (yig'indi 100% bo'lishi shart — tekshiruv servis ichida)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Criteria(Dictionary<string, double> weights)
        {
            var success = await _service.UpdateCriteriaAsync(weights ?? new Dictionary<string, double>());

            if (!success)
            {
                TempData["Error"] = "Saqlanmadi: og'irliklar yig'indisi aniq 100% ga teng bo'lishi kerak.";
            }
            else
            {
                TempData["Success"] = "Mezonlar muvaffaqiyatli saqlandi.";
            }

            return RedirectToAction(nameof(Criteria));
        }
    }
}