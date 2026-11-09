namespace EduTrack.ViewModels
{
    /// <summary>
    /// O'qituvchi baho kiritish jadvalidagi bitta o'quvchi qatori:
    /// qo'lda qo'yilgan ball + imtihonlardan hisoblangan o'rtacha.
    /// </summary>
    public class StudentGradeRowViewModel
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;

        /// <summary>Mavjud qo'lda qo'yilgan ball (bo'lmasa null).</summary>
        public double? ManualScore { get; set; }

        /// <summary>Tasdiqlangan imtihon natijalari bo'yicha o'rtacha.</summary>
        public double? ExamAverage { get; set; }

        public int ExamCount { get; set; }

        /// <summary>Amaldagi ball: qo'lda baho ustuvor.</summary>
        public double? EffectiveScore => ManualScore ?? ExamAverage;

        public bool HasScore => EffectiveScore.HasValue;

        public string SourceLabel => ManualScore.HasValue
            ? "Qo'lda"
            : ExamCount > 0
                ? "Imtihon"
                : "—";
    }
}
