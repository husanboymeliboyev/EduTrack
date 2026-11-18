using EduTrack.Models;

namespace EduTrack.ViewModels
{
    // O'quvchi kabinetidagi umumiy ma'lumot: 1-smenada o'qiydigan sinfi va
    // 2-smenada o'qiydigan (fan bo'yicha) guruhi. Ikkalasi ham bo'sh
    // bo'lishi mumkin — u holda o'quvchi hali biriktirilmagan hisoblanadi.
    public class StudentDashboardViewModel
    {
        public Group? ClassGroup { get; set; }
        public Group? StreamGroup { get; set; }

        public bool HasAnyGroup => ClassGroup != null || StreamGroup != null;
    }
}
