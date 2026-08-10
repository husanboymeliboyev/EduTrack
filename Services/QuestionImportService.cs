using System.Text;

namespace EduTrack.Services
{
    /// <summary>
    /// O'qituvchi AI (ChatGPT/Claude/Gemini) yordamida tayyorlagan LaTeX-uslubidagi
    /// matn faylini o'qib, savollar ro'yxatiga aylantiradi. Qavslarni "chuqurlik"
    /// bo'yicha hisoblaydi, shuning uchun \frac{1}{2} kabi ichma-ich LaTeX
    /// buyruqlari ham to'g'ri o'qiladi.
    /// </summary>
    public class QuestionImportService : IQuestionImportService
    {
        public string CreateTemplateText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("% EduTrack savollar shabloni");
            sb.AppendLine("% Har bir savol \\question{} bilan boshlanadi.");
            sb.AppendLine("% Har bir variant \\option{} (noto'g'ri) yoki \\option*{} (to'g'ri) bilan yoziladi.");
            sb.AppendLine("% Savollar orasida bo'sh qator qoldiring. Matematik formulalar uchun $...$ ishlating.");
            sb.AppendLine();
            sb.AppendLine("\\question{Agar $x^2 - 5x + 6 = 0$ bo'lsa, $x$ ning qiymatlarini toping.}");
            sb.AppendLine("\\option{x=1, x=6}");
            sb.AppendLine("\\option*{x=2, x=3}");
            sb.AppendLine("\\option{x=-2, x=-3}");
            sb.AppendLine("\\option{x=0, x=5}");
            sb.AppendLine();
            sb.AppendLine("\\question{Uchburchakning yuzini toping: asos $8$ sm, balandlik $5$ sm.}");
            sb.AppendLine("\\option*{20 sm^2}");
            sb.AppendLine("\\option{40 sm^2}");
            sb.AppendLine("\\option{13 sm^2}");
            return sb.ToString();
        }

        public QuestionImportResult Parse(string rawText)
        {
            var result = new QuestionImportResult();

            if (string.IsNullOrWhiteSpace(rawText))
            {
                result.Errors.Add("Fayl bo'sh yoki o'qib bo'lmadi.");
                return result;
            }

            rawText = StripComments(rawText);

            // 1-bosqich: barcha \question{}, \option{}, \option*{} buyruqlarini
            // hujjatdagi tartibda, qavs chuqurligini hisoblab ajratib olamiz.
            var tokens = new List<(string Type, string Content)>();
            int pos = 0;

            while (pos < rawText.Length)
            {
                var next = FindNextCommand(rawText, pos);
                if (next == null) break;

                var (commandIndex, type, bracePos) = next.Value;

                string content;
                int closeBraceIndex;
                if (!TryExtractBraced(rawText, bracePos, out content, out closeBraceIndex))
                {
                    result.Errors.Add($"Qavs yopilmagan: \"...{Snippet(rawText, commandIndex)}...\" atrofida. Import to'xtatildi.");
                    return result;
                }

                tokens.Add((type, content.Trim()));
                pos = closeBraceIndex + 1;
            }

            // 2-bosqich: buyruqlarni savollarga guruhlaymiz
            ParsedQuestion? current = null;
            foreach (var (type, content) in tokens)
            {
                if (type == "question")
                {
                    current = new ParsedQuestion { Text = content };
                    result.Questions.Add(current);
                }
                else if (current == null)
                {
                    result.Errors.Add($"\"\\question{{}}\"dan oldin variant topildi: \"{Snippet(content, 0)}\" — fayl formatini tekshiring.");
                }
                else
                {
                    current.Options.Add(new ParsedOption { Text = content, IsCorrect = type == "option_correct" });
                }
            }

            if (result.Questions.Count == 0 && result.Errors.Count == 0)
            {
                result.Errors.Add("Faylda birorta ham \\question{} topilmadi. Shablonga qarab qaytadan tekshiring.");
                return result;
            }

            // 3-bosqich: har bir savolni tekshiramiz
            for (int i = 0; i < result.Questions.Count; i++)
            {
                var q = result.Questions[i];
                int qNum = i + 1;

                if (string.IsNullOrWhiteSpace(q.Text))
                    result.Errors.Add($"{qNum}-savol: matn bo'sh.");

                if (q.Options.Count < 2)
                    result.Errors.Add($"{qNum}-savol: kamida 2 ta variant kerak (hozir {q.Options.Count} ta).");
                else if (q.Options.Count > 4)
                    result.Errors.Add($"{qNum}-savol: ko'pi bilan 4 ta variant bo'lishi mumkin (hozir {q.Options.Count} ta).");

                var correctCount = q.Options.Count(o => o.IsCorrect);
                if (correctCount == 0)
                    result.Errors.Add($"{qNum}-savol: to'g'ri javob belgilanmagan (\\option* kerak).");
                else if (correctCount > 1)
                    result.Errors.Add($"{qNum}-savol: bir nechta to'g'ri javob belgilangan, faqat bittasi bo'lishi kerak.");
            }

            return result;
        }

        private static (int CommandIndex, string Type, int BracePos)? FindNextCommand(string text, int fromIndex)
        {
            var candidates = new (string Marker, string Type)[]
            {
                ("\\question{", "question"),
                ("\\option*{", "option_correct"),
                ("\\option{", "option"),
            };

            int bestIndex = -1;
            string bestType = "";
            int bestBracePos = -1;

            foreach (var (marker, type) in candidates)
            {
                int idx = text.IndexOf(marker, fromIndex, StringComparison.Ordinal);
                if (idx >= 0 && (bestIndex == -1 || idx < bestIndex))
                {
                    bestIndex = idx;
                    bestType = type;
                    bestBracePos = idx + marker.Length - 1; // '{' belgisining o'zi
                }
            }

            return bestIndex == -1 ? null : (bestIndex, bestType, bestBracePos);
        }

        // Qavs ichidagi matnni, ichma-ich qavslarni hisobga olib ajratib oladi
        private static bool TryExtractBraced(string text, int openBraceIndex, out string content, out int closeBraceIndex)
        {
            int depth = 0;
            for (int i = openBraceIndex; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        content = text.Substring(openBraceIndex + 1, i - openBraceIndex - 1);
                        closeBraceIndex = i;
                        return true;
                    }
                }
            }
            content = string.Empty;
            closeBraceIndex = -1;
            return false;
        }

        private static string Snippet(string text, int fromIndex)
        {
            var start = Math.Max(0, fromIndex);
            var len = Math.Min(40, text.Length - start);
            return text.Substring(start, len);
        }
        // Har bir qatordagi '%' belgisidan boshlab, qator oxirigacha bo'lgan
        // qismni izoh sifatida olib tashlaydi (haqiqiy LaTeX qoidasiga mos).
        private static string StripComments(string text)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder();

            foreach (var line in lines)
            {
                var percentIndex = line.IndexOf('%');
                var cleaned = percentIndex >= 0 ? line.Substring(0, percentIndex) : line;
                sb.AppendLine(cleaned);
            }

            return sb.ToString();
        }
    }
}