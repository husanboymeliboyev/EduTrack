using EduTrack.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EduTrack.Tests.Infrastructure
{
    // Har bir test sinfi uchun alohida SQLite in-memory baza. Ulanish ochiq
    // turgani uchun baza test tugaguncha saqlanadi.
    public sealed class TestDbFactory : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDbFactory(SqliteConnection connection, ApplicationDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public ApplicationDbContext Context { get; }

        public static async Task<TestDbFactory> CreateAsync(params IInterceptor[] interceptors)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection);

            if (interceptors.Length > 0)
            {
                optionsBuilder.AddInterceptors(interceptors);
            }

            var context = new ApplicationDbContext(optionsBuilder.Options);
            await context.Database.EnsureCreatedAsync();

            return new TestDbFactory(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
