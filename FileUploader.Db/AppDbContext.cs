using FileUploader.Db.Entities;
using Microsoft.EntityFrameworkCore;

namespace FileUploader.Db
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<EFileAsset> FileAssets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<EFileAsset>()
            .HasIndex(f => new { f.Name, f.Extension })
            .IsUnique();

            modelBuilder.HasDefaultSchema("public");
        }
    }
}