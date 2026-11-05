using EduTrack.Data;
using EduTrack.Models;

namespace EduTrack.Tests.Infrastructure
{
    public sealed record AttendanceSeed(
        int GroupId,
        int OtherGroupId,
        int SubjectId,
        int OtherSubjectId,
        IReadOnlyList<string> StudentIds,
        string OtherGroupStudentId);

    // Davomat testlari uchun minimal ma'lumot: 2 guruh, 2 fan.
    // Faqat birinchi fan birinchi guruhga bog'langan (GroupSubjects).
    public static class AttendanceSeedDataFactory
    {
        public static async Task<AttendanceSeed> CreateAsync(ApplicationDbContext db, int studentCount = 3)
        {
            var group = new Group { Name = "Davomat A" };
            var otherGroup = new Group { Name = "Davomat B" };
            db.Groups.AddRange(group, otherGroup);
            await db.SaveChangesAsync();

            var subject = new Subject { Name = "Matematika" };
            var otherSubject = new Subject { Name = "Fizika" };
            db.Subjects.AddRange(subject, otherSubject);
            await db.SaveChangesAsync();

            db.GroupSubjects.Add(new GroupSubject
            {
                GroupId = group.Id,
                SubjectId = subject.Id
            });
            await db.SaveChangesAsync();

            var students = new List<ApplicationUser>(studentCount);
            for (var i = 0; i < studentCount; i++)
            {
                students.Add(NewStudent($"Talaba {i:000}", $"4{i:0000}", group.Id));
            }

            var otherStudent = NewStudent("Boshqa guruh talabasi", "49999", otherGroup.Id);

            db.Users.AddRange(students);
            db.Users.Add(otherStudent);
            await db.SaveChangesAsync();

            return new AttendanceSeed(
                group.Id,
                otherGroup.Id,
                subject.Id,
                otherSubject.Id,
                students.Select(s => s.Id).ToList(),
                otherStudent.Id);
        }

        private static ApplicationUser NewStudent(string fullName, string loginId, int groupId) => new()
        {
            UserName = loginId,
            NormalizedUserName = loginId.ToUpperInvariant(),
            FullName = fullName,
            LoginId = loginId,
            Email = $"{loginId}@example.com",
            EmailConfirmed = true,
            MustChangePassword = false,
            GroupId = groupId
        };
    }
}
