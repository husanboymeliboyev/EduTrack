using System.ComponentModel.DataAnnotations;

namespace EduTrack.ViewModels
{
    public class BulkCreateStudentsViewModel
    {
        [Required(ErrorMessage = "Guruh nomini kiriting")]
        [Display(Name = "Guruh nomi")]
        public string GroupName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kamida bitta ism-familiya kiriting")]
        [Display(Name = "Talabalar ro'yxati (har bir qatorda bitta ism-familiya)")]
        public string NamesText { get; set; } = string.Empty;
    }
}