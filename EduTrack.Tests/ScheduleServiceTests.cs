using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTrack.Tests
{
    public class ScheduleServiceTests
    {
        private static readonly DateTime Today = new(2026, 11, 5, 9, 0, 0, DateTimeKind.Unspecified);

        private sealed record Seed(
            Group ClassGroup,
            Group Stream1,
            Group Stream2,
            Subject Math,
            Subject English,
            ApplicationUser TeacherA,
            ApplicationUser TeacherB,
            ApplicationUser Student);

        private static ScheduleService CreateService(ApplicationDbContext db)
            => new(db, new StudentContextService(db, new TestAppClock(Today)));

        private static ApplicationUser NewUser(string loginId, string fullName) => new()
        {
            UserName = loginId,
            NormalizedUserName = loginId.ToUpperInvariant(),
            LoginId = loginId,
            FullName = fullName,
            Email = $"{loginId}@example.com",
            EmailConfirmed = true,
            MustChangePassword = false
        };

        private static async Task<Seed> SeedAsync(ApplicationDbContext db)
        {
            var classGroup = new Group { Name = "10A", Type = GroupType.Class, Code = "10A" };
            var stream1 = new Group { Name = "1-guruh", Type = GroupType.Stream, Code = "G1" };
            var stream2 = new Group { Name = "2-guruh", Type = GroupType.Stream, Code = "G2" };
            db.Groups.AddRange(classGroup, stream1, stream2);

            var teacherA = NewUser("20001", "Gʻaniyev Samandar");
            var teacherB = NewUser("20002", "Samandar Turdaliyev");
            var student = NewUser("10001", "Aliyev Vali");
            db.Users.AddRange(teacherA, teacherB, student);
            await db.SaveChangesAsync();

            var math = new Subject { Name = "Matematika", TeacherId = teacherA.Id };
            var english = new Subject { Name = "Ingliz tili", TeacherId = teacherB.Id };
            db.Subjects.AddRange(math, english);
            await db.SaveChangesAsync();

            // 1-guruhda matematikani boshqa o'qituvchi olib boradi.
            db.GroupSubjects.Add(new GroupSubject
            {
                GroupId = stream1.Id,
                SubjectId = math.Id,
                TeacherId = teacherB.Id
            });

            db.GroupMemberships.AddRange(
                new GroupMembership
                {
                    StudentId = student.Id,
                    GroupId = classGroup.Id,
                    Scope = GroupType.Class,
                    StartDate = Today.Date,
                    IsActive = true
                },
                new GroupMembership
                {
                    StudentId = student.Id,
                    GroupId = stream1.Id,
                    Scope = GroupType.Stream,
                    StartDate = Today.Date,
                    IsActive = true
                });

            await db.SaveChangesAsync();

            return new Seed(classGroup, stream1, stream2, math, english, teacherA, teacherB, student);
        }

        private static Schedule NewSchedule(
            Group group,
            Subject subject,
            DayOfWeek day,
            string start,
            string end,
            ScheduleShift shift,
            string? teacherId = null) => new()
            {
                GroupId = group.Id,
                SubjectId = subject.Id,
                DayOfWeek = day,
                StartTime = TimeSpan.Parse(start),
                EndTime = TimeSpan.Parse(end),
                Shift = shift,
                TeacherId = teacherId
            };

        [Fact]
        public async Task GroupConflict_SameShift_IsDetected()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            factory.Context.Schedules.Add(
                NewSchedule(seed.Stream1, seed.Math, DayOfWeek.Monday, "13:00", "13:45", ScheduleShift.Afternoon));
            await factory.Context.SaveChangesAsync();

            var candidate = NewSchedule(seed.Stream1, seed.English, DayOfWeek.Monday, "13:30", "14:15", ScheduleShift.Afternoon);
            var result = await service.CheckConflictAsync(candidate);

            Assert.True(result.HasConflict);
            Assert.Contains("allaqachon", result.Message);
        }

        [Fact]
        public async Task GroupConflict_DifferentShift_IsIgnored()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            factory.Context.Schedules.Add(
                NewSchedule(seed.Stream1, seed.Math, DayOfWeek.Monday, "08:00", "08:45", ScheduleShift.Morning));
            await factory.Context.SaveChangesAsync();

            var candidate = NewSchedule(seed.Stream1, seed.English, DayOfWeek.Monday, "08:00", "08:45", ScheduleShift.Afternoon);
            var result = await service.CheckConflictAsync(candidate);

            Assert.False(result.HasConflict);
        }

        [Fact]
        public async Task TeacherConflict_UsesGroupSubjectOverride()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            // 1-guruhda matematika — TeacherB (GroupSubject override).
            factory.Context.Schedules.Add(
                NewSchedule(seed.Stream1, seed.Math, DayOfWeek.Tuesday, "13:00", "13:45", ScheduleShift.Afternoon));
            await factory.Context.SaveChangesAsync();

            // 2-guruhda matematika — Subject.TeacherId = TeacherA: to'qnashuv yo'q.
            var withoutOverride = NewSchedule(seed.Stream2, seed.Math, DayOfWeek.Tuesday, "13:00", "13:45", ScheduleShift.Afternoon);
            Assert.False((await service.CheckConflictAsync(withoutOverride)).HasConflict);

            // Xuddi shu vaqtda TeacherB — to'qnashuv bor.
            var withOverride = NewSchedule(seed.Stream2, seed.Math, DayOfWeek.Tuesday, "13:00", "13:45", ScheduleShift.Afternoon, seed.TeacherB.Id);
            var result = await service.CheckConflictAsync(withOverride);

            Assert.True(result.HasConflict);
            Assert.Contains("band", result.Message);
        }

        [Fact]
        public async Task StudentConflict_AcrossClassAndStream_IsDetected()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            factory.Context.Schedules.Add(
                NewSchedule(seed.ClassGroup, seed.Math, DayOfWeek.Wednesday, "08:00", "08:45", ScheduleShift.Morning));
            await factory.Context.SaveChangesAsync();

            var candidate = NewSchedule(seed.Stream1, seed.English, DayOfWeek.Wednesday, "08:00", "08:45", ScheduleShift.Morning);
            var result = await service.CheckConflictAsync(candidate);

            Assert.True(result.HasConflict);
            Assert.Contains("10A", result.Message);
        }

        [Fact]
        public async Task StudentConflict_DifferentShift_IsIgnored()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            factory.Context.Schedules.Add(
                NewSchedule(seed.ClassGroup, seed.Math, DayOfWeek.Wednesday, "08:00", "08:45", ScheduleShift.Morning));
            await factory.Context.SaveChangesAsync();

            var candidate = NewSchedule(seed.Stream1, seed.English, DayOfWeek.Wednesday, "08:00", "08:45", ScheduleShift.Afternoon);
            var result = await service.CheckConflictAsync(candidate);

            Assert.False(result.HasConflict);
        }

        [Fact]
        public void ResolveTeacherId_PrefersScheduleThenGroupSubjectThenSubject()
        {
            var math = new Subject { Id = 1, Name = "Matematika", TeacherId = "subject-teacher" };
            var overrides = new List<GroupSubject>
            {
                new() { GroupId = 5, SubjectId = 1, TeacherId = "group-teacher" }
            };

            var schedule = new Schedule { GroupId = 5, SubjectId = 1, Subject = math, TeacherId = "schedule-teacher" };
            Assert.Equal("schedule-teacher", ScheduleService.ResolveTeacherId(schedule, overrides));

            schedule.TeacherId = null;
            Assert.Equal("group-teacher", ScheduleService.ResolveTeacherId(schedule, overrides));

            schedule.GroupId = 6;
            Assert.Equal("subject-teacher", ScheduleService.ResolveTeacherId(schedule, overrides));
        }

        [Fact]
        public async Task Workload_UsesEffectiveTeacher()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            factory.Context.Schedules.AddRange(
                NewSchedule(seed.Stream1, seed.Math, DayOfWeek.Monday, "13:00", "13:45", ScheduleShift.Afternoon),
                NewSchedule(seed.Stream2, seed.English, DayOfWeek.Monday, "13:00", "13:45", ScheduleShift.Afternoon));
            await factory.Context.SaveChangesAsync();

            var workloadA = await service.GetTeacherWorkloadAsync(seed.TeacherA.Id);
            var workloadB = await service.GetTeacherWorkloadAsync(seed.TeacherB.Id);

            // TeacherA: Subject.TeacherId, lekin 1-guruhda override bor — 0 soat.
            Assert.Equal(0d, workloadA.TotalHoursPerWeek);

            // TeacherB: override (0.75) + ingliz tili (0.75).
            Assert.Equal(1.5, workloadB.TotalHoursPerWeek);
        }
    }
}
