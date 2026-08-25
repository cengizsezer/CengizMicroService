using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.Muhasebe;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Uçtan uca akış: yükle → satırlar ayrışsın → belirsizler onaya düşsün → onayla →
    /// öğrenilsin → aynı açıklama ikinci kez geldiğinde Katman 2'den çözülsün →
    /// eksik satır varken dışa aktarım engellensin.
    /// </summary>
    public class EkstreServiceTests
    {
        private const string GelenAciklama = "0000123 sorgu numaralı DAĞI GİYİM SANAYİ A.Ş. tarafından gönderilmiştir";

        private static EkstreService Servis(CatalogContext db, int firmaId = BankaEkstreTestOrtami.FirmaId)
        {
            var secici = new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() });
            var kapsam = BankaEkstreTestOrtami.Kapsam(firmaId);

            return new EkstreService(db, secici, new UnvanCikarici(), new AciklamaUretici(),
                                     new HesapEslestirici(), new HesapEslesmeService(db, kapsam),
                                     new SabitKullanici(), kapsam);
        }

        /// <summary>Bir banka hesabı, yapılandırma satırları ve iki cari içeren hazır context.</summary>
        private static async Task<(CatalogContext Db, int HesapId)> HazirlaAsync(string? veritabaniAdi = null)
        {
            var db = BankaEkstreTestOrtami.YeniContext(veritabaniAdi);

            db.EkstreAciklamaSablonlari.AddRange(BankaEkstreTestOrtami.Sablonlar());
            db.EkstreUnvanDesenleri.AddRange(BankaEkstreTestOrtami.Desenler());

            db.EkstreHesapPlani.AddRange(
                Plan("120 D22", "DAĞI GİYİM SANAYİ"),
                Plan("120 Z01", "ZETA MADENCİLİK"),
                Plan("329 K08", "KEMAL TEKSTİL"));

            var hesap = new BankaHesabi
            {
                FirmaId = BankaEkstreTestOrtami.FirmaId,
                BankaAdi = "Vakıfbank",
                OrkaHesapKodu = "102 1 1 01",
                ParserTipi = VakifbankVadesizParser.Tip,
                Aktif = true
            };
            db.EkstreBankaHesaplari.Add(hesap);

            await db.SaveChangesAsync();
            return (db, hesap.Id);
        }

        /// <summary>
        /// Firma izolasyon testi için: aynı veritabanına, verilen firmanın kendi planı ve
        /// banka hesabı kurulur. Yapılandırma tabloları (şablon/desen) GLOBAL olduğu için
        /// yalnız ilk çağrıda eklenir; iki kez eklenirse aynı desen iki kez denenirdi.
        /// </summary>
        private static async Task<int> HazirlaFirmaAsync(CatalogContext db, int firmaId,
                                                         string cariAdi, string cariKodu,
                                                         bool yapilandirmaEkle)
        {
            if (yapilandirmaEkle)
            {
                db.EkstreAciklamaSablonlari.AddRange(BankaEkstreTestOrtami.Sablonlar());
                db.EkstreUnvanDesenleri.AddRange(BankaEkstreTestOrtami.Desenler());
            }

            db.EkstreHesapPlani.Add(Plan(cariKodu, cariAdi, firmaId));

            var hesap = new BankaHesabi
            {
                FirmaId = firmaId,
                BankaAdi = "Vakıfbank",
                OrkaHesapKodu = "102 1 1 01",
                ParserTipi = VakifbankVadesizParser.Tip,
                Aktif = true
            };
            db.EkstreBankaHesaplari.Add(hesap);

            await db.SaveChangesAsync();
            return hesap.Id;
        }

        private static HesapPlaniKaydi Plan(string kod, string ad,
                                            int firmaId = BankaEkstreTestOrtami.FirmaId) => new()
        {
            FirmaId = firmaId,
            Kod = kod,
            Ad = ad,
            NormalizeAd = Normalizasyon.UnvanNormalize(ad),
            AnaGrup = Normalizasyon.AnaGrup(kod),
            BaslangicHarfi = Normalizasyon.BaslangicHarfi(kod),
            Aktif = true
        };

        [Fact]
        public async Task Ayristiricisiz_hesaba_ekstre_yuklenemez()
        {
            var (db, _) = await HazirlaAsync();
            using var _db = db;

            // Vadeli/süpürme hesabı: kayıt defterinde duruyor ama ekstresi yüklenmiyor.
            var vadeli = new BankaHesabi
            {
                FirmaId = BankaEkstreTestOrtami.FirmaId,
                BankaAdi = "Vakıfbank",
                HesapAdi = "Vakıfbank, Vadeli Tl - Otomatik Süpürme Hesabı",
                OrkaHesapKodu = "102 1 1 04",
                ParserTipi = null,
                Aktif = true
            };
            db.EkstreBankaHesaplari.Add(vadeli);
            await db.SaveChangesAsync();

            using var dosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "15.01.2026", "Gelen EFT Otomatik Yatan", 1000m, "EFT", "", "A", GelenAciklama });

            var hata = await Assert.ThrowsAsync<BankaEkstreKuralException>(
                () => Servis(db).YukleAsync(vadeli.Id, dosya, "ocak.xlsx"));

            Assert.Contains("ayrıştırıcı", hata.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(db.EkstreYuklemeler.Where(y => y.BankaHesabiId == vadeli.Id));
        }

        [Fact]
        public async Task Yukleme_satirlari_ayristirir_ve_aciklama_uretir()
        {
            var (db, hesapId) = await HazirlaAsync();
            using var _ = db;

            using var dosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "15.01.2026", "Gelen EFT Otomatik Yatan", 1000m, "EFT", "", "A", GelenAciklama });

            var yukleme = await Servis(db).YukleAsync(hesapId, dosya, "ocak.xlsx");
            var satirlar = await Servis(db).GetSatirlarAsync(yukleme.Id, null);

            var satir = Assert.Single(satirlar!);
            Assert.Equal(new DateTime(2026, 1, 15), satir.Tarih);
            Assert.Equal(1000m, satir.Tutar);
            Assert.Equal(Yon.Giren, satir.Yon);
            Assert.Equal("DAĞI GİYİM SANAYİ A.Ş.", satir.CikarilanUnvan);
            Assert.Equal("Gelen Eft - Dağı Giyim Sanayi A.Ş.", satir.UretilenAciklama);
            Assert.True(satir.UretilenAciklama!.Length <= 50);

            // Cari eşleşmesi benzersiz önek katmanından geldi: hesap adı çekirdeği
            // ("DAGI GIYIM") açıklamanın token dizisiyle başlıyor ve tek sonuç veriyor.
            // Bu katman desen tabanlı unvan benzerliğinden önce denenir.
            Assert.Equal(KaynakKatman.BenzersizOnek, satir.KaynakKatman);
            Assert.Equal("120 D22", satir.OnerilenHesapKodu);
            Assert.Equal(SatirDurum.Otomatik, satir.Durum);
        }

        [Fact]
        public async Task Cozulemeyen_satir_onaya_duser_ve_disa_aktarimi_engeller()
        {
            var (db, hesapId) = await HazirlaAsync();
            using var _ = db;

            using var dosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "15.01.2026", "Gelen EFT Otomatik Yatan", 1000m, "", "", "A", "unvan çıkarılamayan serbest metin" });

            var yukleme = await Servis(db).YukleAsync(hesapId, dosya, "ocak.xlsx");
            var satir = (await Servis(db).GetSatirlarAsync(yukleme.Id, null))!.Single();

            Assert.Equal(SatirDurum.Cozulemedi, satir.Durum);
            Assert.Null(satir.CikarilanUnvan);

            var hata = await Assert.ThrowsAsync<BankaEkstreKuralException>(
                () => Servis(db).DisaAktarAsync(yukleme.Id));

            Assert.Contains("çözülmemiş", hata.Message);
        }

        [Fact]
        public async Task Onay_ogrenme_kaydi_yazar_ve_ikinci_yuklemede_katman2_cozer()
        {
            var veritabani = $"ekstre-ogrenme-{Guid.NewGuid()}";
            var (db, hesapId) = await HazirlaAsync(veritabani);
            using var _ = db;

            // İlk yükleme: unvan çıkmıyor → çözülemedi.
            using var ilkDosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "15.01.2026", "Gelen EFT Otomatik Yatan", 1000m, "", "", "A", "kimliksiz gelen ödeme" });

            var ilk = await Servis(db).YukleAsync(hesapId, ilkDosya, "ocak.xlsx");
            var ilkSatir = (await Servis(db).GetSatirlarAsync(ilk.Id, null))!.Single();
            Assert.Equal(SatirDurum.Cozulemedi, ilkSatir.Durum);

            // Kullanıcı elle çözer.
            var onaylanan = await Servis(db).OnaylaAsync(ilkSatir.Id, "120 Z01");
            Assert.Equal(SatirDurum.Onaylandi, onaylanan!.Durum);
            Assert.Equal("120 Z01", onaylanan.OnaylananHesapKodu);
            Assert.Equal("ZETA MADENCİLİK", onaylanan.OnaylananHesapAdi);

            var ogrenilen = db.EkstreHesapEslesmeleri.ToList();
            var kayit = Assert.Single(ogrenilen);
            Assert.Equal("120 Z01", kayit.HesapKodu);
            Assert.Equal(1, kayit.KullanimSayisi);

            // Kimlik kaydi global; firma filtresi yok.
            Assert.Single(db.EkstreKimlikKayitlari.ToList());

            // Aynı açıklama ikinci kez gelince Katman 2 çözer.
            using var ikinciDosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "20.02.2026", "Gelen EFT Otomatik Yatan", 500m, "", "", "A", "kimliksiz gelen ödeme" });

            var ikinci = await Servis(db).YukleAsync(hesapId, ikinciDosya, "subat.xlsx");
            var ikinciSatir = (await Servis(db).GetSatirlarAsync(ikinci.Id, null))!.Single();

            Assert.Equal(KaynakKatman.GecmisOnay, ikinciSatir.KaynakKatman);
            Assert.Equal("120 Z01", ikinciSatir.OnerilenHesapKodu);
            Assert.Equal(SatirDurum.Otomatik, ikinciSatir.Durum);
            Assert.Equal(1.0m, ikinciSatir.GuvenSkoru);
        }

        [Fact]
        public async Task Farkli_kod_secilirse_ogrenme_kaydi_ezilir()
        {
            var (db, hesapId) = await HazirlaAsync();
            using var _ = db;

            using var dosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "15.01.2026", "Gelen EFT Otomatik Yatan", 1000m, "", "", "A", GelenAciklama });

            var yukleme = await Servis(db).YukleAsync(hesapId, dosya, "ocak.xlsx");
            var satir = (await Servis(db).GetSatirlarAsync(yukleme.Id, null))!.Single();

            await Servis(db).OnaylaAsync(satir.Id, "120 D22");
            await Servis(db).OnaylaAsync(satir.Id, "120 Z01");

            var kayit = db.EkstreHesapEslesmeleri.Single();

            // Düzeltme kazanır; sayaç eski koda ait olduğu için sıfırlanır.
            Assert.Equal("120 Z01", kayit.HesapKodu);
            Assert.Equal(1, kayit.KullanimSayisi);
        }

        [Fact]
        public async Task Bilinmeyen_kod_kaydedilir_ama_ogrenilmez()
        {
            var (db, hesapId) = await HazirlaAsync();
            using var _ = db;

            using var dosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "15.01.2026", "Gelen EFT Otomatik Yatan", 1000m, "", "", "A", GelenAciklama });

            var yukleme = await Servis(db).YukleAsync(hesapId, dosya, "ocak.xlsx");
            var satir = (await Servis(db).GetSatirlarAsync(yukleme.Id, null))!.Single();

            // ORKA'da yeni açılmış bir kod olabilir; kaydetmesine izin verilir...
            var onaylanan = await Servis(db).OnaylaAsync(satir.Id, "999 Q99");

            Assert.Equal(SatirDurum.Onaylandi, onaylanan!.Durum);
            Assert.Equal("999 Q99", onaylanan.OnaylananHesapKodu);
            Assert.Contains("hesap planında yok", onaylanan.Uyari);

            // ...ama doğrulanmamış kod öğrenme tablosuna yazılmaz.
            Assert.Empty(db.EkstreHesapEslesmeleri.ToList());
        }

        [Fact]
        public async Task Gecmis_onaydan_cozulen_satir_duzeltilince_ogrenme_kaydi_guncellenir()
        {
            var veritabani = $"ekstre-duzeltme-{Guid.NewGuid()}";
            var (db, hesapId) = await HazirlaAsync(veritabani);
            using var _ = db;

            using var ilkDosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "15.01.2026", "Gelen EFT Otomatik Yatan", 1000m, "", "", "A", GelenAciklama });

            var ilk = await Servis(db).YukleAsync(hesapId, ilkDosya, "ocak.xlsx");
            var ilkSatir = (await Servis(db).GetSatirlarAsync(ilk.Id, null))!.Single();
            await Servis(db).OnaylaAsync(ilkSatir.Id, "120 D22");

            // İkinci ay: satır geçmiş onaydan çözülüyor ama kullanıcı kodu düzeltiyor.
            using var ikinciDosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "20.02.2026", "Gelen EFT Otomatik Yatan", 500m, "", "", "A", GelenAciklama });

            var ikinci = await Servis(db).YukleAsync(hesapId, ikinciDosya, "subat.xlsx");
            var ikinciSatir = (await Servis(db).GetSatirlarAsync(ikinci.Id, null))!.Single();
            Assert.Equal(KaynakKatman.GecmisOnay, ikinciSatir.KaynakKatman);

            await Servis(db).OnaylaAsync(ikinciSatir.Id, "120 Z01");

            // Sadece satır değil, öğrenme kaydı da düzeldi; aksi hâlde hata gelecek ay geri gelirdi.
            var kayit = db.EkstreHesapEslesmeleri.Single();
            Assert.Equal("120 Z01", kayit.HesapKodu);
        }

        [Fact]
        public async Task Ayni_cari_farkli_sorgu_numarasiyla_ikinci_kez_gecmis_onaydan_cozulur()
        {
            var veritabani = $"ekstre-cekirdek-{Guid.NewGuid()}";
            var (db, hesapId) = await HazirlaAsync(veritabani);
            using var _ = db;

            using var ilkDosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "15.01.2026", "Gelen EFT Otomatik Yatan", 1000m, "", "", "A",
                               "0000123 sorgu numaralı KEMAL TEKSTİL SANAYİ A.Ş. tarafından gönderilmiştir" });

            var ilk = await Servis(db).YukleAsync(hesapId, ilkDosya, "ocak.xlsx");
            var ilkSatir = (await Servis(db).GetSatirlarAsync(ilk.Id, null))!.Single();
            // Kullanıcı cariyi kendi adını taşıyan muavine bağlar; öğrenme kaydı ancak
            // anahtar hesabın adıyla örtüşürse yazılır (bkz. AnahtarKalitesi).
            await Servis(db).OnaylaAsync(ilkSatir.Id, "329 K08");

            // İkinci ay: banka farklı sorgu numarası, farklı tarih ve tutar yazıyor.
            // Ham hash anahtarıyla bu satır ASLA eşleşmezdi; unvan çekirdeğiyle eşleşir.
            using var ikinciDosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "20.02.2026", "Gelen EFT Otomatik Yatan", 7350.42m, "", "", "A",
                               "0009987 sorgu numaralı KEMAL TEKSTİL SAN. VE TİC. A.Ş. tarafından gönderilmiştir" });

            var ikinci = await Servis(db).YukleAsync(hesapId, ikinciDosya, "subat.xlsx");
            var ikinciSatir = (await Servis(db).GetSatirlarAsync(ikinci.Id, null))!.Single();

            Assert.Equal(KaynakKatman.GecmisOnay, ikinciSatir.KaynakKatman);
            Assert.Equal("329 K08", ikinciSatir.OnerilenHesapKodu);
            Assert.Equal(SatirDurum.Otomatik, ikinciSatir.Durum);
        }

        /// <summary>
        /// İKİ FİRMA AYNI VERİTABANINI PAYLAŞIR VE BİRBİRİNİ GÖRMEZ.
        ///
        /// Test artık tek context kullanıyor: kapsam global query filter'dan değil,
        /// servise verilen firma kimliğinden geliyor (bkz. KARARLAR §69). Bu yüzden
        /// doğrulamalar da açıkça FirmaId ile süzülüyor — "context ne görüyorsa odur"
        /// varsayımı, gizli filtreyi sınamak demekti; artık gizli filtre yok.
        /// </summary>
        [Fact]
        public async Task Iki_firma_birbirinin_hesap_planini_ve_eslesmelerini_gormez()
        {
            const int aday = 201;
            const int smmm = 106;

            using var db = BankaEkstreTestOrtami.YeniContext();

            var adayHesapId = await HazirlaFirmaAsync(db, aday, "DAĞI GİYİM SANAYİ", "120 D22", yapilandirmaEkle: true);
            var smmmHesapId = await HazirlaFirmaAsync(db, smmm, "DAĞI GİYİM SANAYİ", "120 A07", yapilandirmaEkle: false);

            using var adayDosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "15.01.2026", "Gelen EFT Otomatik Yatan", 1000m, "", "", "A", GelenAciklama });
            using var smmmDosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "15.01.2026", "Gelen EFT Otomatik Yatan", 1000m, "", "", "A", GelenAciklama });

            var adayYukleme = await Servis(db, aday).YukleAsync(adayHesapId, adayDosya, "aday.xlsx");
            var smmmYukleme = await Servis(db, smmm).YukleAsync(smmmHesapId, smmmDosya, "smmm.xlsx");

            var adaySatir = (await Servis(db, aday).GetSatirlarAsync(adayYukleme.Id, null))!.Single();
            var smmmSatir = (await Servis(db, smmm).GetSatirlarAsync(smmmYukleme.Id, null))!.Single();

            // Her firma kendi hesap planını görüyor: aynı unvan farklı koda eşleşiyor.
            Assert.Equal("120 D22", adaySatir.OnerilenHesapKodu);
            Assert.Equal("120 A07", smmmSatir.OnerilenHesapKodu);

            await Servis(db, aday).OnaylaAsync(adaySatir.Id, "120 D22");
            await Servis(db, smmm).OnaylaAsync(smmmSatir.Id, "120 A07");

            // Eşleşmeler firma bazlı: her firmanın tek ve kendi kaydı var.
            Assert.Equal("120 D22", db.EkstreHesapEslesmeleri.Single(e => e.FirmaId == aday).HesapKodu);
            Assert.Equal("120 A07", db.EkstreHesapEslesmeleri.Single(e => e.FirmaId == smmm).HesapKodu);

            // Yüklemeler ve hesap planları da karışmıyor.
            Assert.Single(db.EkstreYuklemeler.Where(y => y.FirmaId == aday).ToList());
            Assert.Single(db.EkstreYuklemeler.Where(y => y.FirmaId == smmm).ToList());
            Assert.Equal("120 D22", db.EkstreHesapPlani.Single(h => h.FirmaId == aday).Kod);
            Assert.Equal("120 A07", db.EkstreHesapPlani.Single(h => h.FirmaId == smmm).Kod);

            // Kimlik kaydı ise global: aynı unvan tek kayıt, iki firma paylaşıyor.
            Assert.Single(db.EkstreKimlikKayitlari.ToList());
        }

        [Fact]
        public async Task Duzeltilmis_ekstre_aciklama_kolonunu_degistirir()
        {
            var (db, hesapId) = await HazirlaAsync();
            using var _ = db;

            using var dosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "15.01.2026", "Gelen EFT Otomatik Yatan", 1000m, "", "", "A", GelenAciklama });

            var yukleme = await Servis(db).YukleAsync(hesapId, dosya, "ocak.xlsx");
            var duzeltilmis = await Servis(db).DuzeltilmisEkstreAsync(yukleme.Id);

            Assert.NotNull(duzeltilmis);
            Assert.Equal("ocak-duzeltilmis.xlsx", duzeltilmis!.DosyaAdi);

            using var akis = new MemoryStream(duzeltilmis.Icerik);
            using var kitap = new ClosedXML.Excel.XLWorkbook(akis);
            var sayfa = kitap.Worksheets.First();

            // Açıklama kolonu (17) ham banka metni yerine bizim ürettiğimizi taşıyor.
            Assert.Equal("Gelen Eft - Dağı Giyim Sanayi A.Ş.", sayfa.Cell(8, 17).GetString());
            // Diğer kolonlar dokunulmadan kaldı: dosya orijinal yapıda.
            Assert.Equal("Gelen EFT Otomatik Yatan", sayfa.Cell(8, 6).GetString());
        }

        [Fact]
        public async Task Diger_bankada_isaretli_satir_disa_aktarimdan_duser()
        {
            var (db, hesapId) = await HazirlaAsync();
            using var _ = db;

            using var dosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "15.01.2026", "Gelen EFT Otomatik Yatan", 1000m, "", "", "A", GelenAciklama },
                new object[] { "16.01.2026", "Gelen EFT Otomatik Yatan", 2000m, "", "", "A", "kimliksiz gelen ödeme" });

            var yukleme = await Servis(db).YukleAsync(hesapId, dosya, "ocak.xlsx");
            var satirlar = (await Servis(db).GetSatirlarAsync(yukleme.Id, null))!;

            var cozulemeyen = satirlar.Single(s => s.Durum == SatirDurum.Cozulemedi);
            await Servis(db).DigerBankadaAsync(cozulemeyen.Id);

            var sonuc = await Servis(db).DisaAktarAsync(yukleme.Id);

            Assert.NotNull(sonuc);
            Assert.Equal(1, sonuc!.SatirSayisi);
            Assert.Equal(1, sonuc.DigerBankadaAtlanan);

            var orka = Assert.Single(sonuc.Satirlar);
            Assert.Equal("120 D22", orka.KarsiHesapKodu);
            // Kaydın diğer bacağı: ekstresi işlenen banka hesabının ORKA kodu.
            Assert.Equal("102 1 1 01", orka.BankaHesapKodu);
            Assert.Equal("Gelen Eft - Dağı Giyim Sanayi A.Ş.", orka.Aciklama);
        }

        [Fact]
        public async Task Sayaclar_yukleme_ozetinde_dogru_gelir()
        {
            var (db, hesapId) = await HazirlaAsync();
            using var _ = db;

            using var dosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "15.01.2026", "Gelen EFT Otomatik Yatan", 1000m, "", "", "A", GelenAciklama },
                new object[] { "16.01.2026", "Gelen EFT Otomatik Yatan", 2000m, "", "", "A", "kimliksiz gelen ödeme" });

            var yukleme = await Servis(db).YukleAsync(hesapId, dosya, "ocak.xlsx");

            Assert.Equal(2, yukleme.Sayaclar.Toplam);
            Assert.Equal(1, yukleme.Sayaclar.Otomatik);
            Assert.Equal(1, yukleme.Sayaclar.Cozulemeyen);
            Assert.Equal(1, yukleme.Sayaclar.Eksik);
        }
    }
}
