using CatalogService.Api.Core.Base;
using CatalogService.Api.Core.Domain;
using CatalogService.Api.Infrastructure.EntityConfigurations;
using CatalogService.Api.Infrastructure.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CatalogService.Api.Infrastructure.Context
{
    public class CatalogContext : DbContext
    {
        public const string DEFAULT_SCHEMA = "catalog";
        private readonly ITenantAccessor _tenant;
        public CatalogContext(DbContextOptions<CatalogContext> options, ITenantAccessor tenant): base(options) => _tenant = tenant;
       

        public DbSet<Expense> Expenses { get; set; }
        public DbSet<ReceiptItem> ReceiptItems { get; set; }
        public DbSet<ProductDetail> ProductDetails { get; set; }
        public DbSet<AccountingCode> AccountingCodes { get; set; }
        public DbSet<Personnel> Personnels { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {

            builder.ApplyConfiguration(new ExpenseEntityTypeConfiguration());
            builder.ApplyConfiguration(new ReceiptItemEntityTypeConfiguration());
            builder.ApplyConfiguration(new ProductDetailEntityTypeConfiguration());
            builder.ApplyConfiguration(new AccountingCodeEntityTypeConfiguration());
            builder.ApplyConfiguration(new PersonnelEntityTypeConfiguration());
            builder.ApplyConfiguration(new VehicleEntityTypeConfiguration());

            builder.Entity<Expense>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<ReceiptItem>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<ProductDetail>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<AccountingCode>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<Personnel>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
        }

        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            foreach (var e in ChangeTracker.Entries<TenantEntity>()
                                           .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
            {
                // Trimle
                e.Entity.TenantNo = e.Entity.TenantNo?.Trim();

                if (string.IsNullOrWhiteSpace(e.Entity.TenantNo))
                {
                    var current = _tenant.CurrentTenantNo?.Trim();
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        e.Entity.TenantNo = current;         // HTTP isteği varsa doldur
                    }
                    else
                    {
                        // Seed veya başka yoldan dolu verilmediyse, engelle:
                        throw new InvalidOperationException("TenantNo boş; kayıt engellendi.");
                    }
                }
            }
            return base.SaveChangesAsync(ct);
        }





    }
}