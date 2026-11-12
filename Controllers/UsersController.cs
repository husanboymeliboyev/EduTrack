using EduTrack.Data;
using EduTrack.Models;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EduTrack.Services;
using ClosedXML.Excel;

namespace EduTrack.Controllers
{
    // Foydalanuvchilar: Admin to'liq boshqaradi, Moderator (zauch) faqat
    // o'quvchi/o'qituvchi hisoblarini cheklangan tarzda tahrirlay oladi.
    [Authorize(Roles = "Admin,Moderator")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly IPasswordGeneratorService _passwordGenerator;
        private readonly IExcelExportService _excelExport;
        private readonly IUserDirectoryService _userDirectoryService;
        private readonly IStudentTransferService _transfers;
        private readonly IAppClock _clock;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            IPasswordGeneratorService passwordGenerator,
            IExcelExportService excelExport,
            IUserDirectoryService userDirectoryService,
            IStudentTransferService transfers,
            IAppClock clock)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _passwordGenerator = passwordGenerator;
            _excelExport = excelExport;
            _userDirectoryService = userDirectoryService;
            _transfers = transfers;
            _clock = clock;
        }

        // Barcha foydalanuvchilarni qidiruv, rol/guruh filtri va sahifalash bilan ko'rsatish
        public async Task<IActionResult> Index([FromQuery] UsersFilterViewModel filter)
        {
            var model = await _userDirectoryService.GetForAdminAsync(
                filter ?? new UsersFilterViewModel());

            return View(model);
        }

        private bool IsModeratorOnly =>
            User.IsInRole("Moderator") && !User.IsInRole("Admin");

        private async Task LoadDropdownsAsync()
        {
            // Moderator faqat Student/Teacher hisoblarini yarata oladi;
            // Admin/Moderator rollari unga ko'rinmaydi.
            ViewBag.Roles = IsModeratorOnly
                ? new SelectList(new[] { "Student", "Teacher" })
                : new SelectList(await _roleManager.Roles.Select(r => r.Name).ToListAsync());

            // O'quvchi 1-smenada sinfda, 2-smenada yo'nalish guruhida o'qiydi —
            // shuning uchun ikkala ro'yxat alohida beriladi.
            ViewBag.ClassGroups = await GetGroupOptionsAsync(GroupType.Class);
            ViewBag.StreamGroups = await GetGroupOptionsAsync(GroupType.Stream);
        }

        private async Task<List<SelectListItem>> GetGroupOptionsAsync(GroupType type)
        {
            return await _context.Groups
                .AsNoTracking()
                .Where(g => g.Type == type && !g.IsArchived)
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem
                {
                    Value = g.Id.ToString(),
                    Text = g.Name
                })
                .ToListAsync();
        }

        private async Task<Group?> FindStudentGroupAsync(int? groupId, GroupType type)
        {
            if (groupId == null)
            {
                return null;
            }

            return await _context.Groups
                .FirstOrDefaultAsync(g => g.Id == groupId && g.Type == type && !g.IsArchived);
        }

        // Yangi foydalanuvchi qo'shish sahifasi
        public async Task<IActionResult> Create()
        {
            await LoadDropdownsAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            // Moderator faqat o'quvchi yoki o'qituvchi hisobini yarata oladi.
            if (IsModeratorOnly && model.Role != "Student" && model.Role != "Teacher")
            {
                ModelState.AddModelError(nameof(model.Role), "Moderator faqat Student yoki Teacher rolini tanlay oladi.");
            }

            Group? classGroup = null;
            Group? streamGroup = null;
            if (model.Role == "Student")
            {
                // O'quvchi 1-smenada sinfda, 2-smenada guruhda o'qiydi —
                // ikkalasi ham majburiy.
                classGroup = await FindStudentGroupAsync(model.ClassGroupId, GroupType.Class);
                streamGroup = await FindStudentGroupAsync(model.StreamGroupId, GroupType.Stream);

                if (classGroup == null)
                {
                    ModelState.AddModelError(nameof(model.ClassGroupId), "1-smena uchun sinfni tanlang.");
                }

                if (streamGroup == null)
                {
                    ModelState.AddModelError(nameof(model.StreamGroupId), "2-smena uchun guruhni tanlang.");
                }
            }

            if (ModelState.IsValid)
            {
                var loginId = await GenerateNextLoginIdAsync();
                var password = _passwordGenerator.Generate();

                var user = new ApplicationUser
                {
                    UserName = loginId,
                    LoginId = loginId,
                    Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email,
                    FullName = model.FullName,
                    EmailConfirmed = true,
                    // Eski maydon: sinfni saqlaydi (a'zoliklar asosiy manba).
                    GroupId = classGroup?.Id
                };

                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var result = await _userManager.CreateAsync(user, password);

                    if (!result.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                    }
                    else
                    {
                        await _userManager.AddToRoleAsync(user, model.Role);

                        if (classGroup != null && streamGroup != null)
                        {
                            var startDate = _clock.Today;
                            _context.GroupMemberships.AddRange(
                                new GroupMembership
                                {
                                    StudentId = user.Id,
                                    GroupId = classGroup.Id,
                                    Scope = GroupType.Class,
                                    StartDate = startDate,
                                    IsActive = true
                                },
                                new GroupMembership
                                {
                                    StudentId = user.Id,
                                    GroupId = streamGroup.Id,
                                    Scope = GroupType.Stream,
                                    StartDate = startDate,
                                    IsActive = true
                                });
                            await _context.SaveChangesAsync();
                        }

                        await transaction.CommitAsync();

                        // Parol faqat shu daqiqada, oddiy (hash qilinmagan) holda mavjud —
                        // shuning uchun uni bazaga yozmasdan, to'g'ridan-to'g'ri shu sahifada ko'rsatamiz.
                        var credentials = new UserCredentialsViewModel
                        {
                            FullName = user.FullName ?? "",
                            LoginId = loginId,
                            Password = password,
                            Role = model.Role
                        };

                        return View("Created", credentials);
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(string.Empty, "Foydalanuvchini yaratishda xatolik: " + ex.Message);
                }
            }

            await LoadDropdownsAsync();
            return View(model);
        }
        // Keyingi bo'sh raqamli Login ID'ni topadi (masalan: 10001, 10002, ...)
        private async Task<string> GenerateNextLoginIdAsync()
        {
            var existingIds = await _context.Users
                .Select(u => u.LoginId)
                .Where(id => id != null && id != "")
                .ToListAsync();

            int maxId = 10000;
            foreach (var id in existingIds)
            {
                if (int.TryParse(id, out var num) && num > maxId)
                {
                    maxId = num;
                }
            }

            return (maxId + 1).ToString();
        }

        // Shablon faylni yuklab berish (faqat "Ism-familiya" ustuni bilan)
        [Authorize(Roles = "Admin")]
        public IActionResult DownloadTemplate()
        {
            var bytes = _excelExport.CreateNamesTemplate();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "talabalar_shabloni.xlsx");
        }

        // Ommaviy qo'shish sahifasi
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BulkCreate()
        {
            ViewBag.Groups = new SelectList(await _context.Groups.ToListAsync(), "Id", "Name");
            return View(new BulkCreateStudentsViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BulkCreate(BulkCreateStudentsViewModel model)
        {
            if (model.ExcelFile == null || model.ExcelFile.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Excel fayl tanlanmadi.");
            }

            var group = await _context.Groups.FindAsync(model.GroupId);
            if (group == null)
            {
                ModelState.AddModelError(string.Empty, "Guruh topilmadi.");
            }

            if (!ModelState.IsValid || group == null)
            {
                ViewBag.Groups = new SelectList(await _context.Groups.ToListAsync(), "Id", "Name");
                return View(model);
            }

            var names = new List<string>();
            using (var stream = new MemoryStream())
            {
                await model.ExcelFile!.CopyToAsync(stream);
                using var workbook = new XLWorkbook(stream);
                var ws = workbook.Worksheet(1);
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

                for (int row = 2; row <= lastRow; row++)
                {
                    var name = ws.Cell(row, 1).GetString().Trim();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        names.Add(name);
                    }
                }
            }

            var result = new BulkCreateResultViewModel { GroupName = group.Name };

            var existingIds = await _context.Users.Select(u => u.LoginId).ToListAsync();
            int nextId = 10000;
            foreach (var id in existingIds)
            {
                if (int.TryParse(id, out var num) && num > nextId) nextId = num;
            }

            foreach (var fullName in names)
            {
                nextId++;
                var loginId = nextId.ToString();
                var password = _passwordGenerator.Generate();

                var user = new ApplicationUser
                {
                    UserName = loginId,
                    LoginId = loginId,
                    FullName = fullName,
                    EmailConfirmed = true,
                    GroupId = group.Id
                };

                var created = await _userManager.CreateAsync(user, password);
                if (created.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Student");
                    result.CreatedUsers.Add(new UserCredentialsViewModel
                    {
                        FullName = fullName,
                        LoginId = loginId,
                        Password = password,
                        Role = "Student"
                    });
                }
                else
                {
                    result.Skipped.Add($"{fullName} — xato: {string.Join(", ", created.Errors.Select(e => e.Description))}");
                }
            }

            return View("BulkCreated", result);
        }
        // Foydalanuvchini tahrirlash sahifasi
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var targetRole = roles.FirstOrDefault() ?? string.Empty;

            // Moderator admin yoki boshqa moderator hisobini ocholmaydi.
            if (IsModeratorOnly && (targetRole == "Admin" || targetRole == "Moderator"))
            {
                return Forbid();
            }

            var (classGroupId, streamGroupId) = await GetActiveGroupIdsAsync(user.Id, user.GroupId);

            var model = new EditUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName ?? "",
                Email = user.Email ?? "",
                Role = targetRole,
                ClassGroupId = classGroupId,
                StreamGroupId = streamGroupId
            };

            await LoadDropdownsAsync();
            return View(model);
        }

        /// <summary>
        /// O'quvchining aktiv sinf va guruh ID'lari. A'zolik yozuvlari bo'lmasa
        /// (eski hisoblar) <see cref="ApplicationUser.GroupId"/> dan foydalaniladi.
        /// </summary>
        private async Task<(int? ClassId, int? StreamId)> GetActiveGroupIdsAsync(
            string studentId, int? legacyGroupId)
        {
            var active = await _context.GroupMemberships
                .AsNoTracking()
                .Where(m => m.StudentId == studentId && m.IsActive)
                .Select(m => new { m.GroupId, m.Scope })
                .ToListAsync();

            var classId = active.FirstOrDefault(m => m.Scope == GroupType.Class)?.GroupId;
            var streamId = active.FirstOrDefault(m => m.Scope == GroupType.Stream)?.GroupId;

            if (classId == null && streamId == null && legacyGroupId != null)
            {
                var legacyType = await _context.Groups
                    .AsNoTracking()
                    .Where(g => g.Id == legacyGroupId)
                    .Select(g => (GroupType?)g.Type)
                    .FirstOrDefaultAsync();

                if (legacyType == GroupType.Class)
                {
                    classId = legacyGroupId;
                }
                else if (legacyType == GroupType.Stream)
                {
                    streamId = legacyGroupId;
                }
            }

            return (classId, streamId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, EditUserViewModel model)
        {
            if (id != model.Id) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var currentRoles = await _userManager.GetRolesAsync(user);
            var currentRole = currentRoles.FirstOrDefault() ?? string.Empty;

            // Moderator admin yoki moderator hisobini tahrirlay olmaydi.
            if (IsModeratorOnly && (currentRole == "Admin" || currentRole == "Moderator"))
            {
                TempData["Error"] = "Moderator admin yoki moderator hisobini tahrirlay olmaydi.";
                return RedirectToAction(nameof(Index));
            }

            // Moderator rol o'zgartira olmaydi — faqat ism, sinf va guruh yangilanadi.
            var newRole = IsModeratorOnly ? currentRole : model.Role;

            // Admin o'zining rolini o'zgartira olmaydi — bu tasodifan o'zini tizimdan
            // chiqarib qo'yishning (masalan yagona Admin bo'lsa) oldini oladi.
            if (id == currentUserId && newRole != currentRole)
            {
                TempData["Error"] = "O'zingizning rolingizni o'zgartira olmaysiz. Buni boshqa Admin orqali bajaring.";
                return RedirectToAction(nameof(Index));
            }

            Group? classGroup = null;
            Group? streamGroup = null;
            if (newRole == "Student")
            {
                classGroup = await FindStudentGroupAsync(model.ClassGroupId, GroupType.Class);
                streamGroup = await FindStudentGroupAsync(model.StreamGroupId, GroupType.Stream);

                if (classGroup == null)
                {
                    ModelState.AddModelError(nameof(model.ClassGroupId), "1-smena uchun sinfni tanlang.");
                }

                if (streamGroup == null)
                {
                    ModelState.AddModelError(nameof(model.StreamGroupId), "2-smena uchun guruhni tanlang.");
                }
            }

            if (ModelState.IsValid)
            {
                var isStudent = newRole == "Student" && classGroup != null && streamGroup != null;
                string? changeSummary = null;

                // Sinf/guruh o'zgarishini avval bajaramiz: u o'z tranzaksiyasida
                // ishlaydi. Muvaffaqiyatsiz bo'lsa foydalanuvchi ma'lumotlari
                // (ism, rol) o'zgarmaydi.
                if (isStudent)
                {
                    var activeMemberships = await _context.GroupMemberships
                        .Where(m => m.StudentId == id && m.IsActive)
                        .ToListAsync();

                    var currentClass = activeMemberships.FirstOrDefault(m => m.Scope == GroupType.Class);
                    var currentStream = activeMemberships.FirstOrDefault(m => m.Scope == GroupType.Stream);

                    var request = new TransferRequest
                    {
                        StudentId = id,
                        Date = _clock.Today,
                        Reason = "Foydalanuvchi tahrirlandi"
                    };

                    if (currentClass?.GroupId != classGroup!.Id)
                    {
                        request.NewClassGroupId = classGroup.Id;
                    }

                    if (currentStream?.GroupId != streamGroup!.Id)
                    {
                        request.NewStreamGroupId = streamGroup.Id;
                    }

                    if (request.NewClassGroupId != null || request.NewStreamGroupId != null)
                    {
                        var transfer = await _transfers.TransferAsync(request);
                        if (!transfer.Success)
                        {
                            ModelState.AddModelError(string.Empty,
                                transfer.Error ?? "Sinf/guruhni yangilab bo'lmadi.");
                        }
                        else
                        {
                            changeSummary = string.Join("; ", transfer.Changes);
                        }
                    }
                }

                if (ModelState.IsValid)
                {
                    user.FullName = model.FullName;

                    // Rolni faqat haqiqatan o'zgargan bo'lsa yangilaymiz.
                    if (newRole != currentRole)
                    {
                        await _userManager.RemoveFromRolesAsync(user, currentRoles);
                        await _userManager.AddToRoleAsync(user, newRole);
                    }

                    if (isStudent)
                    {
                        // Eski maydon: sinfni saqlaydi (a'zoliklar asosiy manba).
                        user.GroupId = classGroup!.Id;
                    }
                    else
                    {
                        user.GroupId = null;

                        var activeMemberships = await _context.GroupMemberships
                            .Where(m => m.StudentId == id && m.IsActive)
                            .ToListAsync();

                        foreach (var membership in activeMemberships)
                        {
                            membership.IsActive = false;
                            membership.EndDate = _clock.Today;
                            membership.EndReason = "Rol o'zgartirildi";
                        }
                    }

                    await _context.SaveChangesAsync();

                    TempData["Success"] = changeSummary != null
                        ? "Ma'lumotlar yangilandi. " + changeSummary
                        : "Ma'lumotlar yangilandi.";
                    return RedirectToAction(nameof(Index));
                }
            }

            await LoadDropdownsAsync();
            return View(model);
        }

        // Foydalanuvchini o'chirish sahifasi
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _context.Users.Include(u => u.Group).FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            ViewBag.Role = roles.FirstOrDefault() ?? "Rolsiz";

            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (id == currentUserId)
            {
                TempData["Error"] = "O'zingizni o'chira olmaysiz.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.DeleteAsync(user);
            }

            return RedirectToAction(nameof(Index));
        }

        // Foydalanuvchining parolini tiklash: yangi tasodifiy vaqtinchalik parol
        // o'rnatiladi va foydalanuvchi keyingi kirishda uni albatta almashtirishi
        // shart qilib belgilanadi. Yangi parol faqat shu so'rovdan keyingi sahifada,
        // bir marta, TempData orqali ko'rsatiladi — hech qayerda saqlanmaydi.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ResetPassword(string userId, string? returnUrl = null)
        {
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "Foydalanuvchi aniqlanmadi.";
                return RedirectToIndexOr(returnUrl);
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "Foydalanuvchi topilmadi.";
                return RedirectToIndexOr(returnUrl);
            }

            // Mavjud "yangi foydalanuvchi yaratish" oqimida ishlatilgan aynan shu
            // parol generatsiya logikasi (IPasswordGeneratorService) qayta ishlatiladi.
            var newPassword = _passwordGenerator.Generate();

            var removeResult = await _userManager.RemovePasswordAsync(user);
            if (!removeResult.Succeeded)
            {
                TempData["Error"] = "Parolni tiklab bo'lmadi: " +
                    string.Join(", ", removeResult.Errors.Select(e => e.Description));
                return RedirectToIndexOr(returnUrl);
            }

            var addResult = await _userManager.AddPasswordAsync(user, newPassword);
            if (!addResult.Succeeded)
            {
                TempData["Error"] = "Parolni tiklab bo'lmadi: " +
                    string.Join(", ", addResult.Errors.Select(e => e.Description));
                return RedirectToIndexOr(returnUrl);
            }

            user.MustChangePassword = true;
            await _userManager.UpdateAsync(user);

            TempData["Success"] = $"{user.FullName} uchun parol tiklandi.";
            TempData["NewPasswordFullName"] = user.FullName;
            TempData["NewPasswordLoginId"] = user.LoginId;
            TempData["NewPasswordValue"] = newPassword;

            return RedirectToIndexOr(returnUrl);
        }

        // Filtr holatini saqlab qolish uchun faqat lokal manzilga qaytadi.
        private IActionResult RedirectToIndexOr(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        // Guruhdagi barcha yaratilgan Login ID/parollarni Excel faylga eksport qilish
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public IActionResult ExportExcel(List<string> fullNames, List<string> loginIds, List<string> passwords, string groupName)
        {
            var credentials = new List<UserCredentialsViewModel>();

            for (int i = 0; i < fullNames.Count; i++)
            {
                credentials.Add(new UserCredentialsViewModel
                {
                    FullName = fullNames[i],
                    LoginId = loginIds[i],
                    Password = passwords[i],
                    Role = "Student"
                });
            }

            var bytes = _excelExport.ExportCredentials(credentials, groupName);
            var fileName = $"{groupName}_talabalar.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
