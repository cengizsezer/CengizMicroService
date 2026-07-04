using CatalogService.Api.Features.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class JobEntityTypeConfiguration : IEntityTypeConfiguration<Job>
    {
        public void Configure(EntityTypeBuilder<Job> b)
        {
            b.ToTable("Jobs", "catalog");
            b.HasKey(x => x.Id);

            b.Property(x => x.Title).HasMaxLength(256).IsRequired();
            b.Property(x => x.Description).HasMaxLength(4000);
            b.Property(x => x.CreatedById).HasMaxLength(64).IsRequired();
            b.Property(x => x.CreatedByName).HasMaxLength(256);
            b.Property(x => x.TenantNo).IsRequired();

            b.HasMany(x => x.Assignments)
             .WithOne(x => x.Job)
             .HasForeignKey(x => x.JobId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(x => x.Attachments)
             .WithOne(x => x.Job)
             .HasForeignKey(x => x.JobId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => new { x.TenantNo, x.Start, x.End });
        }
    }
}
