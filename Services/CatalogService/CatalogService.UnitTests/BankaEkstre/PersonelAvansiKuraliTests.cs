using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.Muhasebe;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Madde 5: açıklamada "masraf ödemesi / iş avansı / maaş avansı / avans" geçen satırlar
    /// personele yapılan ödemelerdir — 195 veya 196'ya gider, cariye değil.
    ///
    /// Kural açıklama kapsamındadır ve öğrenme katmanından önce çalışır. Çıkarılan unvan bu
    /// satırlarda bir cari sayılmaz (öğrenme anahtarı üretilmez, 120/329 aranmaz); yalnız
    /// <b>kuralın</b> ana grubu içinde kişi muavini aramakta kullanılır. Muavin bulunamazsa
    /// satır ana grupla onaya düşer.
    /// </summary>
    public class PersonelAvansiKuraliTests
    {
        private readonly HesapEslestirici _eslestirici = new();

        private static EslestirmeVerisi Veri(params HesapPlaniKaydi[] plan) => new()
        {
            SabitKurallar = BankaEkstreTestOrtami.SabitKurallar(),
            HesapPlani = plan
        };

        private static SatirBaglami Baglam(string hamAciklama, string? unvan = null,
                                           string islemTipi = "Gönderilen havale",
                                           Yon yon = Yon.Cikan) => new()
        {
            IslemTipi = islemTipi,
            HamAciklama = hamAciklama,
            Unvan = unvan,
            Yon = yon
        };

        private static HesapPlaniKaydi Plan(string kod, string ad) => new()
        {
            Kod = kod,
            Ad = ad,
            NormalizeAd = Normalizasyon.UnvanNormalize(ad),
            AnaGrup = Normalizasyon.AnaGrup(kod),
            BaslangicHarfi = Normalizasyon.BaslangicHarfi(kod),
            Aktif = true
        };

        // ---- Katman davranışı ----

        [Theory]
        [InlineData("MEHMET YILMAZ iş avansı ödemesi", "195")]
        [InlineData("AYŞE DEMİR masraf ödemesi", "195")]
        [InlineData("ALİ KAYA maaş avansı", "196")]
        [InlineData("VELİ ÖZTÜRK avans", "196")]
        public void Avans_satirlari_ana_gruba_gider(string aciklama, string beklenenKod)
        {
            var sonuc = _eslestirici.Coz(Baglam(aciklama), Veri());

            Assert.Equal(beklenenKod, sonuc.HesapKodu);
            Assert.Equal(KaynakKatman.SabitKural, sonuc.Katman);
        }

        [Fact]
        public void Alt_hesap_planda_bulunamazsa_satir_ana_grupla_onaya_duser()
        {
            // Veri() boş hesap planı verir: aranacak muavin yok, kural ana grupta kalır.
            var sonuc = _eslestirici.Coz(Baglam("MEHMET YILMAZ iş avansı ödemesi"), Veri());

            // Kod eksik (yalnız ana grup) olduğu için otomatik kapanmaz ve güven bildirilmez.
            Assert.Equal(SatirDurum.OnayBekliyor, sonuc.Durum);
            Assert.Equal(0m, sonuc.Guven);
        }

        [Fact]
        public void Alt_hesap_kuralin_ana_grubunda_unvanla_bulunur()
        {
            // Kural 195'i veriyor; kişi adı 195'in içinde aranır ve muavine inilir.
            var veri = Veri(Plan("195 M01", "MEHMET YILMAZ"), Plan("329 M12", "MEHMET YILMAZ İNŞAAT"));

            var sonuc = _eslestirici.Coz(Baglam("MEHMET YILMAZ iş avansı ödemesi", unvan: "MEHMET YILMAZ"), veri);

            Assert.Equal("195 M01", sonuc.HesapKodu);
            Assert.Equal(KaynakKatman.SabitKural, sonuc.Katman);
        }

        [Fact]
        public void Alt_hesap_aramasi_yonun_ana_grubuna_tasmaz()
        {
            // Aynı ada sahip bir cari 329'da da var. Arama uzayı yönün grubu (329) değil,
            // kuralın grubu (195) olduğu için cariye düşülmemeli.
            var veri = Veri(Plan("329 M12", "MEHMET YILMAZ"));

            var sonuc = _eslestirici.Coz(Baglam("MEHMET YILMAZ iş avansı ödemesi", unvan: "MEHMET YILMAZ"), veri);

            Assert.Equal("195", sonuc.HesapKodu);
            Assert.Equal(SatirDurum.OnayBekliyor, sonuc.Durum);
        }

        [Fact]
        public void Avans_kurali_gecmis_onay_katmanindan_once_calisir()
        {
            // Aynı işlem tipinden öğrenilmiş bir kayıt varsa bile avans satırı ona düşmemeli:
            // öğrenme katmanı önce çalışsaydı satır ilgisiz bir cariye çözülürdü.
            var veri = new EslestirmeVerisi
            {
                SabitKurallar = BankaEkstreTestOrtami.SabitKurallar(),
                Eslesmeler = new[]
                {
                    new HesapEslesmesi
                    {
                        AnahtarTipi = AnahtarTipi.UnvanCekirdek,
                        AnahtarCekirdek = "ISLEM:GONDERILEN HAVALE",
                        Yon = Yon.Cikan,
                        HesapKodu = "329 K08",
                        HesapAdi = "KEMAL TEKSTİL"
                    }
                }
            };

            var sonuc = _eslestirici.Coz(Baglam("MEHMET YILMAZ iş avansı ödemesi"), veri);

            Assert.Equal("195", sonuc.HesapKodu);
            Assert.NotEqual(KaynakKatman.GecmisOnay, sonuc.Katman);
        }

        [Fact]
        public void Maas_avansi_genel_avans_deseninden_once_denenir()
        {
            // "MAAŞ AVANSI" metni genel "AVANS" desenini de tutar; sıra bozulursa 196
            // yerine yanlış grup seçilirdi. Bu test sıralamayı sabitler.
            var sonuc = _eslestirici.Coz(Baglam("ALİ KAYA maaş avansı ödemesi"), Veri());

            Assert.Equal("196", sonuc.HesapKodu);
        }

        [Fact]
        public void Avans_kelimesi_unvan_icinde_geciyorsa_kural_tutmaz()
        {
            // Tam kelime araması olmasaydı "AVANSAS" içindeki "AVANS" satırı personel
            // avansı sanardı.
            var veri = Veri(Plan("329 A11", "AVANSAS TEKSTİL"));

            var sonuc = _eslestirici.Coz(
                Baglam("0000123 sorgu numaralı AVANSAS TEKSTİL A.Ş. tarafından", unvan: "AVANSAS TEKSTİL"), veri);

            Assert.Equal("329 A11", sonuc.HesapKodu);
            // Cari katmanlarından biri çözmeli; avans kuralı (SabitKural) tutmamalı.
            // Hangi cari katmanının çözdüğü değişebilir: benzersiz önek, unvan
            // benzerliğinden önce denenir ve "AVANSAS TEKSTIL" dizisini doğrudan bulur.
            Assert.Equal(KaynakKatman.BenzersizOnek, sonuc.Katman);
        }

        [Fact]
        public void Islem_tipi_kurallari_eskisi_gibi_calisir()
        {
            var sonuc = _eslestirici.Coz(
                Baglam("MKK saklama bedeli", islemTipi: "MKK Masrafı"), Veri());

            Assert.Equal("770", sonuc.HesapKodu);
            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
            Assert.Equal(0.95m, sonuc.Guven);
        }

        [Fact]
        public void Avans_satirinda_ogrenme_anahtari_uretilmez()
        {
            // Kişi her satırda farklı; anahtar işlem tipine düşseydi ilk onaydan sonra tüm
            // havaleler o kişinin muavinine öğrenilirdi.
            var baglam = Baglam("MEHMET YILMAZ iş avansı ödemesi");
            baglam.AnahtarUretilmesin = true;

            Assert.Equal(string.Empty, HesapEslestirici.AnahtarCekirdek(baglam));
        }

        // ---- Uçtan uca ----

        private static EkstreService Servis(CatalogContext db)
        {
            var secici = new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() });
            return new EkstreService(db, secici, new UnvanCikarici(), new AciklamaUretici(),
                                     new HesapEslestirici(), new HesapEslesmeService(db), new SabitKullanici());
        }

        private static async Task<(CatalogContext Db, int HesapId)> HazirlaAsync()
        {
            var db = BankaEkstreTestOrtami.YeniContext();

            db.EkstreAciklamaSablonlari.AddRange(BankaEkstreTestOrtami.Sablonlar());
            db.EkstreUnvanDesenleri.AddRange(BankaEkstreTestOrtami.Desenler());
            db.EkstreSabitKurallar.AddRange(BankaEkstreTestOrtami.SabitKurallar());

            // Personelin adına benzeyen bir cari: unvan çıkarılsaydı satır buraya eşleşirdi.
            db.EkstreHesapPlani.AddRange(
                Plan("329 M12", "MEHMET YILMAZ İNŞAAT"),
                Plan("195 M01", "MEHMET YILMAZ"));

            var hesap = new BankaHesabi
            {
                BankaAdi = "Vakıfbank",
                OrkaHesapKodu = "102 1 1 01",
                ParserTipi = VakifbankVadesizParser.Tip,
                HesapSahibiUnvani = "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ",
                Aktif = true
            };
            db.EkstreBankaHesaplari.Add(hesap);

            await db.SaveChangesAsync();
            return (db, hesap.Id);
        }

        private static object[] Satir(string aciklama)
            => new object[] { "15.01.2026", "Gönderilen havale", -5000.00, "İnternet", "0070511435", "B", aciklama };

        [Fact]
        public async Task Avans_satirinda_unvan_cikarilir_ama_cari_sayilmaz()
        {
            var (db, hesapId) = await HazirlaAsync();
            using var _ = db;

            var dosya = BankaEkstreTestOrtami.BasliklıEkstre(
                Satir("TR330006200012300006673953 nolu MEHMET YILMAZ hesabına iş avansı ödemesi"));

            var sonuc = await Servis(db).YukleAsync(hesapId, dosya, "ekstre.xlsx");
            var satir = db.EkstreSatirlari.Single(s => s.EkstreYuklemeId == sonuc.Id);

            // Unvan okunur ve kuralın grubundaki (195) muavine iner; benzer adlı 329 M12
            // carisine değil. Öğrenme anahtarı yine üretilmez: kişi bir cari değil.
            Assert.Equal("MEHMET YILMAZ", satir.CikarilanUnvan);
            Assert.Null(satir.AnahtarCekirdek);
            Assert.Equal("195 M01", satir.OnerilenHesapKodu);
        }

        [Fact]
        public async Task Avans_satiri_onaylaninca_ogrenme_kaydi_yazilmaz()
        {
            var (db, hesapId) = await HazirlaAsync();
            using var _ = db;

            var servis = Servis(db);
            var dosya = BankaEkstreTestOrtami.BasliklıEkstre(
                Satir("TR330006200012300006673953 nolu MEHMET YILMAZ hesabına iş avansı ödemesi"));

            var sonuc = await servis.YukleAsync(hesapId, dosya, "ekstre.xlsx");
            var satir = db.EkstreSatirlari.Single(s => s.EkstreYuklemeId == sonuc.Id);

            await servis.OnaylaAsync(satir.Id, "195 M01");

            Assert.Equal("195 M01", db.EkstreSatirlari.Single(s => s.Id == satir.Id).OnaylananHesapKodu);
            Assert.Empty(db.EkstreHesapEslesmeleri);
        }

        [Fact]
        public async Task Yirmi_avans_satirinin_hepsi_ana_gruba_duser()
        {
            // Ölçülen dosyada 20 satır bu kalıba giriyordu.
            var (db, hesapId) = await HazirlaAsync();
            using var _ = db;

            var kaliplar = new[]
            {
                "MEHMET YILMAZ hesabına iş avansı ödemesi",
                "AYŞE DEMİR masraf ödemesi",
                "ALİ KAYA maaş avansı",
                "VELİ ÖZTÜRK avans"
            };

            var satirlar = Enumerable.Range(0, 20).Select(i => Satir(kaliplar[i % 4])).ToArray();

            var sonuc = await Servis(db).YukleAsync(hesapId, BankaEkstreTestOrtami.BasliklıEkstre(satirlar), "ekstre.xlsx");
            var kayitlar = db.EkstreSatirlari.Where(s => s.EkstreYuklemeId == sonuc.Id).ToList();

            Assert.Equal(20, kayitlar.Count);
            Assert.All(kayitlar, s => Assert.Contains(s.OnerilenHesapKodu, new[] { "195", "196" }));
            Assert.All(kayitlar, s => Assert.Equal(SatirDurum.OnayBekliyor, s.Durum));
            // Bu kalıplarda hiçbir unvan deseni tutmuyor (IBAN/parantez/eğik çizgi yok),
            // dolayısıyla aranacak muavin de yok: satırlar ana grupta kalır.
            Assert.All(kayitlar, s => Assert.Null(s.CikarilanUnvan));
        }
    }
}
