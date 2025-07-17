using CatalogService.Api.Core.Domain;
using CatalogService.Api.Core.Domain.Entities;
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

        public DbSet<CatalogItem> CatalogItems { get; set; }
        public DbSet<CatalogBrand> CatalogBrands { get; set; }
        public DbSet<CatalogType> CatalogTypes { get; set; }

        public DbSet<Expense> Expenses { get; set; }
        public DbSet<ReceiptItem> ReceiptItems { get; set; }
        public DbSet<ProductDetail> ProductDetails { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.ApplyConfiguration(new CatalogBrandEntityTypeConfiguration());
            builder.ApplyConfiguration(new CatalogItemEntityTypeConfiguration());
            builder.ApplyConfiguration(new CatalogTypeEntityTypeConfiguration());


            builder.ApplyConfiguration(new ExpenseEntityTypeConfiguration());
            builder.ApplyConfiguration(new ReceiptItemEntityTypeConfiguration());
            builder.ApplyConfiguration(new ProductDetailEntityTypeConfiguration());
        }

    }
}