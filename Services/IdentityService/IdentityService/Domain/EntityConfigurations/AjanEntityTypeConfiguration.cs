using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Domain.EntityConfigurations
{
    public class AjanEntityTypeConfiguration : IEntityTypeConfiguration<Ajan>
    {
        public void Configure(EntityTypeBuilder<Ajan> b)
        {
            b.ToTable("Ajanlar");

            b.Property(x => x.Ad).HasMaxLength(120).IsRequired();

            // Identity'nin PBKDF2 hash'i base64 olarak ~90 karakter; alan
            // algoritma değişirse diye geniş bırakıldı.
            b.Property(x => x.AnahtarHash).HasMaxLength(400).IsRequired();

            b.Property(x => x.AnahtarOnEki).HasMaxLength(16).IsRequired();
            b.Property(x => x.IptalNedeni).HasMaxLength(500);

            // Tekil değil: önek yalnız aday daraltmaya yarıyor, kimliği hash
            // belirliyor. İki anahtarın öneki çakışabilir.
            b.HasIndex(x => x.AnahtarOnEki);
        }
    }
}
