namespace EduTrack.ViewModels
{
    // O'qituvchining jonli monitoring sahifasida (/Exams/MonitorData) har 5 soniyada
    // JSON sifatida qaytariladigan, hozir faol (hali Submit qilmagan, chetlatilmagan)
    // talabaning bitta qatori.
    public class ExamMonitorEntryViewModel
    {
        public int AttemptId { get; set; }
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime StartedDate { get; set; }
        public int ElapsedSeconds { get; set; }
        public int RemainingSeconds { get; set; }
        public int ViolationCount { get; set; }
        public string? LastViolationReason { get; set; }
        public DateTime? LastViolationAtUtc { get; set; }
    }
}
