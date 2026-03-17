using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FolderDiag.Data;

public sealed class DatabaseInitializer
{
    private readonly AppDbContext _db;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(AppDbContext db, ILogger<DatabaseInitializer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.Database.MigrateAsync(ct);
            _logger.LogInformation("Database initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database initialization failed.");
            // Fall back to EnsureCreated
            try { await _db.Database.EnsureCreatedAsync(ct); }
            catch (Exception ex2)
            {
                _logger.LogError(ex2, "EnsureCreated also failed.");
            }
        }
    }
}
