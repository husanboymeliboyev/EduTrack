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
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly IPasswordGeneratorService _passwordGenerator;
        private readonly IExcelExportService _excelExport;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            IPasswordGeneratorService passwordGenerator,
            IExcelExportService excelExport)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _passwordGenerator = passwordGenerator;
            _excelExport = excelExport;
        }
        // Barcha foydalanuvchilarni ko'rsatish
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .Include(u => u.Group)
                .ToListAsync();

            var result = new List<UserListItemViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                result.Add(new UserListItemViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    LoginId = user.LoginId,
                    Email = user.Email ?? "",
                    Role = roles.FirstOrDefault() ?? "Rolsiz",
                    GroupName = user.Group?.Name
                });
            }

            return View(result);
        }

        private async Task LoadDropdownsAsync()
        {
            ViewBag.Roles = new SelectList(await _roleManager.Roles.Select(r => r.Name).ToListAsync());
            ViewBag.Groups = new SelectList(await _context.Groups.ToListAsync(), "Id", "Name");
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
                    GroupId = model.Role == "Student" ? model.GroupId : null
                };

                var result = await _userManager.CreateAsync(user, password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, model.Role);

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

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
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
        public IActionResult DownloadTemplate()
        {
            var bytes = _excelExport.CreateNamesTemplate();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "talabalar_shabloni.xlsx");
        }

        // Ommaviy qo'shish sahifasi
        public async Task<IActionResult> BulkCreate()
        {
            ViewBag.Groups = new SelectList(await _context.Groups.ToListAsync(), "Id", "Name");
            return View(new BulkCreateStudentsViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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

            var model = new EditUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName ?? "",
                Email = user.Email ?? "",
                Role = roles.FirstOrDefault() ?? "",
                GroupId = user.GroupId
            };

            await LoadDropdownsAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, EditUserViewModel model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null) return NotFound();

                var currentUserId = _userManager.GetUserId(User);
                var currentRoles = await _userManager.GetRolesAsync(user);

                // Admin o'zining rolini o'zgartira olmaydi — bu tasodifan o'zini tizimdan
                // chiqarib qo'yishning (masalan yagona Admin bo'lsa) oldini oladi.
                if (id == currentUserId && model.Role != (currentRoles.FirstOrDefault() ?? string.Empty))
                {
                    TempData["Error"] = "O'zingizning rolingizni o'zgartira olmaysiz. Buni boshqa Admin orqali bajaring.";
                    return RedirectToAction(nameof(Index));
                }

                user.FullName = model.FullName;
                user.GroupId = model.Role == "Student" ? model.GroupId : null;

                // Rolni yangilash: eski rollarni olib tashlab, yangisini qo'shamiz
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, model.Role);

                await _context.SaveChangesAsync();

                TempData["Success"] = "Ma'lumotlar yangilandi.";
                return RedirectToAction(nameof(Index));
            }

            await LoadDropdownsAsync();
            return View(model);
        }

        // Foydalanuvchini o'chirish sahifasi
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
        // Guruhdagi barcha yaratilgan Login ID/parollarni Excel faylga eksport qilish
        [HttpPost]
        [ValidateAntiForgeryToken]
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