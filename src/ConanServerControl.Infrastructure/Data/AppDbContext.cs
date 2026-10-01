using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<ActivityLogRow> ActivityLog => Set<ActivityLogRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActivityLogRow>(entity =>
        {
            entity.ToTable("activity_log");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Category).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Actor).HasMaxLength(128);
            entity.Property(e => e.Details).HasMaxLength(4000);
            entity.Property(e => e.TimestampUtc).IsRequired();
            entity.HasIndex(e => e.TimestampUtc);
        });
    }
}

public sealed class ActivityLogRow
{
    public int Id { get; set; }

    public DateTime TimestampUtc { get; set; }

    public string Category { get; set; } = "General";

    public string Message { get; set; } = string.Empty;

    public string? Actor { get; set; }

    public string? Details { get; set; }
}

public sealed class ActivityLogService : IActivityLog
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger<ActivityLogService> _logger;

    public ActivityLogService(IDbContextFactory<AppDbContext> factory, ILogger<ActivityLogService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task AddAsync(
        string category,
        string message,
        string? actor = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            db.ActivityLog.Add(new ActivityLogRow
            {
                TimestampUtc = DateTime.UtcNow,
                Category = Truncate(category, 64),
                Message = Truncate(message, 2000),
                Actor = Truncate(actor, 128),
                Details = Truncate(details, 4000)
            });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("{Category}: {Message}", category, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist activity event: {Message}", message);
        }
    }

    public async Task<IReadOnlyList<ActivityLogEntry>> GetRecentAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.ActivityLog
            .OrderByDescending(r => r.TimestampUtc)
            .Take(Math.Clamp(count, 1, 500))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .OrderBy(r => r.TimestampUtc)
            .Select(r => new ActivityLogEntry
            {
                Id = r.Id,
                Timestamp = new DateTimeOffset(DateTime.SpecifyKind(r.TimestampUtc, DateTimeKind.Utc)),
                Category = r.Category,
                Message = r.Message,
                Actor = r.Actor,
                Details = r.Details
            })
            .ToArray();
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        return value.Length <= max ? value : value[..max];
    }
}
