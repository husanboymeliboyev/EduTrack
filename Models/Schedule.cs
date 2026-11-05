using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    public class Schedule
    {
        public int Id { get; set; }

        public int SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;

        public int GroupId { get; set; }
        public Group Group { get; set; } = null!;

        [Required(ErrorMessage = "Hafta kunini tanlang")]
        public DayOfWeek DayOfWeek { get; set; }

        [Required(ErrorMessage = "Boshlanish vaqtini kiriting")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "Tugash vaqtini kiriting")]
        public TimeSpan EndTime { get; set; }

        /// <summary>1-smena (ertalab) yoki 2-smena (tushdan keyin).</summary>
        public ScheduleShift Shift { get; set; } = ScheduleShift.Morning;

        /// <summary>
        /// Smena ichidagi dars raqami (1..6). Eski yozuvlarda null bo'lishi
        /// mumkin — bunday darslar vaqt bo'yicha tekshiriladi.
        /// </summary>
        public int? Period { get; set; }

        /// <summary>Asosiy yoki qo'shimcha (tushlikdan keyingi) dars.</summary>
        public ScheduleKind Kind { get; set; } = ScheduleKind.Regular;

        /// <summary>
        /// Shu darsni olib boradigan o'qituvchi. null bo'lsa:
        /// GroupSubject.TeacherId → Subject.TeacherId tartibida aniqlanadi.
        /// </summary>
        public string? TeacherId { get; set; }
        public ApplicationUser? Teacher { get; set; }
    }
}
