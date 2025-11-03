using CatalogService.Api.Features.Vehicles.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class VehicleEntityTypeConfiguration : IEntityTypeConfiguration<Vehicle>
    {
        public void Configure(EntityTypeBuilder<Vehicle> builder)
        {
            // Tablo adı
            builder.ToTable("Vehicles");

            // Primary Key
            builder.HasKey(v => v.Id);

            // Plaka unique olsun
            builder.HasIndex(v => v.Plate)
                   .IsUnique();

            // Kolon ayarları
            builder.Property(v => v.Plate)
                   .IsRequired()
                   .HasMaxLength(20);

            builder.Property(v => v.Driver)
                   .HasMaxLength(100);

            builder.Property(v => v.Unit)
                   .HasMaxLength(100);

            builder.Property(v => v.Department)
                   .HasMaxLength(100);

            builder.Property(v => v.Description1)
                   .HasMaxLength(200);

            builder.Property(v => v.Region)
                   .HasMaxLength(100);

            builder.Property(v => v.Description2)
                   .HasMaxLength(200);

            builder.Property(v => v.Type)
                   .HasMaxLength(50);

            builder.Property(v => v.Brand)
                   .HasMaxLength(50);

            builder.Property(v => v.Model)
                   .HasMaxLength(50);

            builder.Property(v => v.Gear)
                   .HasMaxLength(20);

            builder.Property(v => v.Fuel)
                   .HasMaxLength(20);

            builder.Property(v => v.Fleet)
                   .HasMaxLength(100);
        }
    }
}
