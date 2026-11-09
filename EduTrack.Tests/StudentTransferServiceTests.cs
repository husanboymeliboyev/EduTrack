using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTrack.Tests
{
    public class StudentTransferServiceTests
    {
        private static readonly DateTime Today = new(2026, 11, 5, 9, 0, 0, DateTimeKind.Unspecified);

        private static ApplicationUser NewUser(string loginId, string fullName) => new()
        {
            UserName = loginId,
            NormalizedUserName = loginId.ToUpperInvariant(),
            LoginId = loginId,
            FullName = fullName,
            EmailConfirmed = true
        };

        private sealed record Seed(
            Group English8,
            Group English9,
            Group Stream3,
            Group Stream5,
            ApplicationUser Student);

        private static async Task<Seed> SeedAsync(ApplicationDbContext db)
        {
            var c8 = new Group { Name = "8", Type = GroupType.Class, Code = "8" };
            var c9 = new Group { Name = "9", Type = GroupType.Class, Code = "9" };
            var s3 = new Group { Name = "3-guruh", Type = GroupType.Stream, Code = "3" };
            var s5 = new Group { Name = "5-guruh", Type = GroupType.Stream, Code = "5" };
            db.Groups.AddRange(c8, c9, s3, s5);

            var student = NewUser("10001", "Anvarov Mirjahon");
            db.Users.Add(student);
            await db.SaveChangesAsync();

            student.GroupId = c8.Id;
            db.GroupMemberships.AddRange(
                new GroupMembership
                {
                    StudentId = student.Id,
                    GroupId = c8.Id,
                    Scope = GroupType.Class,
                    StartDate = Today.AddMonths(-3),
                    IsActive = true
                },
                new GroupMembership
                {
                    StudentId = student.Id,
                    GroupId = s3.Id,
                    Scope = GroupType.Stream,
                    StartDate = Today.AddMonths(-3),
                    IsActive = true
                });

            await db.SaveChangesAsync();
            return new Seed(c8, c9, s3, s5, student);
        }

        private static StudentTransferService CreateService(ApplicationDbContext db)
            => new(db);

        [Fact]
        public async Task TransferClass_ClosesOldMembership_KeepsStream()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.TransferAsync(new TransferRequest
            {
                StudentId = seed.Student.Id,
                NewClassGroupId = seed.English9.Id,
                Date = Today,
                Reason = "sinf rahbari iltimosi"
            });

            Assert.True(result.Success);
            Assert.Single(result.Changes);
            Assert.Contains("8 → 9", result.Changes[0]);

            var memberships = await factory.Context.GroupMemberships
                .Include(m => m.Group)
                .Where(m => m.StudentId == seed.Student.Id)
                .ToListAsync();

            // 2 eski (class=9 old, yopilgan) + 2 yangi... eski 2 + yangi class = 3 yozuv
            Assert.Equal(3, memberships.Count);

            var oldClass = memberships.Single(m => m.GroupId == seed.English8.Id);
            Assert.False(oldClass.IsActive);
            Assert.Equal(Today.Date, oldClass.EndDate);
            Assert.Equal("sinf rahbari iltimosi", oldClass.EndReason);

            var newClass = memberships.Single(m => m.GroupId == seed.English9.Id);
            Assert.True(newClass.IsActive);
            Assert.Null(newClass.EndDate);

            // Yo'nalish guruhi tegmagan
            var stream = memberships.Single(m => m.GroupId == seed.Stream3.Id);
            Assert.True(stream.IsActive);

            var user = await factory.Context.Users.FindAsync(seed.Student.Id);
            Assert.Equal(seed.English9.Id, user!.GroupId);
        }

        [Fact]
        public async Task TransferStream_ClosesOldStream_KeepsClass()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.TransferAsync(new TransferRequest
            {
                StudentId = seed.Student.Id,
                NewStreamGroupId = seed.Stream5.Id,
                Date = Today,
                Reason = "5-guruh o'quvchi xohishi"
            });

            Assert.True(result.Success);

            var memberships = await factory.Context.GroupMemberships
                .Where(m => m.StudentId == seed.Student.Id)
                .ToListAsync();

            Assert.Equal(3, memberships.Count);

            var oldStream = memberships.Single(m => m.GroupId == seed.Stream3.Id);
            Assert.False(oldStream.IsActive);
            Assert.Equal("5-guruh o'quvchi xohishi", oldStream.EndReason);

            var classMembership = memberships.Single(m => m.Scope == GroupType.Class && m.GroupId == seed.English8.Id);
            Assert.True(classMembership.IsActive);

            var user = await factory.Context.Users.FindAsync(seed.Student.Id);
            Assert.Equal(seed.English8.Id, user!.GroupId); // legacy sinf o'zgarmagan
        }

        [Fact]
        public async Task TransferAsync_RequiresTargetOrClass()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.TransferAsync(new TransferRequest
            {
                StudentId = seed.Student.Id,
                Date = Today
            });

            Assert.False(result.Success);
            Assert.Contains("Kamida", result.Error);
        }

        [Fact]
        public async Task TransferAsync_SameTarget_ReturnsError()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.TransferAsync(new TransferRequest
            {
                StudentId = seed.Student.Id,
                NewClassGroupId = seed.English8.Id,
                Date = Today
            });

            Assert.False(result.Success);
            Assert.Contains("allaqachon", result.Error);
        }

        [Fact]
        public async Task TransferAsync_UnknownTarget_ReturnsError()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.TransferAsync(new TransferRequest
            {
                StudentId = seed.Student.Id,
                NewClassGroupId = 99999,
                Date = Today
            });

            Assert.False(result.Success);
            Assert.Contains("topilmadi", result.Error);

            // Hech narsa o'zgarmagan
            Assert.Equal(2, await factory.Context.GroupMemberships.CountAsync());
        }
    }
}
