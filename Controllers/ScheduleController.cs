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
        private readonly IStudentSubjectGroupService _subjectGroups;

        public ScheduleController(
            ApplicationDbContext context,
            ScheduleService scheduleService,
            UserManager<ApplicationUser> userManager,
            IStudentSubjectGroupService subjectGroups)
        {
            _context = context;
            _scheduleService = scheduleService;
            _userManager = userManager;
            _subjectGroups = subjectGroups;
        }

        // ==================== ADMIN ====================

        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> Index()
        {
            var schedules = await LoadSchedulesAsync();
            var (overrides, names) = await LoadTeacherLookupsAsync(schedules);

            var vm = new WeeklyScheduleViewModel
            {
                Title = "Umumiy dars jadvali",
                Blocks = schedules.Select(s => MapToBlock(s, overrides, names)).ToList()
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
                    EndTime = vm.EndTime,
                    Shift = vm.Shift,
                    Period = vm.Period,
                    Kind = vm.Kind,
                    TeacherId = string.IsNullOrWhiteSpace(vm.TeacherId) ? null : vm.TeacherId
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
                EndTime = schedule.EndTime,
                Shift = schedule.Shift,
                Period = schedule.Period,
                Kind = schedule.Kind,
                TeacherId = schedule.TeacherId
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
                schedule.Shift = vm.Shift;
                schedule.Period = vm.Period;
                schedule.Kind = vm.Kind;
                schedule.TeacherId = string.IsNullOrWhiteSpace(vm.TeacherId) ? null : vm.TeacherId;

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

            var schedules = await LoadSchedulesAsync();
            var (overrides, names) = await LoadTeacherLookupsAsync(schedules);

            var mine = schedules
                .Where(s => ScheduleService.ResolveTeacherId(s, overrides) == teacherId)
                .ToList();

            var vm = new WeeklyScheduleViewModel
            {
                Title = "Mening dars jadvalim",
                Blocks = mine.Select(s => MapToBlock(s, overrides, names)).ToList(),
                Workload = await _scheduleService.GetTeacherWorkloadAsync(teacherId!)
            };

            return View(vm);
        }

        // ==================== TALABA ====================

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyGroupSchedule()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null)
            {
                return View(new WeeklyScheduleViewModel
                {
                    Title = "Guruh jadvali topilmadi",
                    Blocks = new List<ScheduleBlockViewModel>()
                });
            }

            var defaultGroupId = await _subjectGroups.GetDefaultGroupIdAsync(userId);
            var assignments = await _subjectGroups.GetAssignmentsAsync(userId);
            var activeOverrides = assignments.Where(a => a.IsActive).ToList();

            if (defaultGroupId == null && activeOverrides.Count == 0)
            {
                return View(new WeeklyScheduleViewModel
                {
                    Title = "Guruh jadvali topilmadi",
                    Blocks = new List<ScheduleBlockViewModel>()
                });
            }

            var groupIds = activeOverrides
                .Select(a => a.GroupId)
                .Append(defaultGroupId ?? 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var schedules = await LoadSchedulesAsync(groupIds);
            var (overrides, names) = await LoadTeacherLookupsAsync(schedules);

            // Har bir fan uchun amaldagi guruh: override bo'lsa — o'sha guruh,
            // aks holda standart guruh. Standart guruhning override qilingan
            // fan darslari ko'rsatilmaydi.
            var targetGroupBySubject = activeOverrides
                .GroupBy(a => a.SubjectId)
                .ToDictionary(g => g.Key, g => g.First().GroupId);

            var mine = schedules
                .Where(s =>
                {
                    var target = targetGroupBySubject.TryGetValue(s.SubjectId, out var overrideGroupId)
                        ? overrideGroupId
                        : defaultGroupId;

                    return target != null && s.GroupId == target;
                })
                .ToList();

            var defaultGroupName = defaultGroupId is int id
                ? await _context.Groups.Where(g => g.Id == id).Select(g => g.Name).FirstOrDefaultAsync()
                : null;

            var title = activeOverrides.Count == 0 && !string.IsNullOrWhiteSpace(defaultGroupName)
                ? $"{defaultGroupName} guruhi jadvali"
                : "Mening dars jadvalim";

            var vm = new WeeklyScheduleViewModel
            {
                Title = title,
                Blocks = mine.Select(s => MapToBlock(s, overrides, names)).ToList()
            };

            return View(vm);
        }

        // ==================== YORDAMCHI METODLAR ====================

        private Task<List<Schedule>> LoadSchedulesAsync(IReadOnlyCollection<int>? groupIds = null)
        {
            var query = _context.Schedules
                .AsNoTracking()
                .Include(s => s.Subject)
                .Include(s => s.Subject!.Teacher)
                .Include(s => s.Group)
                .Include(s => s.Teacher)
                .AsQueryable();

            if (groupIds is { Count: > 0 })
            {
                query = query.Where(s => groupIds.Contains(s.GroupId));
            }

            return query.ToListAsync();
        }

        private async Task<(List<GroupSubject> Overrides, Dictionary<string, string?> Names)> LoadTeacherLookupsAsync(
            IEnumerable<Schedule> schedules)
        {
            var overrides = await _context.GroupSubjects
                .AsNoTracking()
                .ToListAsync();

            var teacherIds = schedules
                .Select(s => ScheduleService.ResolveTeacherId(s, overrides))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .Distinct()
                .ToList();

            var names = await _context.Users
                .AsNoTracking()
                .Where(u => teacherIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName);

            return (overrides, names);
        }

        private static ScheduleBlockViewModel MapToBlock(
            Schedule s,
            IReadOnlyCollection<GroupSubject> overrides,
            IReadOnlyDictionary<string, string?> teacherNames)
        {
            var teacherId = ScheduleService.ResolveTeacherId(s, overrides);

            string? teacherName = null;
            if (!string.IsNullOrWhiteSpace(teacherId))
            {
                teacherNames.TryGetValue(teacherId, out teacherName);
            }

            return new ScheduleBlockViewModel
            {
                Id = s.Id,
                SubjectName = s.Subject?.Name ?? "",
                GroupName = s.Group?.Name ?? "",
                TeacherName = teacherName,
                DayOfWeek = s.DayOfWeek,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Shift = s.Shift,
                Kind = s.Kind,
                Period = s.Period,
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
                .OrderBy(g => g.Type)
                .ThenBy(g => g.Name)
                .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name })
                .ToListAsync();

            var teacherRoleId = await _context.Roles
                .Where(r => r.Name == "Teacher")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (teacherRoleId is null)
            {
                vm.Teachers = new List<SelectListItem>();
                return;
            }

            vm.Teachers = await _context.UserRoles
                .Where(ur => ur.RoleId == teacherRoleId)
                .Join(_context.Users, ur => ur.UserId, u => u.Id, (ur, u) => u)
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = string.IsNullOrEmpty(u.FullName) ? u.LoginId : u.FullName!
                })
                .ToListAsync();
        }
    }
}
