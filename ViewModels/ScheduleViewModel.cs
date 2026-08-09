using System.ComponentModel.DataAnnotations;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduTrack.ViewModels
{
    // Create/Edit forma uchun
    public class ScheduleFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Fanni tanlang")]
        [Display(Name = "Fan")]
        public int SubjectId { get; set; }

        [Required(ErrorMessage = "Guruhni tanlang")]
        [Display(Name = "Guruh")]
        public int GroupId { get; set; }

        [Required(ErrorMessage = "Hafta kunini tanlang")]
        [Display(Name = "Hafta kuni")]
        public DayOfWeek DayOfWeek { get; set; }

        [Required(ErrorMessage = "Boshlanish vaqtini kiriting")]
        [DataType(DataType.Time)]
        [Display(Name = "Boshlanish vaqti")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "Tugash vaqtini kiriting")]
        [DataType(DataType.Time)]
        [Display(Name = "Tugash vaqti")]
        public TimeSpan EndTime { get; set; }

        public List<SelectListItem> Subjects { get; set; } = new();
        public List<SelectListItem> Groups { get; set; } = new();
    }

    // Grid'da bitta dars blokini chizish uchun
    public class ScheduleBlockViewModel
    {
        public int Id { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string? TeacherName { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        // Fan nomidan hosil qilingan barqaror rang (0-359 gradus, HSL)
        public int HueDegree { get; set; }
    }

    // Haftalik grid uchun umumiy konteyner (Admin/O'qituvchi/Talaba barchasida ishlatiladi)
    public class WeeklyScheduleViewModel
    {
        public string? Title { get; set; }
        public List<ScheduleBlockViewModel> Blocks { get; set; } = new();

        // Faqat O'qituvchi sahifasida to'ldiriladi
        public TeacherWorkloadViewModel? Workload { get; set; }
    }

    public class TeacherWorkloadViewModel
    {
        public double TotalHoursPerWeek { get; set; }
        public List<SubjectWorkloadItem> BySubject { get; set; } = new();
    }

    public class SubjectWorkloadItem
    {
        public string SubjectName { get; set; } = string.Empty;
        public double Hours { get; set; }
    }
}