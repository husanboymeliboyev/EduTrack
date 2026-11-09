namespace EduTrack.Services
{
    /// <summary>
    /// "O'quvchilar guruh ro'yxati sinflar bo'yicha" Excel faylini o'qib,
    /// sinflar va yo'nalish guruhlariga ajratadi. Dry-run (hech narsa saqlaydi) va
    /// confirm (import) bir xil kod yo'lini ishlatadi, natijalar mosligi kafolatlanadi.
    ///
    /// Fayl qoidalari (SOD — ushbu faylga oid, o'zgartirishsiz ishlaydi):
    ///   - Sinfi: 5, 5-6, 6 -> "5-6" sinf; 7, 8, 9 -> o'zi;
    ///     "10-11" -> birinchi bo'sh qatorgacha 10A, keyin 10B;
    ///     "rus"  -> 10D.
    ///   - Barchasi bo'sh bo'sh qatorlar (Familiya/Ism/Sinfi/Guruh) — ajratgich,
    ///     o'quvchi emas, hisobotda alohida ko'rsatiladi.
    ///   - Takror ismlar: rus (10D) qatori qoladi, qolgan qatori tashlab yuboriladi.
    ///   - "Inomov Muxammadziyo" ikki marta (g4 va g6) — bitta o'quvchi, g4 bilan
    ///     import qilinadi (g6 qatori o'tkazib yuboriladi, sababi yoziladi).
    ///   - 9-sinf "Abdurazzoqov Islombek" — guruh faylda bo'sh, 5-guruhga biriktiriladi
    ///     (admin tomonidan tasdiqlangan).
    /// </summary>
    public interface IStudentImportService
    {
        /// <summary>
        /// Dry-run: faylni o'qib, nima bo'lishini ko'rsatadi. Bazaga hech narsa yozmaydi.
        /// </summary>
        StudentImportPreview Preview(Stream xlsxStream);

        /// <summary>
        /// Confirm: faylni qayta o'qib, o'quvchilarni yaratadi (sinflar va
        /// 1-7 guruhlarni topib yoki yaratib), a'zolik tarixini saqlaydi.
        /// </summary>
        Task<StudentImportConfirmResult> ImportAsync(Stream xlsxStream);
    }
}
