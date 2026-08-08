using System.ComponentModel.DataAnnotations;

namespace EduTrack.ViewModels
{
    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "Ism-familiyani kiriting")]
        [Display(Name = "Ism-familiya")]
        public string FullName { get; set; } = string.Empty;

        // Email endi shart emas — tizimga kirish uchun Login ID ishlatiladi.
        [EmailAddress(ErrorMessage = "Email formati noto'g'ri")]
        [Display(Name = "Email (ixtiyoriy)")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Rolni tanlang")]
        [Display(Name = "Rol")]
        public string Role { get; set; } = string.Empty;

        [Display(Name = "Guruh (faqat talaba uchun)")]
        public int? GroupId { get; set; }
    }
}