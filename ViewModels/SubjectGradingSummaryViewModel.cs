namespace EduTrack.ViewModels
{
    /// <summary>
    /// O'qituvchining “Baholash” sahifasidagi fan kartasi: nechta o'quvchi
    /// borligi, nechtasiga baho qo'yilgani va o'rtacha 100 ballik natija.
    /// </summary>
    public class SubjectGradingSummaryViewModel
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;

        public int StudentCount { get; set; }
        public int GradedCount { get; set; }
        public int ManualCount { get; set; }

        public double? AverageScore { get; set; }

        public bool HasGrades => GradedCount > 0;

        public string AverageLabel => AverageScore.HasValue
            ? AverageScore.Value.ToString("0.#")
            : "—";

        public string Tone => AverageScore switch
        {
            null => "is-muted",
            >= 86 => "is-excellent",
            >= 71 => "is-good",
            >= 55 => "is-fair",
            _ => "is-low"
        };
    }
}
