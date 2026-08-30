using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.BankaEkstre;

namespace CatalogService.UnitTests.Anasayfa
{
    /// <summary>
    /// Anasayfadaki firma bilgi paneli.
    ///
    /// Sınanan asıl şey iki başlık: <b>uyarı göstergesi</b> (kullanıcı firmaya tıklamadan
    /// sorunu görebiliyor mu, ve görmemesi gerekirken alarm çalıyor mu) ve <b>kapsam</b>
    /// (sağ panel gerçekten seçili firmanın kayıtlarından mı kuruluyor).
    ///
    /// "Bugün" testlerde sabit veriliyor: takvime bağlı bir kural gerçek saate
    /// bırakılırsa test bir gün kendiliğinden kırmızıya döner.
    /// </summary>
    public class FirmaPaneliTests
    {
        private static readonly DateTime Bugun = new(2026, 8, 30);

        private const int FirmaA = 301;   // ALPHA — künyesi tam, uyarısı yok
        private const int FirmaB = 302;   // CİTADEL — sicili eksik
        private const int FirmaC = 303;   // PROGROUP — imza yetkisi bitiyor, pay tutmuyor

        // ---- Ortam ----

        private static CatalogContext Context()
        {
            var db = BankaEkstreTestOrtami.YeniContext();

            db.Firmalar.AddRange(
                new Firma
                {
                    Id = FirmaA,
                    Unvan = "ALPHA AHŞAP SANAYİ A.Ş.",
                    KisaAd = "ALPHA",
                    VergiKimlikNo = "7721471008",
                    VergiDairesi = "Maslak",
                    TicaretSicilNo = "123456-5",
                    Aktif = true
                },
                new Firma
                {
                    Id = FirmaB,
                    Unvan = "CİTADEL GAYRİMENKUL A.Ş.",
                    KisaAd = "CİTADEL",
                    VergiKimlikNo = "7280624888",
                    VergiDairesi = "",           // eksik
                    TicaretSicilNo = "",         // eksik
                    Aktif = true
                },
                new Firma
                {
                    Id = FirmaC,
                    Unvan = "PROGROUP LOJİSTİK LTD. ŞTİ.",
                    KisaAd = "PROGROUP",
                    VergiKimlikNo = "6110455512",
                    VergiDairesi = "Şişli",
                    TicaretSicilNo = "998877-1",
                    Aktif = true
                });

            db.FirmaSicilBilgileri.AddRange(
                new FirmaSicilBilgisi
                {
                    Id = 1,
                    FirmaId = FirmaA,
                    MersisNo = "0772147100800011",
                    Adres = "Maslak, İstanbul",
                    NaceKodu = "16.23.01",
                    MukellefiyetTurleri = "Kurumlar, KDV, Muhtasar",
                    EFatura = true,
                    EDefter = false,
                    IseBaslamaTarihi = new DateTime(2014, 4, 1)
                },
                new FirmaSicilBilgisi
                {
                    Id = 3,
                    FirmaId = FirmaC,
                    MersisNo = "0611045551200015",
                    Adres = "Şişli, İstanbul"
                });

            // ALPHA: pay toplamı %100, yetki uzun süreli → uyarı yok.
            db.FirmaOrtaklari.AddRange(
                new FirmaOrtak { Id = 1, FirmaId = FirmaA, Ad = "Ahmet Yılmaz", TcknVkn = "12345678901", PayOrani = 60, PayTutari = 600_000 },
                new FirmaOrtak { Id = 2, FirmaId = FirmaA, Ad = "Ayşe Yılmaz", TcknVkn = "10987654321", PayOrani = 40, PayTutari = 400_000 });

            db.FirmaImzaYetkilileri.Add(new FirmaImzaYetkilisi
            {
                Id = 1,
                FirmaId = FirmaA,
                Ad = "Ahmet Yılmaz",
                Gorev = "Yönetim Kurulu Başkanı",
                YetkiBitis = Bugun.AddYears(2)
            });

            // PROGROUP: pay toplamı %90 ve yetki 30 gün sonra doluyor → iki uyarı.
            db.FirmaOrtaklari.Add(
                new FirmaOrtak { Id = 5, FirmaId = FirmaC, Ad = "Mehmet Demir", TcknVkn = "22233344455", PayOrani = 90, PayTutari = 900_000 });

            db.FirmaImzaYetkilileri.Add(new FirmaImzaYetkilisi
            {
                Id = 5,
                FirmaId = FirmaC,
                Ad = "Mehmet Demir",
                Gorev = "Müdür",
                YetkiBitis = Bugun.AddDays(30)
            });

            db.FirmaBelgeleri.AddRange(
                new FirmaBelgesi { Id = 1, FirmaId = FirmaA, Tur = FirmaBelgeTuru.ImzaSirkuleri, FileId = 11, FileName = "alpha-sirkuler.pdf", Length = 1000 },
                new FirmaBelgesi { Id = 2, FirmaId = FirmaC, Tur = FirmaBelgeTuru.VergiLevhasi, FileId = 22, FileName = "progroup-levha.pdf", Length = 2000 });

            db.SaveChanges();
            return db;
        }

