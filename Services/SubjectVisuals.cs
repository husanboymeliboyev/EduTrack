namespace EduTrack.Services
{
    /// <summary>
    /// Fan nomiga qarab barqaror ikonka va rang (hue) tanlaydi.
    /// Bir xil fan nomi har doim bir xil ko'rinishga ega bo'ladi.
    /// </summary>
    public static class SubjectVisuals
    {
        private const string DefaultIcon = "bi-journal-bookmark";

        private static readonly SubjectVisualRule[] Rules =
        {
            new(new[] { "matematika", "algebra", "geometriya", "hisob" }, "bi-calculator", 172),
            new(new[] { "fizika" }, "bi-lightning-charge", 212),
            new(new[] { "astronomiya" }, "bi-stars", 250),
            new(new[] { "kimyo" }, "bi-droplet-half", 268),
            new(new[] { "biologiya", "botanika", "zoologiya", "anatomiya" }, "bi-flower1", 142),
            new(new[] { "tarmoq", "kiberxavfsizlik" }, "bi-shield-lock", 218),
            new(new[] { "informatika", "axborot texnologiya", "dasturlash", "python", "data science", "sun'iy intellekt", "c++", "c#", "java", "web" }, "bi-code-slash", 236),
            new(new[] { "ingliz" }, "bi-translate", 198),
            new(new[] { "rus" }, "bi-translate", 338),
            new(new[] { "ona tili", "adabiyot", "o'zbek" }, "bi-book", 34),
            new(new[] { "tarix" }, "bi-bank", 24),
            new(new[] { "geografiya" }, "bi-globe2", 188),
            new(new[] { "marketing", "iqtisod", "buxgalteriya", "moliya", "tadbirkorlik" }, "bi-cash-coin", 154),
            new(new[] { "musiqa" }, "bi-music-note-beamed", 300),
            new(new[] { "san'at", "tasviriy", "chizmachilik" }, "bi-palette", 16),
            new(new[] { "jismoniy", "sport" }, "bi-dribbble", 96),
            new(new[] { "robototexnika" }, "bi-robot", 262),
            new(new[] { "huquq", "qonun" }, "bi-shield-check", 222)
        };

        public static SubjectVisual Get(string? name)
        {
            var normalized = Normalize(name);
            if (normalized.Length > 0)
            {
                foreach (var rule in Rules)
                {
                    if (rule.Keywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal)))
                    {
                        return new SubjectVisual(rule.Icon, rule.Hue);
                    }
                }
            }

            var hue = ScheduleService.GetHueForSubject(normalized.Length == 0 ? "fan" : normalized);
            return new SubjectVisual(DefaultIcon, hue);
        }

        /// <summary>Ism-familiyadan avatar uchun bosh harflar (masalan, "AV").</summary>
        public static string GetInitials(string? value, int maxLetters = 2)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "?";
            }

            var parts = value.Split(new[] { ' ', '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "?";
            }

            return new string(parts.Take(maxLetters).Select(part => char.ToUpperInvariant(part[0])).ToArray());
        }

        private static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var lower = value.Trim().ToLowerInvariant()
                .Replace('’', '\'')
                .Replace('‘', '\'')
                .Replace('ʻ', '\'');

            return string.Join(' ', lower.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private sealed record SubjectVisualRule(string[] Keywords, string Icon, int Hue);
    }

    public readonly record struct SubjectVisual(string Icon, int Hue);
}
