using CatalogService.Api.Features.FinansmanGiderKisitlamasi.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class FinansmanKisitlamaOraniEntityTypeConfiguration : IEntityTypeConfiguration<FinansmanKisitlamaOrani>
    {
        public void Configure(EntityTypeBuilder<FinansmanKisitlamaOrani> builder)
        {
            builder.ToTable("FinansmanKisitlamaOranlari", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Yil)
                   .IsRequired();

            // Yüzde tutuluyor (10 = %10); binde/on binde oranlara yer kalsın diye 4 hane.
            builder.Property(x => x.Oran)
                   .HasColumnType("decimal(18,4)");

            builder.Property(x => x.Dayanak)
                   .HasMaxLength(200);

            builder.Property(x => x.Not)
                   .HasMaxLength(1000);

            // Yıl başına tek oran.
            builder.HasIndex(x => x.Yil).IsUnique();
        }
    }
}
