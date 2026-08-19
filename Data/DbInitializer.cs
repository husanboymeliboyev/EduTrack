using EduTrack.Models;
using EduTrack.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var passwordGenerator = serviceProvider.GetRequiredService<IPasswordGeneratorService>();

            // Rollarni yaratish
            string[] roles = { "Admin", "Teacher", "Student" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Admin foydalanuvchini yaratish
            var adminEmail = "admin@edutrack.uz";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                var temporaryPassword = passwordGenerator.Generate();
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Admin",
                    EmailConfirmed = true,
                    LoginId = "10000",
                    MustChangePassword = true
                };

                var result = await userManager.CreateAsync(adminUser, temporaryPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    Console.WriteLine($"DIQQAT: Yangi Admin yaratildi. Parol: {temporaryPassword}");
                }
            }
            else
            {
                // VAQTINCHALIK RESET: Parolni "Admin123!" ga o'zgartirish
                var resetToken = await userManager.GeneratePasswordResetTokenAsync(adminUser);
                await userManager.ResetPasswordAsync(adminUser, resetToken, "Admin123!");
                Console.WriteLine("DIQQAT: Admin paroli 'Admin123!' ga reset qilindi.");
            }

            // LoginId to'ldirish
            var usersWithoutLoginId = await context.Users
                .Where(u => u.LoginId == null || u.LoginId == "")
                .ToListAsync();

            if (usersWithoutLoginId.Any())
            {
                var existingIds = await context.Users
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

                foreach (var user in usersWithoutLoginId)
                {
                    maxId++;
                    user.LoginId = maxId.ToString();
                }

                await context.SaveChangesAsync();
            }
        }
    }
}
