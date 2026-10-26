using Generellem.Services;

using Microsoft.EntityFrameworkCore;

namespace Generellem.Repository;

public class GenerellemContext(IGenerellemFiles gemFiles) : DbContext
{
    public DbSet<DocumentHash> DocumentHashes { get; set; }

    public DbSet<VectorChunk> VectorChunks { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        string dbPath = gemFiles.GetAppDataPath("generellem.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath};Default Timeout=5");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VectorChunk>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
        });
    }
}
