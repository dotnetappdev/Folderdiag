using Microsoft.EntityFrameworkCore;
using FolderDiag.Core.Models;

namespace FolderDiag.Data
{
    public class FolderDiagDbContext : DbContext
    {
        public FolderDiagDbContext(DbContextOptions<FolderDiagDbContext> options) : base(options) { }

        public DbSet<FolderItem> FolderItems { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FolderItem>().HasKey(f => f.Id);
            base.OnModelCreating(modelBuilder);
        }
    }
}
