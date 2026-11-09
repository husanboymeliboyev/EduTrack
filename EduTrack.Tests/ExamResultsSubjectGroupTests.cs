using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using EduTrack.ViewModels;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTrack.Tests
{
    // Imtihon natijalari fan-guruh resolveri bilan kesilishi (aralash o'qish):
    // filtrlar va guruh ro'yxati legacy GroupId emas, resolved guruh bo'yicha.
    public class ExamResultsSubjectGroupTests
    {
        private static readonly DateTime Day = new(2026, 11, 5, 9, 0, 0, DateTimeKind.Unspecified);

        private sealed record Seed(
            Group Group1,
            Group Group2,
            Subject Subject,
            Exam Exam,
            ApplicationUser OverriddenStudent,
            ApplicationUser DefaultStudent,
            ApplicationUser OtherStudent,
            ApplicationUser StreamStudent);

        private static async Task<Seed> SeedAsync(ApplicationDbContext db)
        {
            var group1 = new Group { Name = "A-guruh" };
            var group2 = new Group { Name = "B-guruh" };
            db.Groups.AddRange(group1, group2);

            var teacher = new ApplicationUser
            {
                UserName = "20001",
                NormalizedUserName = "20001",
                FullName = "Teacher One",
                LoginId = "20001",
                Email = "20001@example.com",
                EmailConfirmed = true,
                MustChangePassword = false
            };
            await db.SaveChangesAsync();
            db.Users.Add(teacher);
            await db.SaveChangesAsync();

            var subject = new Subject { Name = "Matematika", TeacherId = teacher.Id };
            db.Subjects.Add(subject);
            await db.SaveChangesAsync();

            var bank = new QuestionBank { Name = "Matematika banki", SubjectId = subject.Id };
            db.QuestionBanks.Add(bank);
            await db.SaveChangesAsync();

            var exam = new Exam
            {
                Title = "Matematika test",
                SubjectId = subject.Id,
                QuestionBankId = bank.Id,
                QuestionCount = 10,
                DurationMinutes = 60,
                CreatedDate = Day
            };
            db.Exams.Add(exam);
            await db.SaveChangesAsync();

            // s1: legacy guruh A, lekin fan bo'yicha B-guruhga override qilingan.
            var overridden = NewStudent("Overriden O'quvchi", "40001", group1.Id);
            // s2: guruh A, override yo'q.
            var @default = NewStudent("Default O'quvchi", "40002", group1.Id);
            // s3: guruh B, override yo'q.
            var other = NewStudent("Other O'quvchi", "40003", group2.Id);
            // s4: legacy GroupId A, lekin aktiv stream a'zoligi B — standart guruh B.
            var streamStudent = NewStudent("Stream O'quvchi", "40004", group1.Id);
            db.Users.AddRange(overridden, @default, other, streamStudent);
            await db.SaveChangesAsync();

            db.StudentGroupSubjects.Add(new StudentGroupSubject
            {
                StudentId = overridden.Id,
                SubjectId = subject.Id,
                GroupId = group2.Id,
                StartDate = Day.AddYears(-1),
                IsActive = true
            });

            db.GroupMemberships.Add(new GroupMembership
            {
                StudentId = streamStudent.Id,
                GroupId = group2.Id,
                Scope = GroupType.Stream,
                StartDate = Day.AddYears(-1),
                IsActive = true
            });

            db.ExamResults.AddRange(
                NewResult(exam, overridden, 70, 100),
                NewResult(exam, @default, 80, 100),
                NewResult(exam, other, 90, 100),
                NewResult(exam, streamStudent, 60, 100));
            await db.SaveChangesAsync();

            return new Seed(
                group1, group2, subject, exam, overridden, @default, other, streamStudent);
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

        private static ExamResult NewResult(Exam exam, ApplicationUser student, int correct, int total) => new()
        {
            ExamId = exam.Id,
            StudentId = student.Id,
            CorrectAnswers = correct,
            TotalQuestions = total,
            CompletedDate = Day,
            IsApproved = false
        };

        private static ExamResultsService CreateService(TestDbFactory factory)
            => new(factory.Context, new AppClock(AppClock.ResolveTimeZone("Asia/Tashkent")));

        [Fact]
        public async Task Filter_ByGroup_FollowsSubjectOverride_NotLegacyGroupId()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory);

            var groupA = await service.GetForAdminAsync(seed.Exam.Id, new ExamResultsFilterViewModel
            {
                GroupId = seed.Group1.Id
            });
            Assert.NotNull(groupA);
            Assert.Equal(1, groupA!.TotalCount);
            var groupARow = Assert.Single(groupA.Rows);
            Assert.Equal("40002", groupARow.LoginId);

            var groupB = await service.GetForAdminAsync(seed.Exam.Id, new ExamResultsFilterViewModel
            {
                GroupId = seed.Group2.Id
            });
            Assert.NotNull(groupB);
            Assert.Equal(3, groupB!.TotalCount);
            Assert.Contains(groupB.Rows, r => r.LoginId == "40001");
            Assert.Contains(groupB.Rows, r => r.LoginId == "40003");
            Assert.Contains(groupB.Rows, r => r.LoginId == "40004");
        }

        [Fact]
        public async Task Composite_GroupNameAndDropdown_UseResolvedGroup()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory);

            var page = await service.GetForAdminAsync(seed.Exam.Id, new ExamResultsFilterViewModel());

            Assert.NotNull(page);
            Assert.Equal(4, page!.TotalCount);

            // Ro'yxatdagi guruhlar resolved kesimdan: B-guruh ham ko'rinadi.
            Assert.Equal(2, page.Groups.Count);
            Assert.Contains(page.Groups, g => g.Id == seed.Group1.Id);
            Assert.Contains(page.Groups, g => g.Id == seed.Group2.Id);

            // Override qilingan o'quvchining qator nomi B-guruh ko'rsatadi.
            var overriddenRow = page.Rows.Single(r => r.LoginId == "40001");
            Assert.Equal("B-guruh", overriddenRow.GroupName);

            // Standart o'quvchi o'z guruhini ko'rsatadi — legacy bir xil.
            var defaultRow = page.Rows.Single(r => r.LoginId == "40002");
            Assert.Equal("A-guruh", defaultRow.GroupName);

            // Stream a'zoligi legacy GroupId ustidan ustun: s4 → B-guruh.
            var streamRow = page.Rows.Single(r => r.LoginId == "40004");
            Assert.Equal("B-guruh", streamRow.GroupName);
        }
    }
}
