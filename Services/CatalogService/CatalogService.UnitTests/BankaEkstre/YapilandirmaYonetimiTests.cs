using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.Muhasebe;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Sabit kural / açıklama şablonu / unvan deseni tablolarının arayüzden yönetimi.
    ///
    /// İki şey sınanır:
    /// <list type="bullet">
    /// <item><b>Ayrıştırıcı filtresi.</b> Boş ParserTipi tüm bankalarda geçerli, dolusu
    /// yalnız o bankada. Vakıfbank'a özel bir kural başka bankanın ekstresinde çalışmamalı;
    /// bu, üç tablonun da yükleme sorgusundan gelir.</item>
    /// <item><b>Kaydetme denetimleri.</b> Geçersiz hesap kodu, geçersiz regex, tanımsız
    /// ayrıştırıcı ve tanınmayan yer tutucu kaydedilmez — hepsi kaydedilseydi çalışma
    /// zamanında sessizce atlanır, kullanıcı kaydın neden etkisiz olduğunu göremezdi.</item>
    /// </list>
    /// </summary>
    public class YapilandirmaYonetimiTests
    {
        private const string BaskaBanka = "ZIRAAT_VADESIZ";

        private static IEkstreParserSecici Secici()
            => new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() });

        private static HesapPlaniKaydi Plan(string kod, string ad) => new()
        {
            Kod = kod,
            Ad = ad,
            NormalizeAd = Normalizasyon.UnvanNormalize(ad),
            AnaGrup = Normalizasyon.AnaGrup(kod),
            BaslangicHarfi = Normalizasyon.BaslangicHarfi(kod),
            Aktif = true
        };

        /// <summary>Kod denetimi ancak plan doluysa çalışır; testlerin çoğu dolu plan ister.</summary>
        private static async Task<CatalogContext> PlanliContextAsync()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            db.EkstreHesapPlani.AddRange(
                Plan("770 03 005", "Banka Komisyonu"),
                Plan("740", "Hizmet Üretim Maliyeti"));
            await db.SaveChangesAsync();
            return db;
        }

        private static SabitKuralYazDto Kural(string desen, string kod = "770 03 005") => new()
        {
            IslemTipiDeseni = desen,
            Kapsam = KuralKapsami.IslemTipi,
            EslesmeTuru = EslesmeTuru.Tam,
            HesapKodu = kod,
            Sira = 10,
            Aktif = true
        };

        // ---- Ayrıştırıcı filtresi (uçtan uca) ----

        private static EkstreService Servis(CatalogContext db)
            => new(db, Secici(), new UnvanCikarici(), new AciklamaUretici(),
                   new HesapEslestirici(), new HesapEslesmeService(db), new SabitKullanici());

        /// <summary>
        /// Tek satırlık ekstre işler ve satırın önerilen hesap kodunu döndürür. Kural
        /// listesi çağıran tarafından verilir: aynı satır farklı ParserTipi'leriyle sınanır.
        /// </summary>
        private static async Task<string?> IsleAsync(params SabitKural[] kurallar)
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            db.EkstreSabitKurallar.AddRange(kurallar);

            var hesap = new BankaHesabi
            {
                BankaAdi = "Vakıfbank",
                OrkaHesapKodu = "102 1 1 01",
                ParserTipi = VakifbankVadesizParser.Tip,
                Aktif = true
            };
            db.EkstreBankaHesaplari.Add(hesap);
            await db.SaveChangesAsync();

            using var dosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "15.01.2026", "MKK Masrafı", 12.5m, "", "", "B", "MKK saklama masrafı" });

            var yukleme = await Servis(db).YukleAsync(hesap.Id, dosya, "ocak.xlsx");
            var satir = (await Servis(db).GetSatirlarAsync(yukleme.Id, null))!.Single();

            return satir.OnerilenHesapKodu;
        }

        private static SabitKural HamKural(string parserTipi) => new()
        {
            ParserTipi = parserTipi,
            IslemTipiDeseni = "MKK Masrafı",
            Kapsam = KuralKapsami.IslemTipi,
            EslesmeTuru = EslesmeTuru.Tam,
            HesapKodu = "770 03 005",
            HesapAdi = "Banka Komisyonu",
            Guven = 0.95m,
            Sira = 10,
            Aktif = true
        };

        [Fact]
        public async Task Bankaya_ozel_kural_kendi_bankasinda_calisir()
        {
            Assert.Equal("770 03 005", await IsleAsync(HamKural(VakifbankVadesizParser.Tip)));
        }

        [Fact]
        public async Task Baska_bankanin_kurali_bu_ekstrede_calismaz()
        {
            // Vakıfbank ekstresi işleniyor; kural Ziraat'e tanımlı. Eskiden tablo süzülmeden
            // yükleniyordu ve kural burada da tutuyordu.
            Assert.Null(await IsleAsync(HamKural(BaskaBanka)));
        }

        [Fact]
        public async Task Ayristiricisi_bos_kural_tum_bankalarda_calisir()
        {
            Assert.Equal("770 03 005", await IsleAsync(HamKural(string.Empty)));
        }

        // ---- Sabit kural denetimleri ----

        [Fact]
        public async Task Plan_disi_hesap_kodu_kaydedilmez()
        {
            using var db = await PlanliContextAsync();
            var servis = new SabitKuralService(db, Secici());

            var hata = await Assert.ThrowsAsync<BankaEkstreKuralException>(
                () => servis.CreateAsync(Kural("MKK Masrafı", "770 99 999")));

            Assert.Contains("hesap planında yok", hata.Message);
        }

        [Fact]
        public async Task Hesap_adi_bos_birakilirsa_plandan_doldurulur()
        {
            using var db = await PlanliContextAsync();

            var kayit = await new SabitKuralService(db, Secici()).CreateAsync(Kural("MKK Masrafı"));

            Assert.Equal("Banka Komisyonu", kayit.HesapAdi);
        }

        [Fact]
        public async Task Bos_ayristirici_tum_bankalar_olarak_saklanir()
        {
            using var db = await PlanliContextAsync();

            var kayit = await new SabitKuralService(db, Secici()).CreateAsync(Kural("MKK Masrafı"));

            Assert.Equal(string.Empty, kayit.ParserTipi);
            Assert.Equal("Tüm bankalar", kayit.ParserAdi);
        }

        [Fact]
        public async Task Tanimsiz_ayristirici_kaydedilmez()
        {
            using var db = await PlanliContextAsync();
            var servis = new SabitKuralService(db, Secici());

            var dto = Kural("MKK Masrafı");
            dto.ParserTipi = "VAKIFBAK_VADESIZ";   // yazım hatası

            var hata = await Assert.ThrowsAsync<BankaEkstreKuralException>(() => servis.CreateAsync(dto));

            Assert.Contains("Tanımsız ayrıştırıcı", hata.Message);
        }

        [Fact]
        public async Task Ayni_ayristirici_kapsam_ve_ifade_ikinci_kez_eklenemez()
        {
            using var db = await PlanliContextAsync();
            var servis = new SabitKuralService(db, Secici());

            await servis.CreateAsync(Kural("MKK Masrafı"));

            var hata = await Assert.ThrowsAsync<BankaEkstreKuralException>(
                () => servis.CreateAsync(Kural("mkk masrafı")));

            Assert.Contains("zaten var", hata.Message);
        }

        [Fact]
        public async Task Bozuk_regex_kurali_kaydedilmez()
        {
            using var db = await PlanliContextAsync();
            var servis = new SabitKuralService(db, Secici());

            var dto = Kural("(bitmeyen grup");
            dto.EslesmeTuru = EslesmeTuru.Regex;

            var hata = await Assert.ThrowsAsync<BankaEkstreKuralException>(() => servis.CreateAsync(dto));

            Assert.Contains("Geçersiz regex", hata.Message);
        }

        [Fact]
        public async Task Plan_hic_yuklenmemisse_kod_denetimi_atlanir()
        {
            // Kurulum sırası bozulmasın: plan gelmeden kural girilebilmeli.
            using var db = BankaEkstreTestOrtami.YeniContext();

            var kayit = await new SabitKuralService(db, Secici()).CreateAsync(Kural("MKK Masrafı"));

            Assert.Equal("770 03 005", kayit.HesapKodu);
        }

        // ---- Açıklama şablonu denetimleri ----

        private static AciklamaSablonuYazDto Sablon(string sablon) => new()
        {
            IslemTipiDeseni = "Gelen EFT Otomatik Yatan",
            EslesmeTuru = EslesmeTuru.Tam,
            Sablon = sablon,
            Sira = 10,
            Aktif = true
        };

        [Fact]
        public async Task Taninmayan_yer_tutucu_kaydedilmez()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            var servis = new AciklamaSablonuService(db, Secici());

            var hata = await Assert.ThrowsAsync<BankaEkstreKuralException>(
                () => servis.CreateAsync(Sablon("Gelen Eft - {MUSTERI}")));

            Assert.Contains("{MUSTERI}", hata.Message);
        }

        [Fact]
        public async Task Bilinen_yer_tutucu_kaydedilir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            var kayit = await new AciklamaSablonuService(db, Secici()).CreateAsync(Sablon("Gelen Eft - {UNVAN}"));

            Assert.Equal("Gelen Eft - {UNVAN}", kayit.Sablon);
        }

        [Fact]
        public void Yer_tutucu_listesi_ureticinin_doldurduklariyla_ayni()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            var liste = new AciklamaSablonuService(db, Secici()).YerTutucular();

            Assert.Equal(AciklamaUretici.YerTutucular.Count, liste.Count);
            Assert.Contains(liste, y => y.Ad == "{UNVAN}");
        }

        // ---- Unvan deseni denetimleri ve deneme kutusu ----

        private static UnvanDeseniService DesenServisi(CatalogContext db)
            => new(db, Secici(), new UnvanCikarici());

        [Fact]
        public async Task Bozuk_regex_deseni_kaydedilmez()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            var servis = DesenServisi(db);

            var hata = await Assert.ThrowsAsync<BankaEkstreKuralException>(
                () => servis.CreateAsync(new UnvanDeseniYazDto { Desen = @"sorgu (.+ tarafından", GrupNo = 1 }));

            Assert.Contains("Geçersiz regex", hata.Message);
        }

        [Fact]
        public void Deneme_yakalamayi_ve_cikarilan_unvani_dondurur()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            var sonuc = DesenServisi(db).Dene(new DesenDenemeIstegiDto
            {
                Desen = @"sorgu numaralı (.+?) tarafından",
                GrupNo = 1,
                OrnekMetin = "1234 sorgu numaralı YURTİÇİ KARGO A.Ş. tarafından gönderilmiştir"
            });

            Assert.True(sonuc.Gecerli);
            Assert.True(sonuc.Eslesti);
            Assert.Equal("YURTİÇİ KARGO A.Ş.", sonuc.Unvan);
        }

        [Fact]
        public void Deneme_bozuk_regexte_hata_dondurur_patlamaz()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            var sonuc = DesenServisi(db).Dene(new DesenDenemeIstegiDto
            {
                Desen = @"(bitmeyen grup",
                GrupNo = 1,
                OrnekMetin = "herhangi bir metin"
            });

            Assert.False(sonuc.Gecerli);
            Assert.NotNull(sonuc.Hata);
        }

        [Fact]
        public void Deneme_tutmayan_desende_neden_bildirir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            var sonuc = DesenServisi(db).Dene(new DesenDenemeIstegiDto
            {
                Desen = @"sorgu numaralı (.+?) tarafından",
                GrupNo = 1,
                OrnekMetin = "MKK saklama masrafı"
            });

            Assert.True(sonuc.Gecerli);
            Assert.False(sonuc.Eslesti);
            Assert.NotNull(sonuc.Not);
        }

        [Fact]
        public void Deneme_olmayan_yakalama_grubunu_bildirir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            var sonuc = DesenServisi(db).Dene(new DesenDenemeIstegiDto
            {
                Desen = "MKK",
                GrupNo = 3,
                OrnekMetin = "MKK saklama masrafı"
            });

            Assert.True(sonuc.Eslesti);
            Assert.Null(sonuc.HamYakalanan);
            Assert.Contains("yakalama grubu yok", sonuc.Not);
        }
    }
}
