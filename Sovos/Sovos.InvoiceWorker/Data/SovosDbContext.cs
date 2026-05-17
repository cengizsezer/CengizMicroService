using Microsoft.EntityFrameworkCore;
using Sovos.InvoiceWorker.Core.Entities;

namespace Sovos.InvoiceWorker.Data;

public class SovosDbContext : DbContext
{
    public SovosDbContext(DbContextOptions<SovosDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(e =>
        {
            e.ToTable("SovosCompanies");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.CompanyCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.Property(x => x.EncryptedPassword).HasMaxLength(1000).IsRequired();
            e.Property(x => x.NotificationEmails).HasMaxLength(200).IsRequired();
            e.Property(x => x.LastErrorMessage).HasMaxLength(2000);
            e.Property(x => x.ScheduleMode).IsRequired();
            e.Property(x => x.ScheduleHour);
            e.Property(x => x.FirmaId);
            e.HasIndex(x => x.FirmaId);
        });

        modelBuilder.Entity<Invoice>(e =>
        {
            e.ToTable("SovosInvoices");
            e.HasKey(x => x.Id);
            e.Property(x => x.FaturaNo).HasMaxLength(50).IsRequired();
            e.Property(x => x.GondericiVkn).HasMaxLength(20).IsRequired();
            e.Property(x => x.FirmaUnvani).HasMaxLength(300);
            e.Property(x => x.ParaBirimi).HasMaxLength(10);
            e.Property(x => x.SiparisNo).HasMaxLength(100);
            e.Property(x => x.FaturaTutari).HasPrecision(18, 2);
            e.Property(x => x.ToplamVergi).HasPrecision(18, 2);
            e.Property(x => x.IskontoTutari).HasPrecision(18, 2);
            e.Property(x => x.Artirim).HasPrecision(18, 2);
            e.HasIndex(x => new { x.CompanyId, x.FaturaNo, x.GondericiVkn }).IsUnique();
            e.HasOne(x => x.Company).WithMany(x => x.Invoices).HasForeignKey(x => x.CompanyId);
        });
    }
}
