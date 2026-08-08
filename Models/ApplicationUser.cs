using Microsoft.AspNetCore.Identity;

namespace EduTrack.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }

        // Talaba qaysi guruhga tegishli (faqat Student roli uchun ishlatiladi)
        public int? GroupId { get; set; }
        public Group? Group { get; set; }

        // Tizimga kirish uchun ishlatiladigan raqamli login (masalan: 10001).
        // Email o'rniga shu orqali kiriladi — Hemis'dagiga o'xshash tanish tajriba uchun.
        public string LoginId { get; set; } = string.Empty;

        // Admin hisob yaratganda vaqtinchalik parol beradi; foydalanuvchi birinchi marta
        // kirganda shu belgi true bo'lsa, avtomatik "Parolni almashtirish" sahifasiga yo'naltiriladi.
        public bool MustChangePassword { get; set; } = true;
    }
}