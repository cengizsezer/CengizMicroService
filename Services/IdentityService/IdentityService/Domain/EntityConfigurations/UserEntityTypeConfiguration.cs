using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Domain.EntityConfigurations
{
    public class UserEntityTypeConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            // AspNetUsers yerine özelleştirilmiş tablo adı/şema:
            builder.ToTable("Users", schema: "identity");

            // IdentityUser<int> için PK/indices Identity tarafından zaten tanımlanıyor.
            // Ek alan(lar) varsa burada konfigure edebilirsin.

            // Legacy role alanını tuttuysan (opsiyonel):
            builder.Property(u => u.LegacyRole).HasMaxLength(64);

            builder.HasMany(u => u.UserTenants)
                   .WithOne(ut => ut.User)
                   .HasForeignKey(ut => ut.UserId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
