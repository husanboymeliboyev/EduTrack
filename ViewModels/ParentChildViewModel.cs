namespace EduTrack.ViewModels
{
    // Ota-ona panelida ro'yxat sifatida ko'rsatiladigan, bog'langan farzand.
    public class ParentChildViewModel
    {
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        // Ko'rsatish uchun qulay nom: sinf -> yo'nalish guruhi -> eski guruh.
        public string GroupName { get; set; } = string.Empty;

        public string ClassName { get; set; } = string.Empty;
        public string StreamName { get; set; } = string.Empty;
        public int SubjectCount { get; set; }
    }
}
