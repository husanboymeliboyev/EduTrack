using EduTrack.Models;
using EduTrack.Services;
using Xunit;

namespace EduTrack.Tests
{
    // Topshiriqlar moduli: yangi fayl turlari, matnli javob va fayl turini
    // aniqlash yordamchilari uchun testlar.
    public class AssignmentSubmissionFeaturesTests
    {
        [Theory]
        [InlineData(".py")]
        [InlineData(".cpp")]
        [InlineData(".c")]
        [InlineData(".h")]
        [InlineData(".hpp")]
        [InlineData(".java")]
        [InlineData(".js")]
        [InlineData(".ts")]
        [InlineData(".cs")]
        [InlineData(".go")]
        [InlineData(".rs")]
        [InlineData(".sql")]
        [InlineData(".json")]
        [InlineData(".html")]
        [InlineData(".css")]
        [InlineData(".txt")]
        [InlineData(".md")]
        [InlineData(".sh")]
        public void Programming_and_text_extensions_are_allowed(string extension)
        {
            Assert.Contains(extension, FileUploadService.AllowedExtensions);
            Assert.True(FileUploadService.IsAllowedExtension("main" + extension));
            Assert.True(FileUploadService.IsAllowedExtension("MAIN" + extension.ToUpperInvariant()));
        }

        [Theory]
        [InlineData(".exe")]
        [InlineData(".dll")]
        [InlineData(".svg")]
        [InlineData(".psd")]
        [InlineData("")]
        public void Unsupported_extensions_are_rejected(string extension)
        {
            Assert.False(FileUploadService.IsAllowedExtension("file" + extension));
        }

        [Fact]
        public void Accept_attribute_contains_every_allowed_extension()
        {
            foreach (var extension in FileUploadService.AllowedExtensions)
            {
                Assert.Contains(extension, FileUploadService.AcceptAttribute);
            }
        }

        [Theory]
        [InlineData("main.py", "Python")]
        [InlineData("solution.cpp", "C++")]
        [InlineData("Program.cs", "C#")]
        [InlineData("script.sql", "SQL")]
        [InlineData("hisobot.pdf", "PDF")]
        [InlineData("arxiv.zip", "Arxiv")]
        [InlineData("rasm.png", "Rasm")]
        public void File_kind_is_detected_from_extension(string fileName, string expectedLabel)
        {
            var kind = FileKinds.Resolve(fileName);

            Assert.Equal(expectedLabel, kind.Label);
            Assert.StartsWith("bi-", kind.Icon);
            Assert.StartsWith("is-", kind.Tone);
        }

        [Fact]
        public void Unknown_file_kind_falls_back_to_generic_icon()
        {
            var kind = FileKinds.Resolve("fayl.unknown");

            Assert.Equal("Fayl", kind.Label);
            Assert.Equal("bi-file-earmark", kind.Icon);
        }

        [Fact]
        public void Empty_file_name_falls_back_to_generic_icon()
        {
            Assert.Equal("Fayl", FileKinds.Resolve(null).Label);
            Assert.Equal("Fayl", FileKinds.Resolve("").Label);
        }

        [Fact]
        public void Answer_text_is_html_encoded()
        {
            var html = AnswerTextFormatter.ToHtml("<script>alert('x')</script>");

            Assert.DoesNotContain("<script>", html);
            Assert.Contains("&lt;script&gt;", html);
        }

        [Fact]
        public void Answer_text_new_lines_become_breaks()
        {
            var html = AnswerTextFormatter.ToHtml("1) x = 2\r\n2) y = 3\n3) z = 4");

            Assert.Equal("1) x = 2<br />2) y = 3<br />3) z = 4", html);
        }

        [Fact]
        public void Answer_text_empty_returns_empty_html()
        {
            Assert.Equal(string.Empty, AnswerTextFormatter.ToHtml(null));
            Assert.Equal(string.Empty, AnswerTextFormatter.ToHtml("   "));
        }

        [Fact]
        public void Assignment_defaults_to_file_or_text_mode()
        {
            Assert.Equal(SubmissionMode.FileOrText, new Assignment().SubmissionMode);
            Assert.Null(new Submission().TextAnswer);
        }

        [Theory]
        [InlineData(SubmissionMode.FileOrText, true, true)]
        [InlineData(SubmissionMode.FileOnly, true, false)]
        [InlineData(SubmissionMode.TextOnly, false, true)]
        public void Answer_mode_controls_available_inputs(SubmissionMode mode, bool fileAllowed, bool textAllowed)
        {
            Assert.Equal(fileAllowed, AssignmentUi.AllowsFile(mode));
            Assert.Equal(textAllowed, AssignmentUi.AllowsText(mode));
        }
    }
}
