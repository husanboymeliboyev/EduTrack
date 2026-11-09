namespace EduTrack.ViewModels
{
    /// <summary>
    /// Bitta fan bo'yicha 100 ballik yakuniy baho. Bahoning yagona manbai:
    /// o'qituvchi qo'lda qo'ygan baho bo'lsa — u ustuvor; aks holda fan
    /// bo'yicha tasdiqlangan imtihon natijalarining o'rtachasi ishlatiladi.
    /// Davomat va topshiriqlar bu bahoga ta'sir qilmaydi.
    /// </summary>
    public class StudentSubjectGradeViewModel
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;

        /// <summary>O'qituvchi qo'lda qo'ygan ball (0–100).</summary>
        public double? ManualScore { get; set; }

        /// <summary>Tasdiqlangan imtihon natijalari bo'yicha o'rtacha (0–100).</summary>
        public double? ExamAverage { get; set; }

        /// <summary>O'rtacha hisobiga kirgan imtihonlar soni.</summary>
        public int ExamCount { get; set; }

        public DateTime? UpdatedDate { get; set; }

        /// <summary>Amaldagi ball: qo'lda baho ustuvor, bo'lmasa imtihon o'rtachasi.</summary>
        public double? Score => ManualScore ?? ExamAverage;

        public bool IsManual => ManualScore.HasValue;
        public bool HasScore => Score.HasValue;

        public string SourceLabel => IsManual
            ? "Qo'lda qo'yilgan"
            : ExamCount > 0
                ? $"{ExamCount} imtihon o'rtachasi"
                : "Baho yo'q";

        public string ScoreLabel => HasScore ? Score!.Value.ToString("0.#") : "—";

        /// <summary>iz-* dizayn tizimidagi rang toni.</summary>
        public string Tone => Score switch
        {
            null => "is-muted",
            >= 86 => "is-excellent",
            >= 71 => "is-good",
            >= 55 => "is-fair",
            _ => "is-low"
        };

        public string LetterGrade => Score switch
        {
            null => "—",
            >= 86 => "A'lo",
            >= 71 => "Yaxshi",
            >= 55 => "Qoniqarli",
            _ => "Qoniqarsiz"
        };
    }
}
