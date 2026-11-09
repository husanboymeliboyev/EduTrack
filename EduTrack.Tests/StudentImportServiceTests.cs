using ClosedXML.Excel;
using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTrack.Tests
{
    public class StudentImportServiceTests
    {
        private static readonly DateTime Today = new(2026, 11, 5, 9, 0, 0, DateTimeKind.Unspecified);

        private static ApplicationUser CreateUser(string loginId, string fullName) => new()
        {
            UserName = loginId,
            NormalizedUserName = loginId.ToUpperInvariant(),
            LoginId = loginId,
            FullName = fullName,
            EmailConfirmed = true,
            MustChangePassword = false
        };

        /// <summary>Test konfiguratsiyasi Program.cs dagi parol siyosatini takrorlaydi.</summary>
        private static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext db)
        {
            var store = new UserStore<ApplicationUser>(db, new IdentityErrorDescriber());
            var identityOptions = Options.Create(new IdentityOptions
            {
                Password = new PasswordOptions
                {
                    RequiredLength = 8,
                    RequireDigit = true,
                    RequireLowercase = true,
                    RequireUppercase = false,
                    RequireNonAlphanumeric = false,
                }
            });

            return new UserManager<ApplicationUser>(
                store,
                identityOptions,
                new PasswordHasher<ApplicationUser>(),
                new IUserValidator<ApplicationUser>[] { new UserValidator<ApplicationUser>() },
                new IPasswordValidator<ApplicationUser>[] { new PasswordValidator<ApplicationUser>() },
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null!,
                NullLogger<UserManager<ApplicationUser>>.Instance);
        }

        private static StudentImportService CreateService(ApplicationDbContext db)
            => new(db, CreateUserManager(db), new TestAppClock(Today));

        /// <summary>
        /// Test uchun Excel oqimini yaratadi. Qatorlar (satr, familiya, ism, sinf, guruh)
        /// sarlavhasiz parametr sifatida beriladi; birinchi satr sarlavha.
        /// </summary>
        private static MemoryStream CreateWorkbook(params object?[][] rows)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("O'quvchilar");
            ws.Cell(1, 1).Value = "№";
            ws.Cell(1, 2).Value = "Familiya";
            ws.Cell(1, 3).Value = "Ism";
            ws.Cell(1, 4).Value = "Sinfi";
            ws.Cell(1, 5).Value = "Guruh";
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                for (var c = 0; c < 5; c++)
                {
                    if (row[c] != null)
                    {
                        ws.Cell(i + 2, c + 1).Value = row[c]!.ToString();
                    }
                }
            }

            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            return stream;
        }

        [Fact]
        public async Task Preview_ParsesFileAndSplitsClasses()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var service = CreateService(factory.Context);

            using var xlsx = CreateWorkbook(
                new object?[] { 1, "Abduqodirev", "Parvizbek", "6", "5" },
                new object?[] { 2, "Anvarov", "Mirjahon", "5-6", "3" },
                new object?[] { 3, "Azizov", "Muhammadabduloh", "5", "6" },
                new object?[] { 4, null, null, null, null }, // ajratgich
                new object?[] { 5, "Adhamov", "Zakaryo", "10-11", "2" },   // 10A qatori — 10D ga o'tkazildi (takror)
                new object?[] { 6, "Alimov", "Ahror", "10-11", "1" },       // 10A
                new object?[] { 7, "Ashurov", "Shuhrat", "10-11", "4" },    // 10A
                new object?[] { 8, null, null, null, null }, // ajratgich (10A/10B)
                new object?[] { 9, "G'afurov", "Elbek", "10-11", "6" },     // 10B
                new object?[] { 10, "Abdullayev", "Saidkamol", "rus", "6" },
                new object?[] { 11, "Adhamov", "Zakaryo", "rus", "2" },      // 10D (takror emas, qoladi)
                new object?[] { 12, "Shodiyev", "Amirbek", "6", "1" },      // 5-6 (qoladi)
                new object?[] { 13, "Shodiyev", "Amirbek", "rus", "1" }     // rus (takror — tashlanadi)
            );

            var preview = service.Preview(xlsx);

            Assert.Empty(preview.Errors);
            Assert.Equal(2, preview.SeparatorCount);

            // 5-6: N=1,2,3,12 = 4 o'quvchi
            Assert.Equal(4, preview.CountByClass["5-6"]);
            // 10A: N=6, N=7 (Adhamov Zakaryo tashlanadi) = 2
            Assert.Equal(2, preview.CountByClass["10A"]);
            // 10B: N=9 = 1
            Assert.Equal(1, preview.CountByClass["10B"]);
            // 10D: N=10, N=11 = 2
            Assert.Equal(2, preview.CountByClass["10D"]);

            var skipped = preview.Rows.Where(r => r.SkipReason != null).Select(r => r.FullName).ToList();
            Assert.Contains("Adhamov Zakaryo", skipped); // 10-11 dagi qatori
            Assert.Contains("Shodiyev Amirbek", skipped); // rus dagi qatori

            // Preview hech narsa yozmaydi
            Assert.Equal(0, await factory.Context.Users.CountAsync());
            Assert.Equal(0, await factory.Context.GroupMemberships.CountAsync());
        }

        [Fact]
        public async Task Preview_ReportsErrors()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var service = CreateService(factory.Context);

            using var xlsx = CreateWorkbook(
                new object?[] { 1, "Testov", "Test", "11", "1" },   // noma'lum sinf
                new object?[] { 2, null, "Muxammadziyo", "rus", "6" }, // familiyasi yo'q (ism ro'yxatda boshqa joyda yo'q)
                new object?[] { 3, "Guruhsiz", "O'quvchi", "7", null } // guruhi bo'sh
            );

            var preview = service.Preview(xlsx);

            Assert.NotEmpty(preview.Errors);
            Assert.False(preview.CanImport);
            Assert.Equal(0, await factory.Context.Users.CountAsync());
        }

        [Fact]
        public async Task ImportAsync_CreatesUsersGroupsMemberships()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            await factory.Context.Roles.AddAsync(new IdentityRole("Student") { NormalizedName = "STUDENT" });
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);

            using var xlsx = CreateWorkbook(
                new object?[] { 1, "Abdurazzoqov", "Islombek", "9", null },  // guruh faylda bo'sh -> 5-guruh
                new object?[] { 2, "Anvarov", "Mirjahon", "7", "3" },
                new object?[] { 3, "Adhamov", "Zakaryo", "rus", "2" }
            );

            var result = await service.ImportAsync(xlsx);

            Assert.True(result.Succeeded, string.Join(" | ", result.Errors));
            Assert.Equal(3, result.CreatedCount);

            var users = await factory.Context.Users.OrderBy(u => u.LoginId).ToListAsync();
            Assert.Equal(new[] { "10001", "10002", "10003" }, users.Select(u => u.LoginId).ToArray());
            Assert.All(users, u => Assert.True(u.MustChangePassword));
            Assert.All(users, u => Assert.Equal("Student", factory.Context.UserRoles
                .Join(factory.Context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur, r })
                .Where(x => x.ur.UserId == u.Id)
                .Select(x => x.r.Name)
                .First()));

            // Sinflar va guruhlar yaratildi
            var groups = await factory.Context.Groups.ToListAsync();
            Assert.Contains(groups, g => g.Type == GroupType.Class && g.Name == "9");
            Assert.Contains(groups, g => g.Type == GroupType.Class && g.Name == "7");
            Assert.Contains(groups, g => g.Type == GroupType.Class && g.Name == "10D");
            Assert.Contains(groups, g => g.Type == GroupType.Stream && g.Name == "5-guruh");

            // Abdurazzoqov 5-guruhga birlashtirilgan (override)
            var islombek = users.Single(u => u.FullName == "Abdurazzoqov Islombek");
            var membership = await factory.Context.GroupMemberships
                .SingleOrDefaultAsync(m => m.StudentId == islombek.Id && m.Scope == GroupType.Stream && m.IsActive);
            Assert.NotNull(membership);
            var streamGroup = await factory.Context.Groups.FindAsync(membership!.GroupId);
            Assert.Equal("5-guruh", streamGroup!.Name);

            // Har bir o'quvchida 1 sinf + 1 guruh a'zoligi bor (IsActive)
            var memberships = await factory.Context.GroupMemberships.ToListAsync();
            Assert.Equal(6, memberships.Count);
            Assert.All(memberships, m => Assert.True(m.IsActive));
        }

        [Fact]
        public async Task ImportAsync_IsIdempotent()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            await factory.Context.Roles.AddAsync(new IdentityRole("Student") { NormalizedName = "STUDENT" });
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);

            using var xlsx1 = CreateWorkbook(new object?[] { 1, "Anvarov", "Mirjahon", "7", "3" });
            using var xlsx2 = CreateWorkbook(new object?[] { 1, "Anvarov", "Mirjahon", "7", "3" });

            var first = await service.ImportAsync(xlsx1);
            var second = await service.ImportAsync(xlsx2);

            Assert.True(first.Succeeded, string.Join(" | ", first.Errors));
            Assert.True(second.Succeeded, string.Join(" | ", second.Errors));
            Assert.Equal(1, first.CreatedCount);
            Assert.Equal(0, second.CreatedCount);
            Assert.Single(second.SkippedExistingUsers);
            Assert.Equal(1, await factory.Context.Users.CountAsync());
        }

        [Fact]
        public async Task ImportAsync_SkipsExistingStudentWithoutMembership()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            await factory.Context.Roles.AddAsync(new IdentityRole("Student") { NormalizedName = "STUDENT" });
            await factory.Context.SaveChangesAsync();

            var userManager = CreateUserManager(factory.Context);
            var existing = CreateUser("500", "Anvarov Mirjahon");
            var created = await userManager.CreateAsync(existing, "parol123");
            Assert.True(created.Succeeded, string.Join(" | ", created.Errors.Select(e => e.Description)));
            var roleResult = await userManager.AddToRoleAsync(existing, "Student");
            Assert.True(roleResult.Succeeded, string.Join(" | ", roleResult.Errors.Select(e => e.Description)));

            var service = new StudentImportService(factory.Context, userManager, new TestAppClock(Today));

            using var xlsx = CreateWorkbook(new object?[] { 1, "Anvarov", "Mirjahon", "7", "3" });
            var result = await service.ImportAsync(xlsx);

            Assert.True(result.Succeeded, string.Join(" | ", result.Errors));
            Assert.Equal(0, result.CreatedCount);
            Assert.Single(result.SkippedExistingUsers);
            Assert.Equal(1, await factory.Context.Users.CountAsync());
            Assert.Equal(0, await factory.Context.GroupMemberships.CountAsync());
        }
    }
}
