using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTrack.Tests
{
    public class StudentContextServiceTests
    {
        private static readonly DateTime Today = new(2026, 11, 4, 9, 0, 0, DateTimeKind.Unspecified);

        private static StudentContextService CreateService(ApplicationDbContext db)
            => new(db, new TestAppClock(Today));

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

        private static async Task<(Group stream1, Group stream2, Group clazz, ApplicationUser student)> SeedAsync(ApplicationDbContext db)
        {
            var stream1 = new Group { Name = "1-guruh", Type = GroupType.Stream, Code = "G1" };
            var stream2 = new Group { Name = "2-guruh", Type = GroupType.Stream, Code = "G2" };
            var clazz = new Group { Name = "10A", Type = GroupType.Class, Code = "10A" };
            db.Groups.AddRange(stream1, stream2, clazz);

            var student = NewStudent("10001");
            db.Users.Add(student);

            await db.SaveChangesAsync();
            return (stream1, stream2, clazz, student);
        }

        [Fact]
        public async Task Assign_ClassAndStream_AreBothAvailable()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var (stream, _, clazz, student) = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            await service.AssignAsync(student.Id, clazz.Id, Today);
            await service.AssignAsync(student.Id, stream.Id, Today);

            var context = await service.GetContextAsync(student.Id);

            Assert.True(context.HasClass);
            Assert.True(context.HasStream);
            Assert.Equal(clazz.Id, context.ClassGroup!.Id);
            Assert.Equal(stream.Id, context.StreamGroup!.Id);
        }

        [Fact]
        public async Task Assign_NewStream_ClosesPreviousAndKeepsHistory()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var (stream1, stream2, _, student) = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            await service.AssignAsync(student.Id, stream1.Id, new DateTime(2026, 9, 1));
            await service.AssignAsync(student.Id, stream2.Id, new DateTime(2026, 11, 1));

            var memberships = await factory.Context.GroupMemberships
                .Where(m => m.StudentId == student.Id)
                .OrderBy(m => m.StartDate)
                .ToListAsync();

            Assert.Equal(2, memberships.Count);

            var old = memberships[0];
            Assert.False(old.IsActive);
            Assert.Equal(new DateTime(2026, 11, 1), old.EndDate);

            var current = memberships[1];
            Assert.True(current.IsActive);
            Assert.Equal(stream2.Id, current.GroupId);
            Assert.Equal(GroupType.Stream, current.Scope);
        }

        [Fact]
        public async Task GetStudentIds_UsesDateRange_NotOnlyIsActive()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var (stream1, stream2, _, student) = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            await service.AssignAsync(student.Id, stream1.Id, new DateTime(2026, 9, 1));
            await service.AssignAsync(student.Id, stream2.Id, new DateTime(2026, 11, 1));

            var inOctober = await service.GetStudentIdsAsync(stream1.Id, new DateTime(2026, 10, 15));
            var stream1Today = await service.GetStudentIdsAsync(stream1.Id);
            var stream2Today = await service.GetStudentIdsAsync(stream2.Id);

            Assert.Contains(student.Id, inOctober);
            Assert.DoesNotContain(student.Id, stream1Today);
            Assert.Contains(student.Id, stream2Today);
        }

        [Fact]
        public async Task LegacyGroupId_IsUsedWhenNoMembershipExists()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var (stream1, _, _, student) = await SeedAsync(factory.Context);

            student.GroupId = stream1.Id;
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);
            var context = await service.GetContextAsync(student.Id);
            var ids = await service.GetStudentIdsAsync(stream1.Id);
            var isMember = await service.IsActiveMemberAsync(student.Id, stream1.Id);

            Assert.Equal(stream1.Id, context.StreamGroup!.Id);
            Assert.Contains(student.Id, ids);
            Assert.True(isMember);
        }

        [Fact]
        public async Task Membership_TakesPrecedenceOverLegacyGroupId()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var (stream1, stream2, _, student) = await SeedAsync(factory.Context);

            student.GroupId = stream1.Id;
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);
            await service.AssignAsync(student.Id, stream2.Id, Today);

            var context = await service.GetContextAsync(student.Id);
            var stream1Ids = await service.GetStudentIdsAsync(stream1.Id);
            var stream2Ids = await service.GetStudentIdsAsync(stream2.Id);

            Assert.Equal(stream2.Id, context.StreamGroup!.Id);
            Assert.DoesNotContain(student.Id, stream1Ids);
            Assert.Contains(student.Id, stream2Ids);
        }

        [Fact]
        public async Task Assign_Stream_AlsoWritesLegacyGroupId()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var (stream1, _, _, student) = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            await service.AssignAsync(student.Id, stream1.Id, Today);

            var stored = await factory.Context.Users.AsNoTracking().FirstAsync(u => u.Id == student.Id);
            Assert.Equal(stream1.Id, stored.GroupId);
        }

        [Fact]
        public async Task End_ClearsLegacyGroupId()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var (stream1, _, _, student) = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            await service.AssignAsync(student.Id, stream1.Id, Today);
            var ended = await service.EndAsync(student.Id, GroupType.Stream, Today);

            var stored = await factory.Context.Users.AsNoTracking().FirstAsync(u => u.Id == student.Id);

            Assert.True(ended);
            Assert.Null(stored.GroupId);
            Assert.False(await service.IsActiveMemberAsync(student.Id, stream1.Id));
        }

        [Fact]
        public async Task Assign_SameGroupTwice_IsIdempotent()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var (stream1, _, _, student) = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var first = await service.AssignAsync(student.Id, stream1.Id, Today);
            var second = await service.AssignAsync(student.Id, stream1.Id, Today.AddDays(1));

            var count = await factory.Context.GroupMemberships.CountAsync(m => m.StudentId == student.Id);

            Assert.Equal(first.Id, second.Id);
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task TwoActiveMembershipsInSameScope_AreRejectedByDatabase()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var (stream1, stream2, _, student) = await SeedAsync(factory.Context);

            factory.Context.GroupMemberships.AddRange(
                new GroupMembership
                {
                    StudentId = student.Id,
                    GroupId = stream1.Id,
                    Scope = GroupType.Stream,
                    StartDate = Today,
                    IsActive = true
                },
                new GroupMembership
                {
                    StudentId = student.Id,
                    GroupId = stream2.Id,
                    Scope = GroupType.Stream,
                    StartDate = Today,
                    IsActive = true
                });

            await Assert.ThrowsAsync<DbUpdateException>(() => factory.Context.SaveChangesAsync());
        }

        [Fact]
        public async Task Assign_ToArchivedGroup_Throws()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var (stream1, _, _, student) = await SeedAsync(factory.Context);
            stream1.IsArchived = true;
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AssignAsync(student.Id, stream1.Id, Today));
        }
    }
}
