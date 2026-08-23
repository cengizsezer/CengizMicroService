using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Hesap sahibi kimliğinin <b>firma bazlı</b> yönetimi. Değer banka hesabı satırlarında
    /// saklanıyor ama firmanın kimliği bankaya göre değişmez: Firma Tanımları ekranı tek
    /// kayıt olarak yönetir, yazma işlemi tüm hesaplara kopyalar.
    ///
    /// Tek hesaba yazılsaydı ekstresi başka bir hesaptan işlenen banka firmanın adını
    /// tanımaz, açıklamada geçen kendi unvanımızı karşı taraf sanıp benzer adlı bir cariye
    /// eşlerdi (ölçümde 287 satırın 268'inde firmanın kendi unvanı geçiyordu).
    /// </summary>
    public class HesapSahibiKimligiYonetimiTests
    {
        private const string Unvan = "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ";
        private const string TakmaAd = "ADAY BAĞIMSIZ DENETİM VE SMMM A.Ş.";

        private static BankaHesabiService Servis(CatalogContext db)
            => new(db, new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() }),
                   BankaEkstreTestOrtami.Kapsam());

        private static BankaHesabi Hesap(string banka, string kod, string? unvan = null, string? takmaAdlar = null) => new()
        {
            FirmaId = BankaEkstreTestOrtami.FirmaId,
            BankaAdi = banka,
            OrkaHesapKodu = kod,
            ParaBirimi = "TRY",
            HesapSahibiUnvani = unvan,
            HesapSahibiTakmaAdlari = takmaAdlar,
            Aktif = true
        };

        private static async Task<CatalogContext> UcHesapliAsync()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            db.EkstreBankaHesaplari.AddRange(
                Hesap("Vakıfbank", "102 1 1 01"),
                Hesap("Ziraat", "102 1 5 01"),
                Hesap("TEB", "102 1 32 87"));
            await db.SaveChangesAsync();
            return db;
        }

        [Fact]
        public async Task Kimlik_tum_hesaplara_yazilir()
        {
            using var db = await UcHesapliAsync();

            var sonuc = await Servis(db).HesapSahibiKaydetAsync(
                new HesapSahibiKimlikYazDto { Unvan = Unvan, TakmaAdlar = TakmaAd });

            Assert.Equal(3, sonuc.HesapSayisi);
            Assert.All(db.EkstreBankaHesaplari, h => Assert.Equal(Unvan, h.HesapSahibiUnvani));
            Assert.All(db.EkstreBankaHesaplari, h => Assert.Equal(TakmaAd, h.HesapSahibiTakmaAdlari));
        }

        [Fact]
        public async Task Okuma_dolu_olan_ilk_hesaptan_unvani_alir()
        {
            // Eski kurulum: unvan yalnız bir hesaba girilmiş. Ekran yine de değeri göstermeli.
            using var db = BankaEkstreTestOrtami.YeniContext();
            db.EkstreBankaHesaplari.AddRange(
                Hesap("Vakıfbank", "102 1 1 01"),
                Hesap("Ziraat", "102 1 5 01", Unvan));
            await db.SaveChangesAsync();

            var kimlik = await Servis(db).HesapSahibiGetAsync();

            Assert.Equal(Unvan, kimlik.Unvan);
            Assert.Equal(2, kimlik.HesapSayisi);
        }

        [Fact]
        public async Task Okuma_farkli_hesaplardaki_takma_adlari_birlestirir()
        {
            // Eski kurulumda yazımlar hesaplara dağılmış olabilir; hiçbiri kaybolmamalı.
            using var db = BankaEkstreTestOrtami.YeniContext();
            db.EkstreBankaHesaplari.AddRange(
                Hesap("Vakıfbank", "102 1 1 01", Unvan, "PKF ADAY"),
                Hesap("Ziraat", "102 1 5 01", null, TakmaAd));
            await db.SaveChangesAsync();

            var kimlik = await Servis(db).HesapSahibiGetAsync();

            Assert.Contains("PKF ADAY", kimlik.TakmaAdlar);
            Assert.Contains(TakmaAd, kimlik.TakmaAdlar);
        }

        [Fact]
        public async Task Ayni_yazim_iki_hesapta_varsa_tek_kez_okunur()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            db.EkstreBankaHesaplari.AddRange(
                Hesap("Vakıfbank", "102 1 1 01", Unvan, TakmaAd),
                Hesap("Ziraat", "102 1 5 01", Unvan, TakmaAd));
            await db.SaveChangesAsync();

            var kimlik = await Servis(db).HesapSahibiGetAsync();

            Assert.Equal(TakmaAd, kimlik.TakmaAdlar);
        }

        [Fact]
        public async Task Bos_kaydetmek_kimligi_temizler()
        {
            using var db = await UcHesapliAsync();
            var servis = Servis(db);

            await servis.HesapSahibiKaydetAsync(new HesapSahibiKimlikYazDto { Unvan = Unvan, TakmaAdlar = TakmaAd });
            await servis.HesapSahibiKaydetAsync(new HesapSahibiKimlikYazDto());

            var kimlik = await servis.HesapSahibiGetAsync();

            Assert.Null(kimlik.Unvan);
            Assert.Null(kimlik.TakmaAdlar);
        }

        [Fact]
        public async Task Hesap_yokken_okuma_bos_kimlik_dondurur()
        {
            // Kurulumun ilk anı: hesap girilmeden Firma Tanımları açılabilir, patlamamalı.
            using var db = BankaEkstreTestOrtami.YeniContext();

            var kimlik = await Servis(db).HesapSahibiGetAsync();

            Assert.Null(kimlik.Unvan);
            Assert.Equal(0, kimlik.HesapSayisi);
        }

        [Fact]
        public async Task Oneriler_kimlik_bossa_bos_doner()
        {
            using var db = await UcHesapliAsync();

            var oneriler = await Servis(db).HesapSahibiOnerileriAsync();

            Assert.Empty(oneriler);
        }

        [Fact]
        public async Task Oneriler_ekstrelerdeki_eklenmemis_yazimi_bulur()
        {
            using var db = await UcHesapliAsync();
            var servis = Servis(db);

            await servis.HesapSahibiKaydetAsync(new HesapSahibiKimlikYazDto { Unvan = Unvan });

            // Satırın kendi FirmaId'si yok; kapsamını bağlı olduğu yüklemeden alıyor.
            // Yükleme olmadan satır hiçbir firmaya ait değildir ve önerilere girmez.
            db.EkstreYuklemeler.Add(new EkstreYukleme
            {
                FirmaId = BankaEkstreTestOrtami.FirmaId,
                BankaHesabiId = db.EkstreBankaHesaplari.First().Id,
                DosyaAdi = "ocak.xlsx",
                SatirSayisi = 3
            });
            await db.SaveChangesAsync();

            // Çıkarılmış unvanlar: biri firmanın başka yazımı, biri ilgisiz bir cari.
            db.EkstreSatirlari.AddRange(
                Satir(TakmaAd), Satir(TakmaAd), Satir("YURTİÇİ KARGO A.Ş."));
            await db.SaveChangesAsync();

            var oneriler = await servis.HesapSahibiOnerileriAsync();

            var oneri = Assert.Single(oneriler);
            Assert.Equal(TakmaAd, oneri.Yazim);
            Assert.Equal(2, oneri.Adet);
        }

        private static EkstreSatiri Satir(string cikarilanUnvan) => new()
        {
            EkstreYuklemeId = 1,
            SiraNo = 1,
            Tarih = new DateTime(2026, 1, 15),
            Yon = Yon.Giren,
            Tutar = 100m,
            IslemTipi = "Gelen EFT Otomatik Yatan",
            HamAciklama = cikarilanUnvan,
            CikarilanUnvan = cikarilanUnvan,
            Durum = SatirDurum.Cozulemedi
        };
    }
}
