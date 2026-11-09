using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using Xunit;

namespace EduTrack.Tests
{
    /// <summary>
    /// 100 ballik yakuniy baho servisi: qo'lda baho ustuvorligi, har bir
    /// imtihon uchun eng yaxshi urinish, imtihonlar o'rtachasi va faqat
    /// tasdiqlangan natijalar hisobga olinishi.
    /// </summary>
    public class SubjectGradeServiceTests
    {
        private static readonly DateTime Today = new(2026, 11, 9, 9, 0, 0, DateTimeKind.Unspecified);

        private sealed record Seed(
            ApplicationUser Student,
            ApplicationUser Teacher,
            Subject Math,
            Subject English);

        private static async Task<Seed> SeedAsync(ApplicationDbContext db)
        {
            var student = new ApplicationUser
            {
                UserName = "10001",
                NormalizedUserName = "10001",
                LoginId = "10001",
                FullName = "Aliyev Ali",
                MustChangePassword = false
            };
            var teacher = new ApplicationUser
            {
                UserName = "10002",
                NormalizedUserName = "10002",
                LoginId = "10002",
                FullName = "Meliboyev Husanboy",
                MustChangePassword = false
            };
            db.Users.AddRange(student, teacher);

            var math = new Subject { Name = "Matematika" };
            var english = new Subject { Name = "Ingliz tili" };
            db.Subjects.AddRange(math, english);
            await db.SaveChangesAsync();

            var bank = new QuestionBank
            {
                Name = "Matematika banki",
                SubjectId = math.Id
            };
            db.QuestionBanks.Add(bank);
            await db.SaveChangesAsync();

            var exam1 = new Exam
            {
                Title = "1-nazorat",
                SubjectId = math.Id,
                QuestionBankId = bank.Id,
                QuestionCount = 100,
                DurationMinutes = 60,
                CreatedDate = Today.AddDays(-20)
            };
            var exam2 = new Exam
            {
                Title = "2-nazorat",
                SubjectId = math.Id,
                QuestionBankId = bank.Id,
                QuestionCount = 100,
                DurationMinutes = 60,
                CreatedDate = Today.AddDays(-10)
            };
            var exam3 = new Exam
            {
                Title = "Tasdiqlanmagan",
                SubjectId = math.Id,
                QuestionBankId = bank.Id,
                QuestionCount = 100,
                DurationMinutes = 60,
                CreatedDate = Today.AddDays(-5)
            };
            db.Exams.AddRange(exam1, exam2, exam3);
            await db.SaveChangesAsync();

            // 1-imtihon: ikki urinish (60 va 80) — eng yaxshisi 80 olinadi.
            // 2-imtihon: 90.
            // 3-imtihon: 100, lekin tasdiqlanmagan — hisobga kirmaydi.
            db.ExamResults.AddRange(
                Result(exam1.Id, student.Id, 60, approved: true),
                Result(exam1.Id, student.Id, 80, approved: true),
                Result(exam2.Id, student.Id, 90, approved: true),
                Result(exam3.Id, student.Id, 100, approved: false));

            // Ingliz tili — qo'lda qo'yilgan 72 ball.
            db.SubjectGrades.Add(new SubjectGrade
            {
                StudentId = student.Id,
                SubjectId = english.Id,
                Score = 72,
                UpdatedDate = Today,
                TeacherId = teacher.Id
            });

            await db.SaveChangesAsync();

            return new Seed(student, teacher, math, english);
        }

        private static ExamResult Result(int examId, string studentId, int correct, bool approved) => new()
        {
            ExamId = examId,
            StudentId = studentId,
            TotalQuestions = 100,
            CorrectAnswers = correct,
            CompletedDate = Today.AddDays(-1),
            IsApproved = approved
        };

        [Fact]
        public async Task GetStudentGrades_ComputesBestAttemptThenExamAverage()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = new SubjectGradeService(factory.Context, new TestAppClock(Today));

            var grades = await service.GetStudentGradesAsync(
                seed.Student.Id, new[] { seed.Math.Id, seed.English.Id });

            var math = grades[seed.Math.Id];
            Assert.Equal(2, math.ExamCount);
            Assert.Equal(85, math.ExamAverage);
            Assert.Equal(85, math.Score);
            Assert.False(math.IsManual);
        }

        [Fact]
        public async Task GetStudentGrades_ManualScoreOverridesExamAverage()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = new SubjectGradeService(factory.Context, new TestAppClock(Today));

            // Qo'lda baho qo'shamiz — imtihon o'rtachasi (85) e'tiborsiz qoladi.
            await service.SaveManualGradeAsync(seed.Student.Id, seed.Math.Id, 95, seed.Teacher.Id);

            var grades = await service.GetStudentGradesAsync(
                seed.Student.Id, new[] { seed.Math.Id });

            var math = grades[seed.Math.Id];
            Assert.True(math.IsManual);
            Assert.Equal(95, math.ManualScore);
            Assert.Equal(85, math.ExamAverage);
            Assert.Equal(95, math.Score);
        }

        [Fact]
        public async Task GetStudentGrades_ManualOnlySubject_HasNoExamAverage()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = new SubjectGradeService(factory.Context, new TestAppClock(Today));

            var grades = await service.GetStudentGradesAsync(
                seed.Student.Id, new[] { seed.English.Id });

            var english = grades[seed.English.Id];
            Assert.True(english.IsManual);
            Assert.Equal(72, english.Score);
            Assert.Null(english.ExamAverage);
            Assert.Equal(0, english.ExamCount);
        }

        [Fact]
        public async Task SaveManualGrade_UpsertsSingleRow()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = new SubjectGradeService(factory.Context, new TestAppClock(Today));

            await service.SaveManualGradeAsync(seed.Student.Id, seed.English.Id, 88.4, seed.Teacher.Id);
            await service.SaveManualGradeAsync(seed.Student.Id, seed.English.Id, 91.2, seed.Teacher.Id);

            var row = Assert.Single(factory.Context.SubjectGrades
                .Where(g => g.StudentId == seed.Student.Id && g.SubjectId == seed.English.Id));
            Assert.Equal(91.2, row.Score);
            Assert.Equal(seed.Teacher.Id, row.TeacherId);
        }

        [Fact]
        public async Task SaveManualGrade_RejectsOutOfRange()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = new SubjectGradeService(factory.Context, new TestAppClock(Today));

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                service.SaveManualGradeAsync(seed.Student.Id, seed.Math.Id, 101, seed.Teacher.Id));

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                service.SaveManualGradeAsync(seed.Student.Id, seed.Math.Id, -1, seed.Teacher.Id));
        }
    }
}
