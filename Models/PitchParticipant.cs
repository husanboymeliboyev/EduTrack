namespace EduTrack.Models
{
    // Sessiyadagi bitta talaba navbati. Har bir talaba bitta sessiyada faqat bir marta
    // qatnashishi mumkin (DB darajasida unique index bilan ta'minlanadi).
    public class PitchParticipant
    {
        public int Id { get; set; }

        public int PitchSessionId { get; set; }
        public PitchSession? PitchSession { get; set; }

        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }

        // Navbat o'rni (qaysi tartibda tanlanganligi).
        public int QueuePosition { get; set; }

        public PitchParticipantStatus Status { get; set; } = PitchParticipantStatus.Queued;

        public DateTime? DrawnDate { get; set; }      // Navbatga tanlangan vaqt
        public DateTime? ActivatedDate { get; set; }  // Current (Faol) bo'lgan vaqt
        public DateTime? CompletedDate { get; set; }  // Yakunlangan vaqt
        public DateTime? SkippedDate { get; set; }    // O'tkazib yuborilgan vaqt

        // O'tkazib yuborish sababi (faqat Skip holatida to'ldiriladi).
        public string? Reason { get; set; }
    }
}
