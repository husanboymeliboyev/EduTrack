using System.Data.Common;
using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTrack.Tests
{
    public class AttendanceServiceTests
    {
        private static readonly DateTime Today = new(2026, 11, 2, 10, 0, 0, DateTimeKind.Unspecified);

        private static AttendanceService CreateService(
            ApplicationDbContext db,
            DateTime? today = null,
            int backfillDays = 30)
        {
            var options = Options.Create(new AttendanceOptions { BackfillDays = backfillDays });
            var clock = new TestAppClock(today ?? Today);
            var studentContext = new StudentContextService(db, clock);
            var subjectGroups = new StudentSubjectGroupService(db, studentContext, clock);
            return new AttendanceService(db, clock, options, subjectGroups);
        }

        private static Dictionary<string, string?> AllStatus(
            IReadOnlyList<string> studentIds,
            string status) =>
            studentIds.ToDictionary(id => id, _ => (string?)status);

        [Fact]
        public async Task Save_InvalidStatus_RejectsWholeSave()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);

            var statuses = AllStatus(seed.StudentIds, "Keldi");
            statuses[seed.StudentIds[0]] = "abc";

            var result = await service.SaveAsync(seed.SubjectId, seed.GroupId, Today, statuses);

            Assert.False(result.Succeeded);
            Assert.Equal(AttendanceSaveStatus.InvalidStatus, result.Status);
            Assert.Empty(factory.Context.Attendances);
        }

        [Fact]
        public async Task Save_StatusZero_IsRejected()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);

            var statuses = AllStatus(seed.StudentIds, "Keldi");
            statuses[seed.StudentIds[0]] = "0";

            var result = await service.SaveAsync(seed.SubjectId, seed.GroupId, Today, statuses);

            Assert.False(result.Succeeded);
            Assert.Equal(AttendanceSaveStatus.InvalidStatus, result.Status);
            Assert.Empty(factory.Context.Attendances);
        }

        [Fact]
        public async Task Save_GroupNotLinkedToSubject_IsRejected()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.SaveAsync(
                seed.SubjectId,
                seed.OtherGroupId,
                Today,
                AllStatus(seed.StudentIds, "Keldi"));

            Assert.False(result.Succeeded);
            Assert.Equal(AttendanceSaveStatus.GroupSubjectMismatch, result.Status);
            Assert.Empty(factory.Context.Attendances);
        }

        [Fact]
        public async Task Save_FutureDate_IsRejected()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.SaveAsync(
                seed.SubjectId,
                seed.GroupId,
                Today.AddDays(1),
                AllStatus(seed.StudentIds, "Keldi"));

            Assert.False(result.Succeeded);
            Assert.Equal(AttendanceSaveStatus.DateOutOfRange, result.Status);
            Assert.Empty(factory.Context.Attendances);
        }

        [Fact]
        public async Task Save_TooOldDate_IsRejected()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.SaveAsync(
                seed.SubjectId,
                seed.GroupId,
                Today.AddDays(-31),
                AllStatus(seed.StudentIds, "Keldi"));

            Assert.False(result.Succeeded);
            Assert.Equal(AttendanceSaveStatus.DateOutOfRange, result.Status);
            Assert.Empty(factory.Context.Attendances);
        }

        [Fact]
        public async Task Save_OldestAllowedDate_IsAccepted()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.SaveAsync(
                seed.SubjectId,
                seed.GroupId,
                Today.AddDays(-30),
                AllStatus(seed.StudentIds, "Keldi"));

            Assert.True(result.Succeeded);
            Assert.Equal(seed.StudentIds.Count, result.CreatedCount);
        }

        [Fact]
        public async Task Save_Twice_UpdatesInsteadOfDuplicating()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);

            var first = await service.SaveAsync(
                seed.SubjectId, seed.GroupId, Today, AllStatus(seed.StudentIds, "Keldi"));

            var second = await service.SaveAsync(
                seed.SubjectId, seed.GroupId, Today, AllStatus(seed.StudentIds, "Kelmadi"));

            Assert.True(first.Succeeded);
            Assert.Equal(seed.StudentIds.Count, first.CreatedCount);
            Assert.Equal(0, first.UpdatedCount);

            Assert.True(second.Succeeded);
            Assert.Equal(0, second.CreatedCount);
            Assert.Equal(seed.StudentIds.Count, second.UpdatedCount);

            Assert.Equal(seed.StudentIds.Count, await factory.Context.Attendances.CountAsync());
            Assert.All(
                await factory.Context.Attendances.ToListAsync(),
                a => Assert.Equal(AttendanceStatus.Kelmadi, a.Status));
        }

        [Fact]
        public async Task Save_EmptyStatuses_DefaultsToKelmadi()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.SaveAsync(seed.SubjectId, seed.GroupId, Today, null);

            Assert.True(result.Succeeded);
            Assert.Equal(seed.StudentIds.Count, result.CreatedCount);
            Assert.All(
                await factory.Context.Attendances.ToListAsync(),
                a => Assert.Equal(AttendanceStatus.Kelmadi, a.Status));
        }

        [Fact]
        public async Task Save_IgnoresStudentsFromOtherGroups()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);

            var statuses = AllStatus(seed.StudentIds, "Keldi");
            statuses[seed.OtherGroupStudentId] = "Kelmadi";

            var result = await service.SaveAsync(seed.SubjectId, seed.GroupId, Today, statuses);

            Assert.True(result.Succeeded);
            Assert.Equal(seed.StudentIds.Count, result.CreatedCount);
            Assert.Equal(seed.StudentIds.Count, await factory.Context.Attendances.CountAsync());
            Assert.False(await factory.Context.Attendances
                .AnyAsync(a => a.StudentId == seed.OtherGroupStudentId));
        }

        [Fact]
        public async Task Save_AllPresentPayload_SavesEveryoneAsKeldi()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.SaveAsync(
                seed.SubjectId, seed.GroupId, Today, AllStatus(seed.StudentIds, "Keldi"));

            Assert.True(result.Succeeded);
            Assert.All(
                await factory.Context.Attendances.ToListAsync(),
                a => Assert.Equal(AttendanceStatus.Keldi, a.Status));
        }

        [Fact]
        public async Task Save_ManyStudents_DoesNotQueryPerStudent()
        {
            var interceptor = new CountingCommandInterceptor();
            await using var factory = await TestDbFactory.CreateAsync(interceptor);
            var seed = await AttendanceSeedDataFactory.CreateAsync(factory.Context, studentCount: 100);
            var service = CreateService(factory.Context);

            factory.Context.ChangeTracker.Clear();
            interceptor.Reset();

            var result = await service.SaveAsync(
                seed.SubjectId, seed.GroupId, Today, AllStatus(seed.StudentIds, "Keldi"));

            Assert.True(result.Succeeded);
            Assert.Equal(100, result.CreatedCount);

            // Guruh-fan tekshiruvi + override yozuvlar + standart a'zolik
            // (guruh, a'zoliklar, legacy) + talabalar + mavjud yozuvlar.
            // Talaba soniga qarab o'smasligi kerak.
            Assert.True(
                interceptor.Count <= 9,
                $"Kutilganidan ko'p so'rov: {interceptor.Count} ta (N+1 bo'lishi mumkin).");
        }

        private sealed class CountingCommandInterceptor : DbCommandInterceptor
        {
            public int Count { get; private set; }

            public void Reset() => Count = 0;

            // Faqat ma'lumot o'qiydigan SELECT so'rovlar sanaladi. SQLite'da INSERT
            // "RETURNING" bilan bajarilishi va "SELECT changes()" kabi texnik
            // so'rovlar N+1 hisobiga kirmasligi kerak.
            private void Register(DbCommand command)
            {
                var sql = command.CommandText.TrimStart();
                if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (sql.StartsWith("SELECT changes()", StringComparison.OrdinalIgnoreCase)
                    || sql.StartsWith("SELECT last_insert_rowid()", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                Count++;
            }

            public override InterceptionResult<DbDataReader> ReaderExecuting(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result)
            {
                Register(command);
                return result;
            }

            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result,
                CancellationToken cancellationToken = default)
            {
                Register(command);
                return ValueTask.FromResult(result);
            }

            public override InterceptionResult<object> ScalarExecuting(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<object> result)
            {
                Register(command);
                return result;
            }

            public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<object> result,
                CancellationToken cancellationToken = default)
            {
                Register(command);
                return ValueTask.FromResult(result);
            }
        }
    }
}
