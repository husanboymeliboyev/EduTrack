using EduTrack.Services;
using Xunit;

namespace EduTrack.Tests.Infrastructure
{
    public sealed class ExamResultsTestFixture : IAsyncLifetime
    {
        public TestDbFactory Db { get; private set; } = null!;
        public ExamResultsService Service { get; private set; } = null!;
        public ExamResultsExcelExporter Exporter { get; private set; } = null!;
        public SeedData Seed { get; private set; } = null!;
        public IAppClock Clock { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Db = await TestDbFactory.CreateAsync();
            Seed = await SeedDataFactory.CreateAsync(Db.Context);
            Clock = new AppClock(AppClock.ResolveTimeZone("Asia/Tashkent"));
            Service = new ExamResultsService(Db.Context, Clock);
            Exporter = new ExamResultsExcelExporter();
        }

        public async Task DisposeAsync()
        {
            await Db.DisposeAsync();
        }
    }
}
