using Database.FileUploader.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Database.FileUploader
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<EFileAsset> FileAssets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EFileAsset>()
                .HasIndex(f => new { f.Name, f.Extension })
                .IsUnique(); // Enforce unique name + extension
        }
    }
}