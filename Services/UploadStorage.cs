using System.Data.Common;
using Microsoft.AspNetCore.Hosting;

namespace EduTrack.Services
{
    /// <summary>
    /// Yuklangan fayllar qayerda saqlanishini bitta joyda hal qiladi.
    ///
    /// Railway kabi muhitlarda ilova konteyneri har deploy'da qaytadan yaratiladi.
    /// Konteyner ichidagi fayllar (masalan wwwroot/uploads) o'shanda o'chib ketadi,
    /// SQLite bazasi esa doimiy volume'da turadi. Shu sabab fayllarni ham baza
    /// fayli yonidagi "uploads" papkasiga saqlaymiz — ular deploy'dan keyin ham
    /// joyida qoladi.
    ///
    /// Yo'lni <c>App:UploadsRoot</c> sozlamasi (Railway'da <c>App__UploadsRoot</c>
    /// env o'zgaruvchisi) orqali alohida ko'rsatish ham mumkin.
    /// </summary>
    public static class UploadStorage
    {
        public const string ConfigurationKey = "App:UploadsRoot";

        /// <summary>
        /// Yuklangan fayllar saqlanadigan papkaning to'liq yo'lini aniqlaydi.
        /// </summary>
        public static string ResolveRoot(IWebHostEnvironment environment, IConfiguration configuration)
        {
            var configured = configuration[ConfigurationKey];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Path.GetFullPath(ResolveAgainst(configured.Trim(), environment.ContentRootPath));
            }

            var databasePath = TryGetDatabasePath(
                configuration.GetConnectionString("DefaultConnection"), environment.ContentRootPath);

            if (databasePath != null)
            {
                var databaseDirectory = Path.GetDirectoryName(databasePath);
                if (!string.IsNullOrWhiteSpace(databaseDirectory))
                {
                    return Path.Combine(databaseDirectory, "uploads");
                }
            }

            // Baza yo'li aniqlanmasa — eski xatti-harakat (lokal ishlab chiqish uchun).
            return Path.Combine(environment.WebRootPath, "uploads");
        }

        /// <summary>Papkani yaratadi (mavjud bo'lsa hech narsa qilmaydi).</summary>
        public static string EnsureDirectory(string root)
        {
            Directory.CreateDirectory(root);
            return root;
        }

        /// <summary>
        /// Nisbiy yo'lni (masalan "uploads/abc_file.pdf" yoki "abc_file.pdf")
        /// papka ichidagi to'liq yo'lga aylantiradi. Papkadan tashqariga chiqadigan
        /// yo'llar (path traversal) uchun null qaytadi.
        /// </summary>
        public static string? ResolveFile(string root, string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;

            var rootFull = Path.GetFullPath(root);
            var normalized = Normalize(relativePath);
            if (normalized.Length == 0) return null;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(Path.Combine(rootFull, normalized));
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }

            return fullPath.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : null;
        }

        // Eski yozuvlar "uploads/xxx" ko'rinishida saqlangan, yangilari ham shunday —
        // shuning uchun yetakchi "uploads/" bo'lagini tashlab yuboramiz.
        private static string Normalize(string relativePath)
        {
            var normalized = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            var prefix = "uploads" + Path.DirectorySeparatorChar;
            while (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[prefix.Length..];
            }

            return normalized;
        }

        private static string ResolveAgainst(string path, string baseDirectory)
            => Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path);

        private static string? TryGetDatabasePath(string? connectionString, string contentRoot)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return null;

            try
            {
                var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

                if (!builder.TryGetValue("Data Source", out var value) || value is not string source)
                {
                    return null;
                }

                if (string.IsNullOrWhiteSpace(source) ||
                    source.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return Path.GetFullPath(ResolveAgainst(source, contentRoot));
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }
    }
}
