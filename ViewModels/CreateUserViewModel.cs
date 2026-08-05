using System.ComponentModel.DataAnnotations;

namespace EduTrack.ViewModels
{
    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "Ism-familiyani kiriting")]
        [Display(Name = "Ism-familiya")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email kiriting")]
        [EmailAddress(ErrorMessage = "Email formati noto'g'ri")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Parol kiriting")]
        [DataType(DataType.Password)]
        [Display(Name = "Parol")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Rolni tanlang")]
        [Display(Name = "Rol")]
        public string Role { get; set; } = string.Empty;

        [Display(Name = "Guruh (faqat talaba uchun)")]
        public int? GroupId { get; set; }
    }
}