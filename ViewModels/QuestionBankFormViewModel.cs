using System.ComponentModel.DataAnnotations;

namespace EduTrack.ViewModels
{
    public class QuestionBankFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Bank nomini kiriting")]
        [StringLength(150)]
        [Display(Name = "Bank nomi")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Fanni tanlang")]
        [Display(Name = "Fan")]
        public int SubjectId { get; set; }

        [Display(Name = "Guruh (ixtiyoriy)")]
        public int? GroupId { get; set; }
    }
}
