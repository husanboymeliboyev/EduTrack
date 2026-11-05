namespace EduTrack.Services
{
    /// <summary>
    /// Davomat moduli sozlamalari. Konfiguratsiyada "App:Attendance" bo'limi ostida
    /// beriladi (appsettings.json).
    /// </summary>
    public sealed class AttendanceOptions
    {
        public const string SectionName = "App:Attendance";

        /// <summary>
        /// O'tmishga necha kun orqaga davomat belgilash mumkin. Standart — 30 kun.
        /// Admin uchun keyinchalik cheklovsiz qilish mumkin (D4 bosqichi).
        /// </summary>
        public int BackfillDays { get; set; } = 30;
    }
}
