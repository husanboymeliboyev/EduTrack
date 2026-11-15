using System.ComponentModel.DataAnnotations;

namespace EduTrack.ViewModels
{
    public class SubjectFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Fan nomini kiriting")]
        [StringLength(100)]
        [Display(Name = "Fan nomi")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Asosiy o'qituvchi")]
        public string? TeacherId { get; set; }

        [Display(Name = "Guruhlar")]
        public List<int> SelectedGroupIds { get; set; } = new();

        /// <summary>
        /// Guruh bo'yicha o'qituvchi (ko'p o'qituvchi modeli): kalit — GroupId,
        /// qiymat — shu guruhda dars o'tadigan o'qituvchi Id (null/bo'sh —
        /// asosiy o'qituvchi o'qitadi). Bitta fanga bir nechta o'qituvchi
        /// biriktirish aynan shu orqali amalga oshadi.
        /// </summary>
        [Display(Name = "Guruh bo'yicha o'qituvchi")]
        public Dictionary<int, string?> GroupTeacherIds { get; set; } = new();
    }
}
