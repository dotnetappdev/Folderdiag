using Microsoft.EntityFrameworkCore;
using FolderDiag.Core.Models;

namespace FolderDiag.Data
{
    public class FolderDiagDbContext : DbContext
    {
        public FolderDiagDbContext(DbContextOptions<FolderDiagDbContext> options) : base(options) { }

        public DbSet<FolderItem> FolderItems { get; set; } = null!;
        public DbSet<FileItem> FileItems { get; set; } = null!;
        public DbSet<ScanSnapshot> ScanSnapshots { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FolderItem>().HasKey(f => f.Id);
            modelBuilder.Entity<FolderItem>().Ignore(f => f.SizeFormatted);
            modelBuilder.Entity<FolderItem>().Ignore(f => f.AllocatedFormatted);
            modelBuilder.Entity<FolderItem>().Ignore(f => f.FileCountFormatted);
            modelBuilder.Entity<FolderItem>().Ignore(f => f.FolderCountFormatted);
            modelBuilder.Entity<FolderItem>().Ignore(f => f.PercentFormatted);

            modelBuilder.Entity<FileItem>().HasKey(f => f.Id);
            modelBuilder.Entity<FileItem>().Ignore(f => f.SizeFormatted);
            modelBuilder.Entity<FileItem>().Ignore(f => f.Category);
            modelBuilder.Entity<FileItem>().Ignore(f => f.AgeInDays);

            modelBuilder.Entity<ScanSnapshot>().HasKey(s => s.Id);
            modelBuilder.Entity<ScanSnapshot>().Ignore(s => s.TotalSizeFormatted);
            modelBuilder.Entity<ScanSnapshot>().Ignore(s => s.CreatedFormatted);

            base.OnModelCreating(modelBuilder);
        }
    }
}
