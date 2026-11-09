using EduTrack.Data;
using EduTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Middleware
{
    /// <summary>
    /// Parolni almashtirish talab qilinadigan foydalanuvchilarni
    /// ChangePassword sahifasiga majburlaydi. Login sahifasiga tegilmaydi:
    /// almashtirish sharti shu yerda, barcha yo'llarda qo'llanadi.
    /// </summary>
    public class MustChangePasswordMiddleware
    {
        private readonly RequestDelegate _next;

        public MustChangePasswordMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        // ChangePassword sahifasi va Logout ochiq qoladi; statik fayllar o'tadi.
        private static readonly string[] AllowedPrefixes =
        {
            "/Identity/Account/Manage/ChangePassword",
            "/Identity/Account/Logout",
        };

        private static readonly HashSet<string> StaticExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".css", ".js", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico",
            ".woff", ".woff2", ".ttf", ".map", ".webp"
        };

        private static bool IsAllowedPath(string path)
        {
            foreach (var prefix in AllowedPrefixes)
            {
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            var ext = Path.GetExtension(path);
            return ext.Length > 0 && StaticExtensions.Contains(ext);
        }

        // DbContext va UserManager har bir so'rov uchun DI orqali beriladi.
        public async Task InvokeAsync(
            HttpContext context,
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager)
        {
            if (context.User.Identity?.IsAuthenticated != true || IsAllowedPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            var userId = userManager.GetUserId(context.User);
            if (userId == null)
            {
                await _next(context);
                return;
            }

            var mustChange = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => (bool?)u.MustChangePassword)
                .FirstOrDefaultAsync();

            if (mustChange == true)
            {
                if (HttpMethods.IsGet(context.Request.Method))
                {
                    context.Response.Redirect("/Identity/Account/Manage/ChangePassword");
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Avval parolingizni almashtiring.");
                return;
            }

            await _next(context);
        }
    }
}
