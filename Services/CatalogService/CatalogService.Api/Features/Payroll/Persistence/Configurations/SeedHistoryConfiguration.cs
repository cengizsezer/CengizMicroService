using CatalogService.Api.Features.Payroll.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Features.Payroll.Persistence.Configurations
{
    public class SeedHistoryConfiguration : IEntityTypeConfiguration<SeedHistory>
    {
        public void Configure(EntityTypeBuilder<SeedHistory> builder)
        {
            builder.ToTable("SeedHistories", "pkf");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.SeedKey)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.Version)
                .IsRequired();

            builder.Property(x => x.AppliedAtUtc)
                .IsRequired();

            // 🔥 KRİTİK
            builder.HasIndex(x => x.SeedKey)
                .IsUnique();
        }
    }
}
