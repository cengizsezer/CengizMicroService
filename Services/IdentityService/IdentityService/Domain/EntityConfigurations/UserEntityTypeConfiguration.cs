using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Domain.EntityConfigurations
{
    public class UserEntityTypeConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users", "identity");

            builder.HasKey(u => u.Id);

            builder.Property(u => u.Id)
                   .UseIdentityColumn(); // Otomatik artan ID

            builder.Property(u => u.Username)
                   .IsRequired()
                   .HasMaxLength(50);

            builder.Property(u => u.Email)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(u => u.PasswordHash)
                   .IsRequired();

            builder.Property(u => u.Role)
                   .IsRequired()
                   .HasMaxLength(20);

            builder.Property(u => u.RefreshToken)
                   .HasMaxLength(255);

            builder.Property(u => u.RefreshTokenExpiryTime)
                   .IsRequired();
        }
    }
}
