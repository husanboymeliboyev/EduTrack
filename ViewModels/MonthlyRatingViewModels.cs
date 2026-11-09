using EduTrack.Models;

namespace EduTrack.ViewModels
{
    // ==================== Oylik reyting: umumiy ====================

    public sealed class MonthlyPeriodOptionViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string Label { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public bool IsCurrent { get; set; }

        public string Value => $"{Year}-{Month:00}";
    }

    /// <summary>Bitta fan bo'yicha oylik natija (100 ballik shkala).</summary>
    public sealed class MonthlyRatingSubjectViewModel
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;

        /// <summary>Shu oy uchun oylik imtihon mavjudmi (o'rtacha hisobiga kiradi).</summary>
        public bool HasMonthlyExam { get; set; }

        /// <summary>Tasdiqlangan eng yuqori natija (0–100). null — natija yo'q.</summary>
        public double? Score { get; set; }

        /// <summary>Guruh ichida shu fan bo'yicha o'rin.</summary>
        public int? Rank { get; set; }

        public string? ExamTitle { get; set; }
        public DateTime? CompletedDate { get; set; }

        /// <summary>O'rtachaga ta'sir qiluvchi ball: natija yo'q bo'lsa 0.</summary>
        public double EffectiveScore => Score ?? 0;

        public string Tone
        {
            get
            {
                if (!HasMonthlyExam) return "muted";
                if (!Score.HasValue) return "missing";
                return ToneFor(Score.Value);
            }
        }

        public string ScoreLabel => !HasMonthlyExam
            ? "—"
            : Score.HasValue
                ? Score.Value.ToString("0.#")
                : "0";

