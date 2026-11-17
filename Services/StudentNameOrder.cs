using System.Globalization;
using System.Text;

namespace EduTrack.Services
{
    /// <summary>
    /// O'quvchi ismini familiyasi bo'yicha tartiblash uchun yordamchi.
    /// Tizimda <see cref="Models.ApplicationUser.FullName"/> "Familiya Ism"
    /// ko'rinishida saqlanadi (import ham shunday yozadi), shuning uchun
    /// birinchi so'z familiya hisoblanadi.
    ///
    /// Taqqoslashda apostrof turlari (G' / Gʻ / G‘) va diakritik belgilar
    /// e'tiborsiz qoldiriladi — natijada "G'aniyev" va "Gʻaniyev" yonma-yon
    /// turadi, lotin/kirill harflari esa barqaror tartibda saralanadi.
    /// </summary>
    public static class StudentNameOrder
    {
        private static readonly IComparer<string?> Comparer =
            Comparer<string?>.Create(Compare);

        /// <summary>Familiya — ismning birinchi so'zi (bo'sh bo'lsa butun ism).</summary>
        public static string Surname(string? fullName)
        {
            var normalized = NormalizeWhitespace(fullName);
            if (normalized.Length == 0)
            {
                return string.Empty;
            }

            var spaceIndex = normalized.IndexOf(' ');
            return spaceIndex < 0 ? normalized : normalized[..spaceIndex];
        }

        /// <summary>Familiya, keyin to'liq ism bo'yicha taqqoslaydi.</summary>
        public static int Compare(string? left, string? right)
        {
            var bySurname = string.CompareOrdinal(SortKey(Surname(left)), SortKey(Surname(right)));
            if (bySurname != 0)
            {
                return bySurname;
            }

            return string.CompareOrdinal(SortKey(left), SortKey(right));
        }

        /// <summary>Ro'yxatni familiya bo'yicha tartiblaydi (barqaror tartib).</summary>
        public static List<T> SortBySurname<T>(IEnumerable<T> source, Func<T, string?> nameSelector)
        {
            return source
                .OrderBy(nameSelector, Comparer)
                .ToList();
        }

        private static string NormalizeWhitespace(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return string.Join(
                ' ',
                value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string SortKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var decomposed = NormalizeWhitespace(value).Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);

            foreach (var ch in decomposed)
            {
                // Diakritik belgilarni tashlaymiz (á → a, oʻ → o kabi).
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                // Apostrofning barcha ko'rinishlarini butunlay olib tashlaymiz.
                if (ch is '\'' or '\u2018' or '\u2019' or '\u02BB' or '\u02BC' or '`')
                {
                    continue;
                }

                builder.Append(char.ToUpperInvariant(ch));
            }

            return builder.ToString();
        }
    }
}
