using EduTrack.Data;
using EduTrack.Models;

namespace EduTrack.Tests.Infrastructure
{
    public sealed record SeedData(
        int Exam1Id,
        int Exam2Id,
        int Exam3Id,
        int Exam4Id,
        string Teacher1Id,
        string Teacher2Id,
        int Group1Id,
        int Group2Id);

    // Testlar uchun barqaror ma'lumot to'plami: foiz chegaralari (59.9/60/85.9/86),
    // sana chegaralari, status, ishonchlilik va qidiruv uchun maxsus belgilar.
    public static class SeedDataFactory
    {
        public static async Task<SeedData> CreateAsync(ApplicationDbContext db)
        {
            var groupA = new Group { Name = "Guruh A" };
            var groupB = new Group { Name = "Guruh B" };
            db.Groups.AddRange(groupA, groupB);
            await db.SaveChangesAsync();

            var teacher1 = NewUser("Teacher One", "20001");
            var teacher2 = NewUser("Teacher Two", "20002");
            db.Users.AddRange(teacher1, teacher2);
            await db.SaveChangesAsync();

            var subject1 = new Subject { Name = "Matematika", TeacherId = teacher1.Id };
            var subject2 = new Subject { Name = "Fizika", TeacherId = teacher2.Id };
            db.Subjects.AddRange(subject1, subject2);
            await db.SaveChangesAsync();

            var bank1 = new QuestionBank { Name = "Matematika banki", SubjectId = subject1.Id };
            var bank2 = new QuestionBank { Name = "Fizika banki", SubjectId = subject2.Id };
            db.QuestionBanks.AddRange(bank1, bank2);
            await db.SaveChangesAsync();

            var exam1 = NewExam("Matematika 1", subject1.Id, bank1.Id, 1);
            var exam2 = NewExam("Fizika 1", subject2.Id, bank2.Id, 2);
            var exam3 = NewExam("Matematika pagination", subject1.Id, bank1.Id, 3);
            var exam4 = NewExam("Matematika bo'sh", subject1.Id, bank1.Id, 4);
            db.Exams.AddRange(exam1, exam2, exam3, exam4);
            await db.SaveChangesAsync();

            var aliyev = NewStudent("Aliyev Vali", "10001", groupA.Id);
            var bekova = NewStudent("Bekova Nodira", "10002", groupA.Id);
            var cyrillic = NewStudent("Алиев Кирилл", "10003", groupB.Id);
            var apostrophe = NewStudent("O'zbekiston O'quvchi", "10004", groupB.Id);
            var typographic = NewStudent("O’zbekiston Tipografik", "10005", groupB.Id);
            var special = NewStudent("100% Real_Student", "10006", groupB.Id);
            var zero = NewStudent("Zero Savol", "10007", groupA.Id);
            var lowercase = NewStudent("abdulaziz Karimov", "10008", groupA.Id);

            db.Users.AddRange(aliyev, bekova, cyrillic, apostrophe, typographic, special, zero, lowercase);
            await db.SaveChangesAsync();

            db.ExamResults.AddRange(
                NewResult(exam1, aliyev, 6, 10, At(2026, 1, 10, 10), approved: true),
                NewResult(exam1, bekova, 86, 100, At(2026, 1, 15, 11), approved: false, tabSwitch: 2),
                NewResult(exam1, cyrillic, 859, 1000, At(2026, 2, 1, 12), approved: true, exitedFullscreen: true),
                NewResult(exam1, apostrophe, 599, 1000, At(2026, 2, 10, 13), approved: false),
                NewResult(exam1, typographic, 60, 100, At(2026, 3, 1, 14), approved: true),
                NewResult(exam1, special, 70, 100, At(2026, 3, 5, 15), approved: false),
                NewResult(exam1, zero, 0, 0, At(2026, 3, 10, 16), approved: false),
                NewResult(exam1, lowercase, 75, 100, At(2026, 3, 12, 17), approved: true),
                NewResult(exam2, aliyev, 50, 100, At(2026, 1, 20, 10), approved: true));
            await db.SaveChangesAsync();

            db.ExamAttempts.Add(new ExamAttempt
            {
                ExamId = exam1.Id,
                StudentId = apostrophe.Id,
                StartedDate = At(2026, 2, 10, 12),
                ViolationCount = 1,
                LastViolationReason = "tab-switch",
                LastViolationAtUtc = At(2026, 2, 10, 12).AddMinutes(5)
            });
            await db.SaveChangesAsync();

            var paginationStudents = new List<ApplicationUser>();
            for (var i = 1; i <= 25; i++)
            {
                paginationStudents.Add(NewStudent($"Student {i:00}", $"30{i:000}", groupA.Id));
            }

            db.Users.AddRange(paginationStudents);
            await db.SaveChangesAsync();

            foreach (var student in paginationStudents)
            {
                db.ExamResults.Add(NewResult(
                    exam3,
                    student,
                    50,
                    100,
                    At(2026, 4, 1, 0).AddMinutes(paginationStudents.IndexOf(student) + 1),
                    approved: true));
            }

            await db.SaveChangesAsync();

            return new SeedData(
                exam1.Id,
                exam2.Id,
                exam3.Id,
                exam4.Id,
                teacher1.Id,
                teacher2.Id,
                groupA.Id,
                groupB.Id);
        }

        private static ApplicationUser NewUser(string fullName, string loginId) => new()
        {
            UserName = loginId,
            NormalizedUserName = loginId.ToUpperInvariant(),
            FullName = fullName,
            LoginId = loginId,
            Email = $"{loginId}@example.com",
            EmailConfirmed = true,
            MustChangePassword = false
        };

        private static ApplicationUser NewStudent(string fullName, string loginId, int groupId)
        {
            var student = NewUser(fullName, loginId);
            student.GroupId = groupId;
            return student;
        }

        private static Exam NewExam(string title, int subjectId, int bankId, int day) => new()
        {
            Title = title,
            SubjectId = subjectId,
            QuestionBankId = bankId,
            QuestionCount = 10,
            DurationMinutes = 60,
            CreatedDate = At(2026, 1, day, 9)
        };

        private static ExamResult NewResult(
            Exam exam,
            ApplicationUser student,
            int correct,
            int total,
            DateTime completed,
            bool approved,
            int tabSwitch = 0,
            bool exitedFullscreen = false) => new()
        {
            ExamId = exam.Id,
            StudentId = student.Id,
            CorrectAnswers = correct,
            TotalQuestions = total,
            CompletedDate = completed,
            IsApproved = approved,
            TabSwitchCount = tabSwitch,
            ExitedFullscreen = exitedFullscreen
        };

        private static DateTime At(int year, int month, int day, int hour)
            => new(year, month, day, hour, 0, 0, DateTimeKind.Unspecified);
    }
}
