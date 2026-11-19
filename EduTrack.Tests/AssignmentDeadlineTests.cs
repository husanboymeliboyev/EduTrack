using EduTrack.Services;
using Xunit;

namespace EduTrack.Tests
{
    // "Faqat sana" tanlanganda vaqt 00:00 bo'lib qoladi. Bunday muddat kun
    // boshida tugab, o'quvchilarda darhol "muddati o'tgan" ko'rinmasligi uchun
    // u shu kunning oxiri (23:59) deb saqlanishi kerak.
    public class AssignmentDeadlineTests
    {
        [Fact]
        public void Midnight_is_moved_to_the_end_of_the_same_day()
        {
            var normalized = AssignmentUi.NormalizeDueDate(new DateTime(2026, 11, 24, 0, 0, 0));

            Assert.Equal(new DateTime(2026, 11, 24, 23, 59, 0), normalized);
        }

        [Theory]
        [InlineData(18, 30)]
        [InlineData(23, 59)]
        [InlineData(0, 1)]
        public void Explicit_time_is_kept(int hour, int minute)
        {
            var due = new DateTime(2026, 11, 24, hour, minute, 0);

            Assert.Equal(due, AssignmentUi.NormalizeDueDate(due));
        }

        [Fact]
        public void Date_part_is_not_changed()
        {
            var normalized = AssignmentUi.NormalizeDueDate(new DateTime(2026, 12, 31, 0, 0, 0));

            Assert.Equal(new DateTime(2026, 12, 31), normalized.Date);
        }
    }
}
