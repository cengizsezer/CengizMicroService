using FileApiService.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileApiService.Api.Infrastructure.Persistence.EntityConfigurations
{
    public class FileRecordEntityTypeConfiguration : IEntityTypeConfiguration<FileRecord>
    {
        public void Configure(EntityTypeBuilder<FileRecord> builder)
        {
            // tablo adı (schema gerekiyorsa ToTable("Files", "dosya") gibi ver)
            builder.ToTable("Files");

            // pk
            builder.HasKey(x => x.Id);

            // kolon kuralları
            builder.Property(x => x.CompanyId)
                   .IsRequired()
                   .HasMaxLength(64);

            builder.Property(x => x.Year)
                   .IsRequired()
                   .HasMaxLength(4);   // "2025"

            builder.Property(x => x.Month)
                   .IsRequired(false)
                   .HasMaxLength(2);   // "01".."12"

            builder.Property(x => x.DeclType)
                   .IsRequired()
                   .HasMaxLength(64);  // örn: "KDV1"

            builder.Property(x => x.DocType)
                   .IsRequired()
                   .HasMaxLength(64);  // örn: "Beyanname" / "Tahakkuk"

            builder.Property(x => x.Key)
                   .IsRequired()
                   .HasMaxLength(512); // MinIO object key

            builder.Property(x => x.FileName)
                   .IsRequired()
                   .HasMaxLength(255);

            builder.Property(x => x.ContentType)
                   .IsRequired()
                   .HasMaxLength(128)
                   .HasDefaultValue("application/pdf");

            builder.Property(x => x.Length)
                   .IsRequired();      // bigint

            builder.Property(x => x.CreatedAtUtc)
                   .IsRequired()
                   .HasDefaultValueSql("GETUTCDATE()");

            // indexler
            builder.HasIndex(x => new { x.CompanyId, x.Year, x.Month, x.DeclType, x.DocType });
            builder.HasIndex(x => x.Key).IsUnique();

            builder.Property(x => x.Description).HasMaxLength(512);   // opsiyonel
            builder.Property(x => x.SequenceNo);                      // int? zaten uygun
        }
    }
}
