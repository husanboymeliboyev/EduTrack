namespace EduTrack.Models
{
    /// <summary>
    /// Guruh va Fan orasidagi bog'lovchi (junction) jadval. Bitta fan bir nechta guruhda
    /// o'qitilishi mumkin (masalan "Matematika" — 5A ham, 5B ham), shuning uchun
    /// oddiy GroupId maydonini Subject'ga qo'shish yetarli emas edi.
    /// </summary>
    public class GroupSubject
    {
        public int Id { get; set; }

        public int GroupId { get; set; }
        public Group? Group { get; set; }

        public int SubjectId { get; set; }
        public Subject? Subject { get; set; }
    }
}