using FolderDiag.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FolderDiag.Data;

public sealed class AppDbContext : DbContext
{
    public DbSet<ScanHistoryEntity> ScanHistory => Set<ScanHistoryEntity>();
    public DbSet<SettingEntity> Settings => Set<SettingEntity>();
    public DbSet<FileIndexEntity> FileIndex => Set<FileIndexEntity>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FileIndexEntity>(e =>
        {
            e.HasIndex(x => x.Name);
            e.HasIndex(x => new { x.ScanHistoryId, x.IsDirectory });
            e.HasIndex(x => x.Extension);
            e.HasIndex(x => x.LastModified);
            e.HasIndex(x => x.Size);
        });

        modelBuilder.Entity<ScanHistoryEntity>(e =>
        {
            e.HasIndex(x => x.RootPath);
            e.HasIndex(x => x.ScanTime);
        });

        modelBuilder.Entity<SettingEntity>(e =>
        {
            e.HasKey(x => x.Key);
        });
    }
}
