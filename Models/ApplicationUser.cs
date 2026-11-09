using Microsoft.AspNetCore.Identity;

namespace EduTrack.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }

        // Talaba qaysi guruhga tegishli (faqat Student roli uchun ishlatiladi).
        // O'tish davrida saqlanadi: a'zolikning yagona manbai — GroupMemberships.
        public int? GroupId { get; set; }
        public Group? Group { get; set; }

        // Sinf va guruh a'zoliklari tarixi.
        public ICollection<GroupMembership> Memberships { get; set; } = new List<GroupMembership>();

        // Fanlar bo'yicha aniq guruh biriktirmalari (aralash o'qish).
        public ICollection<StudentGroupSubject> StudentGroupSubjects { get; set; } = new List<StudentGroupSubject>();

        // Tizimga kirish uchun ishlatiladigan raqamli login (masalan: 10001).
        // Email o'rniga shu orqali kiriladi — Hemis'dagiga o'xshash tanish tajriba uchun.
        public string LoginId { get; set; } = string.Empty;

        // Admin hisob yaratganda vaqtinchalik parol beradi; foydalanuvchi birinchi marta
        // kirganda shu belgi true bo'lsa, avtomatik "Parolni almashtirish" sahifasiga yo'naltiriladi.
        public bool MustChangePassword { get; set; } = true;
    }
}