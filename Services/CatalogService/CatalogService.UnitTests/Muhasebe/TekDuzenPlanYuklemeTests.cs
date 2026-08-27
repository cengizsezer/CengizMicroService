using CatalogService.Api.Features.Muhasebe;
using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Features.Muhasebe.Services;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.Muhasebe
{
    /// <summary>
    /// "Tek düzen hesap planını yükle" düğmesinin arkasındaki yükleme.
    ///
    /// Açılıştaki seed yalnız <c>Program.cs</c>'teki sabit tenant listesi için çalışıyordu;
    /// listede olmayan firma planssız kalıyor ve ekran sessizce boş görünüyordu
    /// (KARARLAR §83). Yükleme artık istekten de tetiklenebiliyor ve üç sonucu da açıkça
    /// bildiriyor (§84).
    /// </summary>
    public class TekDuzenPlanYuklemeTests
    {
        [Fact]
        public async Task Bos_plana_yukleme_yapilir_ve_agac_kurulur()
        {
            using var db = MuhasebeTestOrtami.YeniContext();

            var (sonuc, adet) = await MuhasebeTestOrtami.HesapPlaniServisi(db).TekDuzenPlaniYukleAsync();

            Assert.Equal(PlanYuklemeSonuc.Yuklendi, sonuc);
            Assert.Equal(7, adet);

            var hesaplar = await db.HesapPlanlari.OrderBy(h => h.KodDuz).ToListAsync();
            Assert.Equal(7, hesaplar.Count);

            // Ağaç kurulmuş: kökler üstsüz, kebirler grubun altında.
            var sinif1 = hesaplar.Single(h => h.Kod == "1");
            var grup10 = hesaplar.Single(h => h.Kod == "10");
            var kebir100 = hesaplar.Single(h => h.Kod == "100");

            Assert.Null(sinif1.UstHesapId);
            Assert.Equal(sinif1.Id, grup10.UstHesapId);
            Assert.Equal(grup10.Id, kebir100.UstHesapId);

            // Yaprak hareket görür, üstler görmez; hepsi sistem hesabı.
            Assert.True(kebir100.HareketGorur);
            Assert.False(grup10.HareketGorur);
            Assert.All(hesaplar, h => Assert.True(h.SistemHesabi));
        }

        [Fact]
        public async Task Yuklenen_plan_istegin_tenantina_yazilir()
        {
            using var db = MuhasebeTestOrtami.YeniContext();

            await MuhasebeTestOrtami.HesapPlaniServisi(db).TekDuzenPlaniYukleAsync();

            // TenantNo'yu context damgalar; kullanıcı hangi firmadaysa plan oraya gider.
            var tenantlar = await db.HesapPlanlari.Select(h => h.TenantNo).Distinct().ToListAsync();

            Assert.Equal(new[] { MuhasebeTestOrtami.TenantNo }, tenantlar);
        }

        [Fact]
        public async Task Kod_maskesi_yoksa_varsayilan_maske_de_olusur()
        {
            using var db = MuhasebeTestOrtami.YeniContext();

            await MuhasebeTestOrtami.HesapPlaniServisi(db).TekDuzenPlaniYukleAsync();

            var maske = await db.KodMaskeleri.SingleAsync();
            Assert.Equal("3,2,2,4", maske.SegmentUzunluk);
            Assert.Equal(".", maske.Ayrac);
        }

        [Fact]
        public async Task Plan_doluysa_ikinci_yukleme_kayit_eklemez()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();

            var oncekiAdet = await db.HesapPlanlari.CountAsync();

            var (sonuc, adet) = await MuhasebeTestOrtami.HesapPlaniServisi(db).TekDuzenPlaniYukleAsync();

            Assert.Equal(PlanYuklemeSonuc.ZatenDolu, sonuc);
            Assert.Equal(0, adet);
            Assert.Equal(oncekiAdet, await db.HesapPlanlari.CountAsync());
        }

        [Fact]
        public async Task Ayni_yukleme_iki_kez_calistirilsa_da_plan_ikilenmez()
        {
            using var db = MuhasebeTestOrtami.YeniContext();
            var servis = MuhasebeTestOrtami.HesapPlaniServisi(db);

            await servis.TekDuzenPlaniYukleAsync();
            var ikinci = await servis.TekDuzenPlaniYukleAsync();

            Assert.Equal(PlanYuklemeSonuc.ZatenDolu, ikinci.Sonuc);
            Assert.Equal(7, await db.HesapPlanlari.CountAsync());
        }

        /// <summary>
        /// Şablon dosyası yoksa sessizce geçilmez: sonuç açıkça bildirilir ki controller
        /// loglayıp ekranda anlaşılır hata gösterebilsin. Eski davranış (sessiz `return`)
        /// prod teşhisini zorlaştırmıştı (KARARLAR §83).
        /// </summary>
        [Fact]
        public async Task Sablon_dosyasi_yoksa_sessizce_gecilmez()
        {
            using var db = MuhasebeTestOrtami.YeniContext();
            var servis = MuhasebeTestOrtami.HesapPlaniServisi(
                db, new MuhasebeTestOrtami.SahtePlanKaynagi(var: false));

            var (sonuc, adet) = await servis.TekDuzenPlaniYukleAsync();

            Assert.Equal(PlanYuklemeSonuc.SablonYok, sonuc);
            Assert.Equal(0, adet);
            Assert.Empty(await db.HesapPlanlari.ToListAsync());
        }

        /// <summary>
        /// Şablon dosyası gerçekten yerinde mi? Yayına kopyalanması csproj'daki
        /// <c>PreserveNewest</c> kuralına bağlı; dosya depodan düşerse bu test uyarır.
        /// </summary>
        [Fact]
        public void Gercek_sablon_dosyasi_depoda_duruyor()
        {
            var dizin = new DirectoryInfo(AppContext.BaseDirectory);

            while (dizin is not null)
            {
                var aday = Path.Combine(dizin.FullName, "Infrastructure", "Setup", "SeedFiles",
                                        DosyadanTekDuzenPlanKaynagi.DosyaAdi);
                if (File.Exists(aday))
                {
                    var kayitlar = System.Text.Json.JsonSerializer.Deserialize<List<ThpDugum>>(
                        File.ReadAllText(aday),
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    Assert.NotNull(kayitlar);
                    Assert.NotEmpty(kayitlar!);
                    Assert.Contains(kayitlar!, k => k.Kod == "100");   // Kasa
                    return;
                }

                dizin = dizin.Parent;
            }

            Assert.Fail($"{DosyadanTekDuzenPlanKaynagi.DosyaAdi} bulunamadı; " +
                        "tekdüzen plan yüklemesi yayında çalışmaz.");
        }
    }
}
