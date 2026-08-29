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
    /// İşlem kategorileri: kuralların muhasebe sınıflandırması. Kategori bir <b>etikettir</b>;
    /// eşleştirme kararına girmez. Testlerin ilk işi de bunu sabitlemek: kategori atanmış ve
    /// atanmamış kural aynı sonucu vermeli.
    /// </summary>
    public class IslemKategorisiTests
    {
        private static IslemKategorisiService Servis(CatalogContext db)
            => new(db, new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() }),
                   BankaEkstreTestOrtami.Kapsam());

        private static async Task<CatalogContext> SeedliContextAsync()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            await BankaEkstreSeed.SeedAsync(db);
            return db;
        }

        // ---- Seed ----

        [Fact]
        public async Task Ondokuz_kategori_tohumlanir()
        {
            using var db = await SeedliContextAsync();

            var kategoriler = db.EkstreIslemKategorileri.OrderBy(k => k.Sira).ToList();

            Assert.Equal(19, kategoriler.Count);
            Assert.Equal("Hesaplar arası", kategoriler[0].Ad);
            Assert.Equal("102", kategoriler[0].VarsayilanAnaGrup);
            Assert.Contains(kategoriler, k => k.Ad == "Personel iş avansı" && k.VarsayilanAnaGrup == "195");
            Assert.Contains(kategoriler, k => k.Ad == "KKEG" && k.VarsayilanAnaGrup == "689");
        }

        [Fact]
        public async Task Seed_iki_kez_calisinca_kategori_tekrarlanmaz()
        {
            using var db = await SeedliContextAsync();
            await BankaEkstreSeed.SeedAsync(db);

            Assert.Equal(19, db.EkstreIslemKategorileri.Count());
        }

        [Fact]
        public async Task Mevcut_kurallara_kategori_atanir()
        {
            using var db = await SeedliContextAsync();

            string? KategoriAdi(string desen)
            {
                var kural = db.EkstreSabitKurallar.First(k => k.IslemTipiDeseni == desen);
                return db.EkstreIslemKategorileri.FirstOrDefault(x => x.Id == kural.IslemKategorisiId)?.Ad;
            }

            // Kod taşıyan kayıtlar kategoriyi hesap kodunun ana grubundan alır.
            Assert.Equal("Banka gideri", KategoriAdi("MKK Masrafı"));       // 770 03 005
            Assert.Equal("Personel iş avansı", KategoriAdi("İş Avansı"));   // 195
            Assert.Equal("Personel maaş avansı", KategoriAdi("Avans"));     // 196
            Assert.Equal("Araç/hizmet gideri", KategoriAdi("HGS Bakiye Yükle")); // 740

            // Şablonda kod yok; kategori işlemin niteliğinden geliyor.
            var sablon = db.EkstreAciklamaSablonlari.First(s => s.IslemTipiDeseni == "Hesaplar Arası EFT");
            Assert.Equal("Hesaplar arası",
                         db.EkstreIslemKategorileri.First(k => k.Id == sablon.IslemKategorisiId).Ad);

            // Vergi kodu: trafik cezası KKEG (689), damga vergi borcu (360).
            var trafik = db.EkstreVergiKodlari.First(v => v.AnahtarKelime == "TRAFİK CEZ");
            Assert.Equal("KKEG", db.EkstreIslemKategorileri.First(k => k.Id == trafik.IslemKategorisiId).Ad);
        }

        [Fact]
        public async Task Kullanicinin_atadigi_kategori_seedde_ezilmez()
        {
            using var db = await SeedliContextAsync();

            var kural = db.EkstreSabitKurallar.First(k => k.IslemTipiDeseni == "MKK Masrafı");
            var kkeg = db.EkstreIslemKategorileri.First(k => k.Ad == "KKEG");

            kural.IslemKategorisiId = kkeg.Id;
            await db.SaveChangesAsync();

            await BankaEkstreSeed.SeedAsync(db);

            Assert.Equal(kkeg.Id, db.EkstreSabitKurallar.First(k => k.IslemTipiDeseni == "MKK Masrafı").IslemKategorisiId);
        }

        // ---- Kapsam görünümü ----

        [Fact]
        public async Task Kapsam_bankanin_kurallarini_kategorilere_dagitir()
        {
            using var db = await SeedliContextAsync();

            var ozet = await Servis(db).KapsamAsync(BankaEkstreTestOrtami.ParserTipi);

            Assert.Equal(19, ozet.Toplam);
            Assert.InRange(ozet.Tanimli, 1, 19);
            Assert.True(ozet.Tanimli < ozet.Toplam, "Ölçülen seed tüm kategorileri doldurmuyor; eksikler kırmızı gösterilecek.");

            var bankaGideri = ozet.Kategoriler.First(k => k.Ad == "Banka gideri");
            Assert.True(bankaGideri.KuralSayisi > 0);
            Assert.Contains("770 03 005", bankaGideri.HesapKodlari);

            // Aynı kategoride hem sabit kural hem şablon olabilir; mekanizma etiketle ayrılır.
            Assert.Contains(bankaGideri.Kurallar, k => k.Mekanizma == IslemKategorisiService.Mekanizmalar.SabitKural);
            Assert.Contains(bankaGideri.Kurallar, k => k.Mekanizma == IslemKategorisiService.Mekanizmalar.Sablon);

            // Kuralsız kategori "yok" olarak gösterilecek; sayısı sıfır.
            Assert.Contains(ozet.Kategoriler, k => k.KuralSayisi == 0);
        }

        [Fact]
        public async Task Kapsam_baska_bankanin_kurallarini_saymaz()
        {
            using var db = await SeedliContextAsync();

            var ozet = await Servis(db).KapsamAsync("BASKA_BANKA_VADESIZ");

            // Seed'i olmayan bir banka: bankaya özel kurallar sayılmaz, global tablolar
            // (vergi kodları) sayılır.
            var toplamKural = ozet.Kategoriler.Sum(k => k.KuralSayisi);
            var vergiler = ozet.Kategoriler.SelectMany(k => k.Kurallar)
                                           .Count(k => k.Mekanizma == IslemKategorisiService.Mekanizmalar.VergiKodu);

            Assert.Equal(vergiler, toplamKural);
            Assert.True(vergiler > 0);
        }

        [Fact]
        public async Task Kategorisiz_kural_ozet_satirinda_sayilir()
        {
            using var db = await SeedliContextAsync();

            var kural = db.EkstreSabitKurallar.First();
            kural.IslemKategorisiId = null;
            await db.SaveChangesAsync();

            var ozet = await Servis(db).KapsamAsync(BankaEkstreTestOrtami.ParserTipi);

            Assert.True(ozet.KategorisizKural > 0);
        }

        // ---- CRUD ----

        [Fact]
        public async Task Yeni_kategori_eklenir_ve_ana_grup_normalize_edilir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            var dto = await Servis(db).CreateAsync(new IslemKategorisiYazDto
            {
                Ad = " Kira gideri ",
                VarsayilanAnaGrup = "770 03 010",
                Sira = 5
            });

            Assert.Equal("Kira gideri", dto.Ad);
            // Ana grup kodun ilk segmentidir; kullanıcı tam kod girse de "770" saklanır.
            Assert.Equal("770", dto.VarsayilanAnaGrup);
        }

        [Fact]
        public async Task Ayni_ad_iki_kez_eklenemez()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await Servis(db).CreateAsync(new IslemKategorisiYazDto { Ad = "Banka gideri", VarsayilanAnaGrup = "770" });

            var hata = await Assert.ThrowsAsync<BankaEkstreKuralException>(
                () => Servis(db).CreateAsync(new IslemKategorisiYazDto { Ad = "banka gideri" }));

            Assert.Equal(nameof(IslemKategorisiYazDto.Ad), hata.Field);
        }

        [Fact]
        public async Task Kategori_silinince_kural_kalir_kategorisiz_olur()
        {
            using var db = await SeedliContextAsync();

            var kategori = db.EkstreIslemKategorileri.First(k => k.Ad == "Banka gideri");
            var kuralSayisi = db.EkstreSabitKurallar.Count(k => k.IslemKategorisiId == kategori.Id);
            Assert.True(kuralSayisi > 0);

            Assert.True(await Servis(db).DeleteAsync(kategori.Id));

            Assert.Empty(db.EkstreIslemKategorileri.Where(k => k.Id == kategori.Id).ToList());
            Assert.Equal(0, db.EkstreSabitKurallar.Count(k => k.IslemKategorisiId == kategori.Id));
            Assert.Contains(db.EkstreSabitKurallar, k => k.IslemTipiDeseni == "MKK Masrafı");
        }

        [Fact]
        public async Task Tanimsiz_kategori_kurala_yazilamaz()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            var servis = new SabitKuralService(
                db, new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() }),
                BankaEkstreTestOrtami.Kapsam());

            var hata = await Assert.ThrowsAsync<BankaEkstreKuralException>(
                () => servis.CreateAsync(new SabitKuralYazDto
                {
                    IslemTipiDeseni = "MKK Masrafı",
                    HesapKodu = "770 03 005",
                    IslemKategorisiId = 4242
                }));

            Assert.Equal(nameof(SabitKuralYazDto.IslemKategorisiId), hata.Field);
        }

        // ---- Satır etiketi ----

        [Fact]
        public void Satirin_kategorisi_hesap_kodunun_ana_grubundan_okunur()
        {
            var cozucu = KategoriCozucu.Kur(new[]
            {
                new IslemKategorisi { Id = 1, Ad = "Personel iş avansı", VarsayilanAnaGrup = "195", Aktif = true },
                new IslemKategorisi { Id = 2, Ad = "Personel maaş avansı", VarsayilanAnaGrup = "196", Aktif = true }
            });

            // 195 ile 196 ayrı kategoriler; muavin kırılımı etiketi değiştirmez.
            Assert.Equal("Personel iş avansı", cozucu.Coz("195 01 A20").Ad);
            Assert.Equal("Personel maaş avansı", cozucu.Coz("196").Ad);

            // Eşleşmeyen grup ve boş kod etiketsiz kalır; tahmin edilmez.
            Assert.Null(cozucu.Coz("120 D22").Ad);
            Assert.Null(cozucu.Coz(null).Ad);
        }

        [Fact]
        public void Pasif_kategori_satir_etiketi_uretmez()
        {
            var cozucu = KategoriCozucu.Kur(new[]
            {
                new IslemKategorisi { Id = 1, Ad = "Kredi", VarsayilanAnaGrup = "300", Aktif = false }
            });

            Assert.Null(cozucu.Coz("300 1 20").Ad);
        }

        // ---- Uçtan uca: onay ekranının etiketi ve filtresi ----

        private static EkstreService EkstreServisi(CatalogContext db)
        {
            var secici = new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() });
            return new EkstreService(db, secici, new UnvanCikarici(), new AciklamaUretici(),
                                     new HesapEslestirici(), new HesapEslesmeService(db, BankaEkstreTestOrtami.Kapsam()),
                                     new SabitKullanici(), BankaEkstreTestOrtami.Kapsam());
        }

        /// <summary>
        /// Gerçek dosyayı, gerçek planla ve seed'lenmiş kategorilerle işler. Kategori
        /// satıra yazılmıyor; sunucu hesap kodunun ana grubundan okuyor.
        /// </summary>
        private static async Task<(CatalogContext Db, int YuklemeId)> GercekDosyaAsync()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            await BankaEkstreSeed.SeedAsync(db);

            db.EkstreUnvanDesenleri.RemoveRange(db.EkstreUnvanDesenleri);
            db.EkstreAciklamaSablonlari.RemoveRange(db.EkstreAciklamaSablonlari);
            db.EkstreSabitKurallar.RemoveRange(db.EkstreSabitKurallar);
            await db.SaveChangesAsync();

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
            var yukleme = await EkstreServisi(db).YukleAsync(hesaplar[0].Id, dosya, "Vakıfbank_Hesap_Ekstresi.xlsx");

            return (db, yukleme.Id);
        }

        [Fact]
        public async Task Satirlar_hesap_kodunun_kategorisiyle_etiketlenir()
        {
            var (db, yuklemeId) = await GercekDosyaAsync();
            using var _ = db;

            var satirlar = (await EkstreServisi(db).GetSatirlarAsync(yuklemeId, null))!;

            // Banka masrafı (770 03 005) → Banka gideri; hesaplar arası (102 …) → Hesaplar arası.
            var masraf = satirlar.First(s => s.OnerilenHesapKodu == "770 03 005");
            Assert.Equal("Banka gideri", masraf.IslemKategorisiAdi);

            var bankalarArasi = satirlar.First(s => (s.OnerilenHesapKodu ?? string.Empty).StartsWith("102 "));
            Assert.Equal("Hesaplar arası", bankalarArasi.IslemKategorisiAdi);

            // Çözülemeyen satırın kodu yok; etiket de yok — tahmin edilmiyor.
            Assert.All(satirlar.Where(s => s.OnerilenHesapKodu is null && s.OnaylananHesapKodu is null),
                       s => Assert.Null(s.IslemKategorisiAdi));
        }

        [Fact]
        public async Task Kategori_filtresi_yalniz_o_kategorinin_satirlarini_verir()
        {
            var (db, yuklemeId) = await GercekDosyaAsync();
            using var _ = db;

            var servis = EkstreServisi(db);
            var hepsi = (await servis.GetSatirlarAsync(yuklemeId, null))!;

            var bankaGideri = db.EkstreIslemKategorileri.First(k => k.Ad == "Banka gideri");
            var suzulmus = (await servis.GetSatirlarAsync(yuklemeId, null, bankaGideri.Id))!;

            Assert.NotEmpty(suzulmus);
            Assert.True(suzulmus.Count < hepsi.Count);
            Assert.All(suzulmus, s => Assert.Equal(bankaGideri.Id, s.IslemKategorisiId));
        }

        [Fact]
        public async Task Kategori_eslestirme_sonucunu_degistirmez()
        {
            // Kategori bir etiket: tablo boşaltılınca kodlar ve katmanlar aynı kalmalı.
            var (db, yuklemeId) = await GercekDosyaAsync();
            using var _ = db;

            var servis = EkstreServisi(db);
            var kategorili = (await servis.GetSatirlarAsync(yuklemeId, null))!
                .Select(s => (s.Id, s.OnerilenHesapKodu, s.KaynakKatman, s.Durum)).ToList();

            db.EkstreIslemKategorileri.RemoveRange(db.EkstreIslemKategorileri);
            await db.SaveChangesAsync();

            using var dosya = BankaEkstreTestOrtami.GercekEkstre();
            var hesapId = db.EkstreBankaHesaplari.First(h => h.ParserTipi != null).Id;
            var ikinci = await servis.YukleAsync(hesapId, dosya, "ikinci.xlsx");

            var kategorisiz = (await servis.GetSatirlarAsync(ikinci.Id, null))!
                .Select(s => (s.OnerilenHesapKodu, s.KaynakKatman, s.Durum)).ToList();

            Assert.Equal(kategorili.Select(k => (k.OnerilenHesapKodu, k.KaynakKatman, k.Durum)).ToList(), kategorisiz);
            Assert.All((await servis.GetSatirlarAsync(ikinci.Id, null))!, s => Assert.Null(s.IslemKategorisiAdi));
        }
    }
}
