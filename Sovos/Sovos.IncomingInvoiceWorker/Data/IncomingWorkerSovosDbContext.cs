using Microsoft.EntityFrameworkCore;
using Sovos.InvoiceWorker.Core.Entities;

namespace Sovos.IncomingInvoiceWorker.Data;

// SovosService.Api ve Sovos.InvoiceWorker ile aynı SovosCompanies tablosuna read-only
// erişim için minimum DbContext. Invoice tablosu burada gerekmiyor (mail gönderimi yok).
public class IncomingWorkerSovosDbContext : DbContext
{
    public IncomingWorkerSovosDbContext(DbContextOptions<IncomingWorkerSovosDbContext> options)
        : base(options) { }

    public DbSet<Company> Companies => Set<Company>();

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

            // Bu DbContext'te Invoices yok — navigation property'i map dışı bırak.
            e.Ignore(x => x.Invoices);
        });
    }
}
