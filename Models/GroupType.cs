namespace EduTrack.Models
{
    /// <summary>
    /// Guruh turi. <see cref="Stream"/> — tushdan keyingi yo'nalish guruhi
    /// (mavjud barcha guruhlar shu turda), <see cref="Class"/> — tushlikdan
    /// oldingi sinf. Standart qiymat ataylab Stream (0): eski yozuvlar
    /// migratsiyadan keyin o'zgarishsiz qoladi.
    /// </summary>
    public enum GroupType
    {
        Stream = 0,
        Class = 1
    }
}
