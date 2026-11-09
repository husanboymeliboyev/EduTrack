using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduTrack.Models
{
    public class Exam
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Imtihon nomini kiriting")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public int SubjectId { get; set; }
        public Subject? Subject { get; set; }

        // Imtihon endi aniq bitta Bankka bog'lanadi — savollar shu bankdan tanlanadi.
        public int QuestionBankId { get; set; }
        public QuestionBank? QuestionBank { get; set; }

        public int? GroupId { get; set; }
        public Group? Group { get; set; }

        [Required]
        [Display(Name = "Savollar soni")]
        [Range(1, 100, ErrorMessage = "1 dan 100 gacha bo'lishi kerak")]
        public int QuestionCount { get; set; }

        [Required]
        [Display(Name = "Vaqt chegarasi (daqiqa)")]
        [Range(1, 300)]
        public int DurationMinutes { get; set; }

        // Yaratilgan vaqt — ilova vaqt mintaqasida (IAppClock.Now) to'ldiriladi.
        public DateTime CreatedDate { get; set; }

        // Imtihon hayot sikli: arxivlangan imtihonlar standart ro'yxatda ko'rinmaydi,
        // lekin ma'lumotlari (natijalari) saqlanib qoladi.
        public bool IsArchived { get; set; } = false;

        // ===== Ochish/Yopish boshqaruvi =====
        // Bular IsArchived'dan FARQLI narsa: IsArchived — imtihon "tarixga o'tdi, endi
        // ko'rinmasin" degani (doimiy), Ochish/Yopish esa — imtihon hozir talabalar uchun
        // FAOLmi degan vaqtinchalik holat (bugun yopiq, ertaga qayta ochilishi mumkin).

        // Qo'lda ON/OFF: o'qituvchi istalgan vaqt Index sahifasidan bosib almashtira oladi.
        // Standart holat TRUE — bu eski xatti-harakatni (har doim ochiq) saqlab qoladi,
        // shuning uchun migratsiyadan keyin mavjud imtihonlar to'satdan yopilib qolmaydi.
        [Display(Name = "Ochiq")]
        public bool IsOpen { get; set; } = true;

        // Ixtiyoriy: belgilansa, shu vaqtgacha imtihon "ochiq" deb hisoblanmaydi (garchi IsOpen=true bo'lsa ham)
        [Display(Name = "Ochilish vaqti (ixtiyoriy)")]
        public DateTime? OpenAt { get; set; }

        // Ixtiyoriy: belgilansa, shu vaqtdan keyin imtihon avtomatik yopiq hisoblanadi
        [Display(Name = "Yopilish vaqti (ixtiyoriy)")]
        public DateTime? CloseAt { get; set; }

        // ===== Oylik reyting =====
        // Kind = Monthly bo'lsa, tasdiqlangan natijalar PeriodYear/PeriodMonth
        // uchun 100 ballik oylik reytingga qo'shiladi. Barcha eski imtihonlar
        // Regular (0) bo'lib qoladi — migratsiya xatti-harakatni o'zgartirmaydi.
        [Display(Name = "Imtihon turi")]
        public ExamKind Kind { get; set; } = ExamKind.Regular;

        [Display(Name = "Oy")]
        [Range(1, 12, ErrorMessage = "Oy 1 dan 12 gacha bo'lishi kerak")]
        public int? PeriodMonth { get; set; }

        [Display(Name = "Yil")]
        [Range(2000, 2100, ErrorMessage = "Yil 2000 dan 2100 gacha bo'lishi kerak")]
        public int? PeriodYear { get; set; }

        public ICollection<ExamResult> Results { get; set; } = new List<ExamResult>();

        /// <summary>Oylik imtihonning ko'rsatish uchun tayyor davri.</summary>
        [NotMapped]
        public string MonthlyPeriodLabel => PeriodYear.HasValue && PeriodMonth.HasValue
            ? MonthlyPeriod.Label(PeriodYear.Value, PeriodMonth.Value)
            : "Oy belgilanmagan";

        // Bazaga saqlanmaydigan hisoblangan holat: IsOpen + OpenAt/CloseAt oralig'ini
        // birlashtirib, "hozir, shu daqiqada talaba imtihonni boshlay oladimi" javobini beradi.
        // Controller va View'larda shu YAGONA joydan foydalaniladi — mantiq ikki marta yozilmaydi.
        public bool IsCurrentlyOpenAt(DateTime now)
        {
            if (!IsOpen) return false;

            if (OpenAt.HasValue && now < OpenAt.Value) return false;
            if (CloseAt.HasValue && now > CloseAt.Value) return false;

            return true;
        }
    }
}
