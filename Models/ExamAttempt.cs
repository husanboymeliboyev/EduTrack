namespace EduTrack.Models
{
    /// <summary>
    /// Talaba imtihonni qachon boshlaganini bazada saqlaydi. Bu yozuv bo'lmasa,
    /// server talaba imtihonni qachon boshlaganini bilmaydi va vaqt chegarasini
    /// faqat brauzerdagi JavaScript orqali "nazorat qilish" mumkin bo'lardi —
    /// buni talaba dev tools orqali osongina chetlab o'tishi mumkin.
    /// </summary>
    public class ExamAttempt
    {
        public int Id { get; set; }

        public int ExamId { get; set; }
        public Exam? Exam { get; set; }

        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }

        public DateTime StartedDate { get; set; } = DateTime.Now;

        // Shubhali harakatlar soni (tab almashtirish, fullscreen'dan chiqish va h.k.)
        public int ViolationCount { get; set; } = 0;

        // Oxirgi qayd etilgan buzilish sababi (masalan "tab-switch", "fullscreen-exit")
        public string? LastViolationReason { get; set; }

        // Oxirgi buzilish qachon sodir bo'lgani (UTC)
        public DateTime? LastViolationAtUtc { get; set; }

        // O'qituvchi tomonidan imtihondan majburan chetlatilganmi
        public bool IsForceFinished { get; set; } = false;

        // Majburan chetlatilgan vaqt (UTC)
        public DateTime? ForceFinishedAtUtc { get; set; }

        // Kim chetlatgani — o'qituvchining ApplicationUser.Id'si (audit uchun).
        // Navigation property qo'shilmaydi, PitchSession.CreatedById uslubiga mos.
        public string? ForceFinishedById { get; set; }
    }
}