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

        public DateTime UpdatedDate { get; set; } = DateTime.Now;
    }
}