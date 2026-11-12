using System.ComponentModel.DataAnnotations;
using EduTrack.Models;
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
        [Display(Name = "Guruh / sinf")]
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

        [Display(Name = "Smena")]
        public ScheduleShift Shift { get; set; } = ScheduleShift.Morning;

        [Range(1, 8, ErrorMessage = "Dars raqami 1 dan 8 gacha bo'lishi kerak")]
        [Display(Name = "Dars raqami (ixtiyoriy)")]
        public int? Period { get; set; }

        [Display(Name = "Dars turi")]
        public ScheduleKind Kind { get; set; } = ScheduleKind.Regular;

        [Display(Name = "O'qituvchi (ixtiyoriy)")]
        public string? TeacherId { get; set; }

        public List<SelectListItem> Subjects { get; set; } = new();
        public List<SelectListItem> Groups { get; set; } = new();
        public List<SelectListItem> Teachers { get; set; } = new();
        public List<SelectListItem> DayOptions { get; set; } = new();
        public List<SelectListItem> ShiftOptions { get; set; } = new();
        public List<SelectListItem> KindOptions { get; set; } = new();

        // Guruh id → "Class"/"Stream". Formada guruh tanlanganda smenani
        // avtomatik taklif qilish uchun (sinf → 1-smena, guruh → 2-smena).
        public Dictionary<int, string> GroupTypes { get; set; } = new();
    }

    // Grid'da bitta dars blokini chizish uchun
    public class ScheduleBlockViewModel
    {
        public int Id { get; set; }
        public int SubjectId { get; set; }
        public int GroupId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string? TeacherId { get; set; }
        public string? TeacherName { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public ScheduleShift Shift { get; set; }
        public ScheduleKind Kind { get; set; }
        public int? Period { get; set; }

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

        // ===== Admin "Umumiy jadval" sahifasi uchun filtrlar =====
        public bool ShowFilters { get; set; }
        public bool IsListView { get; set; }
        public int? SelectedGroupId { get; set; }
        public string? SelectedTeacherId { get; set; }
        public ScheduleShift? SelectedShift { get; set; }
        public List<SelectListItem> GroupOptions { get; set; } = new();
        public List<SelectListItem> TeacherOptions { get; set; } = new();
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
