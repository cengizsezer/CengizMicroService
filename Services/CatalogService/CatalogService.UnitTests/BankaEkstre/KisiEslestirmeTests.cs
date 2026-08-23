using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.Muhasebe;
using ClosedXML.Excel;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Tur 3 — kişi eşleştirmesi. Her iddia <b>gerçek Vakıfbank ekstresinin kendi
    /// açıklama metniyle</b> sınanır; satır sıra numarasıyla değil, açıklamada geçen
    /// ifadeyle bulunur.
    ///
    /// Ölçülen sorun: sabit kural grubu (masraf ödemesi → 195) doğruydu ama grup içindeki
    /// alt hesap araması difflib benzerliğiyle yapılıyor ve <b>yanlış kişiyi</b> seçiyordu:
    /// <code>
    /// "ABDULKADİR SAYICI Masraf Ödemesi" → 195 01 A20 Abdülkadir Yılmaz (0.65)
    /// "dilara sager masraf ödemesi"      → 195 01 D06 Dilara Kaya       (0.67)
    /// </code>
    /// Satır onay kuyruğunda olduğu için kayıt bozulmuyordu, ama kullanıcı ONAYLA'ya
    /// basarken yanlış kişi kolayca gözden kaçıyor.
    /// </summary>
    public class KisiEslestirmeTests
    {
        // ---- Ortam ----

        private static EkstreService Servis(CatalogContext db)
        {
            var secici = new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() });
            return new EkstreService(db, secici, new UnvanCikarici(), new AciklamaUretici(),
                                     new HesapEslestirici(), new HesapEslesmeService(db, BankaEkstreTestOrtami.Kapsam()),
                                     new SabitKullanici(), BankaEkstreTestOrtami.Kapsam());
        }

        /// <summary>
        /// Gerçek dosyayı, isteğe bağlı kişi yönlendirmeleriyle birlikte işler.
        /// Yönlendirmeler <b>yüklemeden önce</b> yazılır: katman yükleme anında çalışır.
        /// </summary>
        private static async Task<(CatalogContext Db, List<EkstreSatiri> Satirlar, int YuklemeId)> IsleAsync(
            params KisiYonlendirme[] yonlendirmeler)
        {
            var db = BankaEkstreTestOrtami.YeniContext();

            db.EkstreAciklamaSablonlari.AddRange(BankaEkstreTestOrtami.Sablonlar());
            db.EkstreUnvanDesenleri.AddRange(BankaEkstreTestOrtami.Desenler());
            db.EkstreSabitKurallar.AddRange(BankaEkstreTestOrtami.SabitKurallar());
            db.EkstreHesapPlani.AddRange(GercekHesapPlani.Kur());
            db.EkstreVergiKodlari.AddRange(GercekHesapPlani.VergiKodlari());
            if (yonlendirmeler.Length > 0) db.EkstreKisiYonlendirmeleri.AddRange(yonlendirmeler);

            var hesaplar = GercekHesapPlani.BankaHesaplari();
            hesaplar[0].HesapSahibiUnvani = GercekHesapPlani.HesapSahibi;
            hesaplar[0].HesapSahibiTakmaAdlari = GercekHesapPlani.TakmaAdlar;

            db.EkstreBankaHesaplari.AddRange(hesaplar);
            await db.SaveChangesAsync();

            using var dosya = BankaEkstreTestOrtami.GercekEkstre();
            var yukleme = await Servis(db).YukleAsync(hesaplar[0].Id, dosya, "Vakıfbank_Hesap_Ekstresi.xlsx");

            var satirlar = db.EkstreSatirlari
                .Where(s => s.EkstreYuklemeId == yukleme.Id)
                .OrderBy(s => s.SiraNo)
                .ToList();

            return (db, satirlar, yukleme.Id);
        }

        /// <summary>Açıklamasında verilen ifadelerin <b>hepsi</b> geçen tek satır.</summary>
        private static EkstreSatiri Satir(IEnumerable<EkstreSatiri> satirlar, params string[] ifadeler)
            => satirlar.First(s => ifadeler.All(i => s.HamAciklama.Contains(i, StringComparison.OrdinalIgnoreCase)));

        private static bool AdayVar(EkstreSatiri satir, string kod)
            => satir.Adaylar is not null && satir.Adaylar.Contains($"\"{kod}\"", StringComparison.Ordinal);

        private static KisiYonlendirme Yonlendirme(string isim, YonlendirmeYonu yon, string kod, string? ad = null) => new()
        {
            FirmaId = BankaEkstreTestOrtami.FirmaId,
            Isim = isim,
            IsimCekirdegi = Normalizasyon.Cekirdek(isim),
            Yon = yon,
            HesapKodu = kod,
            HesapAdi = ad,
            Aktif = true
        };

        // ---- 1. Kural grubu içinde yanlış kişi seçilmiyor ----

        [Fact]
        public async Task Kisi01_Abdulkadir_sayici_yakin_isimli_baska_kisiye_onerilmiyor()
        {
            // Kural "Masraf Ödemesi" → 195. Grup içinde "ABDULKADIR SAYICI" ile başlayan
            // hesap yok; benzerlik olsaydı 195 01 A20 Abdülkadir Yılmaz (0.65) önerilirdi.
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, "ABDULKADİR SAYICI Masraf Ödemesi Arta Tekmer");

            Assert.NotEqual("195 01 A20", satir.OnerilenHesapKodu);
            Assert.False(AdayVar(satir, "195 01 A20"));
            Assert.Equal(SatirDurum.OnayBekliyor, satir.Durum);
        }

        [Fact]
        public async Task Kisi02_Kural_ana_grubu_baska_gruptaki_tam_eslesmeyi_gizlemiyor()
        {
            // Kişi planda gerçekten var, ama 331 02 (ortaklar) altında. Kural 195'e
            // kilitlediği için bulunamıyordu; artık aday olarak listeleniyor.
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, "ABDULKADİR SAYICI Masraf Ödemesi Arta Tekmer");

            Assert.True(AdayVar(satir, "331 02"));
            Assert.Equal(SatirDurum.OnayBekliyor, satir.Durum);
            // Kod kutusunda kuralın ana grubu kalır; kişiyi kullanıcı seçer.
            Assert.Equal("195", satir.OnerilenHesapKodu);
        }

        [Fact]
        public async Task Kisi03_Planda_olmayan_kisi_icin_yakin_isim_onerilmiyor()
        {
            // "dilara sager" planda yok. Benzerlik 195 01 D06 Dilara Kaya'yı 0.67 ile
            // öneriyordu; artık alt hesap boş kalır ve yalnız ana grup önerilir.
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, "dilara sager hesabına giden FAST");

            Assert.Equal("195", satir.OnerilenHesapKodu);
            Assert.False(AdayVar(satir, "195 01 D06"));
            Assert.Equal(SatirDurum.OnayBekliyor, satir.Durum);
        }

        [Fact]
        public async Task Kisi04_Soyadsiz_isim_iki_adayla_onaya_duser()
        {
            // "… Akbank T.A.Ş. İlyas hesabına giden FAST ödemesi" — soyad yok. Planda
            // İlyas Ömeroğlu ve İlyas Yücel birlikte var; tahmin edilmez.
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, "Akbank T.A.Ş. İlyas hesabına giden FAST ödemesi)");

            Assert.Equal(SatirDurum.OnayBekliyor, satir.Durum);
            Assert.True(AdayVar(satir, "195 01 H13"));
            Assert.True(AdayVar(satir, "195 01 I02"));
        }

        [Theory]
        [InlineData("Mesut Aktaş hesabına giden FAST", "195 01 M05")]
        [InlineData("İlyas Ömeroğlu hesabına giden FAST", "195 01 H13")]
        [InlineData("EDA BUDAK hesabına giden FAST", "195 01 E03")]
        public async Task Kisi05_Ad_soyad_tam_gecen_satirlar_otomatik_cozulmeye_devam_ediyor(string ifade, string beklenen)
        {
            // Regresyon: önek yöntemi kapsamı daraltmamalı. Ad + soyad grup içinde birebir
            // tutuyorsa satır eskisi gibi otomatik kapanır.
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var uyanlar = satirlar.Where(s => s.HamAciklama.Contains(ifade, StringComparison.OrdinalIgnoreCase)).ToList();

            Assert.NotEmpty(uyanlar);
            Assert.All(uyanlar, s =>
            {
                Assert.Equal(beklenen, s.OnerilenHesapKodu);
                Assert.Equal(KaynakKatman.SabitKural, s.KaynakKatman);
                Assert.Equal(SatirDurum.Otomatik, s.Durum);
            });
        }

        // ---- 2. Kişi yönlendirme tablosu ----

        [Fact]
        public async Task Kisi06_Yonlendirme_sabit_kurali_gecer()
        {
            // Aynı satır: "masraf ödemesi" ifadesi 195'i işaret ediyor, ama kişi tabloda
            // tanımlı olduğu için 331 02'ye gidiyor ve otomatik çözülüyor.
            var (db, satirlar, _) = await IsleAsync(
                Yonlendirme("Abdulkadir Sayıcı", YonlendirmeYonu.Cikan, "331 02", "Abdulkadir Sayıcı"));
            using var _db = db;

            var satir = Satir(satirlar, "ABDULKADİR SAYICI Masraf Ödemesi Arta Tekmer");

            Assert.Equal("331 02", satir.OnerilenHesapKodu);
            Assert.Equal(KaynakKatman.KisiYonlendirme, satir.KaynakKatman);
            Assert.Equal(SatirDurum.Otomatik, satir.Durum);
        }

        [Fact]
        public async Task Kisi07_Yonlendirme_yalniz_tanimli_yonde_calisir()
        {
            // Aynı kişi için yalnız "Giren" tanımlıysa çıkan satır etkilenmemeli:
            // satır sabit kuralın yoluna geri düşer.
            var (db, satirlar, _) = await IsleAsync(
                Yonlendirme("Abdulkadir Sayıcı", YonlendirmeYonu.Giren, "120 A01"));
            using var _db = db;

            var satir = Satir(satirlar, "ABDULKADİR SAYICI Masraf Ödemesi Arta Tekmer");

            Assert.NotEqual(KaynakKatman.KisiYonlendirme, satir.KaynakKatman);
            Assert.Equal("195", satir.OnerilenHesapKodu);
        }

        [Fact]
        public void Kisi08_Ayni_kisi_icin_giren_ve_cikan_ayri_hesaba_gider()
        {
            // Aynı isim için iki kayıt: gelen tahsilat 120'ye, giden ödeme 331'e.
            var veri = new EslestirmeVerisi
            {
                SabitKurallar = BankaEkstreTestOrtami.SabitKurallar(),
                KisiYonlendirmeleri = new[]
                {
                    Yonlendirme("Abdulkadir Sayıcı", YonlendirmeYonu.Giren, "120 A01"),
                    Yonlendirme("Abdulkadir Sayıcı", YonlendirmeYonu.Cikan, "331 02")
                }
            };

            var eslestirici = new HesapEslestirici();

            var cikan = eslestirici.Coz(new SatirBaglami
            {
                IslemTipi = "Gönderilen havale",
                HamAciklama = "ABDULKADİR SAYICI Masraf Ödemesi",
                Unvan = "ABDULKADİR SAYICI",
                Yon = Yon.Cikan
            }, veri);

            var giren = eslestirici.Coz(new SatirBaglami
            {
                IslemTipi = "Alınan Havale",
                HamAciklama = "ABDULKADİR SAYICI tarafından gönderilen tutar",
                Unvan = "ABDULKADİR SAYICI",
                Yon = Yon.Giren
            }, veri);

            Assert.Equal("331 02", cikan.HesapKodu);
            Assert.Equal("120 A01", giren.HesapKodu);
            Assert.All(new[] { cikan, giren }, s => Assert.Equal(KaynakKatman.KisiYonlendirme, s.Katman));
        }

        [Fact]
        public void Kisi09_Yonu_belirtilmis_kayit_farketmezi_yener()
        {
            var veri = new EslestirmeVerisi
            {
                KisiYonlendirmeleri = new[]
                {
                    Yonlendirme("Abdulkadir Sayıcı", YonlendirmeYonu.Farketmez, "120 A01"),
                    Yonlendirme("Abdulkadir Sayıcı", YonlendirmeYonu.Cikan, "331 02")
                }
            };

            var sonuc = new HesapEslestirici().Coz(new SatirBaglami
            {
                IslemTipi = "Gönderilen havale",
                HamAciklama = "ABDULKADİR SAYICI ödemesi",
                Unvan = "ABDULKADİR SAYICI",
                Yon = Yon.Cikan
            }, veri);

            Assert.Equal("331 02", sonuc.HesapKodu);
        }

        [Fact]
        public void Kisi10_Yakin_isimli_baska_kisi_yonlendirmeye_takilmaz()
        {
            // Eşleşme tam; "Abdulkadir Şahin" tanımlıyken "Abdulkadir Sayıcı" satırı
            // yönlendirmeye düşmemeli.
            var veri = new EslestirmeVerisi
            {
                SabitKurallar = BankaEkstreTestOrtami.SabitKurallar(),
                KisiYonlendirmeleri = new[] { Yonlendirme("Abdülkadir Şahin", YonlendirmeYonu.Cikan, "331 03") }
            };

            var sonuc = new HesapEslestirici().Coz(new SatirBaglami
            {
                IslemTipi = "Gönderilen havale",
                HamAciklama = "ABDULKADİR SAYICI Masraf Ödemesi",
                Unvan = "ABDULKADİR SAYICI",
                Yon = Yon.Cikan
            }, veri);

            Assert.NotEqual(KaynakKatman.KisiYonlendirme, sonuc.Katman);
        }

        // ---- Onay ekranı kısayolu ----

        [Fact]
        public async Task Kisi11_Onaydaki_kisayol_yonlendirme_kaydi_olusturur()
        {
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, "ABDULKADİR SAYICI Masraf Ödemesi Arta Tekmer");

            var sonuc = await Servis(db).OnaylaAsync(satir.Id, "331 02", kisiYonlendir: true);

            Assert.NotNull(sonuc);

            var kayit = Assert.Single(db.EkstreKisiYonlendirmeleri.ToList());
            Assert.Equal("ABDULKADIR SAYICI", kayit.IsimCekirdegi);
            // Yön, onaylanan satırın yönünden gelir.
            Assert.Equal(YonlendirmeYonu.Cikan, kayit.Yon);
            Assert.Equal("331 02", kayit.HesapKodu);
            Assert.True(kayit.Aktif);
        }

        [Fact]
        public async Task Kisi12_Kisayol_isaretlenmezse_kayit_olusmaz()
        {
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, "ABDULKADİR SAYICI Masraf Ödemesi Arta Tekmer");

            await Servis(db).OnaylaAsync(satir.Id, "331 02");

            Assert.Empty(db.EkstreKisiYonlendirmeleri.ToList());
        }

        [Fact]
        public async Task Kisi13_Kisi_adi_okunamayan_satirda_kisayol_uyari_doner()
        {
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            // Unvanı hiç çıkarılamamış bir satır: yönlendirme yazılamaz.
            var satir = satirlar.First(s => string.IsNullOrWhiteSpace(s.CikarilanUnvan));

            var sonuc = await Servis(db).OnaylaAsync(satir.Id, "770", kisiYonlendir: true);

            Assert.Empty(db.EkstreKisiYonlendirmeleri.ToList());
            Assert.Contains("yönlendirme oluşturulmadı", sonuc!.Uyari);
        }

        // ---- Kişi yönlendirme tablosunun yönetimi ----

        private static KisiYonlendirmeYazDto Yaz(string isim, string kod, YonlendirmeYonu yon = YonlendirmeYonu.Cikan)
            => new() { Isim = isim, HesapKodu = kod, Yon = yon, Aktif = true };

        /// <summary>
        /// Yalnız hesap planı yüklü context. TenantNo'yu <c>SaveChangesAsync</c> dolduruyor;
        /// senkron <c>SaveChanges</c> kullanılırsa kayıtlar tenant'sız kalır.
        /// </summary>
        private static async Task<CatalogContext> PlanliContextAsync()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            db.EkstreHesapPlani.AddRange(GercekHesapPlani.Kur());
            await db.SaveChangesAsync();
            return db;
        }

        [Fact]
        public async Task Kisi14_Gecersiz_hesap_kodu_kaydedilmez()
        {
            using var db = await PlanliContextAsync();
            var servis = new KisiYonlendirmeService(db, BankaEkstreTestOrtami.Kapsam());

            var hata = await Assert.ThrowsAsync<BankaEkstreKuralException>(
                () => servis.CreateAsync(Yaz("Abdulkadir Sayıcı", "999 99")));

            Assert.Contains("hesap planında yok", hata.Message);
            Assert.Empty(db.EkstreKisiYonlendirmeleri.ToList());
        }

        [Fact]
        public async Task Kisi15_Kayit_normalize_cekirdek_ve_hesap_adiyla_yazilir()
        {
            using var db = await PlanliContextAsync();
            var servis = new KisiYonlendirmeService(db, BankaEkstreTestOrtami.Kapsam());

            var dto = await servis.CreateAsync(Yaz("Abdulkadir Sayıcı", "331 02"));

            Assert.Equal("ABDULKADIR SAYICI", dto.IsimCekirdegi);
            Assert.Equal("Abdulkadir Sayıcı", dto.HesapAdi);
        }

        [Fact]
        public async Task Kisi16_Ayni_isim_ve_yon_icin_ikinci_kayit_reddedilir()
        {
            using var db = await PlanliContextAsync();
            var servis = new KisiYonlendirmeService(db, BankaEkstreTestOrtami.Kapsam());

            await servis.CreateAsync(Yaz("Abdulkadir Sayıcı", "331 02"));

            var hata = await Assert.ThrowsAsync<BankaEkstreKuralException>(
                () => servis.CreateAsync(Yaz("ABDULKADİR SAYICI", "331 02")));

            Assert.Contains("zaten bir yönlendirme var", hata.Message);

            // Aynı isim, farklı yön: kabul edilir.
            await servis.CreateAsync(Yaz("Abdulkadir Sayıcı", "120 K11", YonlendirmeYonu.Giren));
            Assert.Equal(2, db.EkstreKisiYonlendirmeleri.Count());
        }

        // ---- 3. Analiz dışa aktarımı ----

        [Fact]
        public async Task Kisi17_Analiz_dokumu_cozulmemis_satir_varken_de_uretilir()
        {
            var (db, satirlar, yuklemeId) = await IsleAsync();
            using var _db = db;

            // Ön koşul: dosyada hâlâ çözülmemiş satır var.
            Assert.Contains(satirlar, s => s.Durum is SatirDurum.OnayBekliyor or SatirDurum.Cozulemedi);

            var servis = Servis(db);
            var dosya = await servis.AnalizDokumuAsync(yuklemeId);

            Assert.NotNull(dosya);
            Assert.EndsWith("-analiz.xlsx", dosya!.DosyaAdi);

            using var akis = new MemoryStream(dosya.Icerik);
            using var kitap = new XLWorkbook(akis);
            var sayfa = kitap.Worksheets.First();

            var basliklar = new[]
            {
                "SiraNo", "Tarih", "Yon", "Tutar", "HamAciklama", "UretilenAciklama",
                "OnerilenHesapKodu", "OnerilenHesapAdi", "GuvenSkoru", "KaynakKatman",
                "Durum", "AdaySayisi"
            };

            for (var i = 0; i < basliklar.Length; i++)
                Assert.Equal(basliklar[i], sayfa.Cell(1, i + 1).GetString());

            // Başlık satırı + tüm satırlar; durum filtresi yok.
            Assert.Equal(satirlar.Count + 1, sayfa.LastRowUsed()!.RowNumber());
        }

        [Fact]
        public async Task Kisi18_Kod_listesi_ve_duzeltilmis_ekstre_ayni_kisitla_kaliyor()
        {
            var (db, _, yuklemeId) = await IsleAsync();
            using var _db = db;

            var servis = Servis(db);

            await Assert.ThrowsAsync<BankaEkstreKuralException>(() => servis.DisaAktarAsync(yuklemeId));
            await Assert.ThrowsAsync<BankaEkstreKuralException>(() => servis.DuzeltilmisEkstreAsync(yuklemeId));
        }
    }
}