        private static Task<FirmaPaneliDto> Panel(CatalogContext db, int? firmaId)
            => new FirmaPaneliService(db).PanelAsync(firmaId);

        /// <summary>Kurucuyu doğrudan çağırmak için tek firmalık kısayol.</summary>
        private static List<FirmaUyariDto> Uyarilar(Firma firma, FirmaSicilBilgisi? sicil,
                                                    List<FirmaOrtak>? ortaklar = null,
                                                    List<FirmaImzaYetkilisi>? yetkililer = null)
            => FirmaPaneliKurucu.Uyarilar(firma, sicil, ortaklar ?? new(), yetkililer ?? new(), Bugun);

        private static Firma TamFirma() => new()
        {
            Id = 1,
            Unvan = "TAM A.Ş.",
            VergiKimlikNo = "1111111111",
            VergiDairesi = "Kadıköy",
            TicaretSicilNo = "111111-1"
        };

        private static FirmaSicilBilgisi TamSicil() => new()
        {
            FirmaId = 1,
            MersisNo = "0111111111100011",
            Adres = "Kadıköy, İstanbul"
        };

        // ---- Liste ----

        [Fact]
        public async Task Liste_tum_firmalari_dondurur()
        {
            using var db = Context();

            var panel = await Panel(db, null);

            Assert.Equal(3, panel.Firmalar.Count);
            Assert.Equal(new[] { FirmaA, FirmaB, FirmaC }, panel.Firmalar.Select(f => f.FirmaId).OrderBy(id => id));

            // Listede ad ve VKN var: kullanıcı ikisiyle de arayabiliyor.
            var citadel = panel.Firmalar.Single(f => f.FirmaId == FirmaB);
            Assert.Equal("CİTADEL", citadel.Ad);
            Assert.Equal("7280624888", citadel.VergiKimlikNo);
        }

        [Fact]
        public async Task Firma_secilmemisse_ilk_firma_geliyor()
        {
            using var db = Context();

            var panel = await Panel(db, null);

            // Ekran ilk açılışta boş sağ panelle gelmesin diye sunucu ilk firmayı seçiyor;
            // sıralama ada göre olduğu için bu ALPHA.
            Assert.NotNull(panel.Secili);
            Assert.Equal(FirmaA, panel.Secili!.FirmaId);
        }

        [Fact]
        public async Task Firma_yoksa_panel_bos_gelir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            var panel = await Panel(db, null);

            Assert.Empty(panel.Firmalar);
            Assert.Null(panel.Secili);
        }

        // ---- Uyarı göstergesi ----

        [Fact]
        public async Task Kunyesi_tam_firmada_uyari_yok()
        {
            using var db = Context();

            var panel = await Panel(db, null);
            var alpha = panel.Firmalar.Single(f => f.FirmaId == FirmaA);

            Assert.False(alpha.UyariVar);
            Assert.Empty(alpha.Uyarilar);
        }

        [Fact]
        public async Task Eksik_zorunlu_sicil_alani_uyari_veriyor()
        {
            using var db = Context();

            var panel = await Panel(db, null);
            var citadel = panel.Firmalar.Single(f => f.FirmaId == FirmaB);

            var uyari = Assert.Single(citadel.Uyarilar.Where(u => u.Tur == FirmaUyariTuru.EksikSicilAlani));

            // Hangi alanların eksik olduğu mesajda yazıyor; kullanıcı ekranı açmadan biliyor.
            Assert.Contains("Vergi dairesi", uyari.Mesaj);
            Assert.Contains("Ticaret sicil no", uyari.Mesaj);
            Assert.Contains("MERSİS no", uyari.Mesaj);
            Assert.Contains("Adres", uyari.Mesaj);
        }

