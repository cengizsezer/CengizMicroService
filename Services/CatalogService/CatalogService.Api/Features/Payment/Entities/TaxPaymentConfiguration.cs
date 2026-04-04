using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Features.Payment.Entities
{
    public class TaxPaymentConfiguration : IEntityTypeConfiguration<TaxPaymentEntity>
    {
        public void Configure(EntityTypeBuilder<TaxPaymentEntity> builder)
        {
            builder.ToTable("TaxPayments", "pkf"); // schema pkf

            builder.HasKey(x => x.Id);

            builder.Property(x => x.TahakkukNo)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.TaxNumber)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.Amount)
                .HasPrecision(18, 2);

            builder.Property(x => x.TaxpayerName)
                .IsRequired()
                .HasMaxLength(250);

            builder.Property(x => x.TaxType)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.CreatedBy)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.Description)
                .HasMaxLength(500);

            // 🔥 EN KRİTİK KISIM (duplicate engelleme)
            builder.HasIndex(x => new { x.TahakkukNo, x.TaxNumber, x.TaxType })
                .IsUnique();
        }
    }
}
