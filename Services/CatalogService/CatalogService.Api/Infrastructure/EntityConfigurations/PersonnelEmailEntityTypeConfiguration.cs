using CatalogService.Api.Features.PersonnelEmails.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class PersonnelEmailEntityTypeConfiguration : IEntityTypeConfiguration<PersonnelEmail>
    {
        public void Configure(EntityTypeBuilder<PersonnelEmail> b)
        {
            b.ToTable("PersonnelEmails", "catalog");

            // UserId doğal anahtar (her kullanıcı için tek satır → kolay upsert)
            b.HasKey(x => x.UserId);
            b.Property(x => x.UserId).HasMaxLength(64).IsRequired();

            b.Property(x => x.UserName).HasMaxLength(256);
            b.Property(x => x.Email).HasMaxLength(256);
        }
    }
}
