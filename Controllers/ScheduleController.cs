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
    public class ScheduleController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ScheduleService _scheduleService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ScheduleController(
            ApplicationDbContext context,
            ScheduleService scheduleService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _scheduleService = scheduleService;
            _userManager = userManager;
        }

        // ==================== ADMIN ====================

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var schedules = await _context.Schedules
                .Include(s => s.Subject)
                .Include(s => s.Subject!.Teacher)
                .Include(s => s.Group)
                .ToListAsync();

            var vm = new WeeklyScheduleViewModel
            {
                Title = "Umumiy dars jadvali",
                Blocks = schedules.Select(MapToBlock).ToList()
            };

            return View(vm);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            var vm = new ScheduleFormViewModel();
            await FillDropdownsAsync(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(ScheduleFormViewModel vm)
        {
            if (ModelState.IsValid)
            {
                var schedule = new Schedule
                {
                    SubjectId = vm.SubjectId,
                    GroupId = vm.GroupId,
                    DayOfWeek = vm.DayOfWeek,
                    StartTime = vm.StartTime,
                    EndTime = vm.EndTime
                };

                var conflict = await _scheduleService.CheckConflictAsync(schedule);
                if (conflict.HasConflict)
                {
                    ModelState.AddModelError(string.Empty, conflict.Message!);
                }
                else
                {
                    _context.Schedules.Add(schedule);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Dars jadvalga muvaffaqiyatli qo'shildi";
                    return RedirectToAction(nameof(Index));
                }
            }

            await FillDropdownsAsync(vm);
            return View(vm);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var schedule = await _context.Schedules.FindAsync(id);
            if (schedule == null) return NotFound();

            var vm = new ScheduleFormViewModel
            {
                Id = schedule.Id,
                SubjectId = schedule.SubjectId,
                GroupId = schedule.GroupId,
                DayOfWeek = schedule.DayOfWeek,
                StartTime = schedule.StartTime,
                EndTime = schedule.EndTime
            };
            await FillDropdownsAsync(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, ScheduleFormViewModel vm)
        {
            if (id != vm.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var schedule = await _context.Schedules.FindAsync(id);
                if (schedule == null) return NotFound();

                schedule.SubjectId = vm.SubjectId;
                schedule.GroupId = vm.GroupId;
                schedule.DayOfWeek = vm.DayOfWeek;
                schedule.StartTime = vm.StartTime;
                schedule.EndTime = vm.EndTime;

                var conflict = await _scheduleService.CheckConflictAsync(schedule, excludeId: id);
                if (conflict.HasConflict)
                {
                    ModelState.AddModelError(string.Empty, conflict.Message!);
                }
                else
                {
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Dars jadvali yangilandi";
                    return RedirectToAction(nameof(Index));
                }
            }

            await FillDropdownsAsync(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var schedule = await _context.Schedules.FindAsync(id);
            if (schedule != null)
            {
                _context.Schedules.Remove(schedule);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Dars jadvaldan o'chirildi";
            }
            return RedirectToAction(nameof(Index));
        }

        // ==================== O'QITUVCHI ====================

        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> MySchedule()
        {
            var teacherId = _userManager.GetUserId(User);

            var schedules = await _context.Schedules
                .Include(s => s.Subject)
                .Include(s => s.Group)
                .Where(s => s.Subject!.TeacherId == teacherId)
                .ToListAsync();

            var vm = new WeeklyScheduleViewModel
            {
                Title = "Mening dars jadvalim",
                Blocks = schedules.Select(MapToBlock).ToList(),
                Workload = await _scheduleService.GetTeacherWorkloadAsync(teacherId!)
            };

            return View(vm);
        }

        // ==================== TALABA ====================

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyGroupSchedule()
        {
            var userId = _userManager.GetUserId(User);
            var student = await _context.Users
                .Include(u => u.Group)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (student?.GroupId == null)
            {
                return View(new WeeklyScheduleViewModel
                {
                    Title = "Guruh jadvali topilmadi",
                    Blocks = new List<ScheduleBlockViewModel>()
                });
            }

            var schedules = await _context.Schedules
                .Include(s => s.Subject)
                .Include(s => s.Subject!.Teacher)
                .Include(s => s.Group)
                .Where(s => s.GroupId == student.GroupId)
                .ToListAsync();

            var vm = new WeeklyScheduleViewModel
            {
                Title = $"{student.Group?.Name} guruhi jadvali",
                Blocks = schedules.Select(MapToBlock).ToList()
            };

            return View(vm);
        }

        // ==================== YORDAMCHI METODLAR ====================

        private static ScheduleBlockViewModel MapToBlock(Schedule s)
        {
            return new ScheduleBlockViewModel
            {
                Id = s.Id,
                SubjectName = s.Subject?.Name ?? "",
                GroupName = s.Group?.Name ?? "",
                TeacherName = s.Subject?.Teacher?.FullName,
                DayOfWeek = s.DayOfWeek,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                HueDegree = ScheduleService.GetHueForSubject(s.Subject?.Name ?? s.SubjectId.ToString())
            };
        }

        private async Task FillDropdownsAsync(ScheduleFormViewModel vm)
        {
            vm.Subjects = await _context.Subjects
                .OrderBy(s => s.Name)
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
                .ToListAsync();

            vm.Groups = await _context.Groups
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name })
                .ToListAsync();
        }
    }
}