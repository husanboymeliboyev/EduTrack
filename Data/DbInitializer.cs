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

            // LoginId to'ldirish (Eski foydalanuvchilar uchun)
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
