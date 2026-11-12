using EduTrack.Services;
using Xunit;

namespace EduTrack.Tests
{
    public class SubjectVisualsTests
    {
        [Theory]
        [InlineData("Matematika", "bi-calculator", 172)]
        [InlineData("Fizika", "bi-lightning-charge", 212)]
        [InlineData("Kimyo", "bi-droplet-half", 268)]
        [InlineData("Biologiya", "bi-flower1", 142)]
        [InlineData("Informatika", "bi-code-slash", 236)]
        [InlineData("Ingliz tili", "bi-translate", 198)]
        [InlineData("Ona tili", "bi-book", 34)]
        [InlineData("Buxgalteriya", "bi-cash-coin", 154)]
        [InlineData("Tarmoq xavfsizligi", "bi-shield-lock", 218)]
        public void Get_KnownSubject_ReturnsMappedIconAndHue(string name, string icon, int hue)
        {
            var visual = SubjectVisuals.Get(name);

            Assert.Equal(icon, visual.Icon);
            Assert.Equal(hue, visual.Hue);
        }

        [Theory]
        [InlineData("MATEMATIKA")]
        [InlineData("  matematika  ")]
        [InlineData("Matematika ")]
        public void Get_IsCaseAndWhitespaceInsensitive(string name)
        {
            var visual = SubjectVisuals.Get(name);

            Assert.Equal("bi-calculator", visual.Icon);
            Assert.Equal(172, visual.Hue);
        }

        [Fact]
        public void Get_UnknownSubject_IsStableAndUsesFallbackIcon()
        {
            var first = SubjectVisuals.Get("Noma'lum fan");
            var second = SubjectVisuals.Get("Noma'lum fan");

            Assert.Equal("bi-journal-bookmark", first.Icon);
            Assert.Equal(first.Hue, second.Hue);
            Assert.InRange(first.Hue, 0, 359);
        }

        [Fact]
        public void Get_NullOrEmpty_UsesFallback()
        {
            Assert.Equal("bi-journal-bookmark", SubjectVisuals.Get(null).Icon);
            Assert.Equal("bi-journal-bookmark", SubjectVisuals.Get("   ").Icon);
        }

        [Theory]
        [InlineData("Gʻaniyev Samandar", "GS")]
        [InlineData("Aliyev Vali", "AV")]
        [InlineData("madina", "M")]
        [InlineData(null, "?")]
        [InlineData("   ", "?")]
        public void GetInitials_ReturnsAvatarLetters(string? name, string expected)
        {
            Assert.Equal(expected, SubjectVisuals.GetInitials(name));
        }
    }
}
