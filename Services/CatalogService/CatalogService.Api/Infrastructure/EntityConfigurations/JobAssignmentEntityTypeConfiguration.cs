using CatalogService.Api.Features.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class JobAssignmentEntityTypeConfiguration : IEntityTypeConfiguration<JobAssignment>
    {
        public void Configure(EntityTypeBuilder<JobAssignment> b)
        {
            b.ToTable("JobAssignments", "catalog");
            b.HasKey(x => x.Id);

            b.Property(x => x.UserId).HasMaxLength(64).IsRequired();
            b.Property(x => x.UserName).HasMaxLength(256).IsRequired();

            // Aynı işe aynı kullanıcı bir kez atanır
            b.HasIndex(x => new { x.JobId, x.UserId }).IsUnique();

            b.HasIndex(x => x.UserId);
        }
    }
}
