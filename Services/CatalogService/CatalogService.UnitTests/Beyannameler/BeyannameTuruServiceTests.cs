using CatalogService.Api.Features.Declarations;
using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Features.Declarations.Entities;
using CatalogService.Api.Features.Declarations.Services;
using CatalogService.UnitTests.BankaEkstre;

namespace CatalogService.UnitTests.Beyannameler
{
    /// <summary>
    /// Beyanname türü tanımlarının yönetimi.
    ///
    /// Ekranın asıl derdi tek kaynak: Takip sekmesi sabit bir listeden, Özet sekmesi
    /// tablodan okuyunca tablo boşken ikisi farklı şey gösteriyordu. Buradaki sınamalar
    /// tablonun kullanıcı tarafından doldurulabilir ve tutarlı kalabilir olduğunu tutuyor.
    /// </summary>
    public class BeyannameTuruServiceTests
    {
        private static BeyannameTuruYazDto Istek(string deger = "0091 TURIZM PAYI",
                                                 string ad = "Turizm Payı",
                                                 string? kod = "0091",
                                                 int sira = 0,
                                                 bool aktif = true)
            => new() { Deger = deger, Ad = ad, Kod = kod, Sira = sira, Aktif = aktif };

        [Fact]
        public async Task Yeni_tur_eklenebilir_ve_listeye_duser()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            var servis = new BeyannameTuruService(db);

            var eklenen = await servis.CreateAsync(Istek());

            Assert.Equal("0091 TURIZM PAYI", eklenen.Deger);
            Assert.Equal("0091", eklenen.Kod);
            Assert.Contains(await servis.GetHepsiAsync(), t => t.Id == eklenen.Id);
        }

        [Fact]
        public async Task Sira_verilmezse_sona_eklenir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            var servis = new BeyannameTuruService(db);

            await BeyannameTuruSeed.SeedAsync(db);
            var enBuyuk = db.BeyannameTurleri.Max(t => t.Sira);

            var eklenen = await servis.CreateAsync(Istek(sira: 0));

            Assert.True(eklenen.Sira > enBuyuk);
        }

        [Fact]
        public async Task Ayni_deger_iki_kez_eklenemez()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            var servis = new BeyannameTuruService(db);

            await servis.CreateAsync(Istek());

            // Aynı Deger iki tanıma bölünürse beyanname kayıtları hangi kolona düşeceğini
            // bilemez; benzersizlik veritabanı index'inden önce burada söylenir.
            await Assert.ThrowsAsync<BeyannameKuralException>(() => servis.CreateAsync(Istek()));
        }

        [Fact]
        public async Task Bos_deger_ve_bos_ad_reddedilir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            var servis = new BeyannameTuruService(db);

            await Assert.ThrowsAsync<BeyannameKuralException>(
                () => servis.CreateAsync(Istek(deger: "   ")));

            await Assert.ThrowsAsync<BeyannameKuralException>(
                () => servis.CreateAsync(Istek(ad: "")));
        }

        [Fact]
        public async Task Pasif_tanim_varsayilan_listede_cikmaz_pasifDahil_ile_cikar()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            var servis = new BeyannameTuruService(db);

            var eklenen = await servis.CreateAsync(Istek(aktif: false));

            Assert.DoesNotContain(await servis.GetHepsiAsync(), t => t.Id == eklenen.Id);
            Assert.Contains(await servis.GetHepsiAsync(pasifDahil: true), t => t.Id == eklenen.Id);
        }

        [Fact]
        public async Task Guncelleme_alanlari_yazar_olmayan_kayitta_null_doner()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            var servis = new BeyannameTuruService(db);

            var eklenen = await servis.CreateAsync(Istek());

            var guncel = await servis.UpdateAsync(eklenen.Id,
                Istek(ad: "Turizm Payı Beyannamesi", sira: 500, aktif: false));

            Assert.NotNull(guncel);
            Assert.Equal("Turizm Payı Beyannamesi", guncel!.Ad);
            Assert.Equal(500, guncel.Sira);
            Assert.False(guncel.Aktif);

            Assert.Null(await servis.UpdateAsync(9999, Istek()));
        }

        [Fact]
        public async Task Guncelleme_baska_tanimin_degerini_alamaz()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            var servis = new BeyannameTuruService(db);

            await servis.CreateAsync(Istek());
            var ikinci = await servis.CreateAsync(Istek(deger: "0092 X", ad: "X", kod: "0092"));

            await Assert.ThrowsAsync<BeyannameKuralException>(
                () => servis.UpdateAsync(ikinci.Id, Istek()));
        }

        /// <summary>
        /// Ekrandaki "Varsayılanları yükle" düğmesinin karşılığı. Açılış seed'i herhangi bir
        /// sebeple çalışmamışsa kullanıcı tabloyu deploy beklemeden doldurabilmeli; ikinci
        /// çağrı mevcut satırları bozmamalı.
        /// </summary>
        [Fact]
        public async Task Varsayilanlar_elle_yuklenebilir_ve_mevcut_kaydi_bozmaz()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            var servis = new BeyannameTuruService(db);

            db.BeyannameTurleri.Add(new BeyannameTuru
            {
                Deger = "0015 KDV-1",
                Kod = "0015",
                Ad = "Elle düzenlenmiş ad",
                Sira = 5,
                Aktif = true
            });
            db.SaveChanges();

            var (eklenen, toplam) = await servis.VarsayilanlariYukleAsync();

            Assert.Equal(BeyannameTuruSeed.Turler.Length - 1, eklenen);
            Assert.Equal(BeyannameTuruSeed.Turler.Length, toplam);

            // Kullanıcının düzenlediği ad korunur: seed üzerine yazmaz.
            Assert.Equal("Elle düzenlenmiş ad",
                         db.BeyannameTurleri.Single(t => t.Deger == "0015 KDV-1").Ad);

            var (ikinciEklenen, ikinciToplam) = await servis.VarsayilanlariYukleAsync();
            Assert.Equal(0, ikinciEklenen);
            Assert.Equal(toplam, ikinciToplam);
        }
    }
}
