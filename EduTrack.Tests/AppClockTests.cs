using EduTrack.Models;
using EduTrack.Services;
using Xunit;

namespace EduTrack.Tests
{
    public class AppClockTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ResolveTimeZone_UsesDefaultWhenMissing(string? timeZoneId)
        {
            var timeZone = AppClock.ResolveTimeZone(timeZoneId);

            Assert.Equal(AppClock.DefaultTimeZoneId, timeZone.Id);
        }

        [Fact]
        public void ResolveTimeZone_ResolvesIanaId()
        {
            var timeZone = AppClock.ResolveTimeZone("Asia/Tashkent");

            Assert.Equal(TimeSpan.FromHours(5), timeZone.BaseUtcOffset);
        }

        // Noto'g'ri sozlama ilovani qulatmasligi kerak — mahalliy mintaqaga
        // qaytishi va ishlashda davom etishi kerak.
        [Fact]
        public void ResolveTimeZone_FallsBackToLocalForUnknownId()
        {
            var timeZone = AppClock.ResolveTimeZone("Not/A-Real-Zone");

            Assert.Equal(TimeZoneInfo.Local.Id, timeZone.Id);
        }

        [Fact]
        public void Now_IsConvertedFromUtcUsingConfiguredZone()
        {
            var timeZone = TimeZoneInfo.CreateCustomTimeZone(
                "Test/PlusFive",
                TimeSpan.FromHours(5),
                "Test +5",
                "Test +5");
            var clock = new AppClock(timeZone);

            var difference = (clock.Now - clock.UtcNow).TotalHours;

            Assert.InRange(difference, 4.99, 5.01);
        }

        [Fact]
        public void Today_IsDatePartOfNow()
        {
            var clock = new AppClock(AppClock.ResolveTimeZone("Asia/Tashkent"));

            Assert.Equal(clock.Now.Date, clock.Today);
        }

        [Fact]
        public void Exam_IsCurrentlyOpenAt_RespectsManualAndScheduledWindows()
        {
            var now = new DateTime(2026, 11, 2, 10, 0, 0);
            var exam = new Exam { IsOpen = true };

            Assert.True(exam.IsCurrentlyOpenAt(now));

            exam.OpenAt = now.AddMinutes(5);
            Assert.False(exam.IsCurrentlyOpenAt(now));
            Assert.True(exam.IsCurrentlyOpenAt(now.AddMinutes(5)));

            exam.OpenAt = null;
            exam.CloseAt = now.AddMinutes(-1);
            Assert.False(exam.IsCurrentlyOpenAt(now));

            exam.CloseAt = now.AddMinutes(1);
            Assert.True(exam.IsCurrentlyOpenAt(now));

            exam.IsOpen = false;
            Assert.False(exam.IsCurrentlyOpenAt(now));
        }
    }
}
