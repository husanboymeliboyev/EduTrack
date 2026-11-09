namespace EduTrack.Models
{
    /// <summary>
    /// Imtihon turi. <see cref="Regular"/> — odatiy nazorat imtihoni.
    /// <see cref="Monthly"/> — oylik reytingga kiruvchi imtihon: natijasi
    /// (tasdiqlangandan keyin) tanlangan oy uchun 100 ballik shkalada
    /// fan reytingiga qo'shiladi.
    /// </summary>
    public enum ExamKind
    {
        Regular = 0,
        Monthly = 1
    }
}