        [Fact]
        public async Task Pay_orani_toplami_yuz_degilse_uyari_veriyor()
        {
            using var db = Context();

            var panel = await Panel(db, null);
            var progroup = panel.Firmalar.Single(f => f.FirmaId == FirmaC);

            var uyari = Assert.Single(progroup.Uyarilar.Where(u => u.Tur == FirmaUyariTuru.PayOraniTutmuyor));
            Assert.Contains("%90", uyari.Mesaj);
        }

        [Fact]
        public async Task Imza_yetkisi_altmis_gunden_az_kaldiysa_uyari_veriyor()
        {
            using var db = Context();

            var panel = await Panel(db, null);
            var progroup = panel.Firmalar.Single(f => f.FirmaId == FirmaC);

            var uyari = Assert.Single(progroup.Uyarilar.Where(u => u.Tur == FirmaUyariTuru.ImzaYetkisiBitiyor));
            Assert.Contains("30 gün", uyari.Mesaj);

            // Uyarı sırası sabit: imza önce, sonra pay oranı.
            Assert.Equal(FirmaUyariTuru.ImzaYetkisiBitiyor, progroup.Uyarilar[0].Tur);
        }

        [Fact]
        public void Imza_yetkisi_tam_altmis_gun_kaldiysa_uyari_yok()
        {
            // Eşik sınırı: 60 gün "az kalmış" sayılmıyor, 59 sayılıyor.
            var altmis = Uyarilar(TamFirma(), TamSicil(), yetkililer: new()
            {
                new FirmaImzaYetkilisi { Ad = "A", YetkiBitis = Bugun.AddDays(60) }
            });

            var ellidokuz = Uyarilar(TamFirma(), TamSicil(), yetkililer: new()
            {
                new FirmaImzaYetkilisi { Ad = "A", YetkiBitis = Bugun.AddDays(59) }
            });

            Assert.Empty(altmis);
            Assert.Equal(FirmaUyariTuru.ImzaYetkisiBitiyor, Assert.Single(ellidokuz).Tur);
        }

        [Fact]
        public void Suresi_dolmus_yetkilinin_yaninda_gecerli_yetkili_varsa_uyari_yok()
        {
            // Süresi dolan kayıt SİLİNMİYOR (geçmişe dönük belge kontrolü için duruyor).
            // "Herhangi biri dolmuşsa uyar" deseydik, yürürlükteki sirküleri olan firma
            // sonsuza kadar alarm verirdi; kural firmanın EN GEÇ biten yetkisine bakıyor.
            var uyarilar = Uyarilar(TamFirma(), TamSicil(), yetkililer: new()
            {
                new FirmaImzaYetkilisi { Ad = "Eski", YetkiBitis = Bugun.AddYears(-2) },
                new FirmaImzaYetkilisi { Ad = "Yeni", YetkiBitis = Bugun.AddYears(1) }
            });

            Assert.Empty(uyarilar);
        }

        [Fact]
        public void Butun_yetkilerin_suresi_dolmussa_uyari_veriyor()
        {
            var uyari = Assert.Single(Uyarilar(TamFirma(), TamSicil(), yetkililer: new()
            {
                new FirmaImzaYetkilisi { Ad = "Eski", YetkiBitis = Bugun.AddDays(-10) }
            }));

            Assert.Equal(FirmaUyariTuru.ImzaYetkisiBitiyor, uyari.Tur);
            Assert.Contains("10 gün önce doldu", uyari.Mesaj);
        }

        [Fact]
        public void Suresiz_yetkili_uyariyi_kaldiriyor()
        {
            var uyarilar = Uyarilar(TamFirma(), TamSicil(), yetkililer: new()
            {
                new FirmaImzaYetkilisi { Ad = "Dolmuş", YetkiBitis = Bugun.AddDays(-1) },
                new FirmaImzaYetkilisi { Ad = "Süresiz", YetkiBitis = null }
            });

            Assert.Empty(uyarilar);
        }

        [Fact]
        public void Ortak_yoksa_pay_orani_uyarisi_cikmiyor()
        {
            // Boş tabloda "toplam %0" anlamsız bir alarm olurdu.
            Assert.Empty(Uyarilar(TamFirma(), TamSicil()));
        }

