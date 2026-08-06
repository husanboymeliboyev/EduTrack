namespace EduTrack.ViewModels
{
    public class StudentAttendanceStatsViewModel
    {
        public string SubjectName { get; set; } = string.Empty;
        public int TotalLessons { get; set; }
        public int PresentCount { get; set; }
        public double Percentage => TotalLessons == 0 ? 0 : Math.Round((double)PresentCount / TotalLessons * 100, 1);
    }
}