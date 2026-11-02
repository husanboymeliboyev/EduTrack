namespace EduTrack.Services
{
    /// <summary>
    /// <see cref="IAppClock"/> ning standart implementatsiyasi.
    /// Server qaysi mintaqada joylashganidan qat'i nazar, biznes vaqtlari
    /// konfiguratsiyada berilgan mintaqa (standart: Asia/Tashkent) bo'yicha
    /// hisoblanadi. Bu Railway kabi UTC serverlarda noto'g'ri sana/vaqt
    /// saqlanishining oldini oladi.
    /// </summary>
    public sealed class AppClock : IAppClock
    {
        public const string ConfigurationKey = "App:TimeZone";
        public const string DefaultTimeZoneId = "Asia/Tashkent";

        public AppClock(TimeZoneInfo timeZone)
        {
            TimeZone = timeZone ?? throw new ArgumentNullException(nameof(timeZone));
        }

        public TimeZoneInfo TimeZone { get; }

        public DateTime Now =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);

        public DateTime Today => Now.Date;

        public DateTime UtcNow => DateTime.UtcNow;

        /// <summary>
        /// Sozlamadagi vaqt mintaqasi ID'sini hal qiladi. ID bo'sh bo'lsa
        /// standart mintaqa, noto'g'ri bo'lsa serverning mahalliy mintaqasi
        /// ishlatiladi — ilova hech qachon noto'g'ri sozlama sabab qulamaydi.
        /// </summary>
        public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
        {
            var id = string.IsNullOrWhiteSpace(timeZoneId)
                ? DefaultTimeZoneId
                : timeZoneId.Trim();

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Local;
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Local;
            }
        }
    }
}