        [Fact]
        public void Mukellefiyet_alanlari_bos_diye_uyari_cikmiyor()
        {
            // Yeni eklenen alanlar her firmada boş; zorunlu listesine girselerdi panel
            // ilk gün baştan aşağı uyarı gösterir ve simge anlamını yitirirdi.
            var sicil = TamSicil();
            sicil.MukellefiyetTurleri = null;
            sicil.EFatura = null;
            sicil.EDefter = null;
            sicil.IseBaslamaTarihi = null;

            Assert.Empty(Uyarilar(TamFirma(), sicil));
        }

        // ---- Seçili firmanın ayrıntısı (kapsam) ----

        [Fact]
        public async Task Detay_secili_firmanin_kayitlarindan_kuruluyor()
        {
            using var db = Context();

            var panel = await Panel(db, FirmaC);

            Assert.NotNull(panel.Secili);
            var detay = panel.Secili!;

            Assert.Equal(FirmaC, detay.FirmaId);
            Assert.Equal("PROGROUP", detay.Ad);

            // Ortaklar, yetkililer ve belgeler YALNIZ bu firmanın.
            Assert.Equal(new[] { "Mehmet Demir" }, detay.Ortaklik.Ortaklar.Select(o => o.Ad));
            Assert.Equal(new[] { "Mehmet Demir" }, detay.Yetkililer.Select(y => y.Ad));
            Assert.Equal(new[] { "progroup-levha.pdf" }, detay.Belgeler.Select(b => b.FileName));

            // Başka firmanın sicili sızmıyor.
            Assert.Equal("0611045551200015", detay.Sicil.MersisNo);
            Assert.Null(detay.Mukellefiyet.MukellefiyetTurleri);
        }

        [Fact]
        public async Task Detay_mukellefiyet_alanlarini_tasiyor()
        {
            using var db = Context();

            var detay = (await Panel(db, FirmaA)).Secili!;

            Assert.Equal("Kurumlar, KDV, Muhtasar", detay.Mukellefiyet.MukellefiyetTurleri);
            Assert.True(detay.Mukellefiyet.EFatura);
            Assert.False(detay.Mukellefiyet.EDefter);
            Assert.Equal(new DateTime(2014, 4, 1), detay.Mukellefiyet.IseBaslamaTarihi);
            Assert.Equal("16.23.01", detay.Mukellefiyet.NaceKodu);
            Assert.Equal("Maslak", detay.Mukellefiyet.VergiDairesi);
        }

        [Fact]
        public async Task Sicili_olmayan_firmada_detay_bos_alanlarla_geliyor()
        {
            using var db = Context();

            var detay = (await Panel(db, FirmaB)).Secili!;

            // Kayıt yok diye istek patlamıyor; alanlar boş geliyor ve ekran "—" yazıyor.
            Assert.Equal(FirmaB, detay.FirmaId);
            Assert.Null(detay.Sicil.MersisNo);
            Assert.Null(detay.Sicil.Adres);
            Assert.Empty(detay.Belgeler);
        }

        [Fact]
        public async Task Tanimsiz_firma_istenirse_ilk_firmaya_dusuyor()
        {
            using var db = Context();

            // Filtre tanınmayan firmaId'yi zaten 400 ile reddediyor; buraya ancak
            // listeden düşmüş (pasife alınmış) bir firma gelebilir.
            var panel = await Panel(db, 9999);

            Assert.Equal(FirmaA, panel.Secili!.FirmaId);
            Assert.Equal(new[] { "alpha-sirkuler.pdf" }, panel.Secili.Belgeler.Select(b => b.FileName));
        }

        [Fact]
        public async Task Yetkili_satirinda_kalan_gun_sunucuda_hesaplaniyor()
        {
            using var db = Context();

            var yetkili = Assert.Single((await Panel(db, FirmaC)).Secili!.Yetkililer);

            // Sunucu hesaplıyor: istemcinin saatine bırakılsaydı iki kullanıcı aynı kaydı
            // farklı görürdü.
            Assert.Equal(30, yetkili.KalanGun);
            Assert.False(yetkili.SuresiDoldu);
        }

        [Fact]
        public async Task Pasif_firma_listede_gorunmuyor()
        {
            using var db = Context();

            var pasif = db.Firmalar.Single(f => f.Id == FirmaB);
            pasif.Aktif = false;
            db.SaveChanges();

            var panel = await Panel(db, null);

            Assert.Equal(new[] { FirmaA, FirmaC }, panel.Firmalar.Select(f => f.FirmaId).OrderBy(id => id));
        }
    }
}
