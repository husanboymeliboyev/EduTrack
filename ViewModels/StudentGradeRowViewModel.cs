namespace EduTrack.ViewModels
{
    public class StudentGradeRowViewModel
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;

        // Kalit: GradeComponentId, Qiymat: talaba shu komponent bo'yicha olgan ball (hali kiritilmagan bo'lsa null)
        public Dictionary<int, double?> Scores { get; set; } = new();
    }
}