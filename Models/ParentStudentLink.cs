namespace EduTrack.Models
{
    // Ota-ona hisobini talabaga bog'laydigan mustahkam aloqa.
    // Bog'lanish faqat barqaror identifikatorlar (ParentId = ApplicationUser.Id,
    // StudentId = ApplicationUser.Id) orqali saqlanadi — ism, guruh nomi yoki
    // mijozdan kelgan matnga tayanmaydi. Bitta ota-ona bir nechta talabaga,
    // bitta talaba ham bir nechta ota-onaga bog'lanishi mumkin.
    public class ParentStudentLink
    {
        public int Id { get; set; }

        public string ParentId { get; set; } = string.Empty;
        public ApplicationUser? Parent { get; set; }

        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
