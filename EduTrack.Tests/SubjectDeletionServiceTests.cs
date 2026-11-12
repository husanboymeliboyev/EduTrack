using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTrack.Tests
{
    // Fanni o'chirishda RESTRICT bog'lanishlar (imtihon natijalari, savol banki,
    // baholash shkalasi) kaskadni to'sib qo'ymasligi kerak: servis ularni
    // to'g'ri tartibda tozalaydi. Bu test aynan shu zanjirni tekshiradi.
    public class SubjectDeletionServiceTests
    {
        [Fact]
        public async Task DeleteAsync_RemovesSubjectAndAllDependentData()
        {
            await using var db = await TestDbFactory.CreateAsync();
            var context = db.Context;

            var group = new Group { Name = "7-guruh", Type = GroupType.Stream };
            var subject = new Subject { Name = "Data Science" };
            var otherSubject = new Subject { Name = "Matematika" };
            context.Groups.Add(group);
            context.Subjects.AddRange(subject, otherSubject);

            var student = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "90001",
                LoginId = "90001",
                FullName = "Test O'quvchi",
                EmailConfirmed = true
            };
            context.Users.Add(student);
            await context.SaveChangesAsync();

            var bank = new QuestionBank { Name = "1-oraliq", SubjectId = subject.Id, GroupId = group.Id };
            context.QuestionBanks.Add(bank);
            await context.SaveChangesAsync();

            var question = new Question
            {
                Text = "Savol?",
                SubjectId = subject.Id,
                QuestionBankId = bank.Id,
                GroupId = group.Id
            };
            context.Questions.Add(question);
            await context.SaveChangesAsync();

            context.AnswerOptions.Add(new AnswerOption
            {
                Text = "Javob",
                IsCorrect = true,
                QuestionId = question.Id
            });

            var exam = new Exam
            {
                Title = "Yakuniy",
                SubjectId = subject.Id,
                QuestionBankId = bank.Id,
                GroupId = group.Id,
                QuestionCount = 1,
                DurationMinutes = 30,
                IsOpen = true
            };
            context.Exams.Add(exam);
            await context.SaveChangesAsync();

            context.ExamResults.Add(new ExamResult
            {
                ExamId = exam.Id,
                StudentId = student.Id,
                TotalQuestions = 1,
                CorrectAnswers = 1,
                CompletedDate = DateTime.UtcNow
            });

            var scale = new GradingScale { ExamId = exam.Id, Name = "Standart" };
            context.GradingScales.Add(scale);
            await context.SaveChangesAsync();

            var version = new GradingScaleVersion
            {
                GradingScaleId = scale.Id,
                VersionNumber = 1,
                IsPublished = true
            };
            context.GradingScaleVersions.Add(version);
            await context.SaveChangesAsync();

            context.GradingScaleBands.Add(new GradingScaleBand
            {
                GradingScaleVersionId = version.Id,
                MinimumScore = 0,
                MaximumScore = 100,
                Label = "A",
                DisplayOrder = 1
            });

            context.GroupSubjects.Add(new GroupSubject { GroupId = group.Id, SubjectId = subject.Id });
            context.Schedules.Add(new Schedule
            {
                SubjectId = subject.Id,
                GroupId = group.Id,
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(8, 45, 0),
                Shift = ScheduleShift.Morning,
                Period = 1
            });
            context.Attendances.Add(new Attendance
            {
                Date = DateTime.Today,
                StudentId = student.Id,
                SubjectId = subject.Id,
                Status = AttendanceStatus.Keldi
            });
            context.StudentGroupSubjects.Add(new StudentGroupSubject
            {
                StudentId = student.Id,
                SubjectId = subject.Id,
                GroupId = group.Id,
                StartDate = DateTime.Today,
                IsActive = true
            });

            var assignment = new Assignment
            {
                Title = "Amaliy ish",
                DueDate = DateTime.Today.AddDays(7),
                CreatedDate = DateTime.Now,
                SubjectId = subject.Id,
                GroupId = group.Id
            };
            context.Assignments.Add(assignment);
            await context.SaveChangesAsync();

            context.Submissions.Add(new Submission
            {
                AssignmentId = assignment.Id,
                StudentId = student.Id,
                SubmittedDate = DateTime.Now
            });

            var component = new GradeComponent
            {
                Name = "Nazorat",
                MaxScore = 20,
                SubjectId = subject.Id,
                Order = 1
            };
            context.GradeComponents.Add(component);
            await context.SaveChangesAsync();

            context.StudentGrades.Add(new StudentGrade
            {
                GradeComponentId = component.Id,
                StudentId = student.Id,
                Score = 15,
                UpdatedDate = DateTime.Now
            });
            context.SubjectGrades.Add(new SubjectGrade
            {
                StudentId = student.Id,
                SubjectId = subject.Id,
                Score = 85,
                UpdatedDate = DateTime.Now
            });

            await context.SaveChangesAsync();

            var summary = await new SubjectDeletionService(context).GetSummaryAsync(subject.Id);
            Assert.NotNull(summary);
            Assert.Equal(1, summary!.Exams);
            Assert.Equal(1, summary.ExamResults);
            Assert.Equal(1, summary.Questions);
            Assert.Equal(1, summary.QuestionBanks);
            Assert.Empty(summary.Blockers);

            var result = await new SubjectDeletionService(context).DeleteAsync(subject.Id);

            Assert.True(result.Success, result.Error);

            Assert.False(await context.Subjects.AnyAsync(s => s.Id == subject.Id));
            Assert.True(await context.Subjects.AnyAsync(s => s.Id == otherSubject.Id));

            Assert.False(await context.Exams.AnyAsync(e => e.Id == exam.Id));
            Assert.False(await context.ExamResults.AnyAsync(r => r.ExamId == exam.Id));
            Assert.False(await context.GradingScales.AnyAsync(s => s.ExamId == exam.Id));
            Assert.False(await context.GradingScaleVersions.AnyAsync(v => v.GradingScaleId == scale.Id));
            Assert.False(await context.GradingScaleBands.AnyAsync(b => b.GradingScaleVersionId == version.Id));
            Assert.False(await context.QuestionBanks.AnyAsync(b => b.Id == bank.Id));
            Assert.False(await context.Questions.AnyAsync(q => q.Id == question.Id));
            Assert.False(await context.AnswerOptions.AnyAsync(o => o.QuestionId == question.Id));
            Assert.False(await context.GroupSubjects.AnyAsync(gs => gs.SubjectId == subject.Id));
            Assert.False(await context.Schedules.AnyAsync(s => s.SubjectId == subject.Id));
            Assert.False(await context.Attendances.AnyAsync(a => a.SubjectId == subject.Id));
            Assert.False(await context.StudentGroupSubjects.AnyAsync(x => x.SubjectId == subject.Id));
            Assert.False(await context.Assignments.AnyAsync(a => a.SubjectId == subject.Id));
            Assert.False(await context.Submissions.AnyAsync(s => s.AssignmentId == assignment.Id));
            Assert.False(await context.GradeComponents.AnyAsync(c => c.SubjectId == subject.Id));
            Assert.False(await context.StudentGrades.AnyAsync(g => g.GradeComponentId == component.Id));
            Assert.False(await context.SubjectGrades.AnyAsync(g => g.SubjectId == subject.Id));
        }

        [Fact]
        public async Task DeleteAsync_BlocksWhenBankUsedByAnotherSubjectExam()
        {
            await using var db = await TestDbFactory.CreateAsync();
            var context = db.Context;

            var subject = new Subject { Name = "Data Science" };
            var other = new Subject { Name = "Matematika" };
            context.Subjects.AddRange(subject, other);
            await context.SaveChangesAsync();

            var bank = new QuestionBank { Name = "Umumiy bank", SubjectId = subject.Id };
            context.QuestionBanks.Add(bank);
            await context.SaveChangesAsync();

            context.Exams.Add(new Exam
            {
                Title = "Matematika imtihoni",
                SubjectId = other.Id,
                QuestionBankId = bank.Id,
                QuestionCount = 1,
                DurationMinutes = 20
            });
            await context.SaveChangesAsync();

            var summary = await new SubjectDeletionService(context).GetSummaryAsync(subject.Id);
            Assert.NotNull(summary);
            Assert.NotEmpty(summary!.Blockers);

            var result = await new SubjectDeletionService(context).DeleteAsync(subject.Id);
            Assert.False(result.Success);
            Assert.NotNull(result.Error);

            // Hech narsa o'chirilmagan bo'lishi kerak.
            Assert.True(await context.Subjects.AnyAsync(s => s.Id == subject.Id));
            Assert.True(await context.QuestionBanks.AnyAsync(b => b.Id == bank.Id));
        }
    }
}
