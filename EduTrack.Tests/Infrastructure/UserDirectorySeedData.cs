using EduTrack.Data;
using EduTrack.Models;
using Microsoft.AspNetCore.Identity;

namespace EduTrack.Tests.Infrastructure
{
    public sealed record UserDirectorySeed(
        string AdminId,
        string Teacher1Id,
        string Teacher2Id,
        int GroupAId,
        int GroupBId,
        int GroupCId,
        string AliyevId,
        string BekovaId,
        string CyrillicId,
        string ApostropheId,
        string SpecialId,
        string ZeroId,
        string LowercaseId);

    // Foydalanuvchilar qidiruvi/filtri testlari uchun alohida ma'lumot to'plami:
    // rollar, 3 guruh, 2 o'qituvchi (turli guruhlarga biriktirilgan fanlar) va
    // sahifalashni tekshirish uchun 25 ta talaba.
    public static class UserDirectorySeedDataFactory
    {
        public static async Task<UserDirectorySeed> CreateAsync(ApplicationDbContext db)
        {
            var adminRole = new IdentityRole("Admin") { NormalizedName = "ADMIN" };
            var teacherRole = new IdentityRole("Teacher") { NormalizedName = "TEACHER" };
            var studentRole = new IdentityRole("Student") { NormalizedName = "STUDENT" };
            db.Roles.AddRange(adminRole, teacherRole, studentRole);
            await db.SaveChangesAsync();

            var groupA = new Group { Name = "Guruh A" };
            var groupB = new Group { Name = "Guruh B" };
            var groupC = new Group { Name = "Guruh C" };
            db.Groups.AddRange(groupA, groupB, groupC);
            await db.SaveChangesAsync();

            var admin = NewUser("Admin User", "10000");
            var teacher1 = NewUser("Teacher One", "20001");
            var teacher2 = NewUser("Teacher Two", "20002");

            var aliyev = NewStudent("Aliyev Vali", "10001", groupA.Id);
            var bekova = NewStudent("Bekova Nodira", "10002", groupA.Id);
            var cyrillic = NewStudent("Алиев Кирилл", "10003", groupB.Id);
            var apostrophe = NewStudent("O'zbekiston O'quvchi", "10004", groupB.Id);
            var special = NewStudent("100% Real_Student", "10005", groupB.Id);
            var zero = NewStudent("Zero Savol", "10006", groupC.Id);
            var lowercase = NewStudent("abdulaziz Karimov", "10007", groupA.Id);

            var students = new List<ApplicationUser>
            {
                aliyev, bekova, cyrillic, apostrophe, special, zero, lowercase
            };

            for (var i = 1; i <= 25; i++)
            {
                students.Add(NewStudent($"Student {i:00}", $"30{i:000}", groupA.Id));
            }

            var users = new List<ApplicationUser> { admin, teacher1, teacher2 };
            users.AddRange(students);

            db.Users.AddRange(users);
            await db.SaveChangesAsync();

            db.UserRoles.AddRange(
                NewUserRole(admin.Id, adminRole.Id),
                NewUserRole(teacher1.Id, teacherRole.Id),
                NewUserRole(teacher2.Id, teacherRole.Id));

            foreach (var student in students)
            {
                db.UserRoles.Add(NewUserRole(student.Id, studentRole.Id));
            }

            await db.SaveChangesAsync();

            // Teacher 1 — A va B guruhlari; Teacher 2 — faqat C guruhi.
            var subject1 = new Subject { Name = "Matematika", TeacherId = teacher1.Id };
            var subject2 = new Subject { Name = "Fizika", TeacherId = teacher2.Id };
            db.Subjects.AddRange(subject1, subject2);
            await db.SaveChangesAsync();

            db.GroupSubjects.AddRange(
                new GroupSubject { GroupId = groupA.Id, SubjectId = subject1.Id },
                new GroupSubject { GroupId = groupB.Id, SubjectId = subject1.Id },
                new GroupSubject { GroupId = groupC.Id, SubjectId = subject2.Id });
            await db.SaveChangesAsync();

            return new UserDirectorySeed(
                admin.Id,
                teacher1.Id,
                teacher2.Id,
                groupA.Id,
                groupB.Id,
                groupC.Id,
                aliyev.Id,
                bekova.Id,
                cyrillic.Id,
                apostrophe.Id,
                special.Id,
                zero.Id,
                lowercase.Id);
        }

        private static ApplicationUser NewUser(string fullName, string loginId) => new()
        {
            UserName = loginId,
            NormalizedUserName = loginId.ToUpperInvariant(),
            FullName = fullName,
            LoginId = loginId,
            Email = $"{loginId}@example.com",
            EmailConfirmed = true,
            MustChangePassword = false
        };

        private static ApplicationUser NewStudent(string fullName, string loginId, int groupId)
        {
            var student = NewUser(fullName, loginId);
            student.GroupId = groupId;
            return student;
        }

        private static IdentityUserRole<string> NewUserRole(string userId, string roleId) => new()
        {
            UserId = userId,
            RoleId = roleId
        };
    }
}
