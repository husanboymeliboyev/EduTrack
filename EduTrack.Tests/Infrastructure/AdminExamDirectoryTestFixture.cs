using EduTrack.Services;
using Xunit;

namespace EduTrack.Tests.Infrastructure
{
    public sealed class AdminExamDirectoryTestFixture : IAsyncLifetime
    {
        public TestDbFactory Db { get; private set; } = null!;
        public AdminExamDirectoryService Service { get; private set; } = null!;
        public AdminExamDirectorySeed Seed { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Db = await TestDbFactory.CreateAsync();
            Seed = await AdminExamDirectorySeedDataFactory.CreateAsync(Db.Context);
            Service = new AdminExamDirectoryService(Db.Context);
        }

        public async Task DisposeAsync()
        {
            await Db.DisposeAsync();
        }
    }
}
