using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EduTrack.ViewModels
{
    public class QuestionImportUploadViewModel
    {
        [Required(ErrorMessage = "Fanni tanlang")]
        [Display(Name = "Fan")]
        public int SubjectId { get; set; }

        [Display(Name = "Guruh (ixtiyoriy)")]
        public int? GroupId { get; set; }

        [Display(Name = "Fayl (.txt)")]
        public IFormFile? File { get; set; }
    }

    public class QuestionImportPreviewViewModel
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public int? GroupId { get; set; }
        public string? GroupName { get; set; }
        public List<Services.ParsedQuestion> Questions { get; set; } = new();
        public List<string> Errors { get; set; } = new();

        // Tasdiqlash bosqichida savollarni qayta ishlatish uchun, matnni saqlab turamiz
        public string RawText { get; set; } = string.Empty;
    }
}