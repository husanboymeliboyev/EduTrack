using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTrack.Tests
{
    /// <summary>
    /// Ko'p o'qituvchi modeli: bitta fan bir nechta o'qituvchida bo'lishi.
    /// Fan egasi (Subject.TeacherId) — fan darajasi; guruh bo'yicha o'qituvchi
    /// (GroupSubject.TeacherId) — "(fan, guruh)" juftligi darajasi.
    /// </summary>
    public class TeacherAccessServiceTests
    {
        private const string OwnerId = "teacher-owner";
        private const string OverrideId = "teacher-override";
        private const string OutsiderId = "teacher-outsider";

        private sealed record Seed(
            Subject Subject,
            Group GroupA,
            Group GroupB);

        private static TeacherAccessService CreateService(ApplicationDbContext db) => new(db);

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

        private static async Task<Seed> SeedAsync(ApplicationDbContext db)
        {
            db.Users.AddRange(
                NewUser(OwnerId, "Fan egasi"),
                NewUser(OverrideId, "Guruh o'qituvchisi"),
                NewUser(OutsiderId, "Begona o'qituvchi"));
            await db.SaveChangesAsync();

            var subject = new Subject { Name = "Matematika", TeacherId = OwnerId };
            var otherSubject = new Subject { Name = "Fizika", TeacherId = OutsiderId };
            var groupA = new Group { Name = "1-guruh" };
            var groupB = new Group { Name = "2-guruh" };

            db.AddRange(subject, otherSubject, groupA, groupB);
            await db.SaveChangesAsync();

            // Fan egasi: 1-guruh override'siz (o'zi o'qitadi),
            // 2-guruhda boshqa o'qituvchi override qilingan (ko'p o'qituvchi modeli).
            db.GroupSubjects.AddRange(
                new GroupSubject { SubjectId = subject.Id, GroupId = groupA.Id },
                new GroupSubject { SubjectId = subject.Id, GroupId = groupB.Id, TeacherId = OverrideId },
                // Boshqa o'qituvchining fanida 1-guruhda override.
                new GroupSubject { SubjectId = otherSubject.Id, GroupId = groupA.Id, TeacherId = OverrideId });
            await db.SaveChangesAsync();

            return new Seed(subject, groupA, groupB);
        }

        [Fact]
        public async Task GetOwnedSubjectIdsAsync_IncludesOwnerAndOverrideSubjects()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var ownerIds = await service.GetOwnedSubjectIdsAsync(OwnerId);
            var overrideIds = await service.GetOwnedSubjectIdsAsync(OverrideId);
            var outsiderIds = await service.GetOwnedSubjectIdsAsync(OutsiderId);

            // Fan egasi o'z fanini ko'radi.
            Assert.Contains(seed.Subject.Id, ownerIds);
            // Override o'qituvchi ham o'sha fanni ko'radi (ko'p o'qituvchi).
            Assert.Contains(seed.Subject.Id, overrideIds);
            // Override o'qituvchining boshqa fani ham bor (1-guruh, Fizika).
            Assert.Contains(await factory.Context.Subjects.Where(s => s.Name == "Fizika").Select(s => s.Id).SingleAsync(), overrideIds);
            // Boshqa o'qituvchiga Matematika tegishli emas.
            Assert.DoesNotContain(seed.Subject.Id, outsiderIds);
        }

        [Fact]
        public async Task OwnsPairAsync_OverrideTeacherOwnsOnlyTheirPairs()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            // Fan egasi override'siz juftlikka ega; override qilingan guruh esa yo'q.
            Assert.True(await service.OwnsPairAsync(OwnerId, seed.Subject.Id, seed.GroupA.Id));
            Assert.False(await service.OwnsPairAsync(OwnerId, seed.Subject.Id, seed.GroupB.Id));
            var fizikaId = await factory.Context.Subjects.Where(s => s.Name == "Fizika").Select(s => s.Id).SingleAsync();
            // Fan egasi boshqa o'qituvchi override qilgan juftlikga ega emas.
            Assert.False(await service.OwnsPairAsync(OwnerId, fizikaId, seed.GroupA.Id));

            // Override o'qituvchi faqat override qilingan juftliklarga ega.
            Assert.True(await service.OwnsPairAsync(OverrideId, seed.Subject.Id, seed.GroupB.Id));
            Assert.False(await service.OwnsPairAsync(OverrideId, seed.Subject.Id, seed.GroupA.Id));
            Assert.True(await service.OwnsPairAsync(OverrideId, fizikaId, seed.GroupA.Id));

            // Guruh null bo'lsa — fan darajasidagi tekshiruv.
            Assert.True(await service.OwnsPairAsync(OwnerId, seed.Subject.Id, null));
        }

        [Fact]
        public async Task GetOwnedPairsAsync_ReturnsEffectiveTeacherPairs()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var overridePairs = await service.GetOwnedPairsAsync(OverrideId);

            // Override o'qituvchi ikkita juftlikka ega: Matematika 2-guruh
            // (override) va Fizika 1-guruh (override). Fan egasining 1-guruh
            // juftligi unga tegishli emas.
            Assert.Equal(2, overridePairs.Count);
            Assert.Contains(overridePairs, p => p.SubjectId == seed.Subject.Id && p.GroupId == seed.GroupB.Id);
            Assert.DoesNotContain(overridePairs, p => p.SubjectId == seed.Subject.Id && p.GroupId == seed.GroupA.Id);
        }

        [Fact]
        public async Task GetTeacherSubjectsAsync_IncludesOverrideSubjects()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);
            var service = CreateService(factory.Context);

            var overrideSubjects = await service.GetTeacherSubjectsAsync(OverrideId);

            Assert.Equal(2, overrideSubjects.Count);
            Assert.Contains(overrideSubjects, s => s.Id == seed.Subject.Id);
        }

        // Bir fanga bir nechta o'qituvchi biriktirilgan bo'lsa ham, savollar
        // banklari o'qituvchilar orasida aralashmaydi: har bir o'qituvchi faqat
        // o'zi yaratgan bankni ko'radi va tahrirlay oladi.
        [Fact]
        public async Task QuestionBanks_AreIsolatedBetweenTeachersOfSameSubject()
        {
            await using var factory = await TestDbFactory.CreateAsync();
            var seed = await SeedAsync(factory.Context);

            var ownerBank = new QuestionBank
            {
                Name = "Fan egasining banki",
                SubjectId = seed.Subject.Id,
                TeacherId = OwnerId
            };
            var overrideBank = new QuestionBank
            {
                Name = "Guruh o'qituvchisining banki",
                SubjectId = seed.Subject.Id,
                TeacherId = OverrideId
            };
            // Legacy bank: TeacherId yo'q — fan o'qituvchilariga fallback.
            var legacyBank = new QuestionBank
            {
                Name = "Eski bank",
                SubjectId = seed.Subject.Id,
                TeacherId = null
            };

            factory.Context.QuestionBanks.AddRange(ownerBank, overrideBank, legacyBank);
            await factory.Context.SaveChangesAsync();

            var service = CreateService(factory.Context);

            // O'z banki — ha, boshqasining banki — yo'q.
            Assert.True(await service.OwnsQuestionBankAsync(OwnerId, ownerBank.Id));
            Assert.False(await service.OwnsQuestionBankAsync(OwnerId, overrideBank.Id));
            Assert.True(await service.OwnsQuestionBankAsync(OverrideId, overrideBank.Id));
            Assert.False(await service.OwnsQuestionBankAsync(OverrideId, ownerBank.Id));

            // Eski bankni fan o'qitadigan ikkala o'qituvchi ham ko'radi.
            Assert.True(await service.OwnsQuestionBankAsync(OwnerId, legacyBank.Id));
            Assert.True(await service.OwnsQuestionBankAsync(OverrideId, legacyBank.Id));

            var ownerBankIds = await service.GetOwnedQuestionBankIdsAsync(OwnerId);
            var overrideBankIds = await service.GetOwnedQuestionBankIdsAsync(OverrideId);
            var outsiderBankIds = await service.GetOwnedQuestionBankIdsAsync(OutsiderId);

            Assert.Contains(ownerBank.Id, ownerBankIds);
            Assert.DoesNotContain(overrideBank.Id, ownerBankIds);

            Assert.Contains(overrideBank.Id, overrideBankIds);
            Assert.DoesNotContain(ownerBank.Id, overrideBankIds);

            // Begona o'qituvchi bu fanning hech qaysi bankini ko'rmaydi.
            Assert.DoesNotContain(ownerBank.Id, outsiderBankIds);
            Assert.DoesNotContain(overrideBank.Id, outsiderBankIds);
            Assert.DoesNotContain(legacyBank.Id, outsiderBankIds);
        }
    }
}
