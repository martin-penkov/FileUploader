using FileUploader.Db.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FileUploader.Db
{
    public class AppDbContext : IdentityDbContext<EUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<EFileAsset> FileAssets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<EFileAsset>()
                .HasIndex(f => new { f.FullName })
                .IsUnique(); // Enforces unique name + extension

            modelBuilder.HasDefaultSchema("public");
        }
    }
}