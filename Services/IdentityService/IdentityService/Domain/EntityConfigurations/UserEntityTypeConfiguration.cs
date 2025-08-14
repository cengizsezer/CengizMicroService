using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Domain.EntityConfigurations
{
    public class UserEntityTypeConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            // Identity'nin default tablosu zaten AspNetUsers, burada override edebilirsin:
            builder.ToTable("Users", "identity");

            // Identity zaten PK ve IDENTITY ayarını yapıyor, tekrar tanımlamana gerek yok.

            builder.Property(u => u.Role)
                   .HasMaxLength(20)
                   .IsRequired();

            builder.Property(u => u.RefreshToken)
                   .HasMaxLength(255);

            builder.Property(u => u.RefreshTokenExpiryTime)
                   .IsRequired();

            // Navigation property
            builder.HasMany(u => u.UserFirmalar)
                   .WithOne(uf => uf.User)
                   .HasForeignKey(uf => uf.UserId);
        }
    }
}
