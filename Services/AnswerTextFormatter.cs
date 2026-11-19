using System.Text.Encodings.Web;

namespace EduTrack.Services
{
    /// <summary>
    /// O'quvchining matnli javobini xavfsiz HTML ko'rinishiga keltiradi.
    /// Matn avval HTML-encode qilinadi (XSS oldini olish uchun), keyin qator
    /// uzilishlari &lt;br /&gt; ga aylanadi. Formulalar $...$ ichida yoziladi va
    /// sahifadagi KaTeX auto-render tomonidan chiziladi.
    /// </summary>
    public static class AnswerTextFormatter
    {
        public static string ToHtml(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            // Avval qatorlarga bo'lamiz, keyin har bir qatorni alohida encode
            // qilamiz — aks holda HtmlEncoder qator uzilishini ham &#xA; ga
            // aylantirib, <br /> ga almashtirish ishlamay qoladi.
            var normalized = text.Trim()
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');

            return string.Join("<br />", normalized
                .Split('\n')
                .Select(HtmlEncoder.Default.Encode));
        }
    }
}
