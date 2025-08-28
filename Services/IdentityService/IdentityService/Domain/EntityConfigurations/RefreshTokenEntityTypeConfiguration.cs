using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Domain.EntityConfigurations
{
    public class RefreshTokenEntityTypeConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> b)
        {
            b.ToTable("RefreshTokens");
            b.HasIndex(x => x.Token).IsUnique();
            b.Property(x => x.Token).HasMaxLength(200).IsRequired();
            b.Property(x => x.DeviceId).HasMaxLength(64);
            b.Property(x => x.IpAddress).HasMaxLength(64);
        }
    }
}
