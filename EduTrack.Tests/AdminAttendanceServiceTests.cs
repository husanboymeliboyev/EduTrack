using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using EduTrack.ViewModels;
using Xunit;

namespace EduTrack.Tests
{
    public class AdminAttendanceServiceTests
    {
        private static readonly DateTime Today = new(2026, 11, 2, 10, 0, 0, DateTimeKind.Unspecified);

        private const string OwnerTeacherId = "teacher-owner";
        private const string OverrideTeacherId = "teacher-override";

        private static AdminAttendanceService CreateService(
            ApplicationDbContext db,
            DateTime? today = null)
        {
            var clock = new TestAppClock(today ?? Today);
            var studentContext = new StudentContextService(db, clock);
            var subjectGroups = new StudentSubjectGroupService(db, studentContext, clock);
            return new AdminAttendanceService(db, clock, subjectGroups);
        }

        [Fact]
        public async Task Overview_ReturnsPairsWithTotalsAndEffectiveTeacher()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await CreateSeedAsync(factory.Context);

            AddAttendance(factory.Context, seed.MathId, seed.GroupAId, seed.GroupAStudentIds[0], new DateTime(2026, 11, 2), AttendanceStatus.Keldi);
            AddAttendance(factory.Context, seed.MathId, seed.GroupAId, seed.GroupAStudentIds[0], new DateTime(2026, 11, 3), AttendanceStatus.Kelmadi);
            AddAttendance(factory.Context, seed.MathId, seed.GroupAId, seed.GroupAStudentIds[1], new DateTime(2026, 11, 2), AttendanceStatus.Keldi);
            AddAttendance(factory.Context, seed.MathId, seed.GroupBId, seed.GroupBStudentId, new DateTime(2026, 11, 2), AttendanceStatus.Sababli);
            AddAttendance(factory.Context, seed.PhysicsId, seed.GroupAId, seed.GroupAStudentIds[0], new DateTime(2026, 11, 2), AttendanceStatus.KechQoldi);
            // Boshqa oyga tegishli yozuv hisobga olinmasligi kerak.
            AddAttendance(factory.Context, seed.MathId, seed.GroupAId, seed.GroupAStudentIds[0], new DateTime(2026, 10, 30), AttendanceStatus.Keldi);
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);
            var model = await service.GetOverviewAsync(new AdminAttendanceFilterViewModel
            {
                Month = new DateTime(2026, 11, 1)
            });

            Assert.Equal("Noyabr 2026", model.MonthLabel);
            Assert.Equal(3, model.PairCount);
            Assert.Equal(3, model.StudentCount);
            Assert.Equal(2, model.Keldi);
            Assert.Equal(1, model.Kelmadi);
            Assert.Equal(1, model.KechQoldi);
            Assert.Equal(1, model.Sababli);
            Assert.Equal(40, model.Percentage);

            var mathA = Assert.Single(model.Pairs, p => p.SubjectId == seed.MathId && p.GroupId == seed.GroupAId);
            Assert.Equal("Matematika", mathA.SubjectName);
            Assert.Equal("A guruhi", mathA.GroupName);
            Assert.Equal("Asosiy O'qituvchi", mathA.TeacherName);
            Assert.Equal(2, mathA.StudentCount);
            Assert.Equal(2, mathA.Keldi);
            Assert.Equal(1, mathA.Kelmadi);
            Assert.Equal(3, mathA.Total);
            Assert.Equal(67, mathA.Percentage);
            Assert.Equal(new DateTime(2026, 11, 3), mathA.LastRecordDate);

