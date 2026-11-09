using EduTrack.Models;
using EduTrack.ViewModels;

namespace EduTrack.Services
{
    /// <summary>
    /// Oylik reyting uchun yagona o'qish nuqtasi. Reyting real vaqtda
    /// hisoblanadi: faqat <see cref="ExamKind.Monthly"/> imtihonlarning
    /// tasdiqlangan natijalari (0–100%) hisobga olinadi. Davomat va
    /// topshiriqlar reytingga umuman kirmaydi.
    ///
    /// Har bir o'quvchining sinfi (1-smena) va yo'nalishi (tushdan keyingi)
    /// alohida 100 ballik shkalada baholanadi: guruhdagi oylik fanlar
    /// bo'yicha ballar yig'ilib, o'rta arifmetik olinadi. Natijasi yo'q fan
    /// 0 hisoblanadi.
    /// </summary>
    public interface IMonthlyRatingService
    {
        /// <summary>Oylik imtihon mavjud davrlar + joriy oy.</summary>
        Task<IReadOnlyList<MonthlyPeriodOptionViewModel>> GetPeriodsAsync(
            int? selectedYear = null,
            int? selectedMonth = null,
            CancellationToken cancellationToken = default);

        /// <summary>Bitta o'quvchining sinf va yo'nalish reytingi + 6 oylik trend.</summary>
        Task<StudentMonthlyRatingViewModel?> GetStudentRatingAsync(
            string studentId,
            int? year = null,
            int? month = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Admin/o'qituvchi uchun guruhlar kesimidagi oylik reyting.
        /// <paramref name="teacherId"/> berilsa — faqat shu o'qituvchining
        /// guruhlari ko'rsatiladi.
        /// </summary>
        Task<MonthlyRatingOverviewViewModel> GetOverviewAsync(
            int? groupId = null,
            int? year = null,
            int? month = null,
            string? teacherId = null,
            bool isTeacherView = false,
            CancellationToken cancellationToken = default);
    }
}
