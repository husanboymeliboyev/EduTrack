using System.ComponentModel.DataAnnotations;

namespace EduTrack.ViewModels
{
    public class EditUserViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ism-familiyani kiriting")]
        [Display(Name = "Ism-familiya")]
        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Rolni tanlang")]
        [Display(Name = "Rol")]
        public string Role { get; set; } = string.Empty;

        [Display(Name = "Sinf (1-smena)")]
        public int? ClassGroupId { get; set; }

        [Display(Name = "Guruh (2-smena)")]
        public int? StreamGroupId { get; set; }
    }
}
