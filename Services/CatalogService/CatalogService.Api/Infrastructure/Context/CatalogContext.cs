using CatalogService.Api.Core.Domain;
using CatalogService.Api.Infrastructure.EntityConfigurations;
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

        public CatalogContext(DbContextOptions<CatalogContext> options) : base(options)
        {
        }

        public DbSet<Expense> Expenses { get; set; }
        public DbSet<ReceiptItem> ReceiptItems { get; set; }
        public DbSet<ProductDetail> ProductDetails { get; set; }
        public DbSet<AccountingCode> AccountingCodes { get; set; }
        public DbSet<Personnel> Personnels { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {

            builder.ApplyConfiguration(new ExpenseEntityTypeConfiguration());
            builder.ApplyConfiguration(new ReceiptItemEntityTypeConfiguration());
            builder.ApplyConfiguration(new ProductDetailEntityTypeConfiguration());
            builder.ApplyConfiguration(new AccountingCodeEntityTypeConfiguration());
            builder.ApplyConfiguration(new PersonnelEntityTypeConfiguration());
        }

    }
}