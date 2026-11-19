namespace EduTrack.Services
{
    /// <summary>
    /// Fayl turi haqida qisqa ma'lumot: ikonka (Bootstrap Icons), yorliq va rang sinfi.
    /// O'quvchi va o'qituvchi sahifalarida faylni darhol tanib olish uchun ishlatiladi.
    /// </summary>
    public sealed record FileKind(string Icon, string Label, string Tone);

    public static class FileKinds
    {
        private static readonly FileKind Default = new("bi-file-earmark", "Fayl", "is-default");

        public static FileKind Resolve(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return Default;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            return extension switch
            {
                ".pdf" => new("bi-filetype-pdf", "PDF", "is-pdf"),

                ".doc" or ".docx" or ".odt" or ".rtf" =>
                    new("bi-file-earmark-word", "Hujjat", "is-doc"),

                ".ppt" or ".pptx" or ".odp" =>
                    new("bi-file-earmark-ppt", "Taqdimot", "is-slide"),

                ".xls" or ".xlsx" or ".ods" or ".csv" =>
                    new("bi-file-earmark-spreadsheet", "Jadval", "is-sheet"),

                ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" =>
                    new("bi-file-earmark-image", "Rasm", "is-image"),

                ".zip" or ".rar" or ".7z" =>
                    new("bi-file-earmark-zip", "Arxiv", "is-archive"),

                ".py" => new("bi-filetype-py", "Python", "is-code"),
                ".ipynb" => new("bi-file-earmark-code", "Jupyter", "is-code"),
                ".cpp" or ".cc" or ".cxx" => new("bi-file-earmark-code", "C++", "is-code"),
                ".c" => new("bi-file-earmark-code", "C", "is-code"),
                ".h" or ".hpp" => new("bi-file-earmark-code", "C/C++ sarlavha", "is-code"),
                ".cs" => new("bi-filetype-cs", "C#", "is-code"),
                ".java" => new("bi-file-earmark-code", "Java", "is-code"),
                ".kt" or ".kts" => new("bi-file-earmark-code", "Kotlin", "is-code"),
                ".js" => new("bi-filetype-js", "JavaScript", "is-code"),
                ".jsx" => new("bi-file-earmark-code", "React JSX", "is-code"),
                ".ts" or ".tsx" => new("bi-file-earmark-code", "TypeScript", "is-code"),
                ".go" => new("bi-file-earmark-code", "Go", "is-code"),
                ".rs" => new("bi-file-earmark-code", "Rust", "is-code"),
                ".rb" => new("bi-filetype-rb", "Ruby", "is-code"),
                ".php" => new("bi-filetype-php", "PHP", "is-code"),
                ".swift" => new("bi-file-earmark-code", "Swift", "is-code"),
                ".lua" => new("bi-file-earmark-code", "Lua", "is-code"),
                ".r" => new("bi-file-earmark-code", "R", "is-code"),
                ".m" => new("bi-file-earmark-code", "MATLAB", "is-code"),
                ".sql" => new("bi-filetype-sql", "SQL", "is-code"),
                ".json" => new("bi-filetype-json", "JSON", "is-code"),
                ".xml" => new("bi-filetype-xml", "XML", "is-code"),
                ".yml" or ".yaml" => new("bi-filetype-yml", "YAML", "is-code"),
                ".html" => new("bi-filetype-html", "HTML", "is-code"),
                ".css" or ".scss" => new("bi-filetype-css", "CSS", "is-code"),
                ".sh" => new("bi-filetype-sh", "Shell", "is-code"),
                ".bat" or ".ps1" => new("bi-terminal", "Skript", "is-code"),
                ".asm" => new("bi-file-earmark-code", "Assembler", "is-code"),
                ".pas" => new("bi-file-earmark-code", "Pascal", "is-code"),
                ".md" => new("bi-filetype-md", "Markdown", "is-text"),
                ".txt" => new("bi-file-text", "Matn", "is-text"),

                _ => Default
            };
        }
    }
}
