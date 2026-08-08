using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EduTrack.ViewModels
{
    public class BulkCreateStudentsViewModel
    {
        [Required(ErrorMessage = "Guruhni tanlang")]
        [Display(Name = "Guruh")]
        public int GroupId { get; set; }

        [Display(Name = "Excel fayl (.xlsx)")]
        public IFormFile? ExcelFile { get; set; }
    }
}