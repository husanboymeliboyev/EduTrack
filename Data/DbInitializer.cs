using EduTrack.Models;
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

            // Rollarni yaratish
            string[] roles = { "Admin", "Teacher", "Student" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Admin foydalanuvchini yaratish (agar yo'q bo'lsa)
            var adminEmail = "admin@edutrack.uz";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Admin",
                    EmailConfirmed = true,
                    LoginId = "10000",
                    MustChangePassword = false
                };

                var result = await userManager.CreateAsync(adminUser, "Admin123!");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // Bir martalik "orqaga qarab to'ldirish": LoginId funksiyasi qo'shilishidan oldin
            // yaratilgan hisoblarda bu maydon bo'sh qolgan bo'lishi mumkin — ularga ham
            // navbat bilan raqamli ID beramiz, hech kim tizimdan chetlanib qolmasin.
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