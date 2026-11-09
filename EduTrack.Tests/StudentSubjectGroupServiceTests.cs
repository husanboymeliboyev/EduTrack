using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTrack.Tests
{
    /// <summary>
    /// Fan bo'yicha guruh (aralash o'qish) servisi: standart holat o'quvchining
    /// guruhi, override esa aniq fan uchun boshqa guruh.
    /// </summary>
    public class StudentSubjectGroupServiceTests
    {
        private static readonly DateTime Today = new(2026, 11, 6, 9, 0, 0, DateTimeKind.Unspecified);

        private sealed record Seed(
            Group Stream1,
            Group Stream2,
            Subject Math,
            Subject English,
            ApplicationUser StudentA,
            ApplicationUser StudentB);

        private static StudentSubjectGroupService CreateService(ApplicationDbContext db)
        {
            var clock = new TestAppClock(Today);
            var context = new StudentContextService(db, clock);
            return new StudentSubjectGroupService(db, context, clock);
        }

        private static async Task<Seed> SeedAsync(ApplicationDbContext db)
        {
            var stream1 = new Group { Name = "1-guruh", Type = GroupType.Stream, Code = "G1" };
            var stream2 = new Group { Name = "2-guruh", Type = GroupType.Stream, Code = "G2" };
            db.Groups.AddRange(stream1, stream2);

            var math = new Subject { Name = "Matematika" };
            var english = new Subject { Name = "Ingliz tili" };
            db.Subjects.AddRange(math, english);
            await db.SaveChangesAsync();

            // 1-guruh: matematika + ingliz tili; 2-guruh: faqat matematika.
            db.GroupSubjects.AddRange(
                new GroupSubject { GroupId = stream1.Id, SubjectId = math.Id },
                new GroupSubject { GroupId = stream1.Id, SubjectId = english.Id },
                new GroupSubject { GroupId = stream2.Id, SubjectId = math.Id });
            await db.SaveChangesAsync();

            var studentA = NewStudent("10001");
            var studentB = NewStudent("10002");
            db.Users.AddRange(studentA, studentB);
            await db.SaveChangesAsync();

            // Ikki o'quvchi ham 1-guruhning a'zosi (standart).
            var clock = new TestAppClock(Today);
            var memberships = new StudentContextService(db, clock);
            await memberships.AssignAsync(studentA.Id, stream1.Id, Today);
            await memberships.AssignAsync(studentB.Id, stream1.Id, Today);

            return new Seed(stream1, stream2, math, english, studentA, studentB);
        }

        private static ApplicationUser NewStudent(string loginId) => new()
        {
            UserName = loginId,
            NormalizedUserName = loginId.ToUpperInvariant(),
            LoginId = loginId,
            FullName = $"Student {loginId}",
            Email = $"{loginId}@example.com",
            EmailConfirmed = true,
            MustChangePassword = false
        };

        [Fact]
        public async Task GetGroupId_NoOverride_ReturnsDefaultGroupId()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var mathGroup = await service.GetGroupIdAsync(seed.StudentA.Id, seed.Math.Id);
            var defaultGroup = await service.GetDefaultGroupIdAsync(seed.StudentA.Id);

            Assert.Equal(defaultGroup, mathGroup);
            Assert.Equal(seed.Stream1.Id, mathGroup);
        }

        [Fact]
        public async Task SetGroup_Override_ResolvedForSubjectOnly()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            // Matematikani 2-guruhga biriktirilgan o'quvchi
            await service.SetGroupAsync(seed.StudentA.Id, seed.Math.Id, seed.Stream2.Id, Today, "Daraja bo'yicha");

            var mathGroup = await service.GetGroupIdAsync(seed.StudentA.Id, seed.Math.Id);
            var englishGroup = await service.GetGroupIdAsync(seed.StudentA.Id, seed.English.Id);

            Assert.Equal(seed.Stream2.Id, mathGroup);
            Assert.Equal(seed.Stream1.Id, englishGroup);

            // Biriktirish tarixini saqlash
            var assignments = await service.GetAssignmentsAsync(seed.StudentA.Id);
            var saved = Assert.Single(assignments);
            Assert.True(saved.IsActive);
            Assert.Equal(seed.Stream2.Id, saved.GroupId);
        }

        [Fact]
        public async Task SetGroup_BackToDefault_ClosesOverrideAndReturnsNull()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var openDate = new DateTime(2026, 11, 2);
            await service.SetGroupAsync(seed.StudentA.Id, seed.Math.Id, seed.Stream2.Id, openDate);
            var resetDate = new DateTime(2026, 11, 5);
            var result = await service.SetGroupAsync(seed.StudentA.Id, seed.Math.Id, seed.Stream1.Id, resetDate);

            Assert.Null(result);
            Assert.Equal(seed.Stream1.Id, await service.GetGroupIdAsync(seed.StudentA.Id, seed.Math.Id));

            var activeCount = await factory.Context.StudentGroupSubjects
                .CountAsync(x => x.StudentId == seed.StudentA.Id && x.IsActive);
            Assert.Equal(0, activeCount);

            var history = await factory.Context.StudentGroupSubjects
                .Where(x => x.StudentId == seed.StudentA.Id)
                .ToListAsync();
            var closed = Assert.Single(history, x => !x.IsActive);
            Assert.Equal(resetDate, closed.EndDate);
        }

        [Fact]
        public async Task GetStudentIds_Roster_HonorsPerSubjectOverride()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            // A o'quvchisi matematikani 2-guruhda o'qiydi, B esa 1-guruhda.
            await service.SetGroupAsync(seed.StudentA.Id, seed.Math.Id, seed.Stream2.Id, Today);

            var mathInGroup1 = await service.GetStudentIdsAsync(seed.Stream1.Id, seed.Math.Id);
            var mathInGroup2 = await service.GetStudentIdsAsync(seed.Stream2.Id, seed.Math.Id);
            var englishInGroup1 = await service.GetStudentIdsAsync(seed.Stream1.Id, seed.English.Id);

            // Matematika: A 2-guruhga o'tkazildi, B faqat 1-guruhda.
            Assert.NotEmpty(mathInGroup1);
            Assert.Contains(seed.StudentB.Id, mathInGroup1);
            Assert.DoesNotContain(seed.StudentA.Id, mathInGroup1);

            Assert.Contains(seed.StudentA.Id, mathInGroup2);
            Assert.DoesNotContain(seed.StudentB.Id, mathInGroup2);

            // Ingliz tili: ikkalasi ham 1-guruhda (standart bo'lgani kabi).
            Assert.Contains(seed.StudentA.Id, englishInGroup1);
            Assert.Contains(seed.StudentB.Id, englishInGroup1);
        }

        [Fact]
        public async Task GetSubjectIds_IncludesOverriddenSubjectAndSkipsOldPair()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            await service.SetGroupAsync(seed.StudentA.Id, seed.Math.Id, seed.Stream2.Id, Today);

            var subjectIds = await service.GetSubjectIdsAsync(seed.StudentA.Id);

            Assert.Contains(seed.Math.Id, subjectIds);
            Assert.Contains(seed.English.Id, subjectIds);
        }

        [Fact]
        public async Task IsStudentInGroupSubject_ChecksResolvedGroupAndLink()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            await service.SetGroupAsync(seed.StudentA.Id, seed.Math.Id, seed.Stream2.Id, Today);

            Assert.True(await service.IsStudentInGroupSubjectAsync(seed.StudentA.Id, seed.Stream2.Id, seed.Math.Id));
            Assert.False(await service.IsStudentInGroupSubjectAsync(seed.StudentA.Id, seed.Stream1.Id, seed.Math.Id));

            // 1-guruh ingliz tilini o'qiydi, A standart holatgacha 1-guruhda.
            Assert.True(await service.IsStudentInGroupSubjectAsync(seed.StudentA.Id, seed.Stream1.Id, seed.English.Id));
        }

        [Fact]
        public async Task SetGroup_GroupNotTeachingSubject_IsRejected()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            // 2-guruh ingliz tilini o'qitmaydi.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SetGroupAsync(seed.StudentA.Id, seed.English.Id, seed.Stream2.Id, Today));
        }

        [Fact]
        public async Task SetGroup_ClassGroup_IsRejected()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var classGroup = new Group { Name = "10A", Type = GroupType.Class };
            factory.Context.Groups.Add(classGroup);
            await factory.Context.SaveChangesAsync();

            factory.Context.GroupSubjects.Add(new GroupSubject
            {
                GroupId = classGroup.Id,
                SubjectId = seed.Math.Id
            });
            await factory.Context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SetGroupAsync(seed.StudentA.Id, seed.Math.Id, classGroup.Id, Today));
        }

        [Fact]
        public async Task GetGroupMap_ExplicitOverride_DoesNotOverrideOtherSubjects()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            await service.SetGroupAsync(seed.StudentA.Id, seed.Math.Id, seed.Stream2.Id, Today);

            var map = await service.GetGroupMapAsync(seed.StudentA.Id, new[] { seed.Math.Id, seed.English.Id });

            Assert.Equal(2, map.Count);
            Assert.Equal(seed.Stream2.Id, map[seed.Math.Id]);
            Assert.Equal(seed.Stream1.Id, map[seed.English.Id]);
        }
    }
}