            var mathB = Assert.Single(model.Pairs, p => p.SubjectId == seed.MathId && p.GroupId == seed.GroupBId);
            Assert.Equal("Override O'qituvchi", mathB.TeacherName);
            Assert.Equal(1, mathB.StudentCount);
            Assert.Equal(1, mathB.Sababli);
        }

        [Fact]
        public async Task Overview_FiltersBySubjectAndGroup()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await CreateSeedAsync(factory.Context);

            var service = CreateService(factory.Context);

            var bySubject = await service.GetOverviewAsync(new AdminAttendanceFilterViewModel
            {
                SubjectId = seed.MathId,
                Month = new DateTime(2026, 11, 1)
            });
            Assert.Equal(2, bySubject.PairCount);
            Assert.All(bySubject.Pairs, p => Assert.Equal(seed.MathId, p.SubjectId));

            var byGroup = await service.GetOverviewAsync(new AdminAttendanceFilterViewModel
            {
                GroupId = seed.GroupAId,
                Month = new DateTime(2026, 11, 1)
            });
            Assert.Equal(2, byGroup.PairCount);
            Assert.All(byGroup.Pairs, p => Assert.Equal(seed.GroupAId, p.GroupId));

            var byBoth = await service.GetOverviewAsync(new AdminAttendanceFilterViewModel
            {
                SubjectId = seed.MathId,
                GroupId = seed.GroupBId,
                Month = new DateTime(2026, 11, 1)
            });
            var pair = Assert.Single(byBoth.Pairs);
            Assert.Equal(seed.MathId, pair.SubjectId);
            Assert.Equal(seed.GroupBId, pair.GroupId);
        }

        [Fact]
        public async Task Overview_ClearsUnknownFilters()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            await CreateSeedAsync(factory.Context);

            var service = CreateService(factory.Context);
            var model = await service.GetOverviewAsync(new AdminAttendanceFilterViewModel
            {
                SubjectId = 99999,
                GroupId = 88888,
                Month = new DateTime(2026, 11, 1)
            });

            Assert.Null(model.SubjectId);
            Assert.Null(model.GroupId);
            Assert.Equal(3, model.PairCount);
        }

        [Fact]
        public async Task Overview_MovesStudentToOverrideGroup()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await CreateSeedAsync(factory.Context);
            var movedStudentId = seed.GroupAStudentIds[0];

            factory.Context.StudentGroupSubjects.Add(new StudentGroupSubject
            {
                StudentId = movedStudentId,
                SubjectId = seed.MathId,
                GroupId = seed.GroupBId,
                StartDate = new DateTime(2026, 10, 1),
                IsActive = true
            });
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);
            var model = await service.GetOverviewAsync(new AdminAttendanceFilterViewModel
            {
                Month = new DateTime(2026, 11, 1)
            });

            var mathA = Assert.Single(model.Pairs, p => p.SubjectId == seed.MathId && p.GroupId == seed.GroupAId);
            var mathB = Assert.Single(model.Pairs, p => p.SubjectId == seed.MathId && p.GroupId == seed.GroupBId);

            Assert.Equal(1, mathA.StudentCount);
            Assert.Equal(2, mathB.StudentCount);
            // O'quvchi ikki guruhda sanalmaydi.
            Assert.Equal(3, model.StudentCount);
        }

        [Fact]
        public async Task Overview_ExcludesArchivedGroups()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await CreateSeedAsync(factory.Context);

            var archivedGroup = new Group { Name = "Arxiv guruh", IsArchived = true };
            factory.Context.Groups.Add(archivedGroup);
            await factory.Context.SaveChangesAsync();

            factory.Context.GroupSubjects.Add(new GroupSubject
            {
                GroupId = archivedGroup.Id,
                SubjectId = seed.MathId
            });
            await factory.Context.SaveChangesAsync();

            AddAttendance(
                factory.Context,
                seed.MathId,
                archivedGroup.Id,
                seed.GroupAStudentIds[0],
                new DateTime(2026, 11, 2),
                AttendanceStatus.Kelmadi);
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);
            var model = await service.GetOverviewAsync(new AdminAttendanceFilterViewModel
            {
                Month = new DateTime(2026, 11, 1)
            });

            Assert.Equal(3, model.PairCount);
            Assert.DoesNotContain(model.Pairs, p => p.GroupId == archivedGroup.Id);
            Assert.Equal(0, model.Kelmadi);
        }

        [Fact]
        public async Task Detail_BuildsDaysFromScheduleAndRecordedDates()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await CreateSeedAsync(factory.Context);

            factory.Context.Schedules.Add(new Schedule
            {
                SubjectId = seed.MathId,
                GroupId = seed.GroupAId,
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 30, 0)
            });
            await factory.Context.SaveChangesAsync();

            AddAttendance(factory.Context, seed.MathId, seed.GroupAId, seed.GroupAStudentIds[0], new DateTime(2026, 11, 2), AttendanceStatus.Keldi);
            AddAttendance(factory.Context, seed.MathId, seed.GroupAId, seed.GroupAStudentIds[0], new DateTime(2026, 11, 3), AttendanceStatus.Kelmadi);
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);
            var model = await service.GetDetailAsync(seed.MathId, seed.GroupAId, new DateTime(2026, 11, 1));

            Assert.NotNull(model);
            var journal = model!.Journal;

            Assert.True(journal.HasSchedule);
            Assert.Equal("Matematika", journal.SubjectName);
            Assert.Equal("A guruhi", journal.GroupName);
            Assert.Equal("Asosiy O'qituvchi", model.TeacherName);

            // Noyabr 2026 dushanbalari: 2, 9, 16, 23, 30 + yozuv bor seshanba (3).
            var expectedDays = new[]
            {
                new DateTime(2026, 11, 2),
                new DateTime(2026, 11, 3),
                new DateTime(2026, 11, 9),
                new DateTime(2026, 11, 16),
                new DateTime(2026, 11, 23),
                new DateTime(2026, 11, 30)
            };
            Assert.Equal(expectedDays, journal.Days.Select(d => d.Date));
            Assert.All(journal.Days, d => Assert.False(d.IsEditable));

            Assert.Equal(5, model.ScheduledDayCount);
            Assert.Equal(2, model.RecordedDayCount);
            Assert.Equal(4, model.UnmarkedDayCount);
            Assert.Equal(2, journal.Rows.Count);
        }

        [Fact]
        public async Task Detail_WithoutSchedule_UsesOnlyRecordedDays()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await CreateSeedAsync(factory.Context);

            AddAttendance(factory.Context, seed.MathId, seed.GroupAId, seed.GroupAStudentIds[0], new DateTime(2026, 11, 2), AttendanceStatus.Keldi);
            AddAttendance(factory.Context, seed.MathId, seed.GroupAId, seed.GroupAStudentIds[1], new DateTime(2026, 11, 5), AttendanceStatus.Kelmadi);
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);
            var model = await service.GetDetailAsync(seed.MathId, seed.GroupAId, new DateTime(2026, 11, 1));

            Assert.NotNull(model);
            Assert.False(model!.Journal.HasSchedule);
            Assert.Equal(
                new[] { new DateTime(2026, 11, 2), new DateTime(2026, 11, 5) },
                model.Journal.Days.Select(d => d.Date));
            Assert.Equal(0, model.ScheduledDayCount);
            Assert.Equal(2, model.RecordedDayCount);
            Assert.Equal(0, model.UnmarkedDayCount);
        }

        [Fact]
        public async Task Detail_ComputesStudentStatusesAndPercentages()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await CreateSeedAsync(factory.Context);
            var studentId = seed.GroupAStudentIds[0];

            AddAttendance(factory.Context, seed.MathId, seed.GroupAId, studentId, new DateTime(2026, 11, 2), AttendanceStatus.Keldi);
            AddAttendance(factory.Context, seed.MathId, seed.GroupAId, studentId, new DateTime(2026, 11, 3), AttendanceStatus.Keldi);
            AddAttendance(factory.Context, seed.MathId, seed.GroupAId, studentId, new DateTime(2026, 11, 4), AttendanceStatus.Kelmadi);
            AddAttendance(factory.Context, seed.MathId, seed.GroupAId, studentId, new DateTime(2026, 11, 5), AttendanceStatus.KechQoldi);
            AddAttendance(factory.Context, seed.MathId, seed.GroupAId, studentId, new DateTime(2026, 11, 6), AttendanceStatus.Sababli);
            // Boshqa fan va boshqa guruh yozuvlari aralashmasligi kerak.
            AddAttendance(factory.Context, seed.PhysicsId, seed.GroupAId, studentId, new DateTime(2026, 11, 2), AttendanceStatus.Kelmadi);
            AddAttendance(factory.Context, seed.MathId, seed.GroupBId, studentId, new DateTime(2026, 11, 2), AttendanceStatus.Kelmadi);
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);
            var model = await service.GetDetailAsync(seed.MathId, seed.GroupAId, new DateTime(2026, 11, 1));

            Assert.NotNull(model);
            var row = Assert.Single(model!.Journal.Rows, r => r.StudentId == studentId);

            Assert.Equal(2, row.Keldi);
            Assert.Equal(1, row.Kelmadi);
            Assert.Equal(1, row.KechQoldi);
            Assert.Equal(1, row.Sababli);
            Assert.Equal(5, row.Total);
            Assert.Equal(40, row.Percentage);

            Assert.Equal(2, model.Keldi);
            Assert.Equal(1, model.Kelmadi);
            Assert.Equal(5, model.Total);
            Assert.Equal(40, model.Percentage);
        }

        [Fact]
        public async Task Detail_ReturnsNullForUnknownPair()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await CreateSeedAsync(factory.Context);

            var service = CreateService(factory.Context);
            var model = await service.GetDetailAsync(seed.MathId, seed.GroupBId, new DateTime(2026, 11, 1));

            // Math + B juftligi mavjud, lekin Physics + B yo'q.
            var missing = await service.GetDetailAsync(seed.PhysicsId, seed.GroupBId, new DateTime(2026, 11, 1));

            Assert.NotNull(model);
            Assert.Null(missing);
        }

        [Fact]
        public async Task Overview_MonthNavigation_IsLimitedToCurrentMonth()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            await CreateSeedAsync(factory.Context);

            var service = CreateService(factory.Context);

            var current = await service.GetOverviewAsync(new AdminAttendanceFilterViewModel
            {
                Month = new DateTime(2026, 11, 20)
            });
            var previous = await service.GetOverviewAsync(new AdminAttendanceFilterViewModel
            {
                Month = new DateTime(2026, 10, 10)
            });

            Assert.Equal(new DateTime(2026, 11, 1), current.Month);
            Assert.False(current.CanGoNext);

            Assert.Equal(new DateTime(2026, 10, 1), previous.Month);
            Assert.True(previous.CanGoNext);
            Assert.Equal(new DateTime(2026, 9, 1), previous.PreviousMonth);
            Assert.Equal(new DateTime(2026, 11, 1), previous.NextMonth);
        }

        [Fact]
        public async Task Overview_EmptyDatabase_ReturnsEmptyModel()
        {
            await using var factory = await TestDbFactory.CreateAsync();

            var service = CreateService(factory.Context);
            var model = await service.GetOverviewAsync(null);

            Assert.Equal(0, model.PairCount);
            Assert.Equal(0, model.StudentCount);
            Assert.Empty(model.Pairs);
            Assert.Empty(model.Subjects);
            Assert.Empty(model.Groups);
        }

        private static void AddAttendance(
            ApplicationDbContext db,
            int subjectId,
            int groupId,
            string studentId,
            DateTime date,
            AttendanceStatus status)
        {
            db.Attendances.Add(new Attendance
            {
                SubjectId = subjectId,
                GroupId = groupId,
                StudentId = studentId,
                Date = date,
                Status = status
            });
        }

        private sealed record Seed(
            int MathId,
            int PhysicsId,
            int GroupAId,
            int GroupBId,
            IReadOnlyList<string> GroupAStudentIds,
            string GroupBStudentId);

        private static async Task<Seed> CreateSeedAsync(ApplicationDbContext db)
        {
            db.Users.AddRange(
                NewTeacher(OwnerTeacherId, "Asosiy O'qituvchi", "90001"),
                NewTeacher(OverrideTeacherId, "Override O'qituvchi", "90002"));
            await db.SaveChangesAsync();

            var groupA = new Group { Name = "A guruhi", Type = GroupType.Stream };
            var groupB = new Group { Name = "B guruhi", Type = GroupType.Stream };
            db.Groups.AddRange(groupA, groupB);
            await db.SaveChangesAsync();

            var math = new Subject { Name = "Matematika", TeacherId = OwnerTeacherId };
            var physics = new Subject { Name = "Fizika", TeacherId = OwnerTeacherId };
            db.Subjects.AddRange(math, physics);
            await db.SaveChangesAsync();

            db.GroupSubjects.AddRange(
                new GroupSubject { GroupId = groupA.Id, SubjectId = math.Id },
                new GroupSubject { GroupId = groupB.Id, SubjectId = math.Id, TeacherId = OverrideTeacherId },
                new GroupSubject { GroupId = groupA.Id, SubjectId = physics.Id });
            await db.SaveChangesAsync();

            var studentA1 = NewStudent("Ali Valiyev", "10001", groupA.Id);
            var studentA2 = NewStudent("Dilnoza Karimova", "10002", groupA.Id);
            var studentB1 = NewStudent("Bekzod Rasulov", "10003", groupB.Id);
            db.Users.AddRange(studentA1, studentA2, studentB1);
            await db.SaveChangesAsync();

            return new Seed(
                math.Id,
                physics.Id,
                groupA.Id,
                groupB.Id,
                new[] { studentA1.Id, studentA2.Id },
                studentB1.Id);
        }

        private static ApplicationUser NewTeacher(string id, string fullName, string loginId) => new()
        {
            Id = id,
            UserName = id,
            NormalizedUserName = id.ToUpperInvariant(),
            FullName = fullName,
            LoginId = loginId,
            Email = $"{loginId}@example.com",
            EmailConfirmed = true,
            MustChangePassword = false
        };

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
