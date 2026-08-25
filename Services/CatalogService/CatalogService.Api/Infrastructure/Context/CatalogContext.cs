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
        public DbSet<JobAttachment> JobAttachments { get; set; } = default!;

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
        public DbSet<GelenFaturaPdf> GelenFaturaPdfleri => Set<GelenFaturaPdf>();
        public DbSet<AppSetting> AppSettings => Set<AppSetting>();
        public DbSet<Duzenleyen> Duzenleyenler => Set<Duzenleyen>();

        // Firma Kontrol / Raporlar modülü — kontrol maddelerinin firma-bazlı durumu + özel maddeler
        public DbSet<Features.FirmaKontrol.Domain.FirmaKontrolMadde> FirmaKontrolMaddeler => Set<Features.FirmaKontrol.Domain.FirmaKontrolMadde>();

        // Firma Kontrol / Raporlar modülü — ham mizan satırları (firma + dönem + yıl bazında)
        public DbSet<Features.FirmaKontrol.Domain.FirmaKontrolMizanSatir> FirmaKontrolMizanSatirlari => Set<Features.FirmaKontrol.Domain.FirmaKontrolMizanSatir>();

        // Firma Kontrol / Raporlar modülü — mizan hesap satırlarına yazılan gerekçe notları
        public DbSet<Features.FirmaKontrol.Domain.MizanNotu> MizanNotlari => Set<Features.FirmaKontrol.Domain.MizanNotu>();

        // Firma Kontrol / Raporlar modülü — vergi paneli girdileri (firma + dönem + yıl bazında)
        public DbSet<Features.FirmaKontrol.Domain.FirmaKontrolVergi> FirmaKontrolVergiler => Set<Features.FirmaKontrol.Domain.FirmaKontrolVergi>();

        // Firma Kontrol / Kurumlar vergisi beyannamesi — kalem katalogu (firmadan bağımsız)
        public DbSet<Features.FirmaKontrol.Domain.VergiKalemi> VergiKalemleri => Set<Features.FirmaKontrol.Domain.VergiKalemi>();

        // Firma Kontrol / Kurumlar vergisi beyannamesi — firma + dönem bazında girdiler
        public DbSet<Features.FirmaKontrol.Domain.VergiHesaplama> VergiHesaplamalar => Set<Features.FirmaKontrol.Domain.VergiHesaplama>();
        public DbSet<Features.FirmaKontrol.Domain.VergiHesaplamaSatir> VergiHesaplamaSatirlari => Set<Features.FirmaKontrol.Domain.VergiHesaplamaSatir>();
        public DbSet<Features.FirmaKontrol.Domain.GecmisYilZarari> GecmisYilZararlari => Set<Features.FirmaKontrol.Domain.GecmisYilZarari>();

        // Ticaret Sicil İşlemleri modülü (ortak referans içerik; tenant'a bağlı değil)
        public DbSet<Features.TicaretSicil.Domain.TicaretSicilIslem> TicaretSicilIslemler => Set<Features.TicaretSicil.Domain.TicaretSicilIslem>();
        public DbSet<Features.TicaretSicil.Domain.TicaretSicilAdim> TicaretSicilAdimlar => Set<Features.TicaretSicil.Domain.TicaretSicilAdim>();
        public DbSet<Features.TicaretSicil.Domain.TicaretSicilEk> TicaretSicilEkler => Set<Features.TicaretSicil.Domain.TicaretSicilEk>();

        // Mevzuat Notları modülü (ortak referans içerik; tenant'a bağlı değil)
        public DbSet<Features.MevzuatNotlari.Domain.MevzuatNotu> MevzuatNotlari => Set<Features.MevzuatNotlari.Domain.MevzuatNotu>();

        // SMMM Takip modülü (ortak referans içerik; tenant'a bağlı değil)
        public DbSet<Features.SmmmTakip.Domain.SmmmKonu> SmmmKonular => Set<Features.SmmmTakip.Domain.SmmmKonu>();
        public DbSet<Features.SmmmTakip.Domain.SmmmHad> SmmmHadler => Set<Features.SmmmTakip.Domain.SmmmHad>();
        public DbSet<Features.SmmmTakip.Domain.SmmmHadDegeri> SmmmHadDegerleri => Set<Features.SmmmTakip.Domain.SmmmHadDegeri>();

        // Muhasebe modülü (Hesap Planı + Fiş/Yevmiye) — firma (tenant) bazlı
        public DbSet<Features.Muhasebe.Domain.KodMaskesi> KodMaskeleri => Set<Features.Muhasebe.Domain.KodMaskesi>();
        public DbSet<Features.Muhasebe.Domain.HesapPlani> HesapPlanlari => Set<Features.Muhasebe.Domain.HesapPlani>();
        public DbSet<Features.Muhasebe.Domain.MasrafMerkezi> MasrafMerkezleri => Set<Features.Muhasebe.Domain.MasrafMerkezi>();
        public DbSet<Features.Muhasebe.Domain.Fis> Fisler => Set<Features.Muhasebe.Domain.Fis>();
        public DbSet<Features.Muhasebe.Domain.FisSatir> FisSatirlar => Set<Features.Muhasebe.Domain.FisSatir>();

        // Banka Ekstresi İşleme modülü — firma bazlı tablolar (catalog.Firmalar.Id)
        public DbSet<Features.BankaEkstre.Domain.BankaHesabi> EkstreBankaHesaplari => Set<Features.BankaEkstre.Domain.BankaHesabi>();
        public DbSet<Features.BankaEkstre.Domain.EkstreYukleme> EkstreYuklemeler => Set<Features.BankaEkstre.Domain.EkstreYukleme>();
        public DbSet<Features.BankaEkstre.Domain.EkstreSatiri> EkstreSatirlari => Set<Features.BankaEkstre.Domain.EkstreSatiri>();
        public DbSet<Features.BankaEkstre.Domain.HesapEslesmesi> EkstreHesapEslesmeleri => Set<Features.BankaEkstre.Domain.HesapEslesmesi>();
        public DbSet<Features.BankaEkstre.Domain.HesapPlaniKaydi> EkstreHesapPlani => Set<Features.BankaEkstre.Domain.HesapPlaniKaydi>();

        /// <summary>Kişi → hesap yönlendirmeleri; kimin ortak, kimin personel olduğu firmaya özeldir.</summary>
        public DbSet<Features.BankaEkstre.Domain.KisiYonlendirme> EkstreKisiYonlendirmeleri => Set<Features.BankaEkstre.Domain.KisiYonlendirme>();

        // Banka Ekstresi İşleme modülü — global (firma bağımsız) içerik.
        // Bir unvanın kim olduğu her firmada aynıdır; hangi koda gittiği firmaya özeldir.
        public DbSet<Features.BankaEkstre.Domain.KimlikKaydi> EkstreKimlikKayitlari => Set<Features.BankaEkstre.Domain.KimlikKaydi>();

        // Banka Ekstresi İşleme modülü — banka bazlı yapılandırma (tenant'tan bağımsız referans içerik)
        public DbSet<Features.BankaEkstre.Domain.AciklamaSablonu> EkstreAciklamaSablonlari => Set<Features.BankaEkstre.Domain.AciklamaSablonu>();
        public DbSet<Features.BankaEkstre.Domain.UnvanDeseni> EkstreUnvanDesenleri => Set<Features.BankaEkstre.Domain.UnvanDeseni>();
        public DbSet<Features.BankaEkstre.Domain.SabitKural> EkstreSabitKurallar => Set<Features.BankaEkstre.Domain.SabitKural>();

        /// <summary>Vergi kodu / anahtar kelime → hesap eşlemesi; vergi kodları firmadan firmaya değişmez.</summary>
        public DbSet<Features.BankaEkstre.Domain.VergiKoduEslemesi> EkstreVergiKodlari => Set<Features.BankaEkstre.Domain.VergiKoduEslemesi>();

        /// <summary>
        /// İşlem kategorileri (global). Kuralların muhasebe sınıflandırması; eşleştirme
        /// kararına girmez, yalnız etiket ve görünüm.
        /// </summary>
        public DbSet<Features.BankaEkstre.Domain.IslemKategorisi> EkstreIslemKategorileri => Set<Features.BankaEkstre.Domain.IslemKategorisi>();

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
            builder.ApplyConfiguration(new JobAttachmentEntityTypeConfiguration());
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
            builder.ApplyConfiguration(new GelenFaturaPdfConfiguration());
            builder.ApplyConfiguration(new AppSettingConfiguration());
            builder.ApplyConfiguration(new DuzenleyenConfiguration());

            // Firma Kontrol / Raporlar modülü
            builder.ApplyConfiguration(new FirmaKontrolMaddeEntityTypeConfiguration());
            builder.ApplyConfiguration(new FirmaKontrolMizanSatirEntityTypeConfiguration());
            builder.ApplyConfiguration(new MizanNotuEntityTypeConfiguration());
            builder.ApplyConfiguration(new FirmaKontrolVergiEntityTypeConfiguration());
            builder.ApplyConfiguration(new VergiKalemiEntityTypeConfiguration());
            builder.ApplyConfiguration(new VergiHesaplamaEntityTypeConfiguration());
            builder.ApplyConfiguration(new VergiHesaplamaSatirEntityTypeConfiguration());
            builder.ApplyConfiguration(new GecmisYilZarariEntityTypeConfiguration());

            // Ticaret Sicil İşlemleri modülü
            builder.ApplyConfiguration(new TicaretSicilIslemEntityTypeConfiguration());
            builder.ApplyConfiguration(new TicaretSicilAdimEntityTypeConfiguration());
            builder.ApplyConfiguration(new TicaretSicilEkEntityTypeConfiguration());

            // Mevzuat Notları modülü
            builder.ApplyConfiguration(new MevzuatNotuEntityTypeConfiguration());

            // SMMM Takip modülü
            builder.ApplyConfiguration(new SmmmKonuEntityTypeConfiguration());
            builder.ApplyConfiguration(new SmmmHadEntityTypeConfiguration());
            builder.ApplyConfiguration(new SmmmHadDegeriEntityTypeConfiguration());

            // Muhasebe modülü (Hesap Planı + Fiş)
            builder.ApplyConfiguration(new KodMaskesiEntityTypeConfiguration());
            builder.ApplyConfiguration(new HesapPlaniEntityTypeConfiguration());
            builder.ApplyConfiguration(new MasrafMerkeziEntityTypeConfiguration());
            builder.ApplyConfiguration(new FisEntityTypeConfiguration());
            builder.ApplyConfiguration(new FisSatirEntityTypeConfiguration());

            // Banka Ekstresi İşleme modülü
            builder.ApplyConfiguration(new BankaHesabiEntityTypeConfiguration());
            builder.ApplyConfiguration(new EkstreYuklemeEntityTypeConfiguration());
            builder.ApplyConfiguration(new EkstreSatiriEntityTypeConfiguration());
            builder.ApplyConfiguration(new HesapEslesmesiEntityTypeConfiguration());
            builder.ApplyConfiguration(new KimlikKaydiEntityTypeConfiguration());
            builder.ApplyConfiguration(new HesapPlaniKaydiEntityTypeConfiguration());
            builder.ApplyConfiguration(new AciklamaSablonuEntityTypeConfiguration());
            builder.ApplyConfiguration(new UnvanDeseniEntityTypeConfiguration());
            builder.ApplyConfiguration(new SabitKuralEntityTypeConfiguration());
            builder.ApplyConfiguration(new VergiKoduEslemesiEntityTypeConfiguration());
            builder.ApplyConfiguration(new KisiYonlendirmeEntityTypeConfiguration());
            builder.ApplyConfiguration(new IslemKategorisiEntityTypeConfiguration());


            SetBuilderPKFConfiguration(builder);


            //AccountPlanSeed.Seed(builder);
            builder.Entity<Expense>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<ReceiptItem>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<ProductDetail>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<AccountingCode>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<Personnel>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<Job>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<CustomerCompany>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);

            // Muhasebe modülü tenant filtreleri (FisSatir, bağlı olduğu Fis üzerinden izole olur)
            builder.Entity<Features.Muhasebe.Domain.KodMaskesi>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<Features.Muhasebe.Domain.HesapPlani>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<Features.Muhasebe.Domain.MasrafMerkezi>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);
            builder.Entity<Features.Muhasebe.Domain.Fis>().HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo);

            // Banka Ekstresi modülünün KAPSAMI TENANT DEĞİL FİRMADIR ve global query filter
            // kurulmaz — sorgular kapsamı görünür biçimde yazar (bkz. IBankaFirmaKapsami,
            // KARARLAR §69). Görünmez filtre iki sorun üretiyordu: (1) tek oturumla sekiz
            // firmayı yöneten kullanıcıda token'daki tek tenant tüm firmaları aynı kovaya
            // yazıyordu, (2) firma seçim ekranının sayaçları gibi meşru çoklu-firma
            // sorguları IgnoreQueryFilters() baypasına mecbur kalıyordu.
            // EkstreSatiri'nin kendi FirmaId'si yok; kapsamı bağlı olduğu EkstreYukleme
            // üzerinden alır (Muhasebe'deki FisSatir ile aynı yaklaşım).
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
            // Firma kapsamlı kayıtlarda FirmaId'nin sessiz varsayılanı YOKTUR. Tenant'ta
            // olduğu gibi "boşsa istekten doldur" davranışı buraya konmadı: kapsamı yazan
            // yer servis katmanıdır ve unutulursa kayıt yanlış firmaya değil, hiçbir yere
            // gitmemeli. Modül bu hatayı bir kez tenant tarafında yaptı (KARARLAR §68).
            foreach (var e in ChangeTracker.Entries<FirmaKapsamliEntity>()
                                           .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
            {
                if (e.Entity.FirmaId <= 0)
                    throw new InvalidOperationException(
                        $"FirmaId boş; {e.Entity.GetType().Name} kaydı engellendi. " +
                        "Banka otomasyon kayıtları firma kapsamı olmadan yazılamaz.");
            }

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