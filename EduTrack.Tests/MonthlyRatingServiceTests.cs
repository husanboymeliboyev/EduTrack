using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using EduTrack.ViewModels;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTrack.Tests
{
    /// <summary>
    /// Oylik reyting servisi: 100 ballik shkala, fanlar o'rta arifmetikasi,
    /// sinf va yo'nalishning ajratilishi, faqat tasdiqlangan oylik imtihon
    /// natijalari, retake qoidasi va competition ranking.
    /// </summary>
    public class MonthlyRatingServiceTests
    {
        private const int Year = 2026;
        private const int Month = 11;

        private static readonly DateTime Today = new(2026, 11, 6, 9, 0, 0, DateTimeKind.Unspecified);

        // Tarixiy oylar (iyun–oktabr) uchun ham a'zolik aktiv bo'lishi kerak.
        private static readonly DateTime MembershipStart = new(2026, 1, 5);

        private sealed record Seed(
            ApplicationUser S1,
            ApplicationUser S2,
            ApplicationUser S3,
            ApplicationUser S4,
            Group ClassGroup,
            Group StreamGroup,
            Subject Math,
            Subject English,
            Subject Physics,
            Subject Chemistry,
            Subject Biology,
            Subject Informatics,
            Exam ClassMath,
            Exam ClassEnglish,
            Exam ClassPhysics,
            Exam ClassChemistry,
            Exam ClassBiology,
            Exam StreamMath,
            Exam StreamInformatics);

        private static MonthlyRatingService CreateService(ApplicationDbContext db)
            => new(db, new TestAppClock(Today));

        private static ApplicationUser NewStudent(string loginId, string fullName) => new()
        {
            UserName = loginId,
            NormalizedUserName = loginId.ToUpperInvariant(),
            LoginId = loginId,
            FullName = fullName,
            Email = $"{loginId}@example.com",
            EmailConfirmed = true,
            MustChangePassword = false
        };

        private static Exam NewExam(
            QuestionBank bank,
            Group? group,
            int year,
            int month,
            string title,
            ExamKind kind = ExamKind.Monthly) => new()
        {
            Title = title,
            SubjectId = bank.SubjectId,
            QuestionBankId = bank.Id,
            GroupId = group?.Id,
            QuestionCount = 10,
            DurationMinutes = 30,
            CreatedDate = Today.AddDays(-10),
            Kind = kind,
            PeriodYear = kind == ExamKind.Monthly ? year : null,
            PeriodMonth = kind == ExamKind.Monthly ? month : null
        };

        private static void AddResult(
            ApplicationDbContext db,
            Exam exam,
            ApplicationUser student,
            int correct,
            bool approved = true)
        {
            db.ExamResults.Add(new ExamResult
            {
                ExamId = exam.Id,
                StudentId = student.Id,
                TotalQuestions = 10,
                CorrectAnswers = correct,
                CompletedDate = Today.AddDays(-1),
                IsApproved = approved
            });
        }

        private static async Task<Seed> SeedAsync(ApplicationDbContext db)
        {
            var s1 = NewStudent("20001", "Aliyev Ali");
            var s2 = NewStudent("20002", "Valiyev Vali");
            var s3 = NewStudent("20003", "Karimov Karim");
            var s4 = NewStudent("20004", "Rahimova Dilnoza");
            db.Users.AddRange(s1, s2, s3, s4);

            var classGroup = new Group { Name = "10-A", Type = GroupType.Class, Code = "10A" };
            var streamGroup = new Group { Name = "IT-1", Type = GroupType.Stream, Code = "IT1" };
            db.Groups.AddRange(classGroup, streamGroup);

            var math = new Subject { Name = "Matematika" };
            var english = new Subject { Name = "Ingliz tili" };
            var physics = new Subject { Name = "Fizika" };
            var chemistry = new Subject { Name = "Kimyo" };
            var biology = new Subject { Name = "Biologiya" };
            var informatics = new Subject { Name = "Informatika" };
            db.Subjects.AddRange(math, english, physics, chemistry, biology, informatics);
            await db.SaveChangesAsync();

            db.GroupSubjects.AddRange(
                new GroupSubject { GroupId = classGroup.Id, SubjectId = math.Id },
                new GroupSubject { GroupId = classGroup.Id, SubjectId = english.Id },
                new GroupSubject { GroupId = classGroup.Id, SubjectId = physics.Id },
                new GroupSubject { GroupId = classGroup.Id, SubjectId = chemistry.Id },
                new GroupSubject { GroupId = classGroup.Id, SubjectId = biology.Id },
                new GroupSubject { GroupId = streamGroup.Id, SubjectId = math.Id },
                new GroupSubject { GroupId = streamGroup.Id, SubjectId = informatics.Id });
            await db.SaveChangesAsync();

            foreach (var student in new[] { s1, s2, s3, s4 })
            {
                db.GroupMemberships.AddRange(
                    new GroupMembership
                    {
                        StudentId = student.Id,
                        GroupId = classGroup.Id,
                        Scope = GroupType.Class,
                        StartDate = MembershipStart,
                        IsActive = true
                    },
                    new GroupMembership
                    {
                        StudentId = student.Id,
                        GroupId = streamGroup.Id,
                        Scope = GroupType.Stream,
                        StartDate = MembershipStart,
                        IsActive = true
                    });
            }

            await db.SaveChangesAsync();

            var mathBank = new QuestionBank { Name = "Matematika banki", SubjectId = math.Id };
            var englishBank = new QuestionBank { Name = "Ingliz tili banki", SubjectId = english.Id };
            var physicsBank = new QuestionBank { Name = "Fizika banki", SubjectId = physics.Id };
            var chemistryBank = new QuestionBank { Name = "Kimyo banki", SubjectId = chemistry.Id };
            var biologyBank = new QuestionBank { Name = "Biologiya banki", SubjectId = biology.Id };
            var informaticsBank = new QuestionBank { Name = "Informatika banki", SubjectId = informatics.Id };
            db.QuestionBanks.AddRange(mathBank, englishBank, physicsBank, chemistryBank, biologyBank, informaticsBank);
            await db.SaveChangesAsync();

            var classMath = NewExam(mathBank, classGroup, Year, Month, "Matematika oylik");
            var classEnglish = NewExam(englishBank, classGroup, Year, Month, "Ingliz tili oylik");
            var classPhysics = NewExam(physicsBank, classGroup, Year, Month, "Fizika oylik");
            var classChemistry = NewExam(chemistryBank, classGroup, Year, Month, "Kimyo oylik");
            var classBiology = NewExam(biologyBank, classGroup, Year, Month, "Biologiya oylik");
            var streamMath = NewExam(mathBank, streamGroup, Year, Month, "Matematika oylik (yo'nalish)");
            var streamInformatics = NewExam(informaticsBank, streamGroup, Year, Month, "Informatika oylik");

            db.Exams.AddRange(
                classMath, classEnglish, classPhysics, classChemistry, classBiology,
                streamMath, streamInformatics);
            await db.SaveChangesAsync();

            // Sinf (1-smena): S1 = (100+90+80+70+0)/5 = 68
            AddResult(db, classMath, s1, 10);
            AddResult(db, classMath, s2, 8);
            AddResult(db, classMath, s3, 8);
            AddResult(db, classMath, s4, 6);

            AddResult(db, classEnglish, s1, 9);
            AddResult(db, classEnglish, s2, 7);
            AddResult(db, classEnglish, s3, 5);

            AddResult(db, classPhysics, s1, 8);
            AddResult(db, classPhysics, s2, 6);
            AddResult(db, classPhysics, s3, 4);
            AddResult(db, classPhysics, s4, 5);

            AddResult(db, classChemistry, s1, 7);
            AddResult(db, classChemistry, s2, 5);
            AddResult(db, classChemistry, s3, 3);
            AddResult(db, classChemistry, s4, 4);

            AddResult(db, classBiology, s2, 10);
            AddResult(db, classBiology, s3, 2);
            AddResult(db, classBiology, s4, 1);

            // Yo'nalish: S1 = 90, S2 = 80, S3 = 80, S4 = 70 (2 va 3-o'rinlar teng).
            AddResult(db, streamMath, s1, 10);
            AddResult(db, streamMath, s2, 7);
            AddResult(db, streamMath, s3, 9);
            AddResult(db, streamMath, s4, 8);

            AddResult(db, streamInformatics, s1, 8);
            AddResult(db, streamInformatics, s2, 9);
            AddResult(db, streamInformatics, s3, 7);
            AddResult(db, streamInformatics, s4, 6);

            await db.SaveChangesAsync();

            return new Seed(
                s1, s2, s3, s4,
                classGroup, streamGroup,
                math, english, physics, chemistry, biology, informatics,
                classMath, classEnglish, classPhysics, classChemistry, classBiology,
                streamMath, streamInformatics);
        }

        [Fact]
        public async Task GetStudentRating_ComputesFiveSubjectAverageAndCoverage()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var model = await service.GetStudentRatingAsync(seed.S1.Id, Year, Month);

            Assert.NotNull(model);
            Assert.Equal("Aliyev Ali", model!.StudentName);
            Assert.Equal("10-A", model.ClassName);
            Assert.Equal("IT-1", model.StreamName);
            Assert.Equal("Noyabr 2026", model.PeriodLabel);

            var classRating = Assert.IsType<MonthlyRatingScopeViewModel>(model.ClassRating);
            Assert.Equal(GroupType.Class, classRating.Scope);
            Assert.Equal("10-A", classRating.GroupName);
            Assert.Equal(5, classRating.SubjectCount);
            Assert.Equal(5, classRating.RatedSubjectCount);
            Assert.Equal(4, classRating.ScoredSubjectCount);
            Assert.Equal(4, classRating.ParticipantCount);
            Assert.Equal(68d, classRating.AverageScore!.Value, 3);
            Assert.Equal(2, classRating.Rank);
            Assert.Equal(5, classRating.Subjects.Count);
            Assert.Equal(80d, classRating.CoveragePercentage, 3);
        }

        [Fact]
        public async Task GetStudentRating_ComputesFourSubjectAverage()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var db = factory.Context;

            var student = NewStudent("30001", "To'rayev Temur");
            db.Users.Add(student);

            var group = new Group { Name = "5-B", Type = GroupType.Class, Code = "5B" };
            db.Groups.Add(group);

            var subjects = new[]
            {
                new Subject { Name = "Ona tili" },
                new Subject { Name = "Matematika" },
                new Subject { Name = "Tarix" },
                new Subject { Name = "Geografiya" }
            };
            db.Subjects.AddRange(subjects);
            await db.SaveChangesAsync();

            foreach (var subject in subjects)
            {
                db.GroupSubjects.Add(new GroupSubject { GroupId = group.Id, SubjectId = subject.Id });
            }

            db.GroupMemberships.Add(new GroupMembership
            {
                StudentId = student.Id,
                GroupId = group.Id,
                Scope = GroupType.Class,
                StartDate = MembershipStart,
                IsActive = true
            });
            await db.SaveChangesAsync();

            var banks = new List<QuestionBank>();
            foreach (var subject in subjects)
            {
                var bank = new QuestionBank { Name = $"{subject.Name} banki", SubjectId = subject.Id };
                db.QuestionBanks.Add(bank);
                banks.Add(bank);
            }
            await db.SaveChangesAsync();

            var scores = new[] { 10, 8, 6, 4 };

            for (var i = 0; i < subjects.Length; i++)
            {
                var exam = NewExam(banks[i], group, Year, Month, $"{subjects[i].Name} oylik");
                db.Exams.Add(exam);
                await db.SaveChangesAsync();
                AddResult(db, exam, student, scores[i]);
            }

            await db.SaveChangesAsync();

            var service = CreateService(db);
            var model = await service.GetStudentRatingAsync(student.Id, Year, Month);

            var rating = Assert.IsType<MonthlyRatingScopeViewModel>(model!.ClassRating);
            Assert.Equal(4, rating.SubjectCount);
            Assert.Equal(4, rating.RatedSubjectCount);
            Assert.Equal(4, rating.ScoredSubjectCount);
            Assert.Equal(70d, rating.AverageScore!.Value, 3); // (100+80+60+40)/4
            Assert.Equal(1, rating.Rank);
        }

        [Fact]
        public async Task GetStudentRating_IgnoresUnapprovedResults()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            // S1 uchun biologiya natijasi bor, lekin tasdiqlanmagan.
            AddResult(factory.Context, seed.ClassBiology, seed.S1, 10, approved: false);
            await factory.Context.SaveChangesAsync();

            var model = await service.GetStudentRatingAsync(seed.S1.Id, Year, Month);

            var classRating = Assert.IsType<MonthlyRatingScopeViewModel>(model!.ClassRating);
            Assert.Equal(4, classRating.ScoredSubjectCount);
            Assert.Equal(68d, classRating.AverageScore!.Value, 3);

            var biology = classRating.Subjects.Single(s => s.SubjectName == "Biologiya");
            Assert.True(biology.HasMonthlyExam);
            Assert.Null(biology.Score);
            Assert.Equal("missing", biology.Tone);
        }

        [Fact]
        public async Task GetStudentRating_IgnoresRegularExams()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var biologyBank = await factory.Context.QuestionBanks
                .SingleAsync(b => b.SubjectId == seed.Biology.Id);

            var regular = NewExam(
                biologyBank, seed.ClassGroup, Year, Month, "Biologiya nazorat", ExamKind.Regular);

            factory.Context.Exams.Add(regular);
            await factory.Context.SaveChangesAsync();

            AddResult(factory.Context, regular, seed.S1, 10);
            await factory.Context.SaveChangesAsync();

            var model = await service.GetStudentRatingAsync(seed.S1.Id, Year, Month);

            var classRating = Assert.IsType<MonthlyRatingScopeViewModel>(model!.ClassRating);
            Assert.Equal(5, classRating.RatedSubjectCount);
            Assert.Equal(4, classRating.ScoredSubjectCount);
            Assert.Equal(68d, classRating.AverageScore!.Value, 3);

            var biology = classRating.Subjects.Single(s => s.SubjectName == "Biologiya");
            Assert.Null(biology.Score);
        }

        [Fact]
        public async Task GetStudentRating_MissingMonthlyResultCountsAsZero()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var model = await service.GetStudentRatingAsync(seed.S1.Id, Year, Month);

            var classRating = Assert.IsType<MonthlyRatingScopeViewModel>(model!.ClassRating);
            var biology = classRating.Subjects.Single(s => s.SubjectName == "Biologiya");

            Assert.True(biology.HasMonthlyExam);
            Assert.Null(biology.Score);
            Assert.Equal(0d, biology.EffectiveScore, 3);

            // (100+90+80+70+0)/5 = 68 — yetishmagan fan 0 bo'lib qo'shiladi.
            Assert.Equal(68d, classRating.AverageScore!.Value, 3);
        }

        [Fact]
        public async Task GetStudentRating_UsesHighestApprovedMonthlyResult()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var englishBank = await factory.Context.QuestionBanks
                .SingleAsync(b => b.SubjectId == seed.English.Id);
            var chemistryBank = await factory.Context.QuestionBanks
                .SingleAsync(b => b.SubjectId == seed.Chemistry.Id);

            var englishRetake = NewExam(
                englishBank, seed.ClassGroup, Year, Month, "Ingliz tili qayta");
            var chemistryRetake = NewExam(
                chemistryBank, seed.ClassGroup, Year, Month, "Kimyo qayta");

            factory.Context.Exams.AddRange(englishRetake, chemistryRetake);
            await factory.Context.SaveChangesAsync();

            AddResult(factory.Context, englishRetake, seed.S1, 2);    // 20% < 90% -> hisobga olinmaydi
            AddResult(factory.Context, chemistryRetake, seed.S1, 10); // 100% > 70% -> yangi eng yuqori
            await factory.Context.SaveChangesAsync();

            var model = await service.GetStudentRatingAsync(seed.S1.Id, Year, Month);

            var classRating = Assert.IsType<MonthlyRatingScopeViewModel>(model!.ClassRating);
            var english = classRating.Subjects.Single(s => s.SubjectName == "Ingliz tili");
            var chemistry = classRating.Subjects.Single(s => s.SubjectName == "Kimyo");

            Assert.Equal(90d, english.Score!.Value, 3);
            Assert.Equal(100d, chemistry.Score!.Value, 3);
            Assert.Equal(74d, classRating.AverageScore!.Value, 3); // (100+90+80+100+0)/5
        }

        [Fact]
        public async Task GetStudentRating_RanksWithCompetitionOrdering()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var model = await service.GetStudentRatingAsync(seed.S1.Id, Year, Month);

            var streamRating = Assert.IsType<MonthlyRatingScopeViewModel>(model!.StreamRating);
            var rows = streamRating.Leaderboard.ToDictionary(r => r.StudentId, StringComparer.Ordinal);

            Assert.Equal(1, rows[seed.S1.Id].Rank);
            Assert.Equal(2, rows[seed.S2.Id].Rank);
            Assert.Equal(2, rows[seed.S3.Id].Rank);
            Assert.Equal(4, rows[seed.S4.Id].Rank);

            // Teng ball: 2-o'rin ikki marta, keyingisi 4-o'rin (1, 2, 2, 4).
            Assert.Equal(1, streamRating.Rank);
            Assert.Equal(4, streamRating.ParticipantCount);
        }

        [Fact]
        public async Task GetStudentRating_SeparatesClassAndStreamScopes()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var model = await service.GetStudentRatingAsync(seed.S1.Id, Year, Month);

            Assert.NotNull(model!.ClassRating);
            Assert.NotNull(model.StreamRating);

            Assert.Equal(GroupType.Class, model.ClassRating!.Scope);
            Assert.Equal("10-A", model.ClassRating.GroupName);
            Assert.Equal(5, model.ClassRating.RatedSubjectCount);
            Assert.Equal(68d, model.ClassRating.AverageScore!.Value, 3);

            Assert.Equal(GroupType.Stream, model.StreamRating!.Scope);
            Assert.Equal("IT-1", model.StreamRating.GroupName);
            Assert.Equal(2, model.StreamRating.RatedSubjectCount);
            Assert.Equal(90d, model.StreamRating.AverageScore!.Value, 3);

            Assert.Equal(90d, model.BestAverage!.Value, 3);
        }

        [Fact]
        public async Task GetStudentRating_BuildsSixMonthTrend()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var mathBank = await factory.Context.QuestionBanks
                .SingleAsync(b => b.SubjectId == seed.Math.Id);

            var juneExam = NewExam(mathBank, seed.ClassGroup, 2026, 6, "Matematika iyun");
            factory.Context.Exams.Add(juneExam);
            await factory.Context.SaveChangesAsync();

            AddResult(factory.Context, juneExam, seed.S1, 5); // 50%
            await factory.Context.SaveChangesAsync();

            var model = await service.GetStudentRatingAsync(seed.S1.Id, Year, Month);

            Assert.Equal(6, model!.Trend.Count);

            var first = model.Trend[0];
            Assert.Equal(2026, first.Year);
            Assert.Equal(6, first.Month);
            Assert.Equal("Iyn", first.ShortLabel);
            Assert.Equal(50d, first.ClassAverage!.Value, 3);

            var last = model.Trend[5];
            Assert.Equal(Month, last.Month);
            Assert.True(last.IsSelected);
            Assert.Equal(68d, last.ClassAverage!.Value, 3);
        }

        [Fact]
        public async Task GetOverview_ComputesGroupRatingAndStudentRanks()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var overview = await service.GetOverviewAsync(seed.ClassGroup.Id, Year, Month);

            Assert.Equal(1, overview.GroupCount);
            Assert.Equal(4, overview.StudentCount);
            Assert.Equal(5, overview.ExamCount);
            Assert.Equal("Noyabr 2026", overview.PeriodLabel);

            var group = Assert.Single(overview.GroupRatings);
            Assert.Equal(seed.ClassGroup.Id, group.GroupId);
            Assert.Equal(5, group.RatedSubjectCount);
            Assert.Equal(5, group.SubjectCount);
            Assert.Equal(4, group.StudentCount);
            Assert.Equal(4, group.RatedStudentCount);
            Assert.Equal(54d, group.AverageScore!.Value, 3); // (72+68+44+32)/4
            Assert.Equal(54d, overview.OverallAverage!.Value, 3);

            Assert.Equal(
                new[] { seed.S2.Id, seed.S1.Id, seed.S3.Id, seed.S4.Id },
                group.Students.Select(s => s.StudentId).ToArray());

            Assert.Equal(
                new int?[] { 1, 2, 3, 4 },
                group.Students.Select(s => s.Rank).ToArray());
        }

        [Fact]
        public async Task GetOverview_WithoutGroupFilter_ReturnsOnlyRatedGroups()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var overview = await service.GetOverviewAsync(null, Year, Month);

            Assert.Equal(2, overview.GroupCount);
            Assert.Equal(4, overview.StudentCount);
            Assert.All(overview.GroupRatings, g => Assert.True(g.RatedSubjectCount > 0));

            var classRating = overview.GroupRatings.Single(g => g.GroupId == seed.ClassGroup.Id);
            var streamRating = overview.GroupRatings.Single(g => g.GroupId == seed.StreamGroup.Id);

            Assert.Equal(54d, classRating.AverageScore!.Value, 3);
            Assert.Equal(80d, streamRating.AverageScore!.Value, 3); // (90+80+80+70)/4
            Assert.Equal(67d, overview.OverallAverage!.Value, 3);    // (54+80)/2
        }
    }
}
