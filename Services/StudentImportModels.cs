namespace EduTrack.Services
{
    /// <summary>Fayldan o'qilgan bitta qatorning natijasi.</summary>
    public class StudentImportRow
    {
        /// <summary>Excel qatori (1-based, sarlavha = 1).</summary>
        public int RowNumber { get; set; }

        public string Familiya { get; set; } = "";
        public string Ism { get; set; } = "";
        public string FullName => $"{Familiya} {Ism}".Trim();

        /// <summary>Fayldagi "Sinfi" qiymati (masalan "5-6", "rus").</summary>
        public string SourceClass { get; set; } = "";

        /// <summary>Tizimdagi sinf nomi ("5-6", "7", "8", "9", "10A", "10B", "10D").</summary>
        public string? ClassName { get; set; }

        /// <summary>1-7 oralig'idagi yo'nalish guruhi; null — belgilanmagan.</summary>
        public int? StreamNumber { get; set; }

        /// <summary>O'tkazib yuborilgan qator (takror yoki ajratgich).</summary>
        public string? SkipReason { get; set; }

        /// <summary>Izoh (masalan "Inomov Muxammadziyo 4-guruhda olinadi").</summary>
        public string? Note { get; set; }

        /// <summary>Import paytida biriktirilgan LoginId (preview'da bashorat, importda haqiqiy).</summary>
        public string? AssignedLoginId { get; set; }

        public bool IsStudent => SkipReason == null && ClassName != null;
    }

    public class StudentImportPreview
    {
        public List<StudentImportRow> Rows { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public int SeparatorCount { get; set; }

        public IEnumerable<StudentImportRow> Students => Rows.Where(r => r.IsStudent);
        public IEnumerable<StudentImportRow> Skipped => Rows.Where(r => !r.IsStudent);
        public int StudentCount => Students.Count();
        public bool CanImport => Errors.Count == 0 && StudentCount > 0;

        public Dictionary<string, int> CountByClass =>
            Students.GroupBy(r => r.ClassName!)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count());
    }

    public class StudentImportConfirmResult
    {
        public List<StudentImportRow> Rows { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> CreatedGroups { get; set; } = new();
        public List<string> ExistingGroups { get; set; } = new();
        public List<string> CreatedUsers { get; set; } = new();
        public List<string> SkippedExistingUsers { get; set; } = new();

        public int CreatedCount => CreatedUsers.Count;
        public bool Succeeded => Errors.Count == 0;

        public StudentImportConfirmResult() { }
    }
}
