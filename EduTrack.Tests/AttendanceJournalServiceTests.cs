using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTrack.Tests
{
    public class AttendanceJournalServiceTests
    {
        private static readonly DateTime Today = new(2026, 11, 2, 10, 0, 0, DateTimeKind.Unspecified);
        private const string TeacherId = "teacher-journal";

        private static AttendanceJournalService CreateService(
            ApplicationDbContext db,
            DateTime? today = null,
            int backfillDays = 30)
        {
            var options = Options.Create(new AttendanceOptions { BackfillDays = backfillDays });
            var clock = new TestAppClock(today ?? Today);
            var studentContext = new StudentContextService(db, clock);
            var subjectGroups = new StudentSubjectGroupService(db, studentContext, clock);
            return new AttendanceJournalService(db, clock, options, subjectGroups);
        }

        [Fact]
        public async Task Build_UsesScheduleDays_WhenScheduleExists()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await CreateSeedAsync(factory.Context);

            factory.Context.Schedules.AddRange(
                NewSchedule(seed.SubjectId, seed.GroupId, DayOfWeek.Monday),
                NewSchedule(seed.SubjectId, seed.GroupId, DayOfWeek.Wednesday));
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);
            var model = await service.BuildAsync(TeacherId, seed.SubjectId, seed.GroupId, new DateTime(2026, 11, 1));

            var expectedDays = Enumerable.Range(1, DateTime.DaysInMonth(2026, 11))
                .Select(day => new DateTime(2026, 11, day))
                .Count(date => date.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Wednesday);

            Assert.True(model.HasSchedule);
            Assert.Equal(expectedDays, model.Days.Count);
            Assert.All(
                model.Days,
                day => Assert.Contains(day.Date.DayOfWeek, new[] { DayOfWeek.Monday, DayOfWeek.Wednesday }));
            Assert.All(model.Days, day => Assert.Equal(11, day.Date.Month));
        }

        [Fact]
        public async Task Build_WithoutSchedule_ShowsAllDaysOfMonth()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await CreateSeedAsync(factory.Context);

            var service = CreateService(factory.Context);
            var model = await service.BuildAsync(TeacherId, seed.SubjectId, seed.GroupId, new DateTime(2026, 11, 1));

            Assert.False(model.HasSchedule);
            Assert.Equal(DateTime.DaysInMonth(2026, 11), model.Days.Count);
            Assert.Equal(new DateTime(2026, 11, 1), model.Days.First().Date);
            Assert.Equal(new DateTime(2026, 11, 30), model.Days.Last().Date);
        }

        [Fact]
        public async Task Build_ComputesTotalsAndPercentage_ForSelectedMonthOnly()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await CreateSeedAsync(factory.Context);
            var studentId = seed.StudentIds[0];

            factory.Context.Attendances.AddRange(
                NewAttendance(seed.SubjectId, studentId, new DateTime(2026, 11, 2), AttendanceStatus.Keldi),
                NewAttendance(seed.SubjectId, studentId, new DateTime(2026, 11, 3), AttendanceStatus.Keldi),
                NewAttendance(seed.SubjectId, studentId, new DateTime(2026, 11, 4), AttendanceStatus.Kelmadi),
                NewAttendance(seed.SubjectId, studentId, new DateTime(2026, 11, 5), AttendanceStatus.KechQoldi),
                NewAttendance(seed.SubjectId, studentId, new DateTime(2026, 11, 6), AttendanceStatus.Sababli),
                NewAttendance(seed.SubjectId, studentId, new DateTime(2026, 10, 31), AttendanceStatus.Keldi),
                NewAttendance(seed.OtherSubjectId, studentId, new DateTime(2026, 11, 7), AttendanceStatus.Kelmadi));
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);
            var model = await service.BuildAsync(TeacherId, seed.SubjectId, seed.GroupId, new DateTime(2026, 11, 1));

            var row = Assert.Single(model.Rows, r => r.StudentId == studentId);

            Assert.Equal(2, row.Keldi);
            Assert.Equal(1, row.Kelmadi);
            Assert.Equal(1, row.KechQoldi);
            Assert.Equal(1, row.Sababli);
            Assert.Equal(5, row.Total);
            Assert.Equal(40, row.Percentage);
            Assert.Equal(5, row.Statuses.Count);
            Assert.Equal(AttendanceStatus.Keldi, row.Statuses["2026-11-02"]);
            Assert.DoesNotContain("2026-10-31", row.Statuses.Keys);
        }

        [Fact]
        public async Task Build_RoundsPercentageHalfUp_LikeTheJournalUi()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await CreateSeedAsync(factory.Context);
            var studentId = seed.StudentIds[0];

            factory.Context.Attendances.Add(
                NewAttendance(seed.SubjectId, studentId, new DateTime(2026, 11, 2), AttendanceStatus.Keldi));
            for (var day = 3; day <= 9; day++)
            {
                factory.Context.Attendances.Add(
                    NewAttendance(seed.SubjectId, studentId, new DateTime(2026, 11, day), AttendanceStatus.Kelmadi));
            }
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);
            var model = await service.BuildAsync(TeacherId, seed.SubjectId, seed.GroupId, new DateTime(2026, 11, 1));

            var row = Assert.Single(model.Rows, r => r.StudentId == studentId);

            Assert.Equal(8, row.Total);
            Assert.Equal(13, row.Percentage);
        }

        [Fact]
        public async Task Build_GroupIsolation_ExcludesOtherGroupStudentsAndRecords()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await CreateSeedAsync(factory.Context);

            factory.Context.Attendances.Add(
                NewAttendance(seed.SubjectId, seed.OtherGroupStudentId, new DateTime(2026, 11, 2), AttendanceStatus.Keldi));
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);
            var model = await service.BuildAsync(TeacherId, seed.SubjectId, seed.GroupId, new DateTime(2026, 11, 1));

            Assert.Equal(seed.GroupId, model.GroupId);
            Assert.Equal(seed.StudentIds.Count, model.Rows.Count);
            Assert.DoesNotContain(model.Rows, row => row.StudentId == seed.OtherGroupStudentId);
            Assert.All(model.Rows, row => Assert.Equal(0, row.Keldi));
        }

        [Fact]
        public async Task Build_LocksFutureDaysAndDaysOlderThanBackfill()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await CreateSeedAsync(factory.Context);
            var today = new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Unspecified);

            var service = CreateService(factory.Context, today, backfillDays: 5);
            var model = await service.BuildAsync(TeacherId, seed.SubjectId, seed.GroupId, new DateTime(2026, 11, 1));

            var byDay = model.Days.ToDictionary(day => day.Date.Day);

            Assert.False(byDay[9].IsEditable);
            Assert.True(byDay[10].IsEditable);
            Assert.True(byDay[15].IsEditable);
            Assert.True(byDay[15].IsToday);
            Assert.False(byDay[16].IsEditable);
            Assert.False(byDay[30].IsEditable);
        }

        [Fact]
        public async Task Build_NoSubjects_ReturnsEmptyModel()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            await CreateSeedAsync(factory.Context, teacherId: "another-teacher");

            var service = CreateService(factory.Context);
            var model = await service.BuildAsync(TeacherId, null, null, new DateTime(2026, 11, 1));

            Assert.Empty(model.Subjects);
            Assert.Empty(model.Days);
            Assert.Empty(model.Rows);
            Assert.Null(model.SubjectId);
            Assert.Null(model.GroupId);
        }

        [Fact]
        public async Task Build_MonthNavigation_IsLimitedToCurrentMonth()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await CreateSeedAsync(factory.Context);

            var service = CreateService(factory.Context);

            var current = await service.BuildAsync(TeacherId, seed.SubjectId, seed.GroupId, new DateTime(2026, 11, 20));
            var previous = await service.BuildAsync(TeacherId, seed.SubjectId, seed.GroupId, new DateTime(2026, 10, 10));

            Assert.Equal(new DateTime(2026, 11, 1), current.Month);
            Assert.False(current.CanGoNext);
            Assert.Equal(new DateTime(2026, 12, 1), current.NextMonth);

            Assert.Equal(new DateTime(2026, 10, 1), previous.Month);
            Assert.True(previous.CanGoNext);
            Assert.Equal(new DateTime(2026, 11, 1), previous.NextMonth);
            Assert.Equal(new DateTime(2026, 9, 1), previous.PreviousMonth);
        }

        private static Schedule NewSchedule(int subjectId, int groupId, DayOfWeek day) => new()
        {
            SubjectId = subjectId,
            GroupId = groupId,
            DayOfWeek = day,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 30, 0)
        };

        private static Attendance NewAttendance(
            int subjectId,
            string studentId,
            DateTime date,
            AttendanceStatus status) => new()
        {
            SubjectId = subjectId,
            StudentId = studentId,
            Date = date,
            Status = status
        };

        private sealed record JournalSeed(
            int SubjectId,
            int OtherSubjectId,
            int GroupId,
            int OtherGroupId,
            IReadOnlyList<string> StudentIds,
            string OtherGroupStudentId);

        private static async Task<JournalSeed> CreateSeedAsync(
            ApplicationDbContext db,
            string teacherId = TeacherId,
            int studentCount = 3)
        {
            var teacher = new ApplicationUser
            {
                Id = teacherId,
                UserName = teacherId,
                NormalizedUserName = teacherId.ToUpperInvariant(),
                FullName = "Jurnal o'qituvchisi",
                LoginId = "90001",
                Email = $"{teacherId}@example.com",
                EmailConfirmed = true,
                MustChangePassword = false
            };
            db.Users.Add(teacher);
            await db.SaveChangesAsync();

            var group = new Group { Name = "Jurnal A" };
            var otherGroup = new Group { Name = "Jurnal B" };
            db.Groups.AddRange(group, otherGroup);
            await db.SaveChangesAsync();

            var subject = new Subject { Name = "Matematika", TeacherId = teacherId };
            var otherSubject = new Subject { Name = "Fizika", TeacherId = teacherId };
            db.Subjects.AddRange(subject, otherSubject);
            await db.SaveChangesAsync();

            db.GroupSubjects.AddRange(
                new GroupSubject { GroupId = group.Id, SubjectId = subject.Id },
                new GroupSubject { GroupId = otherGroup.Id, SubjectId = subject.Id },
                new GroupSubject { GroupId = group.Id, SubjectId = otherSubject.Id });
            await db.SaveChangesAsync();

            var students = new List<ApplicationUser>(studentCount);
            for (var i = 0; i < studentCount; i++)
            {
                students.Add(NewStudent($"Jurnal talabasi {i:000}", $"5{i:0000}", group.Id));
            }

            var otherStudent = NewStudent("Boshqa guruh talabasi", "59999", otherGroup.Id);

            db.Users.AddRange(students);
            db.Users.Add(otherStudent);
            await db.SaveChangesAsync();

            return new JournalSeed(
                subject.Id,
                otherSubject.Id,
                group.Id,
                otherGroup.Id,
                students.Select(student => student.Id).ToList(),
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
