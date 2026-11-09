using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    /// <summary>
    /// O'qituvchi tomonidan 100 ballik shkalada qo'lda qo'yilgan yakuniy baho.
    /// Bitta o'quvchi + bitta fan juftligi uchun bitta yozuv bo'ladi.
    ///
    /// Qoida: qo'lda qo'yilgan baho ustuvor; u bo'lmasa fan bo'yicha
    /// tasdiqlangan imtihon natijalarining o'rtachasi ishlatiladi.
    /// Davomat va topshiriqlar bu bahoga ta'sir qilmaydi.
    /// </summary>
    public class SubjectGrade
    {
        public int Id { get; set; }

        [Required]
        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }

        public int SubjectId { get; set; }
        public Subject? Subject { get; set; }

        /// <summary>0 dan 100 gacha bo'lgan yakuniy ball.</summary>
        [Range(0, 100, ErrorMessage = "Ball 0 dan 100 gacha bo'lishi kerak")]
        public double Score { get; set; }

        /// <summary>Baho qo'yilgan vaqt (ilova vaqt mintaqasida).</summary>
        public DateTime UpdatedDate { get; set; }

        /// <summary>Bahoni qo'ygan o'qituvchi (ixtiyoriy, tarix uchun).</summary>
        public string? TeacherId { get; set; }
        public ApplicationUser? Teacher { get; set; }
    }
}
