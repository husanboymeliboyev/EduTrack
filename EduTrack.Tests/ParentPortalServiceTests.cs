using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTrack.Tests
{
    /// <summary>
    /// Ota-ona paneli servisi: faqat bog'langan farzand ma'lumotlari,
    /// davomat/baho/topshiriq/imtihon ko'rsatkichlari va ogohlantirishlar.
    /// </summary>
    public class ParentPortalServiceTests
    {
        private static readonly DateTime Today = new(2026, 11, 6, 9, 0, 0, DateTimeKind.Unspecified);

        private sealed record Seed(
            ApplicationUser Parent,
            ApplicationUser LinkedStudent,
            ApplicationUser OtherStudent,
            Group Group,
            Subject Math,
            Subject English);

        private static ParentPortalService CreateService(ApplicationDbContext db)
        {
            var clock = new TestAppClock(Today);
            var studentContext = new StudentContextService(db, clock);
            var subjectGroups = new StudentSubjectGroupService(db, studentContext, clock);
            var monthlyRating = new MonthlyRatingService(db, clock);
            var subjectGrades = new SubjectGradeService(db, clock);
            return new ParentPortalService(db, subjectGroups, clock, monthlyRating, subjectGrades);
        }

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
            var parent = NewUser("50001", "Ota-ona Aliyev");
            var linked = NewUser("10001", "Aliyev Ali");
            var other = NewUser("10002", "Valiyev Vali");
            db.Users.AddRange(parent, linked, other);

            var group = new Group { Name = "10-A", Type = GroupType.Stream, Code = "10A" };
            db.Groups.Add(group);

            var math = new Subject { Name = "Matematika" };
            var english = new Subject { Name = "Ingliz tili" };
            db.Subjects.AddRange(math, english);
            await db.SaveChangesAsync();

            db.GroupSubjects.AddRange(
                new GroupSubject { GroupId = group.Id, SubjectId = math.Id },
                new GroupSubject { GroupId = group.Id, SubjectId = english.Id });

            db.GroupMemberships.AddRange(
                new GroupMembership
                {
                    StudentId = linked.Id,
                    GroupId = group.Id,
                    Scope = GroupType.Stream,
                    StartDate = Today.AddDays(-60),
                    IsActive = true
                },
                new GroupMembership
                {
                    StudentId = other.Id,
                    GroupId = group.Id,
                    Scope = GroupType.Stream,
                    StartDate = Today.AddDays(-60),
                    IsActive = true
                });

            // Ota-ona faqat "linked" o'quvchiga bog'langan.
            db.ParentStudentLinks.Add(new ParentStudentLink
            {
                ParentId = parent.Id,
                StudentId = linked.Id,
                CreatedDate = Today
            });

            await db.SaveChangesAsync();

            // Davomat: 3 keldi + 1 kelmadi = 75%.
            db.Attendances.AddRange(
                new Attendance { StudentId = linked.Id, SubjectId = math.Id, Date = Today.AddDays(-1), Status = AttendanceStatus.Keldi },
                new Attendance { StudentId = linked.Id, SubjectId = english.Id, Date = Today.AddDays(-2), Status = AttendanceStatus.Keldi },
                new Attendance { StudentId = linked.Id, SubjectId = math.Id, Date = Today.AddDays(-3), Status = AttendanceStatus.Keldi },
                new Attendance { StudentId = linked.Id, SubjectId = english.Id, Date = Today.AddDays(-4), Status = AttendanceStatus.Kelmadi });

            // Yakuniy 100 ballik baholar: ikkala fandan 80 (qo'lda qo'yilgan).
            db.SubjectGrades.AddRange(
                new SubjectGrade { StudentId = linked.Id, SubjectId = math.Id, Score = 80, UpdatedDate = Today.AddDays(-1) },
                new SubjectGrade { StudentId = linked.Id, SubjectId = english.Id, Score = 80, UpdatedDate = Today.AddDays(-2) });
            await db.SaveChangesAsync();

            // Topshiriqlar: bittasi topshirilgan, bittasi muddati o'tgan.
            var submittedAssignment = new Assignment
            {
                Title = "1-amaliy ish",
                SubjectId = math.Id,
                GroupId = group.Id,
                CreatedDate = Today.AddDays(-5),
                DueDate = Today.AddDays(5)
            };
            var overdueAssignment = new Assignment
            {
                Title = "Insho",
                SubjectId = english.Id,
                GroupId = group.Id,
                CreatedDate = Today.AddDays(-10),
                DueDate = Today.AddDays(-2)
            };
            db.Assignments.AddRange(submittedAssignment, overdueAssignment);
            await db.SaveChangesAsync();

            db.Submissions.Add(new Submission
            {
                AssignmentId = submittedAssignment.Id,
                StudentId = linked.Id,
                SubmittedDate = Today.AddDays(-1),
                Grade = 9,
                TeacherComment = "Yaxshi bajarilgan"
            });

            // Imtihon natijasi: tasdiqlanmagan, 8/10 = 80%.
            var bank = new QuestionBank { Name = "Matematika banki", SubjectId = math.Id, GroupId = group.Id };
            db.QuestionBanks.Add(bank);
            await db.SaveChangesAsync();

            var exam = new Exam
            {
                Title = "1-oraliq",
                SubjectId = math.Id,
                QuestionBankId = bank.Id,
                GroupId = group.Id,
                QuestionCount = 10,
                DurationMinutes = 30,
                CreatedDate = Today.AddDays(-7)
            };
            db.Exams.Add(exam);
            await db.SaveChangesAsync();

            db.ExamResults.Add(new ExamResult
            {
                ExamId = exam.Id,
                StudentId = linked.Id,
                TotalQuestions = 10,
                CorrectAnswers = 8,
                CompletedDate = Today.AddDays(-1),
                IsApproved = false
            });

            // Jadval: dushanba, 09:00–09:45.
            db.Schedules.Add(new Schedule
            {
                SubjectId = math.Id,
                GroupId = group.Id,
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(9, 45, 0),
                Shift = ScheduleShift.Morning,
                Kind = ScheduleKind.Regular,
                Period = 1
            });

            await db.SaveChangesAsync();

            return new Seed(parent, linked, other, group, math, english);
        }

        [Fact]
        public async Task GetChildren_ReturnsOnlyLinkedStudent_WithGroupName()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var children = await service.GetChildrenAsync(seed.Parent.Id);

            var child = Assert.Single(children);
            Assert.Equal(seed.LinkedStudent.Id, child.StudentId);
            Assert.Equal("Aliyev Ali", child.FullName);
            Assert.Equal("10-A", child.GroupName);
        }

        [Fact]
        public async Task GetDashboard_ComputesAttendanceGradesAssignmentsAndAlerts()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var dashboard = await service.GetDashboardAsync(seed.Parent.Id);

            var summary = Assert.Single(dashboard.Children);
            Assert.Equal(75, summary.AttendancePercentage);
            Assert.Equal(80, summary.GradePercentage);
            Assert.Equal(50, summary.AssignmentCompletionPercentage);
            Assert.Equal(1, summary.OverdueAssignments);
            Assert.Equal(1, summary.PendingExamResults);
            Assert.Contains(summary.Alerts, a => a.Title == "Muddati o'tgan topshiriqlar");
        }

        [Fact]
        public async Task GetDashboard_IncludesMonthlyRatingAndAttendanceTrend()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);

            var monthlyBank = new QuestionBank
            {
                Name = "Oylik imtihon banki",
                SubjectId = seed.Math.Id,
                GroupId = seed.Group.Id
            };
            factory.Context.QuestionBanks.Add(monthlyBank);
            await factory.Context.SaveChangesAsync();

            var monthlyExam = new Exam
            {
                Title = "Noyabr oylik",
                SubjectId = seed.Math.Id,
                QuestionBankId = monthlyBank.Id,
                GroupId = seed.Group.Id,
                QuestionCount = 100,
                DurationMinutes = 60,
                Kind = ExamKind.Monthly,
                PeriodYear = Today.Year,
                PeriodMonth = Today.Month,
                CreatedDate = Today.AddDays(-5)
            };
            factory.Context.Exams.Add(monthlyExam);
            await factory.Context.SaveChangesAsync();

            factory.Context.ExamResults.Add(new ExamResult
            {
                ExamId = monthlyExam.Id,
                StudentId = seed.LinkedStudent.Id,
                TotalQuestions = 100,
                CorrectAnswers = 90,
                CompletedDate = Today.AddDays(-1),
                IsApproved = true
            });
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);
            var dashboard = await service.GetDashboardAsync(seed.Parent.Id);

            var summary = Assert.Single(dashboard.Children);
            Assert.Equal(90, summary.MonthlyRatingScore);
            Assert.Equal(1, summary.MonthlyRatingRank);
            Assert.Equal(2, summary.MonthlyRatingParticipants);
            Assert.Equal("Yo'nalish", summary.MonthlyRatingScopeLabel);
            Assert.NotNull(summary.MonthlyRatingPeriodLabel);
            Assert.True(summary.HasMonthlyRating);

            Assert.NotEmpty(summary.AttendanceTrend);
            Assert.All(summary.AttendanceTrend, p => Assert.True(p.TotalCount > 0));
            Assert.NotNull(summary.RatingTrend);
        }

        [Fact]
        public async Task GetAttendance_ForOtherStudent_ReturnsNull()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var model = await service.GetAttendanceAsync(seed.Parent.Id, seed.OtherStudent.Id);

            Assert.Null(model);
        }

        [Fact]
        public async Task GetGrades_ForLinkedStudent_ReturnsSubjectBreakdown()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var model = await service.GetGradesAsync(seed.Parent.Id, seed.LinkedStudent.Id);

            Assert.NotNull(model);
            Assert.Equal(80, model!.OverallPercentage);
            Assert.Equal(2, model.Subjects.Count);
            Assert.Equal(2, model.GradedSubjects);
            Assert.Equal(2, model.TotalSubjects);
        }

        [Fact]
        public async Task GetExams_MarksPendingApproval()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var model = await service.GetExamsAsync(seed.Parent.Id, seed.LinkedStudent.Id);

            Assert.NotNull(model);
            var result = Assert.Single(model!.Results);
            Assert.Equal("1-oraliq", result.ExamTitle);
            Assert.Equal(80, result.Percentage);
            Assert.False(result.IsApproved);
            Assert.Equal(1, model.PendingCount);
        }

        [Fact]
        public async Task GetMonthlyRating_ForLinkedStudent_ReturnsRatingModel()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var model = await service.GetMonthlyRatingAsync(seed.Parent.Id, seed.LinkedStudent.Id);

            Assert.NotNull(model);
            Assert.Equal(seed.LinkedStudent.Id, model!.Child.StudentId);
            Assert.Equal("Rating", model.ActiveAction);
            Assert.NotNull(model.Rating.StreamRating);
            Assert.Equal("10-A", model.Rating.StreamRating!.GroupName);

            // Bu seed'da oylik imtihon yo'q — o'rtacha null, lekin model qaytadi.
            Assert.Null(model.Rating.StreamRating.AverageScore);
        }

        [Fact]
        public async Task GetMonthlyRating_ForOtherStudent_ReturnsNull()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var model = await service.GetMonthlyRatingAsync(seed.Parent.Id, seed.OtherStudent.Id);

            Assert.Null(model);
        }

        [Fact]
        public async Task GetAlerts_ForOtherStudent_ReturnsNull()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var model = await service.GetAlertsAsync(seed.Parent.Id, seed.OtherStudent.Id);

            Assert.Null(model);
        }

        [Fact]
        public async Task GetSchedule_ReturnsStudentGroupLesson()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var model = await service.GetScheduleAsync(seed.Parent.Id, seed.LinkedStudent.Id);

            Assert.NotNull(model);
            var block = Assert.Single(model!.Schedule.Blocks);
            Assert.Equal("Matematika", block.SubjectName);
            Assert.Equal("10-A", block.GroupName);
            Assert.Equal(DayOfWeek.Monday, block.DayOfWeek);
        }
    }
}
