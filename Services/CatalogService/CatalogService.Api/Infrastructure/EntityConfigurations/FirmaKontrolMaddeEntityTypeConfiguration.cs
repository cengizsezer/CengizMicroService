using CatalogService.Api.Features.FirmaKontrol.Domain;
using CatalogService.Api.Features.Firmalar.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class FirmaKontrolMaddeEntityTypeConfiguration : IEntityTypeConfiguration<FirmaKontrolMadde>
    {
        public void Configure(EntityTypeBuilder<FirmaKontrolMadde> builder)
        {
            builder.ToTable("FirmaKontrolMaddeler");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.MaddeKey)
                .HasMaxLength(20);

            builder.Property(x => x.Category)
                .IsRequired()
                .HasMaxLength(60);

            // Soru metni sadece özel maddede dolu; şablon maddesi metni kodda kalır.
            builder.Property(x => x.SoruMetni)
                .HasMaxLength(2000);

            builder.HasOne(x => x.Firma)
                .WithMany()
                .HasForeignKey(x => x.FirmaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Şablon durum satırlarına (FirmaId, MaddeKey) ile tekil erişim.
            // MaddeKey nullable (özel maddeler) — filtered unique index ile sadece
            // şablon satırlarında tekillik zorlanır, özel maddeler kapsam dışı.
            builder.HasIndex(x => new { x.FirmaId, x.MaddeKey })
                .IsUnique()
                .HasFilter("[MaddeKey] IS NOT NULL");

            builder.HasIndex(x => x.FirmaId);
        }
    }
}
