using FileApiService.Api.Infrastructure.Persistence.Entities;
using FileApiService.Api.Infrastructure.Persistence.EntityConfigurations;
using Microsoft.EntityFrameworkCore;

public class FileDbContext : DbContext
{
    public FileDbContext(DbContextOptions<FileDbContext> options) : base(options) { }

    public DbSet<FileRecord> Files => Set<FileRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new FileRecordEntityTypeConfiguration());
    }
}
