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
    }
}