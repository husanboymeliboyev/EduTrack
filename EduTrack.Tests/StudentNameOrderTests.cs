using EduTrack.Services;
using Xunit;

namespace EduTrack.Tests
{
    /// <summary>
    /// O'quvchi ismini familiyasi bo'yicha tartiblash. Tizimda FullName
    /// "Familiya Ism" ko'rinishida, shuning uchun birinchi so'z — familiya.
    /// </summary>
    public class StudentNameOrderTests
    {
        [Theory]
        [InlineData("Aliyev Vali", "Aliyev")]
        [InlineData("  G'aniyev   Anvar ", "G'aniyev")]
        [InlineData("Karimov", "Karimov")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void Surname_ReturnsFirstWord(string? fullName, string expected)
        {
            Assert.Equal(expected, StudentNameOrder.Surname(fullName));
        }

        [Fact]
        public void Compare_SortsBySurnameNotFirstName()
        {
            // Ism bo'yicha "Anvar" birinchi bo'lardi; familiya bo'yicha
            // "Aliyev" "Turdiyev" dan oldin turadi.
            Assert.True(StudentNameOrder.Compare("Turdiyev Anvar", "Aliyev Bobur") > 0);
            Assert.True(StudentNameOrder.Compare("Aliyev Bobur", "Turdiyev Anvar") < 0);
        }

        [Fact]
        public void Compare_IgnoresApostropheVariants()
        {
            // G' / Gʻ / G‘ — barchasi bir xil harf sifatida taqqoslanadi.
            Assert.Equal(
                0,
                StudentNameOrder.Compare("G'aniyev Anvar", "G\u02BBaniyev Anvar"));
            Assert.Equal(
                0,
                StudentNameOrder.Compare("G'aniyev Anvar", "G\u2018aniyev Anvar"));
        }

        [Fact]
        public void Compare_IgnoresDiacritics()
        {
            Assert.Equal(
                0,
                StudentNameOrder.Compare("O\u02BBktamov Sardor", "O'ktamov Sardor"));
        }

        [Fact]
        public void SortBySurname_OrdersRowsBySurnameAndIsStable()
        {
            var rows = new[]
            {
                "Turdiyev Anvar",
                "Aliyev Bobur",
                "Aliyev Aziz",
                "Karimov Vali"
            };

            var sorted = StudentNameOrder.SortBySurname(rows, r => r);

            Assert.Equal(
                new[]
                {
                    "Aliyev Aziz",
                    "Aliyev Bobur",
                    "Karimov Vali",
                    "Turdiyev Anvar"
                },
                sorted);
        }
    }
}
