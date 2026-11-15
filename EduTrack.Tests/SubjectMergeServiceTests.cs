using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTrack.Tests
{
    /// <summary>
    /// Dublikat fanlarni birlashtirish: barcha bog'liq yozuvlar asosiy fanga
    /// ko'chadi, dublikat fanning o'qituvchisi guruh bo'yicha override sifatida
    /// saqlanadi, unique kesmalardagi konfliktlar xavfsiz hal qilinadi.
    /// </summary>
    public class SubjectMergeServiceTests
    {
        private static SubjectMergeService CreateService(ApplicationDbContext db) => new(db);

        private static ApplicationUser NewUser(string id, string name) => new()
        {
            Id = id,
            UserName = id,
            NormalizedUserName = id.ToUpperInvariant(),
            LoginId = id,
            FullName = name,
            Email = $"{id}@example.com",
            EmailConfirmed = true,
            MustChangePassword = false
        };

        private sealed record Seed(
            Subject Target,
            Subject Source,
            Group GroupShared,
            Group GroupSourceOnly,
            StudentGroupSubject OverrideOnTarget);

        private static async Task<Seed> SeedAsync(ApplicationDbContext db)
        {
            db.Users.AddRange(
                NewUser("teacher-a", "Asosiy o'qituvchi"),
                NewUser("teacher-b", "Dublikat o'qituvchi"),
                NewUser("student-1", "Talaba Bir"));
            await db.SaveChangesAsync();

            var target = new Subject { Name = "Matematika", TeacherId = "teacher-a" };
            var source = new Subject { Name = "Matematika", TeacherId = "teacher-b" };
            var groupShared = new Group { Name = "7" };
            var groupSourceOnly = new Group { Name = "1-guruh" };

            db.AddRange(target, source, groupShared, groupSourceOnly);
            await db.SaveChangesAsync();

            // Birlashma guruh: ikkala fanda ham 7-guruh bor.
            db.GroupSubjects.AddRange(
                new GroupSubject { SubjectId = target.Id, GroupId = groupShared.Id },
                new GroupSubject { SubjectId = source.Id, GroupId = groupShared.Id, TeacherId = "teacher-b" },
                // Faqat dublikat fanga tegishli guruh (2-smena).
                new GroupSubject { SubjectId = source.Id, GroupId = groupSourceOnly.Id });

            // O'quvchi override'i — target fanda aktiv, source fanda ham aktiv (konflikt).
            db.StudentGroupSubjects.AddRange(
                new StudentGroupSubject
                {
                    StudentId = "student-1",
                    SubjectId = target.Id,
                    GroupId = groupShared.Id,
                    StartDate = new DateTime(2026, 1, 1),
                    IsActive = true
                },
                new StudentGroupSubject
                {
                    StudentId = "student-1",
                    SubjectId = source.Id,
                    GroupId = groupSourceOnly.Id,
                    StartDate = new DateTime(2026, 1, 1),
                    IsActive = true
                });

            // Davomat: target fanda yozuv bor (konflikt yaratadi).
            db.Attendances.Add(new Attendance
            {
                Date = new DateTime(2026, 11, 2),
                StudentId = "student-1",
                SubjectId = target.Id,
                GroupId = groupShared.Id,
                Status = AttendanceStatus.Keldi
            });
            db.Attendances.Add(new Attendance
            {
                Date = new DateTime(2026, 11, 2),
                StudentId = "student-1",
                SubjectId = source.Id,
                GroupId = groupShared.Id,
                Status = AttendanceStatus.Kelmadi
            });
            // Konflikt emas: faqat source fanda bo'lgan yozuv (boshqa guruh).
            db.Attendances.Add(new Attendance
            {
                Date = new DateTime(2026, 11, 3),
                StudentId = "student-1",
                SubjectId = source.Id,
                GroupId = groupSourceOnly.Id,
                Status = AttendanceStatus.KechQoldi
            });

            // Topshiriq, imtihon, savollar — oddiy FK ko'chirish.
            db.Assignments.Add(new Assignment
            {
                Title = "Uy ishi",
                SubjectId = source.Id,
                DueDate = new DateTime(2026, 12, 1),
                MaxPoints = 100,
                CreatedDate = new DateTime(2026, 11, 1)
            });

            db.Schedules.Add(new Schedule
            {
                SubjectId = source.Id,
                GroupId = groupSourceOnly.Id,
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeSpan(14, 0, 0),
                EndTime = new TimeSpan(15, 35, 0)
            });

            await db.SaveChangesAsync();

            return new Seed(target, source, groupShared, groupSourceOnly, null!);
        }

        [Fact]
        public async Task MergeAsync_MovesAllDataAndKeepsSourceTeacherAsOverride()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.MergeAsync(seed.Source.Id, seed.Target.Id);

            Assert.True(result.Success);

            // ExecuteUpdate/ExecuteDelete tracked entity'larni yangilamaydi —
            // keyingi so'rovlar bazadan yangi holatni o'qishi uchun tozalaymiz.
            factory.Context.ChangeTracker.Clear();

            // Fanlar bittasi qoldi — asosiy fan.
            var subjects = await factory.Context.Subjects.ToListAsync();
            var remaining = Assert.Single(subjects);
            Assert.Equal(seed.Target.Id, remaining.Id);

            // Birlashma guruhda dublikat fan o'qituvchisi override sifatida saqlandi
            // (ko'p o'qituvchi modeli).
            var sharedLink = await factory.Context.GroupSubjects
                .SingleAsync(gs => gs.GroupId == seed.GroupShared.Id);
            Assert.Equal(seed.Target.Id, sharedLink.SubjectId);
            Assert.Equal("teacher-b", sharedLink.TeacherId);

            // Dublikat fanning o'z guruhidagi biriktirma oddiy ko'chdi.
            var sourceOnlyLink = await factory.Context.GroupSubjects
                .SingleAsync(gs => gs.GroupId == seed.GroupSourceOnly.Id);
            Assert.Equal(seed.Target.Id, sourceOnlyLink.SubjectId);

            // Barcha FK yozuvlar asosiy fanga ko'chdi.
            Assert.Equal(1, await factory.Context.Schedules.CountAsync(s => s.SubjectId == seed.Target.Id));
            Assert.Equal(1, await factory.Context.Assignments.CountAsync(a => a.SubjectId == seed.Target.Id));
            // Ikkala fanda ham aktiv override bo'lgani uchun konflikt yozuv
            // o'chirildi — faqat asosiy fan yozuvi qoldi.
            Assert.Equal(1, await factory.Context.StudentGroupSubjects.CountAsync(x => x.SubjectId == seed.Target.Id));

            // Davomat: konflikt yozuv o'chirildi (asosiy fan yozuvi saqlanadi),
            // konflikt bo'lmagan yozuv ko'chdi.
            var attendances = await factory.Context.Attendances.ToListAsync();
            Assert.Equal(2, attendances.Count);
            Assert.All(attendances, a => Assert.Equal(seed.Target.Id, a.SubjectId));
            Assert.Contains(attendances, a => a.Status == AttendanceStatus.Keldi);
            Assert.Contains(attendances, a => a.Status == AttendanceStatus.KechQoldi);
        }

        [Fact]
        public async Task GetSummaryAsync_ReturnsCounts()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var summary = await service.GetSummaryAsync(seed.Source.Id, seed.Target.Id);

            Assert.NotNull(summary);
            Assert.Equal(1, summary!.Schedules);
            Assert.Equal(2, summary.GroupLinks);
            Assert.Equal(1, summary.DuplicateGroupLinks);
            Assert.Equal(1, summary.StudentOverrides);
            Assert.Equal(1, summary.DuplicateStudentOverrides);
            Assert.Equal(1, summary.Assignments);
            Assert.Equal(2, summary.Attendances);
            Assert.Equal(1, summary.AttendanceConflicts);
        }

        [Fact]
        public async Task MergeAsync_SameSubject_ReturnsError()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var result = await service.MergeAsync(seed.Target.Id, seed.Target.Id);

            Assert.False(result.Success);
        }
    }
}
