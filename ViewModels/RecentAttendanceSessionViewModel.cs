namespace EduTrack.ViewModels
{
    public class RecentAttendanceSessionViewModel
    {
        public DateTime Date { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public int? GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int StudentCount { get; set; }
    }
}