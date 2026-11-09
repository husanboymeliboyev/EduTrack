namespace EduTrack.Models
{
    /// <summary>
    /// Oylik reyting davrlari uchun yagona nomlash/ayirish yordamchisi.
    /// Oy nomlari o'zbek tilida; hisob-kitoblar <see cref="DateTime"/> orqali
    /// bajariladi (yil chegarasi ham to'g'ri ishlaydi).
    /// </summary>
    public static class MonthlyPeriod
    {
        private static readonly string[] Names =
        {
            string.Empty,
            "Yanvar", "Fevral", "Mart", "Aprel", "May", "Iyun",
            "Iyul", "Avgust", "Sentabr", "Oktabr", "Noyabr", "Dekabr"
        };

        private static readonly string[] ShortNames =
        {
            string.Empty,
            "Yan", "Fev", "Mar", "Apr", "May", "Iyn",
            "Iyl", "Avg", "Sen", "Okt", "Noy", "Dek"
        };

        public static bool IsValidMonth(int month) => month is >= 1 and <= 12;

        public static string Name(int month) => IsValidMonth(month) ? Names[month] : string.Empty;

        public static string ShortName(int month) => IsValidMonth(month) ? ShortNames[month] : string.Empty;

        public static string Label(int year, int month) => $"{Name(month)} {year}";

        public static (int Year, int Month) AddMonths(int year, int month, int delta)
        {
            var date = new DateTime(year, month, 1).AddMonths(delta);
            return (date.Year, date.Month);
        }

        /// <summary>Oyning oxirgi kuni (tarixiy a'zolikni aniqlash uchun).</summary>
        public static DateTime EndOfMonth(int year, int month)
            => new(year, month, DateTime.DaysInMonth(year, month));
    }
}
