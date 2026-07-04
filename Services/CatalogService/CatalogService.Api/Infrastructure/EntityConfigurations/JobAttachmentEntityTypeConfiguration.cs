using CatalogService.Api.Features.Jobs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class JobAttachmentEntityTypeConfiguration : IEntityTypeConfiguration<JobAttachment>
    {
        public void Configure(EntityTypeBuilder<JobAttachment> b)
        {
            b.ToTable("JobAttachments", "catalog");
            b.HasKey(x => x.Id);

            b.Property(x => x.FileName)
             .IsRequired()
             .HasMaxLength(255);

            b.Property(x => x.ContentType)
             .IsRequired()
             .HasMaxLength(128)
             .HasDefaultValue("application/octet-stream");

            b.Property(x => x.Tur)
             .HasConversion<int>();

            b.Property(x => x.Not)
             .HasMaxLength(2000);

            b.HasIndex(x => x.JobId);
        }
    }
}
