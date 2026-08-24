namespace EduTrack.Models
{
    // Bitta talabaning bitta komponent bo'yicha olgan bali
    public class StudentGrade
    {
        public int Id { get; set; }

        public int GradeComponentId { get; set; }
        public GradeComponent? GradeComponent { get; set; }

        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }

        public double Score { get; set; }

        // Manba va qo'lda tahrirlash holati natija panelida shaffof ko'rsatiladi.
        public string Source { get; set; } = "System";
        public bool IsManualOverride { get; set; }
        public double? PreviousScore { get; set; }
        public string? AdjustmentReason { get; set; }
        public string? UpdatedByUserId { get; set; }

        public DateTime UpdatedDate { get; set; } = DateTime.Now;
    }
}