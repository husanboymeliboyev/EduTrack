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

        [Display(Name = "O'qituvchi")]
        public string? TeacherId { get; set; }

        [Display(Name = "Guruhlar")]
        public List<int> SelectedGroupIds { get; set; } = new();
    }
}