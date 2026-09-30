using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Features.Firmalar;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.UnitTests.BankaEkstre;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.Anasayfa
{
    /// <summary>
    /// Prompt 6C: seed dolu sınıflandırmayı asla geri almaz; kod ayıklamada tire şartı kalktı.
    /// </summary>
    public class Prompt6cTests
    {
        // ---- 1. Seed koruması ----

        [Fact]
        public async Task Dolu_siniflandirma_ayristirilamayan_metinle_seedden_sonra_aynen_kalir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            db.Firmalar.Add(new Firma
            {
                Id = 1, Unvan = "DGR A.Ş.", KisaAd = "DGR", VergiKimlikNo = "2951070824", Aktif = true
            });
            db.FirmaSicilBilgileri.Add(new FirmaSicilBilgisi { Id = 1, FirmaId = 1, MukellefiyetTurleri = "0010 - KURUMLAR VERGİSİ" });
            db.SaveChanges();

            await FirmaSiniflandirmaSeed.SeedAsync(db);
            var once = await db.Firmalar.AsNoTracking().SingleAsync();
            Assert.Equal(VergiTuru.KurumlarVergisi, once.VergiTuru);
            Assert.Equal(DefterUsulu.BilancoEsasi, once.DefterUsulu);

            // Metin ayrıştırılamaz hale geliyor (ya da yarın ayrıştırıcı değişiyor).
            var sicil = db.FirmaSicilBilgileri.Single();
            sicil.MukellefiyetTurleri = "Kurumlar vergisi mükellefi (kod yazılmamış)";
            db.SaveChanges();

            await FirmaSiniflandirmaSeed.SeedAsync(db);

            var sonra = await db.Firmalar.AsNoTracking().SingleAsync();
            Assert.Equal(VergiTuru.KurumlarVergisi, sonra.VergiTuru);
            Assert.Equal(DefterUsulu.BilancoEsasi, sonra.DefterUsulu);
            Assert.Equal(SiniflandirmaKaynagi.Otomatik, sonra.VergiTuruKaynagi);
        }

        [Fact]
        public void Celisen_oneri_dolu_degeri_degistirmez()
        {
            var firma = new Firma
            {
                VergiTuru = VergiTuru.GelirVergisi, VergiTuruKaynagi = SiniflandirmaKaynagi.Otomatik,
                DefterUsulu = DefterUsulu.IsletmeHesabi, DefterUsuluKaynagi = SiniflandirmaKaynagi.Otomatik
            };

            Assert.False(MukellefiyetSiniflandirici.Uygula(firma, "0010 - KURUMLAR VERGİSİ"));
            Assert.Equal(VergiTuru.GelirVergisi, firma.VergiTuru);
            Assert.Equal(DefterUsulu.IsletmeHesabi, firma.DefterUsulu);
        }

        [Fact]
        public void Kullanicinin_sectigi_Belirsiz_korunur()
        {
            // Kullanıcı arayüzden Belirsiz seçti (Elle): seed doldurmaz.
            var firma = new Firma { VergiTuru = VergiTuru.Belirsiz, VergiTuruKaynagi = SiniflandirmaKaynagi.Elle };

            MukellefiyetSiniflandirici.Uygula(firma, "0010");

            Assert.Equal(VergiTuru.Belirsiz, firma.VergiTuru);
        }

        [Fact]
        public void Bos_alan_doldurulur_isletme_hesabinda_kurumlar_doldurulmaz()
        {
            var bos = new Firma();
            Assert.True(MukellefiyetSiniflandirici.Uygula(bos, "0010"));
            Assert.Equal(VergiTuru.KurumlarVergisi, bos.VergiTuru);

            // Kısıt: elle işletme hesabı seçilmişse kurumlar vergisi otomatik doldurulmaz.
            var isletme = new Firma { DefterUsulu = DefterUsulu.IsletmeHesabi, DefterUsuluKaynagi = SiniflandirmaKaynagi.Elle };
            MukellefiyetSiniflandirici.Uygula(isletme, "0010");
            Assert.Equal(VergiTuru.Belirsiz, isletme.VergiTuru);
        }

        // ---- 2. Tire şartı kalktı ----

        [Theory]
        [InlineData("0010 - KURUMLAR VERGİSİ")]
        [InlineData("0010- KURUMLAR VERGİSİ")]
        [InlineData("0010-KURUMLAR VERGİSİ")]
        [InlineData("0010 KURUMLAR VERGİSİ")]
        [InlineData("0010")]
        [InlineData(" 0010 · Kurumlar")]
        public void Kabul_edilen_bicimler(string metin)
            => Assert.Equal(new[] { "0010" }, MukellefiyetSiniflandirici.Kodlar(metin));

        [Theory]
        [InlineData("2021 yılından itibaren mükellef")]
        [InlineData("Vergi dairesi 0010 numaralı yazı ile bildirmiştir")]
        [InlineData("00105 numaralı karar")]
        public void Kabul_edilmeyen_bicimler(string metin)
        {
            var sonuc = MukellefiyetSiniflandirici.Ayikla(metin);

            Assert.Empty(sonuc.Kodlar);
            Assert.Equal(metin, sonuc.Kalan);     // yutulmadı
        }

        [Fact]
        public void Tiresiz_0010_kurumlar_ve_bilanco_siniflandirir()
        {
            var firma = new Firma();

            MukellefiyetSiniflandirici.Uygula(firma, "0003 GELİR VERGİSİ S. (MUHTASAR), 0010 KURUMLAR VERGİSİ");

            Assert.Equal(VergiTuru.KurumlarVergisi, firma.VergiTuru);
            Assert.Equal(DefterUsulu.BilancoEsasi, firma.DefterUsulu);
        }

        [Fact]
        public void Tiresiz_bicimde_ad_ve_kisa_ad_ayiklaniyor()
        {
            var kod = Assert.Single(MukellefiyetSiniflandirici.Ayikla(" 0033 · Damga vergisi").Kodlar);

            Assert.Equal("Damga vergisi", kod.Ad);
            Assert.Equal("Damga vergisi", MukellefiyetSiniflandirici.KisaAd(kod));
        }

        // ---- 3. Özel hesap dönemi yılı tek yerde ----

        [Fact]
        public void Ozel_hesap_donemi_yili_kapanis_yili()
            => Assert.Equal(2026, FirmaPaneliKurucu.OzelDonemYili(new DateTime(2025, 7, 1), new DateTime(2026, 6, 30)));
    }
}
