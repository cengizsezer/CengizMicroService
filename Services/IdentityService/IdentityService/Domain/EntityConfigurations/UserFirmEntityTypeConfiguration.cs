using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Domain.EntityConfigurations
{
    public class UserFirmEntityTypeConfiguration : IEntityTypeConfiguration<UserFirm>
    {
        public void Configure(EntityTypeBuilder<UserFirm> builder)
        {
            builder.ToTable("UserFirmalar", "identity");

            builder.HasKey(uf => new { uf.UserId, uf.FirmaId });

            builder.HasOne(uf => uf.User)
                   .WithMany(u => u.UserFirmalar)
                   .HasForeignKey(uf => uf.UserId);

            builder.HasOne(uf => uf.Firma)
                   .WithMany(f => f.UserFirmalar)
                   .HasForeignKey(uf => uf.FirmaId);
        }
    }
}
