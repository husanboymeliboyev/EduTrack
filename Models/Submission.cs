namespace EduTrack.Models
{
    public class Submission
    {
        public int Id { get; set; }

        public int AssignmentId { get; set; }
        public Assignment? Assignment { get; set; }

        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }

        public string? FilePath { get; set; }
        public string? FileName { get; set; }

        public DateTime SubmittedDate { get; set; } = DateTime.Now;

        public int? Grade { get; set; }
        public string? TeacherComment { get; set; }
    }
}