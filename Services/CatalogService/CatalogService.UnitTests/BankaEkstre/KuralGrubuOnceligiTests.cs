using CatalogService.Api.Features.BankaEkstre;
using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.Muhasebe;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Sabit kuralın ana grubu, grup içindeki alt hesap adaylarını <b>önceliklendirir</b>.
    ///
    /// Ölçülen sorun: kural bir ana grup belirlediğinde (MAAŞ AVANSI → 196) grup içindeki
    /// arama, diğer gruplardaki aynı isimli kayıtları da eşit aday sayıyor ve satır
    /// gereksiz yere onaya düşüyordu. Gerçek örnek — ÖMER CAN DİZDAR planda üç kez var:
    /// <code>
    /// 195 01 O09      Ömer Can Dizdar   (iş avansı)
    /// 196 03 25 O04   Ömer Can Dizdar   (maaş avansı)   ← kuralın grubu
    /// 335 01 O09      Ömer Can Dizdar   (personele borçlar)
    /// </code>
    /// Kural 196 dediği ve o grupta <b>tam bir tane</b> aday olduğu için satır otomatik
    /// çözülmeli; diğer gruplardaki kayıtlar alternatif olarak kalır ama engellemez.
    ///
    /// Kural birden fazla ana grup da kapsayabilir (<c>Avans → 195, 196</c>): o zaman sayım
    /// grupların toplamı üzerinden yapılır.
    /// </summary>
    public class KuralGrubuOnceligiTests
    {
        private readonly HesapEslestirici _eslestirici = new();

        // ---- Ortam ----

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

        /// <summary>
        /// Ölçülen ORKA planından kişi muavinleri. Kodlar ve adlar gerçek plandaki hâliyle:
        /// aynı kişinin birden fazla grupta kaydı var, bu testlerin bütün mesele ettiği şey de bu.
        /// </summary>
        private static List<HesapPlaniKaydi> GercekMuavinler() => new()
        {
            // Üç grupta birden duran personel (senaryo 1).
            Plan("195 01 O09", "Ömer Can Dizdar"),
            Plan("196 03 25 O04", "Ömer Can Dizdar"),
            Plan("335 01 O09", "Ömer Can Dizdar"),

            // Kuralın grubunda iki kaydı olan personel (senaryo 2).
            Plan("196 03 25 E01", "Emirhan Özer"),
            Plan("196 IU 77", "Emirhan Özer"),

            // Yalnız ortaklar altında duran kişi (senaryo 3).
            Plan("331 02", "Abdulkadir Sayıcı"),

            // Yalnız iş avansı grubunda duran personel (çoklu grup testleri).
            Plan("195 01 H13", "İlyas Ömeroğlu")
        };

        private static EslestirmeVerisi Veri(IEnumerable<SabitKural>? kurallar = null)
            => new()
            {
                SabitKurallar = (kurallar ?? BankaEkstreTestOrtami.SabitKurallar()).ToList(),
                HesapPlani = GercekMuavinler()
            };

        private static SatirBaglami Baglam(string hamAciklama, string unvan) => new()
        {
            IslemTipi = "FAST Anlık Ödeme",
            HamAciklama = hamAciklama,
            Unvan = unvan,
            Yon = Yon.Cikan
        };

        /// <summary>
        /// Gerçek dosyadaki giden FAST açıklamasının kalıbı; ölçülen satırla birebir aynı
        /// gövde (bkz. <c>Vakıfbank_Hesap_Ekstresi.xlsx</c> 49. satır).
        /// </summary>
        private static string FastAciklamasi(string konu, string kisi)
            => $"{konu} (28/07/2026 tarihli 2811932862 sorgu no'lu PKF ADAY BAĞIMSIZ DENETİM " +
               $"ANONİM ŞİRKETİ hesabından Türkiye Garanti Bankası A.Ş. {kisi} hesabına giden FAST ödemesi)";

        private static bool AdayVar(EslestirmeSonuc sonuc, string kod)
            => sonuc.Adaylar.Any(a => string.Equals(a.Kod, kod, StringComparison.Ordinal));

        // ---- Doğrulanmış senaryolar ----

        [Fact]
        public void Senaryo1_Kural_grubunda_tek_aday_varsa_otomatik_secilir()
        {
            // MAAŞ AVANSI → 196. O grupta tam bir "Ömer Can Dizdar" var; 195 ve 335'teki
            // kayıtlar otomatik çözümü ENGELLEMEZ.
            var sonuc = _eslestirici.Coz(
                Baglam(FastAciklamasi("MAAŞ AVANSI", "ÖMER CAN DİZDAR"), "ÖMER CAN DİZDAR"), Veri());

            Assert.Equal("196 03 25 O04", sonuc.HesapKodu);
            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
            Assert.Equal(KaynakKatman.SabitKural, sonuc.Katman);
        }

        [Fact]
        public void Senaryo1_Diger_gruplardaki_kayitlar_alternatif_olarak_kalir()
        {
            // Otomatik çözülse de kullanıcı satırı açtığında iş avansı kaydını görebilmeli.
            var sonuc = _eslestirici.Coz(
                Baglam(FastAciklamasi("MAAŞ AVANSI", "ÖMER CAN DİZDAR"), "ÖMER CAN DİZDAR"), Veri());

            Assert.True(AdayVar(sonuc, "196 03 25 O04"));
            Assert.True(AdayVar(sonuc, "195 01 O09"));
            // Seçilen kod listenin başında durur.
            Assert.Equal("196 03 25 O04", sonuc.Adaylar[0].Kod);
        }

        [Fact]
        public void Senaryo2_Kural_grubunda_iki_aday_varsa_satir_onaya_duser()
        {
            var sonuc = _eslestirici.Coz(
                Baglam(FastAciklamasi("MAAŞ AVANSI", "EMİRHAN ÖZER"), "EMİRHAN ÖZER"), Veri());

            Assert.Equal(SatirDurum.OnayBekliyor, sonuc.Durum);
            Assert.True(AdayVar(sonuc, "196 03 25 E01"));
            Assert.True(AdayVar(sonuc, "196 IU 77"));
            // Kod kutusunda kuralın ana grubu kalır; kişiyi kullanıcı seçer.
            Assert.Equal("196", sonuc.HesapKodu);
        }

        [Fact]
        public void Senaryo3_Kural_grubunda_hic_aday_yoksa_diger_gruptaki_karsilik_listelenir()
        {
            // Masraf Ödemesi → 195. Kişi planda var ama yalnız ortaklar (331 02) altında:
            // otomatik çözülmez, o kayıt aday olarak gösterilir.
            var sonuc = _eslestirici.Coz(
                Baglam(FastAciklamasi("MASRAF ÖDEMESİ", "ABDULKADİR SAYICI"), "ABDULKADİR SAYICI"), Veri());

            Assert.Equal(SatirDurum.OnayBekliyor, sonuc.Durum);
            Assert.True(AdayVar(sonuc, "331 02"));
            Assert.Equal("195", sonuc.HesapKodu);
        }

        [Fact]
        public void Senaryo4_Planda_hic_karsiligi_olmayan_kisi_icin_oneri_uretilmez()
        {
            // "EMİRHAN ÖZDEMİR" planda yok. Yakın isimli "Emirhan Özer" ÖNERİLMEMELİ:
            // önek yöntemi tam kelime sınırı arıyor, benzerliğe düşmüyor.
            var sonuc = _eslestirici.Coz(
                Baglam(FastAciklamasi("MAAŞ AVANSI", "EMİRHAN ÖZDEMİR"), "EMİRHAN ÖZDEMİR"), Veri());

            Assert.Equal(SatirDurum.OnayBekliyor, sonuc.Durum);
            Assert.Equal("196", sonuc.HesapKodu);
            Assert.Empty(sonuc.Adaylar);
        }

        // ---- Çoklu ana grup ----

        /// <summary>
        /// Genel "Avans" kuralı: hangi avans olduğunu söylemiyor, iki grubu birden kapsar.
        /// Üretim seed'indeki satırla aynı biçim.
        /// </summary>
        private static List<SabitKural> CokGrupluAvansKurali() => new()
        {
            new SabitKural
            {
                ParserTipi = BankaEkstreTestOrtami.ParserTipi,
                IslemTipiDeseni = "Avans",
                Kapsam = KuralKapsami.Aciklama,
                EslesmeTuru = EslesmeTuru.Icerir,
                HesapKodu = "196",
                HesapAdi = "Personel Avansları",
                Guven = 0.95m,
                UnvanCikarilsin = false,
                AltHesapGerekli = true,
                AnaGruplar = "195, 196",
                Sira = 40,
                Aktif = true
            }
        };

        [Fact]
        public void Coklu_grupta_toplam_tek_aday_varsa_otomatik_secilir()
        {
            // "İlyas Ömeroğlu" yalnız 195'te var; kural 195 ve 196'yı birlikte tarıyor,
            // toplamda tek aday çıkıyor.
            var sonuc = _eslestirici.Coz(
                Baglam(FastAciklamasi("AVANS", "İLYAS ÖMEROĞLU"), "İLYAS ÖMEROĞLU"),
                Veri(CokGrupluAvansKurali()));

            Assert.Equal("195 01 H13", sonuc.HesapKodu);
            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
        }

        [Fact]
        public void Coklu_grupta_birden_fazla_aday_varsa_hepsi_listelenir()
        {
            // "Ömer Can Dizdar" hem 195'te hem 196'da var: hangi avans olduğu bilinmiyor.
            var sonuc = _eslestirici.Coz(
                Baglam(FastAciklamasi("AVANS", "ÖMER CAN DİZDAR"), "ÖMER CAN DİZDAR"),
                Veri(CokGrupluAvansKurali()));

            Assert.Equal(SatirDurum.OnayBekliyor, sonuc.Durum);
            Assert.True(AdayVar(sonuc, "195 01 O09"));
            Assert.True(AdayVar(sonuc, "196 03 25 O04"));
        }

        [Fact]
        public void Coklu_grupta_aday_yoksa_kod_onerilmez()
        {
            // Tek gruplu kuralda kutuda ana grup kalırdı (195/196). Çoklu grupta hangisinin
            // kastedildiği bilinmiyor: birini yazmak kullanıcıyı yanlış gruba yönlendirirdi.
            var sonuc = _eslestirici.Coz(
                Baglam(FastAciklamasi("AVANS", "EMİRHAN ÖZDEMİR"), "EMİRHAN ÖZDEMİR"),
                Veri(CokGrupluAvansKurali()));

            Assert.Equal(SatirDurum.OnayBekliyor, sonuc.Durum);
            Assert.Null(sonuc.HesapKodu);
            Assert.Empty(sonuc.Adaylar);
        }

        [Fact]
        public void Coklu_grup_kuralin_disindaki_gruplari_kapsamaz()
        {
            // 331 kuralın kümesinde değil: aday olur ama grup içi sayıma girmez, bu yüzden
            // satır otomatik çözülmez.
            var sonuc = _eslestirici.Coz(
                Baglam(FastAciklamasi("AVANS", "ABDULKADİR SAYICI"), "ABDULKADİR SAYICI"),
                Veri(CokGrupluAvansKurali()));

            Assert.Equal(SatirDurum.OnayBekliyor, sonuc.Durum);
            Assert.True(AdayVar(sonuc, "331 02"));
        }

        [Fact]
        public void Tek_kelimelik_isim_hicbir_zaman_otomatik_secilmez()
        {
            // "İLYAS" ile başlayan tek kayıt var ama planda İlyas Yücel de olabilir;
            // ad+soyad verilmemişse tahmin edilmez.
            var sonuc = _eslestirici.Coz(
                Baglam(FastAciklamasi("MASRAF ÖDEMESİ", "İLYAS"), "İLYAS"), Veri());

            Assert.Equal(SatirDurum.OnayBekliyor, sonuc.Durum);
            Assert.True(AdayVar(sonuc, "195 01 H13"));
        }

        // ---- Uçtan uca: gerçek dosyanın kendi satırlarıyla ----

        private static EkstreService Servis(CatalogContext db)
        {
            var secici = new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() });
            return new EkstreService(db, secici, new UnvanCikarici(), new AciklamaUretici(),
                                     new HesapEslestirici(), new HesapEslesmeService(db, BankaEkstreTestOrtami.Kapsam()),
                                     new SabitKullanici(), BankaEkstreTestOrtami.Kapsam());
        }

        /// <summary>
        /// Depo kökündeki gerçek Vakıfbank ekstresini gerçek hesap planıyla işler. Dört
        /// senaryonun dördü de bu dosyanın son satırlarında duruyor (286–289. satırlar).
        /// </summary>
        private static async Task<(CatalogContext Db, List<EkstreSatiri> Satirlar)> GercekDosyayiIsleAsync()
        {
            var db = BankaEkstreTestOrtami.YeniContext();

            db.EkstreAciklamaSablonlari.AddRange(BankaEkstreTestOrtami.Sablonlar());
            db.EkstreUnvanDesenleri.AddRange(BankaEkstreTestOrtami.Desenler());
            db.EkstreSabitKurallar.AddRange(BankaEkstreTestOrtami.SabitKurallar());
            db.EkstreHesapPlani.AddRange(GercekHesapPlani.Kur());
            db.EkstreVergiKodlari.AddRange(GercekHesapPlani.VergiKodlari());

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

            return (db, satirlar);
        }

        /// <summary>Açıklamasında verilen ifadelerin hepsi geçen tek satır.</summary>
        private static EkstreSatiri GercekSatir(IEnumerable<EkstreSatiri> satirlar, params string[] ifadeler)
            => satirlar.Single(s => ifadeler.All(i => s.HamAciklama.Contains(i, StringComparison.OrdinalIgnoreCase)));

        private static bool AdayVar(EkstreSatiri satir, string kod)
            => satir.Adaylar is not null && satir.Adaylar.Contains($"\"{kod}\"", StringComparison.Ordinal);

        [Fact]
        public async Task Gercek01_Omer_can_dizdar_maas_avansi_otomatik_cozuluyor()
        {
            var (db, satirlar) = await GercekDosyayiIsleAsync();
            using var _db = db;

            var satir = GercekSatir(satirlar, "MAAŞ AVANSI", "ÖMER CAN DİZDAR");

            Assert.Equal("196 03 25 O04", satir.OnerilenHesapKodu);
            Assert.Equal(SatirDurum.Otomatik, satir.Durum);
            // İş avansındaki kaydı alternatif olarak duruyor ama otomatiği engellemedi.
            Assert.True(AdayVar(satir, "195 01 O09"));
        }

        [Fact]
        public async Task Gercek02_Emirhan_ozer_grup_icinde_iki_kayitli_oldugu_icin_onaya_dusuyor()
        {
            var (db, satirlar) = await GercekDosyayiIsleAsync();
            using var _db = db;

            var satir = GercekSatir(satirlar, "MAAŞ AVANSI", "emirhan özer");

            Assert.Equal(SatirDurum.OnayBekliyor, satir.Durum);
            Assert.True(AdayVar(satir, "196 03 25 E01"));
            Assert.True(AdayVar(satir, "196 IU 77"));
        }

        [Fact]
        public async Task Gercek04_Planda_olmayan_emirhan_ozdemir_icin_yakin_isim_onerilmiyor()
        {
            var (db, satirlar) = await GercekDosyayiIsleAsync();
            using var _db = db;

            // İki satır var (biri iptal/geri ödeme); ikisi de aynı kişiyi gösteriyor.
            var satir = satirlar.First(s => s.HamAciklama.Contains("EMİRHAN ÖZDEMİR", StringComparison.Ordinal));

            Assert.Equal(SatirDurum.OnayBekliyor, satir.Durum);
            Assert.Equal("196", satir.OnerilenHesapKodu);
            Assert.False(AdayVar(satir, "196 03 25 E01"));
        }

        // ---- Kural sırası ve seed ----

        [Fact]
        public async Task Seedde_dar_avans_kurallari_genel_avanstan_once_geliyor()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await BankaEkstreSeed.SeedAsync(db);

            // Yalnız Vakıfbank: avans kuralları bu bankaya ait ve aynı desen ("SGK")
            // başka bankalarda da tanımlı olduğu için sözlük parser bazlı kurulur.
            var kurallar = db.EkstreSabitKurallar
                .Where(k => k.Kapsam == KuralKapsami.Aciklama && k.ParserTipi == VakifbankVadesizParser.Tip)
                .ToDictionary(k => k.IslemTipiDeseni, k => k);

            var genel = kurallar["Avans"];

            Assert.True(kurallar["Maaş Avansı"].Sira < genel.Sira);
            Assert.True(kurallar["İş Avansı"].Sira < genel.Sira);
            Assert.True(kurallar["Masraf Ödemesi"].Sira < genel.Sira);
        }

        [Fact]
        public async Task Seedde_genel_avans_iki_grubu_kapsar_darlar_tek_gruplu_kalir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await BankaEkstreSeed.SeedAsync(db);

            // Yalnız Vakıfbank: avans kuralları bu bankaya ait ve aynı desen ("SGK")
            // başka bankalarda da tanımlı olduğu için sözlük parser bazlı kurulur.
            var kurallar = db.EkstreSabitKurallar
                .Where(k => k.Kapsam == KuralKapsami.Aciklama && k.ParserTipi == VakifbankVadesizParser.Tip)
                .ToDictionary(k => k.IslemTipiDeseni, k => k);

            Assert.Equal("195, 196", kurallar["Avans"].AnaGruplar);
            Assert.Null(kurallar["İş Avansı"].AnaGruplar);
            Assert.Null(kurallar["Maaş Avansı"].AnaGruplar);
        }

        [Fact]
        public async Task Seed_onceden_kurulmus_tek_gruplu_avans_kuralini_yukseltir()
        {
            // Çoklu gruptan önce kurulmuş veritabanında bu satır 196 ile tek gruplu duruyor.
            using var db = BankaEkstreTestOrtami.YeniContext();

            db.EkstreSabitKurallar.Add(new SabitKural
            {
                ParserTipi = BankaEkstreTestOrtami.ParserTipi,
                IslemTipiDeseni = "Avans",
                Kapsam = KuralKapsami.Aciklama,
                EslesmeTuru = EslesmeTuru.Icerir,
                HesapKodu = "196",
                HesapAdi = "Personel Avansları",
                UnvanCikarilsin = false,
                AltHesapGerekli = true,
                Sira = 50,
                Aktif = true
            });
            await db.SaveChangesAsync();

            await BankaEkstreSeed.SeedAsync(db);

            var kayit = db.EkstreSabitKurallar.Single(k => k.IslemTipiDeseni == "Avans");
            Assert.Equal("195, 196", kayit.AnaGruplar);
        }

        [Fact]
        public async Task Seed_kullanicinin_duzenledigi_avans_kuralina_dokunmaz()
        {
            // Kod değiştirilmişse kayıt kullanıcınındır; seed üzerine yazmaz.
            using var db = BankaEkstreTestOrtami.YeniContext();

            db.EkstreSabitKurallar.Add(new SabitKural
            {
                ParserTipi = BankaEkstreTestOrtami.ParserTipi,
                IslemTipiDeseni = "Avans",
                Kapsam = KuralKapsami.Aciklama,
                EslesmeTuru = EslesmeTuru.Icerir,
                HesapKodu = "195",
                HesapAdi = "İş Avansları",
                UnvanCikarilsin = false,
                AltHesapGerekli = true,
                Sira = 50,
                Aktif = true
            });
            await db.SaveChangesAsync();

            await BankaEkstreSeed.SeedAsync(db);

            var kayit = db.EkstreSabitKurallar.Single(k => k.IslemTipiDeseni == "Avans");
            Assert.Equal("195", kayit.HesapKodu);
            Assert.Null(kayit.AnaGruplar);
        }

        // ---- Arayüzden yönetim ----

        private static IEkstreParserSecici Secici()
            => new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() });

        private static async Task<CatalogContext> PlanliContextAsync()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            db.EkstreHesapPlani.AddRange(Plan("196", "Personel Avansları"));
            await db.SaveChangesAsync();
            return db;
        }

        private static SabitKuralYazDto AvansKurali(string? anaGruplar, bool altHesapGerekli = true) => new()
        {
            IslemTipiDeseni = "Avans",
            Kapsam = KuralKapsami.Aciklama,
            EslesmeTuru = EslesmeTuru.Icerir,
            HesapKodu = "196",
            UnvanCikarilsin = false,
            AltHesapGerekli = altHesapGerekli,
            AnaGruplar = anaGruplar,
            Sira = 40,
            Aktif = true
        };

        [Fact]
        public async Task Ana_gruplar_virgulle_kaydedilir_ve_normalize_edilir()
        {
            using var db = await PlanliContextAsync();
            var servis = new SabitKuralService(db, Secici(), BankaEkstreTestOrtami.Kapsam());

            // Kullanıcı boşluklu, tam kod ve tekrarlı yazsa da liste ana gruplara iner.
            var kayit = await servis.CreateAsync(AvansKurali("195 01 ,196,  195"));

            Assert.Equal("195, 196", kayit.AnaGruplar);
        }

        [Fact]
        public async Task Ana_gruplar_bos_birakilirsa_kural_tek_gruplu_kalir()
        {
            using var db = await PlanliContextAsync();
            var servis = new SabitKuralService(db, Secici(), BankaEkstreTestOrtami.Kapsam());

            var kayit = await servis.CreateAsync(AvansKurali("   "));

            Assert.Null(kayit.AnaGruplar);
        }

        [Fact]
        public async Task Alt_hesap_beklenmeyen_kuralda_ana_grup_listesi_reddedilir()
        {
            // Sessizce yok saymak, kullanıcının yazdığı şeyin neden etkisiz olduğunu gizlerdi.
            using var db = await PlanliContextAsync();
            var servis = new SabitKuralService(db, Secici(), BankaEkstreTestOrtami.Kapsam());

            var hata = await Assert.ThrowsAsync<BankaEkstreKuralException>(
                () => servis.CreateAsync(AvansKurali("195, 196", altHesapGerekli: false)));

            Assert.Equal(nameof(SabitKuralYazDto.AnaGruplar), hata.Field);
        }

        [Fact]
        public async Task Okunamayan_ana_grup_listesi_reddedilir()
        {
            using var db = await PlanliContextAsync();
            var servis = new SabitKuralService(db, Secici(), BankaEkstreTestOrtami.Kapsam());

            await Assert.ThrowsAsync<BankaEkstreKuralException>(
                () => servis.CreateAsync(AvansKurali(",,,")));
        }
    }
}
