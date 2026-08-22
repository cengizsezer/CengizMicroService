using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.Muhasebe;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Tur 2 kabul kriterleri — <b>gerçek Vakıfbank ekstresi</b> (287 satır) ve gerçek
    /// planın ölçülen özelliklerini taşıyan hesap planıyla.
    ///
    /// Taklit açıklama yazılmaz: her iddia dosyadaki satırın kendi metniyle sınanır.
    /// Satır, açıklamasında geçen bir ifadeyle bulunur (<see cref="Satir"/>), sıra
    /// numarasıyla değil — dosyada satır eklenip çıkarılınca testler bozulmasın.
    /// </summary>
    public class Tur2GercekVeriTests
    {
        // ---- Ortam ----

        private static EkstreService Servis(CatalogContext db)
        {
            var secici = new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() });
            return new EkstreService(db, secici, new UnvanCikarici(), new AciklamaUretici(),
                                     new HesapEslestirici(), new HesapEslesmeService(db), new SabitKullanici());
        }

        /// <summary>Gerçek dosyayı yükler ve işlenmiş satırları döner.</summary>
        private static async Task<(CatalogContext Db, List<EkstreSatiri> Satirlar, int HesapId)> IsleAsync(
            string? veritabaniAdi = null, bool takmaAdlarla = true)
        {
            var db = BankaEkstreTestOrtami.YeniContext(veritabaniAdi);

            db.EkstreAciklamaSablonlari.AddRange(BankaEkstreTestOrtami.Sablonlar());
            db.EkstreUnvanDesenleri.AddRange(BankaEkstreTestOrtami.Desenler());
            db.EkstreSabitKurallar.AddRange(BankaEkstreTestOrtami.SabitKurallar());
            db.EkstreHesapPlani.AddRange(GercekHesapPlani.Kur());
            db.EkstreVergiKodlari.AddRange(GercekHesapPlani.VergiKodlari());

            var hesaplar = GercekHesapPlani.BankaHesaplari();
            hesaplar[0].HesapSahibiUnvani = GercekHesapPlani.HesapSahibi;
            if (takmaAdlarla) hesaplar[0].HesapSahibiTakmaAdlari = GercekHesapPlani.TakmaAdlar;

            db.EkstreBankaHesaplari.AddRange(hesaplar);
            await db.SaveChangesAsync();

            using var dosya = BankaEkstreTestOrtami.GercekEkstre();
            var yukleme = await Servis(db).YukleAsync(hesaplar[0].Id, dosya, "Vakıfbank_Hesap_Ekstresi.xlsx");

            var satirlar = db.EkstreSatirlari
                .Where(s => s.EkstreYuklemeId == yukleme.Id)
                .OrderBy(s => s.SiraNo)
                .ToList();

            return (db, satirlar, hesaplar[0].Id);
        }

        /// <summary>Açıklamasında verilen ifadelerin <b>hepsi</b> geçen tek satır.</summary>
        private static EkstreSatiri Satir(IEnumerable<EkstreSatiri> satirlar, params string[] ifadeler)
            => satirlar.First(s => ifadeler.All(i => s.HamAciklama.Contains(i, StringComparison.OrdinalIgnoreCase)));

        private static List<EkstreSatiri> Satirlar(IEnumerable<EkstreSatiri> satirlar, params string[] ifadeler)
            => satirlar.Where(s => ifadeler.All(i => s.HamAciklama.Contains(i, StringComparison.OrdinalIgnoreCase))).ToList();

        private static bool AdayVar(EkstreSatiri satir, string kod)
            => satir.Adaylar is not null && satir.Adaylar.Contains($"\"{kod}\"", StringComparison.Ordinal);

        // ---- 1. Kesik hesap adı, önek eşleşmesi ----

        [Fact]
        public async Task Kriter01_Baycan_kesik_hesap_adina_ragmen_cozulur()
        {
            // Plan kaydı 50 karakterde kesik ("… Sanayi Ve Ticaret Ano"), açıklamada
            // "… SANAYİ VE TİCARET ANONİM" yazıyor. Bitişik alt metin tutmuyor; önek tutuyor.
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, "BAYCAN ELEKTRİK MÜTEAHHİTLİK SANAYİ VE TİCARET ANO");

            Assert.Equal("120 B62", satir.OnerilenHesapKodu);
            Assert.Equal(KaynakKatman.BenzersizOnek, satir.KaynakKatman);
            Assert.Equal(SatirDurum.Otomatik, satir.Durum);
        }

        [Fact]
        public async Task Kriter02_Solvia_onekten_cozulur()
        {
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, "SOLVİA YAZILIM VE DANIŞMANLIK ANONİM ŞTİ");

            Assert.Equal("120 S97", satir.OnerilenHesapKodu);
            Assert.Equal(SatirDurum.Otomatik, satir.Durum);
        }

        // ---- 3. Banka isimli cariler indekse girmez ----

        [Fact]
        public async Task Kriter03_Ziraat_bankasi_metni_banka_isimli_cariye_eslesmez()
        {
            // Ölçümde "ZİRAAT BANKASI" metni "320 1 10011 ZİRAAT BANK" carisine eşleşip
            // 16 satırı yanlış çözüyordu. Bankalar banka kayıt defteri katmanının işi.
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var ziraatliSatirlar = Satirlar(satirlar, "ZİRAAT BANKASI");
            Assert.NotEmpty(ziraatliSatirlar);

            var bankaCarileri = new[] { "320 1 10011", "320 1 10012", "320 1 10013", "320 1 10014", "320 1 10015" };

            Assert.All(ziraatliSatirlar, s =>
                Assert.DoesNotContain(s.OnerilenHesapKodu ?? string.Empty, bankaCarileri));
        }

        [Fact]
        public async Task Kriter03b_Banka_isimli_cariler_onek_indeksine_girmez()
        {
            var indeks = CariOnekIndeksi.Kur(GercekHesapPlani.Kur(),
                                             HesapSahibiKimligi.Kur(GercekHesapPlani.HesapSahibi));

            Assert.Empty(indeks.OneklerleBaslayanlar("ZIRAAT BANK"));
            Assert.Empty(indeks.OneklerleBaslayanlar("TURKIYE IS BANKASI"));
        }

        // ---- 4. Hesap sahibinin altıncı yazımı ----

        [Fact]
        public async Task Kriter04_Isgold_cozulur_ve_alici_yazimi_elenir()
        {
            // Alıcı "ADAY BAĞIMSIZ DENETİM VE SMMM A.Ş." — ana unvanı kapsamıyor, ancak
            // takma ad olarak eklenince eleniyor.
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, "İSGOLD ALTIN RAFİNERİSİ", "ADAY BAĞIMSIZ DENETİM VE SMMM");

            Assert.Equal("120 I55", satir.OnerilenHesapKodu);
            Assert.Equal(SatirDurum.Otomatik, satir.Durum);
            Assert.DoesNotContain("SMMM", satir.CikarilanUnvan ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Kriter15_Hesap_sahibinin_alti_yazimi_da_elenir()
        {
            var kimlik = HesapSahibiKimligi.Kur(GercekHesapPlani.HesapSahibi, GercekHesapPlani.TakmaAdlar);

            Assert.All(GercekHesapPlani.SahipYazimlari, y => Assert.True(kimlik.Kendisi(y), y));
        }

        [Fact]
        public async Task Kriter15b_Takma_ad_yokken_smmm_yazimi_elenmiyor()
        {
            // Düzeltmenin neyi çözdüğünü sabitler: tek alanla altıncı yazım karşı taraf sanılıyor.
            var kimlik = HesapSahibiKimligi.Kur(GercekHesapPlani.HesapSahibi);

            Assert.False(kimlik.Kendisi("ADAY BAĞIMSIZ DENETİM VE SMMM A.Ş."));
            Assert.True(kimlik.Kendisi("ADAY BAĞIMSIZ DENETİM"));
            Assert.True(kimlik.Kendisi("PKF ADAY"));
        }

        // ---- 5–6. Yön kuralı sahte belirsizliği çözer ----

        [Fact]
        public async Task Kriter05_Burak_gunel_yon_kuraliyla_cozulur()
        {
            // 159 B41 / 329 B41 — hesap adları birebir aynı, fark yalnız ana grup.
            // Para çıkıyor → 329.
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, "Enpara Bank A.Ş. BURAK GÜNEL hesabına giden FAST");

            Assert.Equal("329 B41", satir.OnerilenHesapKodu);
            Assert.Equal(SatirDurum.Otomatik, satir.Durum);
        }

        [Theory]
        [InlineData("YURTİÇİ KARGO", "329 Y10")]
        [InlineData("ARAS KARGO", "329 A20")]
        public async Task Kriter06_Kargo_satirlari_yon_kuraliyla_cozulur(string ifade, string beklenenKod)
        {
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, ifade);

            Assert.Equal(beklenenKod, satir.OnerilenHesapKodu);
            Assert.Equal(SatirDurum.Otomatik, satir.Durum);
        }

        [Fact]
        public void Kriter06b_Yon_kurali_yalniz_adlar_ayniyken_calisir()
        {
            var ayni = new[]
            {
                GercekHesapPlani.Kayit("159 B41", "Burak Günel"),
                GercekHesapPlani.Kayit("329 B41", "Burak Günel")
            };

            Assert.Equal("329 B41", CariOnekIndeksi.YonleCoz(ayni, Yon.Cikan)?.Kod);
            Assert.Equal("159 B41", CariOnekIndeksi.YonleCoz(ayni, Yon.Giren)?.Kod);

            // Adlar farklıysa belirsizlik gerçektir; yön karar vermez.
            var farkli = new[]
            {
                GercekHesapPlani.Kayit("329 P04", "Park Plaza Yönetimi, Aidat"),
                GercekHesapPlani.Kayit("329 P05", "Park Plaza Yönetimi, Elektrik")
            };

            Assert.Null(CariOnekIndeksi.YonleCoz(farkli, Yon.Cikan));
        }

        // ---- 7–8. Hesaplar arası EFT ----

        [Theory]
        [InlineData("HESAPLAR ARASI E.F.T. VAKIFBANK/DENİZBANK", "102 1 3 02", "Denizbank")]
        [InlineData("HESAPLAR ARASI EFT VAKIFBANK/TÜRKİYE İŞ BANKASI", "102 1 5 01", "İş Bankası")]
        public async Task Kriter0708_Hesaplar_arasi_eft_karsi_bankaya_cozulur(
            string ifade, string beklenenKod, string aciklamadaGecen)
        {
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, ifade);

            Assert.Equal(beklenenKod, satir.OnerilenHesapKodu);
            Assert.Equal(KaynakKatman.BankaKayitDefteri, satir.KaynakKatman);
            Assert.Contains(aciklamadaGecen, satir.UretilenAciklama ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        // ---- 9–10. Gerçek belirsizlik onaya düşer ----

        [Fact]
        public async Task Kriter09_Kemal_gulman_park_plaza_satiri_onaya_duser()
        {
            // Metinde hem "KEMAL GÜLMAN" hem "POLAT GÜLMAN" hem "PARK PLAZA" geçiyor;
            // ilk tek sonucu kabul etmek yanlış cariye otomatik kayıt atmak olurdu.
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, "KEMAL GÜLMAN VK POLAT GÜLMAN PARK PLAZA 19.KAT");

            Assert.Equal(SatirDurum.OnayBekliyor, satir.Durum);
            Assert.Null(satir.OnerilenHesapKodu);
            Assert.True(AdayVar(satir, "120 K11"), "Kemal Gülman aday olmalı");
            Assert.True(AdayVar(satir, "329 P90"), "Polat Gülman aday olmalı");
        }

        [Fact]
        public async Task Kriter10_Pardus_portfoy_ailesi_onaya_duser()
        {
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, "PARDUS PORTFÖY YÖNETİMİ ANONİM ŞİRKETİ");

            Assert.Equal(SatirDurum.OnayBekliyor, satir.Durum);
            Assert.Null(satir.OnerilenHesapKodu);
            Assert.False(string.IsNullOrWhiteSpace(satir.BelirsizlikAnahtari));
            Assert.Contains("PARDUS PORTFOY YONETIMI", satir.BelirsizlikAnahtari!, StringComparison.Ordinal);
        }

        // ---- 11. Belirsizlik öğrenilir ----

        [Fact]
        public async Task Kriter11_Belirsizlik_bir_kez_cozulunce_ikinci_yuklemede_sorulmaz()
        {
            var veritabani = $"tur2-ogrenme-{Guid.NewGuid()}";
            var (db, satirlar, hesapId) = await IsleAsync(veritabani);
            using var _db = db;

            var belirsiz = Satir(satirlar, "PARDUS PORTFÖY YÖNETİMİ ANONİM ŞİRKETİ");
            var anahtar = belirsiz.BelirsizlikAnahtari!;
            var ozet = belirsiz.AdayKumesiOzeti!;

            await Servis(db).OnaylaAsync(belirsiz.Id, "120 F01");

            var kayit = db.EkstreHesapEslesmeleri.Single(e => e.AnahtarTipi == AnahtarTipi.Belirsizlik);
            Assert.Equal(anahtar, kayit.AnahtarCekirdek);
            Assert.Equal(ozet, kayit.AdayKumesiOzeti);
            Assert.Equal("120 F01", kayit.HesapKodu);

            // İkinci yükleme: aynı belirsizlik artık sorulmuyor.
            using var dosya = BankaEkstreTestOrtami.GercekEkstre();
            var ikinci = await Servis(db).YukleAsync(hesapId, dosya, "Vakıfbank_Hesap_Ekstresi.xlsx");

            var ikinciSatir = Satir(
                db.EkstreSatirlari.Where(s => s.EkstreYuklemeId == ikinci.Id).ToList(),
                "PARDUS PORTFÖY YÖNETİMİ ANONİM ŞİRKETİ");

            Assert.Equal("120 F01", ikinciSatir.OnerilenHesapKodu);
            Assert.Equal(SatirDurum.Otomatik, ikinciSatir.Durum);
            Assert.Equal(KaynakKatman.GecmisOnay, ikinciSatir.KaynakKatman);
        }

        [Fact]
        public async Task Kriter11b_Aday_kumesi_degisirse_ogrenilen_karar_uygulanmaz()
        {
            var veritabani = $"tur2-kume-{Guid.NewGuid()}";
            var (db, satirlar, hesapId) = await IsleAsync(veritabani);
            using var _db = db;

            var belirsiz = Satir(satirlar, "PARDUS PORTFÖY YÖNETİMİ ANONİM ŞİRKETİ");
            await Servis(db).OnaylaAsync(belirsiz.Id, "120 F01");

            // Yeni bir aile üyesi açılıyor: eski karar sessizce uygulanmamalı, aksi hâlde
            // yeni açılan hesap hiç görünmez olurdu.
            db.EkstreHesapPlani.Add(GercekHesapPlani.Kayit("120 F99", "Pardus Portföy Yönetimi A.Ş. Yeni Girişim Fonu"));
            await db.SaveChangesAsync();

            using var dosya = BankaEkstreTestOrtami.GercekEkstre();
            var ikinci = await Servis(db).YukleAsync(hesapId, dosya, "Vakıfbank_Hesap_Ekstresi.xlsx");

            var ikinciSatir = Satir(
                db.EkstreSatirlari.Where(s => s.EkstreYuklemeId == ikinci.Id).ToList(),
                "PARDUS PORTFÖY YÖNETİMİ ANONİM ŞİRKETİ");

            Assert.Equal(SatirDurum.OnayBekliyor, ikinciSatir.Durum);
            Assert.Null(ikinciSatir.OnerilenHesapKodu);
        }

        // ---- 12. Açıklamanın sonundaki satıcı adı ----

        [Theory]
        [InlineData("Belbim Temsilci Tahsilatı", "Belbim", "329 B43")]
        [InlineData("SuperonlineTahsilatı", "Superonline", "329 T06")]
        [InlineData("Turknet Tahsilatı", "Turknet", "329 T61")]
        [InlineData("Türk Telekom Ses/Data/ICT Tahsilatı", "Türk Telekom", "329 T01")]
        public async Task Kriter12_Tahsilat_satirlari_saticiya_cozulur(string ifade, string beklenenUnvan, string beklenenKod)
        {
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, ifade);

            Assert.Equal(beklenenUnvan, satir.CikarilanUnvan);
            Assert.Equal(beklenenKod, satir.OnerilenHesapKodu);
            Assert.Equal(SatirDurum.Otomatik, satir.Durum);
        }

        [Fact]
        public async Task Kriter12b_Ad_soyad_unvan_alani_unvan_kaynagi_degil()
        {
            // Maskeli yazım ("PK* AD** BA****** DE*****") hesap sahibi elemesine takılmaz;
            // alan etiketi engellendiği için yine de unvan sayılmaz.
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, "PK* AD** BA****** DE*****");

            Assert.DoesNotContain("*", satir.CikarilanUnvan ?? string.Empty, StringComparison.Ordinal);
        }

        // ---- 13. Düşük skorlu öneri gösterilmez ----

        [Fact]
        public async Task Kriter13_Dusuk_skorlu_oneri_gosterilmez()
        {
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            // Eşiğin altındaki hiçbir satırda kod önerilmemeli; ölçümde 0.20 skorla
            // "329 A33 Adobe Systems Ireland" öneriliyordu.
            var dusukSkorlular = satirlar
                .Where(s => s.KaynakKatman == KaynakKatman.UnvanBenzerligi)
                .Where(s => s.GuvenSkoru > 0m && s.GuvenSkoru < HesapEslestirici.EnAzOneriEsigi)
                .ToList();

            Assert.Empty(dusukSkorlular);

            // Alakasız cariler hiçbir satıra önerilmemiş olmalı.
            Assert.DoesNotContain(satirlar, s => s.OnerilenHesapKodu == "329 A33");
            Assert.DoesNotContain(satirlar, s => s.OnerilenHesapKodu == "329 N21");
        }

        [Fact]
        public void Kriter13b_Esik_altindaki_aday_cozulemedi_yapar()
        {
            var eslestirici = new HesapEslestirici();
            var veri = new EslestirmeVerisi
            {
                HesapPlani = new[] { GercekHesapPlani.Kayit("329 A33", "Adobe Systems Ireland") }
            };

            var baglam = new SatirBaglami
            {
                IslemTipi = "Superonline Tahsilatı",
                HamAciklama = "Superonline Tahsilatı",
                Unvan = "Superonline",
                Yon = Yon.Cikan
            };

            var sonuc = eslestirici.Coz(baglam, veri);

            Assert.Equal(SatirDurum.Cozulemedi, sonuc.Durum);
            Assert.Null(sonuc.HesapKodu);
            Assert.Empty(sonuc.Adaylar);
        }

        // ---- 14. Vergi ve plaka ----

        [Fact]
        public async Task Kriter14_Trafik_cezasi_satiri_onaya_duser_ve_iki_aday_listelenir()
        {
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, "9085/TRAFiK CEZ", "Plaka:34MRP081");

            Assert.Equal(SatirDurum.OnayBekliyor, satir.Durum);
            Assert.Equal(KaynakKatman.VergiPlaka, satir.KaynakKatman);
            Assert.True(AdayVar(satir, "689 9 1"), "KKEG hesabı aday olmalı");
            Assert.True(AdayVar(satir, "740 99 01 01 09"), "Plakalı araç hesabı aday olmalı");
        }

        [Fact]
        public async Task Kriter14b_Damga_vergisi_tek_adayla_otomatik_cozulur()
        {
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, "0040/S.DAMGA V.");

            Assert.Equal("360 01 004", satir.OnerilenHesapKodu);
            Assert.Equal(KaynakKatman.VergiPlaka, satir.KaynakKatman);
            Assert.Equal(SatirDurum.Otomatik, satir.Durum);
        }

        [Fact]
        public async Task Kriter14c_Eslemesi_olmayan_vergi_kodu_onaya_duser()
        {
            // "0010/KURUMLAR V." eşleme tablosunda yok: tahmin edilmez, kullanıcıya sorulur.
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, "0010/0010/KURUMLAR V.");

            Assert.Equal(SatirDurum.OnayBekliyor, satir.Durum);
            Assert.Null(satir.OnerilenHesapKodu);
        }

        [Fact]
        public async Task Kriter14d_Vergi_satirinda_unvan_cikarilmaz()
        {
            // Açıklamadaki "Soyadi/Unvani :PKF ADAY …" hesap sahibinin kendi unvanı.
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var vergiSatirlari = satirlar.Where(s => VergiPlakaCozucu.VergiSatiriMi(s.IslemTipi)).ToList();

            Assert.NotEmpty(vergiSatirlari);
            Assert.All(vergiSatirlari, s => Assert.Null(s.CikarilanUnvan));
            Assert.All(vergiSatirlari, s => Assert.Null(s.AnahtarCekirdek));
        }

        // ---- Banka kayıt defteri katmanının tetikleyicisi ----

        /// <summary>
        /// Katman önceden "açıklamada banka adı geçiyor" diye tetikleniyordu. Ölçüm: 87 cari
        /// satırının 59'unda gönderenin bankası açıklamada geçiyor — hepsi cari katmanlarına
        /// gitmeli. Katman artık yalnız (a) bankalar arası ifadesi veya (b) karşı tarafın
        /// hesap sahibinin kendisi olması hâlinde çalışıyor.
        /// </summary>
        [Fact]
        public async Task Aciklamada_banka_adi_gecmesi_tek_basina_banka_katmanini_actirmaz()
        {
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            // "BAYCAN A.Ş. CARİ HESAP ÖDEME/TÜRKİYE CUMHURİYETİ ZİRAAT BANKASI A.Ş.-…"
            var satir = Satir(satirlar, "BAYCAN ELEKTRİK MÜTEAHHİTLİK SANAYİ VE TİCARET ANO");

            Assert.Equal("120 B62", satir.OnerilenHesapKodu);
            Assert.NotEqual(KaynakKatman.BankaKayitDefteri, satir.KaynakKatman);
        }

        [Theory]
        [InlineData("MARBAŞ MENKUL DEĞERLER ANONİM ŞTİ.", "120 M40")]
        [InlineData("DEMET DÖVİZ YETKİLİ MÜESSESE ANONİM ŞİRKETİ", "120 D50")]
        public async Task Gonderenin_bankasi_yazan_musteri_odemesi_cari_katmanina_ulasir(
            string ifade, string beklenenKod)
        {
            // "… sorgu no'lu Akbank T.A.Ş. MARBAŞ MENKUL DEĞERLER ANONİM ŞTİ. hesabından
            //  PKF ADAY … hesabına gelen FAST ödemesi" — gönderenin bankası açıklamada
            // geçiyor ama karşı taraf gerçek bir cari; satır banka hesabına gitmemeli.
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satir = Satir(satirlar, ifade);

            Assert.NotEqual(KaynakKatman.BankaKayitDefteri, satir.KaynakKatman);
            Assert.Equal(beklenenKod, satir.OnerilenHesapKodu);
        }

        /// <summary>
        /// (b) koşulu: bankalar arası ifadesi olmayan gerçek kendi hesapları arası
        /// transferler. Karşı taraf olarak yalnız hesap sahibinin kendisi (veya bankanın
        /// adı) çıkıyor; katman bu satırlarda çalışmaya devam etmeli.
        /// </summary>
        [Theory]
        [InlineData("İŞ BANKASI  (PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ VADESİZ HESABINDAN", "102 1 5 01")]
        [InlineData("DENİZBANK HESABINA (PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ VADESİZ HESABINDAN", "102 1 3 02")]
        public async Task Ifadesiz_kendi_hesaplari_arasi_transfer_b_kosuluyla_yakalanir(
            string ifade, string beklenenKod)
        {
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var satirlarDizi = Satirlar(satirlar, ifade);
            Assert.NotEmpty(satirlarDizi);

            Assert.All(satirlarDizi, s =>
            {
                Assert.Equal(KaynakKatman.BankaKayitDefteri, s.KaynakKatman);
                Assert.Equal(beklenenKod, s.OnerilenHesapKodu);
            });
        }

        [Fact]
        public async Task Banka_katmani_yalniz_iki_kosuldan_biri_tutunca_calisir()
        {
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var bankayaGidenler = satirlar
                .Where(s => s.KaynakKatman == KaynakKatman.BankaKayitDefteri)
                .ToList();

            Assert.NotEmpty(bankayaGidenler);

            // Her biri ya bankalar arası ifadesi taşıyor ya da karşı tarafı hesap sahibinin
            // kendisi (bu durumda desenler ya hiç unvan vermiyor ya da banka adı veriyor).
            Assert.All(bankayaGidenler, s =>
            {
                var metin = Normalizasyon.KisaltmaNormalize(s.HamAciklama + " " + s.IslemTipi);
                var ifadeVar = new[] { "HESAPLAR ARASI", "HESAPLARARASI", "VIRMAN", "SUPURME" }
                    .Any(i => Normalizasyon.IfadeVarMi(metin, i));

                var karsiTarafBankaVeyaBos = string.IsNullOrWhiteSpace(s.CikarilanUnvan) ||
                                             Normalizasyon.BankaAdliMi(s.CikarilanUnvan) ||
                                             s.CikarilanUnvan!.Contains("BANK", StringComparison.OrdinalIgnoreCase) ||
                                             s.CikarilanUnvan!.Contains("İŞ BANKASI", StringComparison.OrdinalIgnoreCase);

                Assert.True(ifadeVar || karsiTarafBankaVeyaBos,
                            $"Satır {s.SiraNo} iki koşulu da sağlamıyor: {s.HamAciklama}");
            });
        }

        // ---- Genel: yanlış eşleşme yok ----

        [Fact]
        public async Task Otomatik_cozulen_hicbir_satir_hesap_sahibinin_kendi_carisine_gitmez()
        {
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            // Ölçülen yanlış eşleşmelerin hedefleri: firmanın kendi adını taşıyan gider
            // hesapları ve ona benzeyen dernek kaydı.
            var yasakli = new[] { "622 0 03 00", "740 0", "120 B58" };

            var hatalilar = satirlar
                .Where(s => s.Durum == SatirDurum.Otomatik)
                .Where(s => yasakli.Contains(s.OnerilenHesapKodu ?? string.Empty))
                .ToList();

            Assert.Empty(hatalilar);
        }

        [Fact]
        public async Task Otomatik_cozulen_satirlarin_hepsi_dogru_cariye_gider()
        {
            // Tur 2'nin asıl iddiası bu: <b>otomatik</b> çözülen satırda yanlış kayıt olmamalı.
            // Fikstür planındaki her cari için satırın açıklamasında o carinin adının geçmesi
            // beklenir; geçmiyorsa eşleşme uydurmadır.
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            var beklenen = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["120 B62"] = "BAYCAN",
                ["120 S97"] = "SOLVİA",
                ["120 I55"] = "İSGOLD",
                ["120 H30"] = "HAKAN",
                ["329 B41"] = "BURAK GÜNEL",
                ["329 Y10"] = "YURTİÇİ KARGO",
                ["329 A20"] = "ARAS KARGO",
                ["329 Z01"] = "ZAFER GENÇ",
                ["329 U05"] = "UFUK ÇOLAK",
                ["329 B43"] = "Belbim",
                ["329 T06"] = "Superonline",
                ["329 T61"] = "Turknet",
                ["329 T01"] = "Türk Telekom"
            };

            var otomatikler = satirlar
                .Where(s => s.Durum == SatirDurum.Otomatik)
                .Where(s => beklenen.ContainsKey(s.OnerilenHesapKodu ?? string.Empty))
                .ToList();

            Assert.NotEmpty(otomatikler);

            Assert.All(otomatikler, s => Assert.Contains(
                beklenen[s.OnerilenHesapKodu!], s.HamAciklama, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Butun_satirlar_islenir_ve_hicbiri_onaylanmis_gelmez()
        {
            var (db, satirlar, _) = await IsleAsync();
            using var _db = db;

            Assert.Equal(287, satirlar.Count);
            Assert.All(satirlar, s => Assert.NotEqual(SatirDurum.Onaylandi, s.Durum));
        }
    }
}
