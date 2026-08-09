using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EduTrack.ViewModels
{
    public class QuestionFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Fanni tanlang")]
        [Display(Name = "Fan")]
        public int SubjectId { get; set; }

        [Required(ErrorMessage = "Savol matnini kiriting")]
        [StringLength(3000)]
        [Display(Name = "Savol matni")]
        public string Text { get; set; } = string.Empty;

        [Display(Name = "Rasm (ixtiyoriy)")]
        public IFormFile? ImageFile { get; set; }

        // Tahrirlashda mavjud rasmni ko'rsatish uchun
        public string? ExistingImagePath { get; set; }

        [Required(ErrorMessage = "1-variantni kiriting")]
        [Display(Name = "1-variant")]
        public string Option1 { get; set; } = string.Empty;

        [Required(ErrorMessage = "2-variantni kiriting")]
        [Display(Name = "2-variant")]
        public string Option2 { get; set; } = string.Empty;

        [Display(Name = "3-variant")]
        public string? Option3 { get; set; }

        [Display(Name = "4-variant")]
        public string? Option4 { get; set; }

        [Required(ErrorMessage = "To'g'ri javobni tanlang")]
        [Display(Name = "To'g'ri javob")]
        public int CorrectOption { get; set; }
    }
}