namespace EduTrack.Services
{
    /// <summary>
    /// Davomat saqlash qoidalari bitta joyda: guruh-fan bog'lanishi, sana chegarasi,
    /// holat qiymatlari va ommaviy (N+1 siz) saqlash. Controller faqat ruxsat va
    /// yo'naltirish bilan shug'ullanadi.
    /// </summary>
    public interface IAttendanceService
    {
        /// <summary>Konfiguratsiyadagi o'tmishga ruxsat chegarasi (kunlarda).</summary>
        int BackfillDays { get; }

        /// <summary>Guruh haqiqatan shu fanni o'qiydimi (GroupSubjects jadvali bo'yicha).</summary>
        Task<bool> IsGroupSubjectAsync(int groupId, int subjectId);

        /// <summary>Sana ruxsat etilgan oraliqda bo'lsa null, aks holda xato matni.</summary>
        string? ValidateDate(DateTime date);

        /// <summary>
        /// Davomatni saqlaydi. Noto'g'ri holat, begona guruh yoki ruxsat etilmagan sana
        /// bo'lsa hech narsa yozilmaydi va sabab qaytariladi.
        /// </summary>
        Task<AttendanceSaveResult> SaveAsync(
            int subjectId,
            int groupId,
            DateTime date,
            IReadOnlyDictionary<string, string?>? statuses);

        /// <summary>
        /// Bitta katakni (bitta talaba + sana) saqlaydi. Oylik jurnalda har bir
        /// katak darhol saqlanadi; boshqa talabalarning yozuvlariga tegmaydi.
        /// </summary>
        Task<AttendanceSaveResult> SaveCellAsync(
            int subjectId,
            int groupId,
            string studentId,
            DateTime date,
            string? status);
    }
}
