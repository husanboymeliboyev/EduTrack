using System.ComponentModel.DataAnnotations;

namespace EduTrack.Models
{
    public class Assignment
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Sarlavhani kiriting")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Muddatni kiriting")]
        [Display(Name = "Topshirish muddati")]
        public DateTime DueDate { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public string? FilePath { get; set; }
        public string? FileName { get; set; }

        public int SubjectId { get; set; }
        public Subject? Subject { get; set; }

        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}