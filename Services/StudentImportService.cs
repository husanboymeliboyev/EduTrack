using ClosedXML.Excel;
using EduTrack.Data;
using EduTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Services
{
    /// <summary>
    /// Batafsil qoidalar IStudentImportService izohida keltirilgan.
    /// Bu sinf faqat o'qish va (ixtiyoriy) yozishni bajaradi; fayl tuzilishi
    /// o'zgarmasa, schema-migratsiyasiz ishlaydi.
    /// </summary>
    public class StudentImportService : IStudentImportService
    {
        /// <summary>Vaqtinchalik parol — birinchi kirishda majburiy almashtiriladi.</summary>
        public const string InitialPassword = "parol123";

        private static readonly Dictionary<string, string> NormalizedClassNames =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["5"] = "5-6",
                ["5-6"] = "5-6",
                ["6"] = "5-6",
                ["7"] = "7",
                ["8"] = "8",
                ["9"] = "9",
                ["rus"] = "10D",
            };

        /// <summary>Bir xil ismli takror qatorlar: ko'rsatilgan bo'limdagi qator qoladi, boshqasi tashlanadi.</summary>
        private static readonly Dictionary<string, string> DuplicateRules =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Adhamov Zakaryo"] = "rus",
                ["G'aybullayev Oybek"] = "rus",
                ["Xokimov Shamsiddin"] = "rus",
                ["Shodiyev Amirbek"] = "6", // 6-sinf qatori qoladi
            };

        /// <summary>Faylda bo'sh qolgan guruhni admin tasdiqlashi bilan to'ldiramiz.</summary>
        private static readonly Dictionary<string, int> StreamOverrides =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Abdurazzoqov Islombek"] = 5,
            };

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAppClock _clock;

        public StudentImportService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAppClock clock)
        {
            _context = context;
            _userManager = userManager;
            _clock = clock;
        }

        public StudentImportPreview Preview(Stream xlsxStream)
        {
            var rows = ParseRows(xlsxStream);
            var preview = new StudentImportPreview { Rows = rows.Rows, Errors = rows.Errors, SeparatorCount = rows.SeparatorCount };

            var nextId = GetNextLoginIdBase();
            foreach (var row in preview.Students)
            {
                row.AssignedLoginId = (++nextId).ToString();
            }

            return preview;
        }

        public async Task<StudentImportConfirmResult> ImportAsync(Stream xlsxStream)
        {
            var parsed = ParseRows(xlsxStream);
            var result = new StudentImportConfirmResult { Rows = parsed.Rows, Errors = parsed.Errors };

            if (parsed.Errors.Count > 0)
            {
                return result;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var nextId = GetNextLoginIdBase();

                // 1) Kerakli sinflar va guruhlarni topib yoki yaratamiz
                var classGroups = await EnsureGroupsAsync(
                    parsed.Rows.Where(r => r.IsStudent).Select(r => r.ClassName!).Distinct(),
                    GroupType.Class,
                    name => new Group { Name = name, Type = GroupType.Class, Code = name },
                    result);

                var streamGroups = await EnsureGroupsAsync(
                    parsed.Rows.Where(r => r.IsStudent && r.StreamNumber.HasValue)
                        .Select(r => $"{r.StreamNumber}-guruh").Distinct(),
                    GroupType.Stream,
                    name =>
                    {
                        var number = int.Parse(name.Split('-')[0]);
                        return new Group { Name = name, Type = GroupType.Stream, Code = number.ToString() };
                    },
                    result);

                // 2) Avvaldan mavjud Student rolidagi o'quvchilar (a'zoligi hali
                // yozilmagan eski yozuvlar ham) — takror hisob yaratmaslik uchun.
                var existingStudentNames = new HashSet<string>(StringComparer.Ordinal);
                var studentRoleId = await _context.Roles
                    .Where(r => r.Name == "Student")
                    .Select(r => r.Id)
                    .FirstOrDefaultAsync();

                if (studentRoleId != null)
                {
                    var existingNames = await _context.Users
                        .AsNoTracking()
                        .Where(u => u.FullName != null && _context.UserRoles
                            .Any(ur => ur.UserId == u.Id && ur.RoleId == studentRoleId))
                        .Select(u => u.FullName!)
                        .ToListAsync();

                    foreach (var name in existingNames)
                    {
                        existingStudentNames.Add(NormalizeKey(name));
                    }
                }

                // 3) O'quvchilarni yaratamiz
                foreach (var row in parsed.Rows.Where(r => r.IsStudent))
                {
                    var classGroup = classGroups[row.ClassName!];
                    var streamGroup = row.StreamNumber.HasValue ? streamGroups[$"{row.StreamNumber}-guruh"] : null;

                    // Idempotentlik: shu ismli o'quvchi avvaldan bo'lsa — yangi hisob
                    // yaratmaymiz. Eski yozuvlarda a'zolik bo'lmasligi ham mumkin.
                    if (existingStudentNames.Contains(NormalizeKey(row.FullName)))
                    {
                        result.SkippedExistingUsers.Add($"{row.FullName} — allaqachon mavjud");
                        continue;
                    }

                    var loginId = (++nextId).ToString();
                    var user = new ApplicationUser
                    {
                        UserName = loginId,
                        LoginId = loginId,
                        FullName = row.FullName,
                        EmailConfirmed = true,
                        MustChangePassword = true, // parol123 — birinchi kirishda majburiy almashtiriladi
                        GroupId = classGroup.Id,
                    };

                    var created = await _userManager.CreateAsync(user, InitialPassword);
                    if (!created.Succeeded)
                    {
                        result.Errors.Add($"{row.FullName} — yaratib bo'lmadi: {string.Join(", ", created.Errors.Select(e => e.Description))}");
                        await transaction.RollbackAsync();
                        return result;
                    }

                    await _userManager.AddToRoleAsync(user, "Student");
                    existingStudentNames.Add(NormalizeKey(row.FullName));

                    _context.GroupMemberships.Add(new GroupMembership
                    {
                        StudentId = user.Id,
                        GroupId = classGroup.Id,
                        Scope = GroupType.Class,
                        StartDate = _clock.Today.Date,
                        IsActive = true,
                    });

                    if (streamGroup != null)
                    {
                        _context.GroupMemberships.Add(new GroupMembership
                        {
                            StudentId = user.Id,
                            GroupId = streamGroup.Id,
                            Scope = GroupType.Stream,
                            StartDate = _clock.Today.Date,
                            IsActive = true,
                        });
                    }

                    row.AssignedLoginId = loginId;
                    result.CreatedUsers.Add($"{loginId} — {row.FullName} ({row.ClassName}, {(streamGroup != null ? streamGroup.Name : "guruhsiz")})");
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                result.Errors.Add("Import bekor qilindi: " + ex.Message);
                return result;
            }
        }

        // ==================== ICHKI USULLAR ====================

        private sealed record ParsedRows(
            List<StudentImportRow> Rows,
            List<string> Errors,
            int SeparatorCount);

        private ParsedRows ParseRows(Stream xlsxStream)
        {
            var rows = new List<StudentImportRow>();
            var errors = new List<string>();
            var separatorCount = 0;

            using var workbook = new XLWorkbook(xlsxStream);
            var ws = workbook.Worksheet(1);
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            // 1-o'tish: xom qatorlarni o'qiymiz
            var raws = new List<(int RowNumber, string Familiya, string Ism, string Sinf, string Guruh)>();
            for (var r = 2; r <= lastRow; r++)
            {
                var fam = CellText(ws, r, 2);
                var ism = CellText(ws, r, 3);
                var sinf = CellText(ws, r, 4);
                var guruh = CellText(ws, r, 5);
                raws.Add((r, fam, ism, sinf, guruh));
            }

            // Bir xil ismli "Familiya bo'sh" qatorni topish uchun (Inomov Muxammadziyo hollari)
            var fullByIsm = raws
                .Where(x => x.Familiya.Length > 0 && x.Ism.Length > 0)
                .GroupBy(x => NormalizeKey(x.Ism))
                .ToDictionary(g => g.Key, g => g.ToList());

            bool inTenEleven = false;
            bool tenElevenSeparatorSeen = false;

            foreach (var (rowNumber, fam, ism, sinf, guruh) in raws)
            {
                var row = new StudentImportRow
                {
                    RowNumber = rowNumber,
                    Familiya = fam,
                    Ism = ism,
                    SourceClass = sinf,
                };
                rows.Add(row);

                var allEmpty = fam.Length == 0 && ism.Length == 0 && sinf.Length == 0 && guruh.Length == 0;

                if (allEmpty)
                {
                    separatorCount++;
                    if (inTenEleven)
                    {
                        tenElevenSeparatorSeen = true;
                    }
                    row.SkipReason = "Ajratgich qatori";
                    continue;
                }

                // ===== Sinfni aniqlash =====
                if (NormalizedClassNames.TryGetValue(sinf, out var mapped))
                {
                    row.ClassName = mapped;
                    inTenEleven = false;
                }
                else if (string.Equals(sinf, "10-11", StringComparison.OrdinalIgnoreCase))
                {
                    inTenEleven = true;
                    row.ClassName = tenElevenSeparatorSeen ? "10B" : "10A";
                }
                else
                {
                    row.SkipReason = $"Noma'lum sinf: \"{sinf}\"";
                    errors.Add($"{rowNumber}-qator: sinf \"{sinf}\" tanilmadi (\"5\", \"5-6\", \"6\", \"7\", \"8\", \"9\", \"10-11\", \"rus\" kutilgan).");
                    continue;
                }

                // ===== Takror qatorlar (ko'rsatilgan bo'limdagisi qoladi) =====
                var fullKey = NormalizeKey($"{fam} {ism}");
                if (DuplicateRules.TryGetValue(fullKey, out var keepInSection)
                    && !string.Equals(sinf, keepInSection, StringComparison.OrdinalIgnoreCase))
                {
                    var keepLabel = keepInSection == "rus" ? "rus (10D)" : keepInSection == "6" ? "6-sinf" : keepInSection;
                    row.SkipReason = $"Takror qator — {keepLabel} ro'yxatidagi o'quvchi olinadi";
                    continue;
                }

                // ===== Familiyasi bo'sh qator =====
                if (fam.Length == 0)
                {
                    var ismKey = NormalizeKey(ism);
                    if (fullByIsm.TryGetValue(ismKey, out var candidates)
                        && candidates.Any(c => string.Equals(c.Sinf, sinf, StringComparison.OrdinalIgnoreCase)))
                    {
                        var kept = candidates.First(c => string.Equals(c.Sinf, sinf, StringComparison.OrdinalIgnoreCase));
                        row.SkipReason = $"\"{kept.Familiya} {kept.Ism}\" sifatida import qilinadi (bu qator bir xil o'quvchi)";
                        continue;
                    }

                    row.SkipReason = "Familiyasi bo'sh";
                    errors.Add($"{rowNumber}-qator: familiyasi bo'sh (ism: \"{ism}\", sinf: \"{sinf}\").");
                    continue;
                }

                // ===== Ism bo'sh qator =====
                if (ism.Length == 0)
                {
                    errors.Add($"{rowNumber}-qator: ismi bo'sh (familiya: \"{fam}\").");
                    row.SkipReason = "Ismi bo'sh";
                    continue;
                }

                // ===== Guruh (1-7) =====
                if (int.TryParse(guruh, out var number) && number is >= 1 and <= 7)
                {
                    row.StreamNumber = number;
                }
                else if (StreamOverrides.TryGetValue($"{fam} {ism}", out var overridden))
                {
                    row.StreamNumber = overridden;
                    row.Note = "Guruh faylda bo'sh edi — admin tasdiqlaganiga biriktirildi";
                }
                else
                {
                    errors.Add($"{rowNumber}-qator: guruh \"{guruh}\" 1-7 oralig'ida emas (familiya: \"{fam}\").");
                    row.SkipReason = "Guruhi noto'g'ri";
                    continue;
                }
            }

            return new ParsedRows(rows, errors, separatorCount);
        }

        private static string CellText(IXLWorksheet ws, int row, int column)
            => ws.Cell(row, column).GetString().Trim();

        private static string NormalizeKey(string value)
        {
            var simplified = value.Trim().ToLowerInvariant()
                .Replace('\u02BB', '\'')
                .Replace('\u2018', '\'')
                .Replace('\u2019', '\'')
                .Replace('\u02BC', '\'')
                .Replace('\u02B9', '\'');
            var parts = simplified.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(' ', parts);
        }

        private int GetNextLoginIdBase()
        {
            var max = 10000;
            var ids = _context.Users
                .Select(u => u.LoginId)
                .Where(id => id != null)
                .ToList();

            foreach (var id in ids)
            {
                if (int.TryParse(id!, out var number) && number > max)
                {
                    max = number;
                }
            }

            return max;
        }

        private async Task<Dictionary<string, Group>> EnsureGroupsAsync(
            IEnumerable<string> names,
            GroupType type,
            Func<string, Group> factory,
            StudentImportConfirmResult result)
        {
            var map = new Dictionary<string, Group>();

            foreach (var name in names)
            {
                var group = await _context.Groups
                    .FirstOrDefaultAsync(g => g.Type == type && g.Name == name && !g.IsArchived);

                if (group == null)
                {
                    group = factory(name);
                    _context.Groups.Add(group);
                    await _context.SaveChangesAsync();
                    result.CreatedGroups.Add($"{name} ({(type == GroupType.Class ? "sinf" : "guruh")})");
                }
                else
                {
                    result.ExistingGroups.Add(name);
                }

                map[name] = group;
            }

            return map;
        }
    }
}
