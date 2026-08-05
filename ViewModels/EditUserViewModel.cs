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

        [Display(Name = "Guruh (faqat talaba uchun)")]
        public int? GroupId { get; set; }
    }
}