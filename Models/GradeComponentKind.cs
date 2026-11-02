namespace EduTrack.Models
{
    // Baholash komponentining turi. Ko'rgazmadagi uchta ko'rsatkich (Ball, Topshiriqlar,
    // Davomat) shu tur bo'yicha ajratiladi.
    public enum GradeComponentKind
    {
        // Oddiy ball komponenti (oraliq, imtihon va h.k.) — "Ball" ko'rsatkichiga kiradi.
        Regular = 0,

        // Topshiriqlar bo'yicha qo'lda kiritiladigan (yoki topshiriqqa bog'langan) ball.
        Assignment = 1,

        // Davomat bo'yicha qo'lda kiritiladigan ball.
        Attendance = 2
    }
}
