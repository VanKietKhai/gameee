using ConanServerControl.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

public class ActivityLogTests
{
    [Fact]
    public async Task Persists_and_returns_events_in_chronological_order()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "csc-tests", Guid.NewGuid().ToString("n"), "control.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        var factory = new TestFactory(options);
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
        }

        var log = new ActivityLogService(factory, NullLogger<ActivityLogService>.Instance);
        await log.AddAsync("Server", "Server started");
        await log.AddAsync("Server", "Server online");
        var recent = await log.GetRecentAsync(10);
        Assert.Equal(2, recent.Count);
        Assert.Equal("Server started", recent[0].Message);
        Assert.Equal("Server online", recent[1].Message);
    }

    private sealed class TestFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestFactory(DbContextOptions<AppDbContext> options) => _options = options;

        public AppDbContext CreateDbContext() => new(_options);

        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(CreateDbContext());
    }
}
