namespace EduTrack.ViewModels
{
    public class StudentShowcaseViewModel
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;

        public double OverallScore { get; set; }        // 0-100, fan baholari o'rtachasi
        public string StatusZone { get; set; } = "Yellow"; // "Green", "Yellow", "Red", "Muted"
        public List<string> StatusReasons { get; set; } = new();

        public double AttendancePercentage { get; set; }  // 0-100 (faqat ma'lumot uchun)

        public List<SubjectPerformanceItem> SubjectBreakdown { get; set; } = new();
        public List<DailyAttendancePoint> AttendanceCalendar { get; set; } = new(); // oxirgi 30 kun
        public List<string> Badges { get; set; } = new();  // masalan "Barcha fanlardan 90+"
        public int GroupRank { get; set; }
        public int GroupSize { get; set; }
    }

    public class SubjectPerformanceItem
    {
        public string SubjectName { get; set; } = string.Empty;
        public double Percentage { get; set; }
    }

    public class WeeklyTrendPoint
    {
        public DateTime WeekStart { get; set; }
        public double AveragePercentage { get; set; }
    }

    public class DailyAttendancePoint
    {
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty; // AttendanceStatus enum qiymatiga mos: "Keldi", "Kelmadi", "KechQoldi", "Sababli"
    }

    public class ClassOverviewViewModel
    {
        public string GroupName { get; set; } = string.Empty;
        public List<StudentShowcaseSummary> Students { get; set; } = new();
        public double ClassAverageScore { get; set; }
        public double ClassAverageAttendance { get; set; }
    }

    public class StudentShowcaseSummary
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public double OverallScore { get; set; }
        public string StatusZone { get; set; } = "Yellow";
        public int Rank { get; set; }
    }
}