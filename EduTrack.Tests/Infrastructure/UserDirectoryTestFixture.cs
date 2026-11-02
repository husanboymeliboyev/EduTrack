using EduTrack.Services;
using Xunit;

namespace EduTrack.Tests.Infrastructure
{
    public sealed class UserDirectoryTestFixture : IAsyncLifetime
    {
        public TestDbFactory Db { get; private set; } = null!;
        public UserDirectoryService Service { get; private set; } = null!;
        public UserDirectorySeed Seed { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Db = await TestDbFactory.CreateAsync();
            Seed = await UserDirectorySeedDataFactory.CreateAsync(Db.Context);
            Service = new UserDirectoryService(Db.Context);
        }

        public async Task DisposeAsync()
        {
            await Db.DisposeAsync();
        }
    }
}
