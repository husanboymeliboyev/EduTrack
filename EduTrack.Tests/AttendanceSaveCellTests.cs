using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTrack.Tests
{
    public class AttendanceSaveCellTests
    {
        private static readonly DateTime Today = new(2026, 11, 2, 10, 0, 0, DateTimeKind.Unspecified);

        private static AttendanceService CreateService(ApplicationDbContext db, int backfillDays = 30)
        {
            var options = Options.Create(new AttendanceOptions { BackfillDays = backfillDays });
            var clock = new TestAppClock(Today);
            var studentContext = new StudentContextService(db, clock);
            var subjectGroups = new StudentSubjectGroupService(db, studentContext, clock);
            return new AttendanceService(db, clock, options, subjectGroups);
        }

        [Fact]
        public async Task SaveCell_CreatesNewRecord()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);
            var studentId = seed.StudentIds[0];

            var result = await service.SaveCellAsync(seed.SubjectId, seed.GroupId, studentId, Today, "Keldi");

            Assert.True(result.Succeeded);
            Assert.Equal(1, result.CreatedCount);
            Assert.Equal(0, result.UpdatedCount);

            var saved = await factory.Context.Attendances.SingleAsync();
            Assert.Equal(studentId, saved.StudentId);
            Assert.Equal(seed.SubjectId, saved.SubjectId);
            Assert.Equal(Today.Date, saved.Date);
            Assert.Equal(AttendanceStatus.Keldi, saved.Status);
        }

        [Fact]
        public async Task SaveCell_UpdatesExistingRecordWithoutDuplicating()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);
            var studentId = seed.StudentIds[0];

            await service.SaveCellAsync(seed.SubjectId, seed.GroupId, studentId, Today, "Keldi");
            var result = await service.SaveCellAsync(seed.SubjectId, seed.GroupId, studentId, Today, "Kelmadi");

            Assert.True(result.Succeeded);
            Assert.Equal(0, result.CreatedCount);
            Assert.Equal(1, result.UpdatedCount);

            var saved = await factory.Context.Attendances.SingleAsync();
            Assert.Equal(AttendanceStatus.Kelmadi, saved.Status);
        }

        [Fact]
        public async Task SaveCell_SameStatus_ReportsNoChanges()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);
            var studentId = seed.StudentIds[0];

            await service.SaveCellAsync(seed.SubjectId, seed.GroupId, studentId, Today, "Keldi");
            var result = await service.SaveCellAsync(seed.SubjectId, seed.GroupId, studentId, Today, "Keldi");

            Assert.True(result.Succeeded);
            Assert.Equal(0, result.CreatedCount);
            Assert.Equal(0, result.UpdatedCount);
            Assert.Equal(1, await factory.Context.Attendances.CountAsync());
        }

        [Fact]
        public async Task SaveCell_InvalidStatus_IsRejected()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.SaveCellAsync(
                seed.SubjectId, seed.GroupId, seed.StudentIds[0], Today, "abc");

            Assert.False(result.Succeeded);
            Assert.Equal(AttendanceSaveStatus.InvalidStatus, result.Status);
            Assert.Empty(factory.Context.Attendances);
        }

        [Fact]
        public async Task SaveCell_StudentFromAnotherGroup_IsRejected()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.SaveCellAsync(
                seed.SubjectId, seed.GroupId, seed.OtherGroupStudentId, Today, "Keldi");

            Assert.False(result.Succeeded);
            Assert.Equal(AttendanceSaveStatus.StudentNotInGroup, result.Status);
            Assert.Empty(factory.Context.Attendances);
        }

        [Fact]
        public async Task SaveCell_GroupNotLinkedToSubject_IsRejected()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.SaveCellAsync(
                seed.SubjectId, seed.OtherGroupId, seed.StudentIds[0], Today, "Keldi");

            Assert.False(result.Succeeded);
            Assert.Equal(AttendanceSaveStatus.GroupSubjectMismatch, result.Status);
            Assert.Empty(factory.Context.Attendances);
        }

        [Fact]
        public async Task SaveCell_FutureDate_IsRejected()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.SaveCellAsync(
                seed.SubjectId, seed.GroupId, seed.StudentIds[0], Today.AddDays(1), "Keldi");

            Assert.False(result.Succeeded);
            Assert.Equal(AttendanceSaveStatus.DateOutOfRange, result.Status);
            Assert.Empty(factory.Context.Attendances);
        }

        [Fact]
        public async Task SaveCell_TooOldDate_IsRejected()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.SaveCellAsync(
                seed.SubjectId, seed.GroupId, seed.StudentIds[0], Today.AddDays(-31), "Keldi");

            Assert.False(result.Succeeded);
            Assert.Equal(AttendanceSaveStatus.DateOutOfRange, result.Status);
            Assert.Empty(factory.Context.Attendances);
        }

        [Fact]
        public async Task SaveCell_OldestAllowedDate_IsAccepted()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.SaveCellAsync(
                seed.SubjectId, seed.GroupId, seed.StudentIds[0], Today.AddDays(-30), "Sababli");

            Assert.True(result.Succeeded);
            Assert.Equal(1, result.CreatedCount);
        }
    }
}
