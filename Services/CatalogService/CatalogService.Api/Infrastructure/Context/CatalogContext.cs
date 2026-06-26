using CatalogService.Api.Features.AccountPlan;
using CatalogService.Api.Features.Banka.Domain;
using CatalogService.Api.Features.Declarations.Entities;
using CatalogService.Api.Features.Education.Domain;
using CatalogService.Api.Features.Expenses.Domain;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.Jobs.Domain;
using CatalogService.Api.Features.KdvBeyanname.Domain;
using CatalogService.Api.Features.Mukellefler.Domain;
using CatalogService.Api.Features.Payment.Entities;
using CatalogService.Api.Features.PersonnelEmails.Domain;
using CatalogService.Api.Features.Payroll.Entities;
using CatalogService.Api.Features.Payroll.Persistence.Configurations;
using CatalogService.Api.Features.Vehicles.Domain;
using CatalogService.Api.Infrastructure.Accessor;
using CatalogService.Api.Infrastructure.Domain;
using CatalogService.Api.Infrastructure.EntityConfigurations;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace CatalogService.Api.Infrastructure.Context
{
    public class CatalogContext : DbContext
    {
        public const string DEFAULT_SCHEMA = "catalog";
        private readonly IHttpCurrentTenant _tenant;
        public CatalogContext(DbContextOptions<CatalogContext> options, IHttpCurrentTenant tenant): base(options) => _tenant = tenant;
       

        public DbSet<Expense> Expenses { get; set; }
        public DbSet<ReceiptItem> ReceiptItems { get; set; }
        public DbSet<ProductDetail> ProductDetails { get; set; }
        public DbSet<AccountingCode> AccountingCodes { get; set; }
        public DbSet<Personnel> Personnels { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<EducationItem> EducationItems { get; set; } = default!;
        public DbSet<Job> Jobs { get; set; } = default!;
        public DbSet<JobAssignment> JobAssignments { get; set; } = default!;

        // Personel mail eşlemesi (global; tenant'a bağlı değil)
        public DbSet<PersonnelEmail> PersonnelEmails => Set<PersonnelEmail>();
        public DbSet<Declaration> Declarations { get; set; }
        public DbSet<CustomerCompany> CustomerCompanies => Set<CustomerCompany>();
        public DbSet<AccountNode> AccountNodes => Set<AccountNode>();

        public DbSet<PayrollParameter> PayrollParameters => Set<PayrollParameter>();
        public DbSet<PayrollTaxBracket> PayrollTaxBrackets => Set<PayrollTaxBracket>();
        public DbSet<PayrollDisabilityExemption> PayrollDisabilityExemptions => Set<PayrollDisabilityExemption>();
        public DbSet<SeedHistory> SeedHistories => Set<SeedHistory>();
        public DbSet<TaxPaymentEntity> TaxPayments { get; set; }
        public DbSet<PayrollLawType> PayrollLawTypes { get; set; }

        public DbSet<Firma> Firmalar => Set<Firma>();
        public DbSet<Mukellef> Mukellefler => Set<Mukellef>();

        // Banka modülü (Banka Tanımları + Banka Takibi)
        public DbSet<Hesap> Hesaplar => Set<Hesap>();
        public DbSet<IslemKaydi> IslemKayitlari => Set<IslemKaydi>();
        public DbSet<Not> HesapNotlari => Set<Not>();

        // KDV Beyannamesi modülü
        public DbSet<KdvBeyannameTarama> KdvBeyannameTaramalar => Set<KdvBeyannameTarama>();
        public DbSet<GelenFatura> GelenFaturalar => Set<GelenFatura>();
        public DbSet<KdvBeyannameYevmiye> KdvBeyannameYevmiye => Set<KdvBeyannameYevmiye>();
        public DbSet<KdvBeyannameMizan> KdvBeyannameMizan => Set<KdvBeyannameMizan>();
        public DbSet<AppSetting> AppSettings => Set<AppSetting>();
        public DbSet<Duzenleyen> Duzenleyenler => Set<Duzenleyen>();

        // Ticaret Sicil İşlemleri modülü (ortak referans içerik; tenant'a bağlı değil)
        public DbSet<Features.TicaretSicil.Domain.TicaretSicilIslem> TicaretSicilIslemler => Set<Features.TicaretSicil.Domain.TicaretSicilIslem>();
        public DbSet<Features.TicaretSicil.Domain.TicaretSicilAdim> TicaretSicilAdimlar => Set<Features.TicaretSicil.Domain.TicaretSicilAdim>();
        public DbSet<Features.TicaretSicil.Domain.TicaretSicilEk> TicaretSicilEkler => Set<Features.TicaretSicil.Domain.TicaretSicilEk>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfiguration(new ExpenseEntityTypeConfiguration());
            builder.ApplyConfiguration(new ReceiptItemEntityTypeConfiguration());
            builder.ApplyConfiguration(new ProductDetailEntityTypeConfiguration());
            builder.ApplyConfiguration(new AccountingCodeEntityTypeConfiguration());
            builder.ApplyConfiguration(new PersonnelEntityTypeConfiguration());
            builder.ApplyConfiguration(new VehicleEntityTypeConfiguration());
            builder.ApplyConfiguration(new EducationItemEntityTypeConfiguration());
            builder.ApplyConfiguration(new JobEntityTypeConfiguration());
            builder.ApplyConfiguration(new JobAssignmentEntityTypeConfiguration());
            builder.ApplyConfiguration(new PersonnelEmailEntityTypeConfiguration());
            builder.ApplyConfiguration(new AccountNodesEntityTypeConfiguration());
            builder.ApplyConfiguration(new DeclarationEntityTypeConfiguration());
            builder.ApplyConfiguration(new CustomerCompanyTypeConfiguration());
            builder.ApplyConfiguration(new TaxPaymentConfiguration());
            builder.ApplyConfiguration(new FirmaEntityTypeConfiguration());
            builder.ApplyConfiguration(new MukellefEntityTypeConfiguration());

            // Banka modülü
            builder.ApplyConfiguration(new HesapEntityTypeConfiguration());
            builder.ApplyConfiguration(new IslemKaydiEntityTypeConfiguration());
            builder.ApplyConfiguration(new NotEntityTypeConfiguration());

            // KDV Beyannamesi modülü
            builder.ApplyConfiguration(new KdvBeyannameTaramaConfiguration());
            builder.ApplyConfiguration(new GelenFaturaConfiguration());
            builder.ApplyConfiguration(new KdvBeyannameYevmiyeConfiguration());
            builder.ApplyConfiguration(new KdvBeyannameMizanConfiguration());
            builder.ApplyConfiguration(new AppSettingConfiguration());
            builder.ApplyConfiguration(new DuzenleyenConfiguration());

            // Ticaret Sicil İşlemleri modülü
            builder.ApplyConfiguration(new TicaretSicilIslemEntityTypeConfiguration());
            builder.ApplyConfiguration(new TicaretSicilAdimEntityTypeConfiguration());
            builder.ApplyConfiguration(new TicaretSicilEkEntityTypeConfiguration());


            SetBuilderPKFConfiguration(builder);


            //AccountPlanSeed.Seed(builder);
            builder.Entity<Expense>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<ReceiptItem>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<ProductDetail>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<AccountingCode>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<Personnel>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<Job>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<CustomerCompany>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
        }


        public void SetBuilderPKFConfiguration(ModelBuilder builder)
        {
            builder.ApplyConfiguration(new PayrollDisabilityExemptionConfiguration());
            builder.ApplyConfiguration(new PayrollParameterConfiguration());
            builder.ApplyConfiguration(new PayrollTaxBracketConfiguration());
            builder.ApplyConfiguration(new SeedHistoryConfiguration());
            builder.ApplyConfiguration(new PayrollLawTypeConfiguration());
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