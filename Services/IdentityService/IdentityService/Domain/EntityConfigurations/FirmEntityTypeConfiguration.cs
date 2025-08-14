using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Domain.EntityConfigurations
{
    public class FirmEntityTypeConfiguration : IEntityTypeConfiguration<Firm>
    {
        public void Configure(EntityTypeBuilder<Firm> builder)
        {
            builder.ToTable("Firmalar", "identity");

            builder.HasKey(f => f.Id);

            builder.Property(f => f.Ad)
                   .HasMaxLength(200)
                   .IsRequired();

            builder.Property(f => f.Vkn)
                   .HasMaxLength(20);

            builder.Property(f => f.FirmaNo)
                   .HasMaxLength(20)
                   .IsRequired();

            builder.HasIndex(f => f.FirmaNo).IsUnique();

            builder.HasMany(f => f.UserFirmalar)
                   .WithOne(uf => uf.Firma)
                   .HasForeignKey(uf => uf.FirmaId);
        }
    }
}
