namespace EduTrack.ViewModels
{
    // Admin uchun imtihonlar ro'yxati (read-only): kimning fani, qaysi guruh,
    // nechta natija bor. Bu sahifadan natijalar sahifasiga o'tiladi.
    public class AdminExamListItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? SubjectName { get; set; }
        public string? TeacherName { get; set; }
        public string? GroupName { get; set; }
        public int QuestionCount { get; set; }
        public int ResultsCount { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsArchived { get; set; }
    }
}
