using EduTrack.Services;

namespace EduTrack.Tests.Infrastructure
{
    /// <summary>Testlar uchun qat'iy (fixed) vaqt — chegara testlari takrorlanadigan bo'lishi uchun.</summary>
    public sealed class TestAppClock : IAppClock
    {
        public TestAppClock(DateTime now)
        {
            Now = now;
            TimeZone = TimeZoneInfo.Utc;
        }

        public DateTime Now { get; set; }

        public DateTime Today => Now.Date;

        public DateTime UtcNow => Now.ToUniversalTime();

        public TimeZoneInfo TimeZone { get; }
    }
}
