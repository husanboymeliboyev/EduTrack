using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    // Tasodifiy talaba navbatining bitta sessiyasi. Admin qaysi guruhlarni tanlaganini,
    // sessiya holatini va vaqt belgilarini saqlaydi. Guruhlar haqidagi ma'lumot
    // faqat barqaror ID'lar (SelectedGroupIds) orqali saqlanadi.
    public class PitchSession
    {
        public int Id { get; set; }

        // Sessiyani yaratgan Admin identifikatori (ApplicationUser.Id).
        // Faqat shu Admin sessiyani boshqarishi mumkin.
        public string CreatedById { get; set; } = string.Empty;

        // Vergul bilan ajratilgan tanlangan guruh ID'lari, masalan: "1,2,3".
        public string SelectedGroupIds { get; set; } = string.Empty;

        public PitchSessionStatus Status { get; set; } = PitchSessionStatus.Open;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ClosedDate { get; set; }

        // Optimistik konkurensiya himoyasi: har bir navbat siljishida oshiriladi,
        // takroriy/kengaytirilgan so'rovlar navbatni ikki marta siljitmasligini ta'minlaydi.
        [ConcurrencyCheck]
        public int Version { get; set; }

        public ICollection<PitchParticipant> Participants { get; set; } = new List<PitchParticipant>();
    }
}
