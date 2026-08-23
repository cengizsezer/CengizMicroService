using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.Muhasebe;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Madde 1: hesap sahibinin kendi unvanı karşı taraf sanılmamalı.
    ///
    /// Test verisi gerçek dosyayı taklit eder: 7. satırda kolon başlıkları, veri 8'den
    /// başlar, açıklamada firmanın kendi unvanı ("PKF ADAY BAĞIMSIZ DENETİM ANONİM
    /// ŞİRKETİ") geçer ve hesap planında ona benzeyen bir cari ("BAĞIMSIZ DENETİM
    /// DERNEĞİ", 329 B58) vardır — yanlış eşleşmenin ölçülen hâli.
    /// </summary>
    public class HesapSahibiUnvaniTests
    {
        private const string HesapSahibi = "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ";

        /// <summary>Ölçülen 268 satırdaki kalıp: açıklamada yalnız hesap sahibinin adı var.</summary>
        private const string SahipAciklamasi =
            "0000123 sorgu numaralı PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ tarafından gönderilmiştir";

        private static EkstreService Servis(CatalogContext db)
        {
            var secici = new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() });
            return new EkstreService(db, secici, new UnvanCikarici(), new AciklamaUretici(),
                                     new HesapEslestirici(), new HesapEslesmeService(db, BankaEkstreTestOrtami.Kapsam()),
                                     new SabitKullanici(), BankaEkstreTestOrtami.Kapsam());
        }

        private static HesapPlaniKaydi Plan(string kod, string ad) => new()
        {
            FirmaId = BankaEkstreTestOrtami.FirmaId,
            Kod = kod,
            Ad = ad,
            NormalizeAd = Normalizasyon.UnvanNormalize(ad),
            AnaGrup = Normalizasyon.AnaGrup(kod),
            BaslangicHarfi = Normalizasyon.BaslangicHarfi(kod),
            Aktif = true
        };

        /// <param name="hesapSahibiUnvani">null verilirse alan boş bırakılır (hata öncesi durum).</param>
        private static async Task<(CatalogContext Db, int HesapId)> HazirlaAsync(string? hesapSahibiUnvani)
        {
            var db = BankaEkstreTestOrtami.YeniContext();

            db.EkstreAciklamaSablonlari.AddRange(BankaEkstreTestOrtami.Sablonlar());
            db.EkstreUnvanDesenleri.AddRange(BankaEkstreTestOrtami.Desenler());

            // Ölçülen yanlış eşleşmenin hedefi: hesap sahibinin adına benzeyen dernek kaydı.
            db.EkstreHesapPlani.AddRange(
                Plan("329 B58", "BAĞIMSIZ DENETİM DERNEĞİ"),
                Plan("120 B58", "BAĞIMSIZ DENETİM DERNEĞİ"),
                Plan("120 D22", "DAĞI GİYİM SANAYİ"));

            var hesap = new BankaHesabi
            {
                FirmaId = BankaEkstreTestOrtami.FirmaId,
                BankaAdi = "Vakıfbank",
                OrkaHesapKodu = "102 1 1 01",
                ParserTipi = VakifbankVadesizParser.Tip,
                HesapSahibiUnvani = hesapSahibiUnvani,
                Aktif = true
            };
            db.EkstreBankaHesaplari.Add(hesap);

            await db.SaveChangesAsync();
            return (db, hesap.Id);
        }

        /// <summary>Gerçek dosya yapısı: başlıklar 7. satırda, veri 8'den.</summary>
        private static MemoryStream Dosya(params object[][] satirlar)
            => BankaEkstreTestOrtami.BasliklıEkstre(satirlar);

        private static object[] Satir(string aciklama, string islemTipi = "Gelen EFT Otomatik Yatan",
                                      double tutar = 15000.50, string ba = "A")
            => new object[] { "15.01.2026", islemTipi, tutar, "İnternet", "0070511435", ba, aciklama };

        [Fact]
        public async Task Hesap_sahibinin_unvani_karsi_tarafa_eslesmez()
        {
            var (db, hesapId) = await HazirlaAsync(HesapSahibi);
            using var _ = db;

            var sonuc = await Servis(db).YukleAsync(hesapId, Dosya(Satir(SahipAciklamasi)), "ekstre.xlsx");

            var satir = db.EkstreSatirlari.Single(s => s.EkstreYuklemeId == sonuc.Id);

            // Ölçülen hata: "329 B58 / 120 B58 Bağımsız Denetim Derneği" öneriliyordu.
            Assert.Null(satir.CikarilanUnvan);
            Assert.Null(satir.OnerilenHesapKodu);
            Assert.Equal(SatirDurum.Cozulemedi, satir.Durum);
        }

        [Fact]
        public async Task Alan_bosken_eski_yanlis_eslesme_uretiliyordu()
        {
            // Düzeltmenin neyi engellediğini sabitler: hesap sahibi unvanı girilmemişse
            // aynı satır hâlâ derneğe eşleşiyor. Alanın doldurulması şart.
            var (db, hesapId) = await HazirlaAsync(hesapSahibiUnvani: null);
            using var _ = db;

            var sonuc = await Servis(db).YukleAsync(hesapId, Dosya(Satir(SahipAciklamasi)), "ekstre.xlsx");

            var satir = db.EkstreSatirlari.Single(s => s.EkstreYuklemeId == sonuc.Id);

            Assert.Equal("PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ", satir.CikarilanUnvan);
            Assert.Equal("120 B58", satir.OnerilenHesapKodu);
        }

        [Fact]
        public async Task Hesap_sahibi_elenen_satirda_ogrenme_anahtari_yazilmaz()
        {
            var (db, hesapId) = await HazirlaAsync(HesapSahibi);
            using var _ = db;

            var servis = Servis(db);
            var sonuc = await servis.YukleAsync(hesapId, Dosya(Satir(SahipAciklamasi)), "ekstre.xlsx");
            var satir = db.EkstreSatirlari.Single(s => s.EkstreYuklemeId == sonuc.Id);

            // Anahtar hiç üretilmemeli: ne hesap sahibinin çekirdeği, ne işlem tipi anahtarı.
            // İşlem tipine düşseydi "ISLEM:GELEN EFT OTOMATIK YATAN" ilk onaydan sonra
            // ilgisiz tüm EFT satırlarını aynı hesaba çözerdi.
            Assert.Null(satir.AnahtarCekirdek);

            await servis.OnaylaAsync(satir.Id, "120 D22");

            Assert.Empty(db.EkstreHesapEslesmeleri);
            Assert.Empty(db.EkstreKimlikKayitlari);
        }

        [Fact]
        public async Task Gercek_karsi_taraf_varsa_normal_cozulur()
        {
            var (db, hesapId) = await HazirlaAsync(HesapSahibi);
            using var _ = db;

            var aciklama = "0000124 sorgu numaralı DAĞI GİYİM SANAYİ VE TİCARET A.Ş. tarafından gönderilmiştir";
            var sonuc = await Servis(db).YukleAsync(hesapId, Dosya(Satir(aciklama)), "ekstre.xlsx");

            var satir = db.EkstreSatirlari.Single(s => s.EkstreYuklemeId == sonuc.Id);

            Assert.Equal("DAĞI GİYİM SANAYİ VE TİCARET A.Ş.", satir.CikarilanUnvan);
            Assert.Equal("120 D22", satir.OnerilenHesapKodu);
        }

        [Fact]
        public async Task Unvan_firmanin_baska_hesabindan_devralinir()
        {
            // "Tek kez girilir": ekstresi işlenen hesapta boşsa aynı firmanın dolu olan
            // başka bir hesabından okunur.
            var (db, hesapId) = await HazirlaAsync(hesapSahibiUnvani: null);
            using var _ = db;

            db.EkstreBankaHesaplari.Add(new BankaHesabi
            {
                FirmaId = BankaEkstreTestOrtami.FirmaId,
                BankaAdi = "Ziraat",
                OrkaHesapKodu = "102 2 1 01",
                HesapSahibiUnvani = HesapSahibi,
                Aktif = true
            });
            await db.SaveChangesAsync();

            var sonuc = await Servis(db).YukleAsync(hesapId, Dosya(Satir(SahipAciklamasi)), "ekstre.xlsx");
            var satir = db.EkstreSatirlari.Single(s => s.EkstreYuklemeId == sonuc.Id);

            Assert.Null(satir.CikarilanUnvan);
            Assert.Equal(SatirDurum.Cozulemedi, satir.Durum);
        }

        [Fact]
        public async Task Iki_yuz_seksen_yedi_satirlik_dosyada_sahip_satirlari_onaya_duser()
        {
            // Ölçülen dağılım: 287 satırın 268'inde hesap sahibinin adı, kalan 19'unda
            // gerçek karşı taraf. Hiçbiri derneğe eşleşmemeli.
            var (db, hesapId) = await HazirlaAsync(HesapSahibi);
            using var _ = db;

            var satirlar = new List<object[]>();
            for (var i = 0; i < 268; i++) satirlar.Add(Satir(SahipAciklamasi));
            for (var i = 0; i < 19; i++)
                satirlar.Add(Satir("0000200 sorgu numaralı DAĞI GİYİM SANAYİ VE TİCARET A.Ş. tarafından gönderilmiştir"));

            var sonuc = await Servis(db).YukleAsync(hesapId, Dosya(satirlar.ToArray()), "ekstre.xlsx");
            var kayitlar = db.EkstreSatirlari.Where(s => s.EkstreYuklemeId == sonuc.Id).ToList();

            Assert.Equal(287, kayitlar.Count);
            Assert.Equal(268, kayitlar.Count(s => s.Durum == SatirDurum.Cozulemedi));
            Assert.Equal(19, kayitlar.Count(s => s.OnerilenHesapKodu == "120 D22"));
            Assert.DoesNotContain(kayitlar, s => s.OnerilenHesapKodu is "329 B58" or "120 B58");
        }
    }
}
