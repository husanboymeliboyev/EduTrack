namespace EduTrack.ViewModels
{
    // Admin -> Fanlar ro'yxati sahifasi uchun
    public class SubjectAnalyticsSummaryViewModel
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public string? TeacherName { get; set; }
        public int GroupCount { get; set; }
        public int ExamCount { get; set; }
    }

    // Admin -> bitta fan ichida, guruhlar kesimida statistika
    public class GroupAnalyticsSummaryViewModel
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int StudentCount { get; set; }
        public double? AverageExamPercentage { get; set; } // null = hali imtihon natijasi yo'q
        public double? AverageGradePercentage { get; set; } // null = hali baholash komponenti yo'q
    }

    // Admin -> bitta fan + bitta guruh ichida, imtihonlar ro'yxati
    public class ExamAnalyticsRowViewModel
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public int ResultCount { get; set; }
        public double? AveragePercentage { get; set; }
    }

    // Admin -> bitta fan + bitta guruh ichida, talabalar reytingi (umumiy o'rtacha ball bo'yicha)
    public class AdminRankingEntryViewModel
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public double AveragePercentage { get; set; }
        public int Rank { get; set; }
    }
}