        public static string ToneFor(double score) => score switch
        {
            >= 86 => "excellent",
            >= 71 => "good",
            >= 55 => "fair",
            _ => "low"
        };
    }

    /// <summary>Sinf yoki yo'nalish bo'yicha alohida reyting kartasi.</summary>
    public sealed class MonthlyRatingScopeViewModel
    {
        public GroupType Scope { get; set; }
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;

        /// <summary>0–100 oralig'idagi o'rtacha. null — shu oy uchun baholanadigan fan yo'q.</summary>
        public double? AverageScore { get; set; }

        /// <summary>Guruh ichidagi o'rin (1 dan boshlanadi).</summary>
        public int? Rank { get; set; }

        public int ParticipantCount { get; set; }

        /// <summary>Guruhga biriktirilgan barcha fanlar soni.</summary>
        public int SubjectCount { get; set; }

        /// <summary>Shu oy uchun oylik imtihoni bor fanlar soni.</summary>
        public int RatedSubjectCount { get; set; }

        /// <summary>O'quvchining tasdiqlangan natijasi bor fanlar soni.</summary>
        public int ScoredSubjectCount { get; set; }

        public List<MonthlyRatingSubjectViewModel> Subjects { get; set; } = new();
        public List<MonthlyRatingLeaderboardRowViewModel> Leaderboard { get; set; } = new();

        public string? EmptyReason { get; set; }

        public string ScopeLabel => Scope == GroupType.Class ? "Sinf (1-smena)" : "Yo'nalish (tushdan keyin)";
        public string ScopeShortLabel => Scope == GroupType.Class ? "Sinf" : "Yo'nalish";
        public string ScopeIcon => Scope == GroupType.Class ? "bi-building" : "bi-signpost-split";
        public string AccentClass => Scope == GroupType.Class ? "is-class" : "is-stream";

        public bool HasLeaderboard => Leaderboard.Count > 0 && AverageScore.HasValue;

        public double CoveragePercentage => RatedSubjectCount == 0
            ? 0
            : Math.Round(100.0 * ScoredSubjectCount / RatedSubjectCount, 1);

        public string Tone
        {
            get
            {
                if (!AverageScore.HasValue) return "muted";
                return MonthlyRatingSubjectViewModel.ToneFor(AverageScore.Value);
            }
        }

        public string AverageLabel => AverageScore.HasValue
            ? AverageScore.Value.ToString("0.#")
            : "—";
    }

    public sealed class MonthlyRatingMiniScoreViewModel
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public double Score { get; set; }
        public string Tone => MonthlyRatingSubjectViewModel.ToneFor(Score);
    }

    public sealed class MonthlyRatingLeaderboardRowViewModel
    {
        public int? Rank { get; set; }
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public double? AverageScore { get; set; }
        public int ScoredSubjectCount { get; set; }
        public int SubjectCount { get; set; }
        public bool IsCurrentStudent { get; set; }

        public List<MonthlyRatingMiniScoreViewModel> SubjectScores { get; set; } = new();

        public bool IsTopThree => Rank is int rank && rank <= 3 && (AverageScore ?? 0) > 0;

        public string RankLabel => Rank.HasValue ? Rank.Value.ToString() : "—";

        public string AverageLabel => AverageScore.HasValue
            ? $"{AverageScore.Value:0.#}%"
            : "—";

        public string Tone
        {
            get
            {
                if (!AverageScore.HasValue) return "muted";
                return MonthlyRatingSubjectViewModel.ToneFor(AverageScore.Value);
            }
        }
    }

    public sealed class MonthlyRatingTrendPointViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string Label { get; set; } = string.Empty;
        public string ShortLabel { get; set; } = string.Empty;
        public double? ClassAverage { get; set; }
        public double? StreamAverage { get; set; }
        public bool IsSelected { get; set; }
    }

    // ==================== Talaba / ota-ona ko'rinishi ====================

    public sealed class StudentMonthlyRatingViewModel
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string StreamName { get; set; } = string.Empty;

        public int Year { get; set; }
        public int Month { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;

        public List<MonthlyPeriodOptionViewModel> Periods { get; set; } = new();
        public MonthlyRatingScopeViewModel? ClassRating { get; set; }
        public MonthlyRatingScopeViewModel? StreamRating { get; set; }
        public List<MonthlyRatingTrendPointViewModel> Trend { get; set; } = new();

        public bool HasAnyRating =>
            (ClassRating?.AverageScore.HasValue ?? false)
            || (StreamRating?.AverageScore.HasValue ?? false);

        public double? BestAverage
        {
            get
            {
                var values = new[] { ClassRating?.AverageScore, StreamRating?.AverageScore }
                    .Where(v => v.HasValue)
                    .Select(v => v!.Value)
                    .ToList();

                return values.Count == 0 ? null : values.Max();
            }
        }

        public string BestAverageLabel => BestAverage.HasValue
            ? $"{BestAverage.Value:0.#}%"
            : "—";

        public string BestScopeLabel
        {
            get
            {
                if (!BestAverage.HasValue) return string.Empty;

                if (ClassRating?.AverageScore == BestAverage && ClassRating is not null)
                {
                    return ClassRating.ScopeShortLabel;
                }

                return StreamRating?.ScopeShortLabel ?? string.Empty;
            }
        }
    }

    public sealed class ParentMonthlyRatingViewModel : ParentPageViewModel
    {
        public StudentMonthlyRatingViewModel Rating { get; set; } = new();
    }

    // ==================== Admin / o'qituvchi ko'rinishi ====================

    public sealed class MonthlyRatingGroupOptionViewModel
    {
        public int GroupId { get; set; }
        public string Name { get; set; } = string.Empty;
        public GroupType Type { get; set; }
        public int SubjectCount { get; set; }

        public string TypeLabel => Type == GroupType.Class ? "Sinf" : "Yo'nalish";

        public string DisplayName => $"{Name} · {TypeLabel}";
    }

    public sealed class MonthlyRatingGroupViewModel
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public GroupType Type { get; set; }

        public int SubjectCount { get; set; }
        public int RatedSubjectCount { get; set; }
        public int StudentCount { get; set; }
        public int RatedStudentCount { get; set; }

        public double? AverageScore { get; set; }
        public List<MonthlyRatingLeaderboardRowViewModel> Students { get; set; } = new();

        public string TypeLabel => Type == GroupType.Class ? "Sinf" : "Yo'nalish";
        public string TypeIcon => Type == GroupType.Class ? "bi-building" : "bi-signpost-split";
        public string AccentClass => Type == GroupType.Class ? "is-class" : "is-stream";

        public string AverageLabel => AverageScore.HasValue
            ? $"{AverageScore.Value:0.#}%"
            : "—";

        public string Tone
        {
            get
            {
                if (!AverageScore.HasValue) return "muted";
                return MonthlyRatingSubjectViewModel.ToneFor(AverageScore.Value);
            }
        }

        public bool HasRating => RatedSubjectCount > 0 && AverageScore.HasValue;
    }

    public sealed class MonthlyRatingOverviewViewModel
    {
        public string Title { get; set; } = "Oylik reyting";
        public string Subtitle { get; set; } = string.Empty;
        public bool IsTeacherView { get; set; }

        public int Year { get; set; }
        public int Month { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;

        public List<MonthlyPeriodOptionViewModel> Periods { get; set; } = new();
        public List<MonthlyRatingGroupOptionViewModel> Groups { get; set; } = new();
        public int? SelectedGroupId { get; set; }

        public List<MonthlyRatingGroupViewModel> GroupRatings { get; set; } = new();

        public int GroupCount { get; set; }
        public int StudentCount { get; set; }
        public int ExamCount { get; set; }
        public double? OverallAverage { get; set; }

        public string OverallAverageLabel => OverallAverage.HasValue
            ? $"{OverallAverage.Value:0.#}%"
            : "—";

        public string OverallTone
        {
            get
            {
                if (!OverallAverage.HasValue) return "muted";
                return MonthlyRatingSubjectViewModel.ToneFor(OverallAverage.Value);
            }
        }

        public MonthlyRatingGroupViewModel? SelectedGroup => SelectedGroupId.HasValue
            ? GroupRatings.FirstOrDefault(g => g.GroupId == SelectedGroupId.Value)
            : null;
    }
}
