using EduTrack.Data;
using EduTrack.Models;

namespace EduTrack.Tests.Infrastructure
{
    public sealed record AdminExamDirectorySeed(
        int Subject1Id,
        int Subject2Id,
        string Teacher1Id,
        string Teacher2Id,
        int GroupAId,
        int GroupBId,
        int ActiveExamId,
        int ArchivedExamId,
        int NoGroupExamId,
        int SpecialExamId);

    // Admin imtihonlar ro'yxati testlari uchun ma'lumot to'plami:
    // 2 fan/o'qituvchi, 2 guruh, arxivlangan imtihon, guruhi yo'q imtihon,
    // maxsus belgili imtihon va sahifalash uchun 25 ta imtihon.
    public static class AdminExamDirectorySeedDataFactory
    {
        public static async Task<AdminExamDirectorySeed> CreateAsync(ApplicationDbContext db)
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

            var activeExam = NewExam(
                "Matematika 1", subject1.Id, bank1.Id, groupA.Id,
                At(2026, 1, 10, 10), archived: false);
            var archivedExam = NewExam(
                "Matematika arxiv", subject1.Id, bank1.Id, groupA.Id,
                At(2026, 3, 1, 9), archived: true);
            var noGroupExam = NewExam(
                "Fizika umumiy", subject2.Id, bank2.Id, null,
                At(2026, 2, 15, 12), archived: false);
            var specialExam = NewExam(
                "100% Test_Exam", subject2.Id, bank2.Id, groupB.Id,
                At(2026, 4, 1, 14), archived: false);

            db.Exams.AddRange(activeExam, archivedExam, noGroupExam, specialExam);

            for (var i = 1; i <= 25; i++)
            {
                db.Exams.Add(NewExam(
                    $"Exam {i:00}", subject1.Id, bank1.Id, groupA.Id,
                    At(2026, 5, 1, 9).AddDays(i), archived: false));
            }

            await db.SaveChangesAsync();

            var student1 = NewStudent("Aliyev Vali", "10001", groupA.Id);
            var student2 = NewStudent("Bekova Nodira", "10002", groupA.Id);
            db.Users.AddRange(student1, student2);
            await db.SaveChangesAsync();

            db.ExamResults.AddRange(
                NewResult(activeExam, student1, 8, 10),
                NewResult(activeExam, student2, 6, 10),
                NewResult(archivedExam, student1, 5, 10));
            await db.SaveChangesAsync();

            return new AdminExamDirectorySeed(
                subject1.Id,
                subject2.Id,
                teacher1.Id,
                teacher2.Id,
                groupA.Id,
                groupB.Id,
                activeExam.Id,
                archivedExam.Id,
                noGroupExam.Id,
                specialExam.Id);
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

        private static Exam NewExam(
            string title,
            int subjectId,
            int bankId,
            int? groupId,
            DateTime createdDate,
            bool archived) => new()
        {
            Title = title,
            SubjectId = subjectId,
            QuestionBankId = bankId,
            GroupId = groupId,
            QuestionCount = 10,
            DurationMinutes = 60,
            CreatedDate = createdDate,
            IsArchived = archived,
            IsOpen = true
        };

        private static ExamResult NewResult(
            Exam exam,
            ApplicationUser student,
            int correct,
            int total) => new()
        {
            ExamId = exam.Id,
            StudentId = student.Id,
            CorrectAnswers = correct,
            TotalQuestions = total,
            CompletedDate = exam.CreatedDate.AddHours(1),
            IsApproved = true
        };

        private static DateTime At(int year, int month, int day, int hour)
            => new(year, month, day, hour, 0, 0, DateTimeKind.Unspecified);
    }
}
