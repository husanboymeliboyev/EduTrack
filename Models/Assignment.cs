using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    public class Assignment
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Sarlavhani kiriting")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Muddatni kiriting")]
        [Display(Name = "Topshirish muddati")]
        public DateTime DueDate { get; set; }

        // Maksimal ball — o'qituvchi topshiriqqa mos qiymat beradi (standart 100).
        [Range(1, 1000, ErrorMessage = "Maksimal ball 1 dan 1000 gacha bo'lishi kerak")]
        [Display(Name = "Maksimal ball")]
        public int MaxPoints { get; set; } = 100;

        // Muddatdan keyin ham topshirishga ruxsat berilsa, ish "kechikkan" belgisi bilan qabul qilinadi.
        [Display(Name = "Kechikkan ishlarni qabul qilish")]
        public bool AllowLate { get; set; }

        // Yaratilgan vaqt — ilova vaqt mintaqasida (IAppClock.Now) to'ldiriladi.
        public DateTime CreatedDate { get; set; }

        public string? FilePath { get; set; }
        public string? FileName { get; set; }

        public int SubjectId { get; set; }
        public Subject? Subject { get; set; }
        public int? GroupId { get; set; }
        public Group? Group { get; set; }

        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}