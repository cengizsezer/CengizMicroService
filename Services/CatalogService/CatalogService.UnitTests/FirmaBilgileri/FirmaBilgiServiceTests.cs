using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Features.FirmaBilgileri.Dtos;
using CatalogService.Api.Features.FirmaBilgileri.Services;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.BankaEkstre;

namespace CatalogService.UnitTests.FirmaBilgileri
{
    /// <summary>
    /// Firma Bilgileri: sicil, ortaklık, imza yetkilileri ve belgeler.
    ///
    /// Sınanan asıl şey <b>kapsam</b> ve <b>kurallar</b>: kayıtlar doğru firmaya
    /// yazılıyor mu, başka firmanın verisi görünüyor mu, pay oranı uyarısı ne zaman
    /// çıkıyor, süresi dolmuş yetkili nasıl işaretleniyor.
    /// </summary>
    public class FirmaBilgiServiceTests
    {
        private const int FirmaA = 201;
        private const int FirmaB = 202;

        private static CatalogContext Context()
        {
            var db = BankaEkstreTestOrtami.YeniContext();

            db.Firmalar.Add(new Firma
            {
                Id = FirmaA,
                Unvan = "ALPHA AHŞAP SANAYİ A.Ş.",
                VergiKimlikNo = "7721471008",
                KisaAd = "ALPHA",
                VergiDairesi = "Maslak",
                Aktif = true
            });

            db.Firmalar.Add(new Firma
            {
                Id = FirmaB,
                Unvan = "CİTADEL GAYRİMENKUL A.Ş.",
                VergiKimlikNo = "7280624888",
                KisaAd = "CİTADEL",
                Aktif = true
            });

            db.SaveChanges();
            return db;
        }

        private static FirmaBilgiService Servis(CatalogContext db, int firmaId = FirmaA)
            => new(db, new SabitBankaFirmaKapsami(firmaId));

        // ---- Sicil ----

        [Fact]
        public async Task Sicil_firmanin_kendi_alanlariyla_birlikte_gelir()
        {
            using var db = Context();

            var sicil = await Servis(db).SicilGetAsync();

            // catalog.Firmalar'daki alanlar kopyalanmadı, oradan okunuyor.
            Assert.Equal("ALPHA AHŞAP SANAYİ A.Ş.", sicil.Unvan);
            Assert.Equal("7721471008", sicil.VergiKimlikNo);
            Assert.Equal("Maslak", sicil.VergiDairesi);
            // Modülün kendi alanları henüz boş.
            Assert.Null(sicil.MersisNo);
        }

        [Fact]
        public async Task Sicil_kaydi_iki_tabloyu_da_yazar()
        {
            using var db = Context();
            var servis = Servis(db);

            var dto = await servis.SicilGetAsync();
            dto.Unvan = "ALPHA AHŞAP VE ORMAN ÜRÜNLERİ A.Ş.";
            dto.VergiDairesi = "Sarıyer";
            dto.MersisNo = "0772147100800011";
            dto.KurulusTarihi = new DateTime(2014, 3, 17);
            dto.Adres = "Maslak Mah. No:1 Sarıyer/İstanbul";
            dto.NaceKodu = "16.23.01";
            dto.Sermaye = 2_500_000m;

            var sonuc = await servis.SicilKaydetAsync(dto);

            Assert.Equal("ALPHA AHŞAP VE ORMAN ÜRÜNLERİ A.Ş.", sonuc.Unvan);
            Assert.Equal("0772147100800011", sonuc.MersisNo);
            Assert.Equal(2_500_000m, sonuc.Sermaye);

            // Firma tablosu gerçekten güncellendi.
            Assert.Equal("Sarıyer", db.Firmalar.Single(f => f.Id == FirmaA).VergiDairesi);
            // Modülün tablosunda firma başına tek kayıt.
            Assert.Single(db.FirmaSicilBilgileri.Where(s => s.FirmaId == FirmaA));
        }

        [Fact]
        public async Task Sicil_orka_firma_kodunu_kaydeder_ve_okur()
        {
            // Alan catalog.Firmalar'da; "ORKA'ya Aktar" işi bunu okuyor.
            using var db = Context();
            var servis = Servis(db);

            var dto = await servis.SicilGetAsync();
            Assert.Null(dto.OrkaFirmaKodu);

            dto.OrkaFirmaKodu = "  0001  ";
            var sonuc = await servis.SicilKaydetAsync(dto);

            Assert.Equal("0001", sonuc.OrkaFirmaKodu);
            Assert.Equal("0001", db.Firmalar.Single(f => f.Id == FirmaA).OrkaFirmaKodu);
        }

        [Fact]
        public async Task Sicil_bos_orka_kodunu_null_yazar()
        {
            // Boş bırakmak alanı temizliyor: nullable kalması işin reddedilme
            // kuralının (boş kod = iş yok) tek anlamlı olmasını sağlıyor.
            using var db = Context();
            var servis = Servis(db);

            var dto = await servis.SicilGetAsync();
            dto.OrkaFirmaKodu = "0001";
            await servis.SicilKaydetAsync(dto);

            dto.OrkaFirmaKodu = "   ";
            var sonuc = await servis.SicilKaydetAsync(dto);

            Assert.Null(sonuc.OrkaFirmaKodu);
            Assert.Null(db.Firmalar.Single(f => f.Id == FirmaA).OrkaFirmaKodu);
        }

        [Fact]
        public async Task Sicil_ikinci_kayitta_yeni_satir_acmaz()
        {
            using var db = Context();
            var servis = Servis(db);

            var dto = await servis.SicilGetAsync();
            dto.MersisNo = "1";
            await servis.SicilKaydetAsync(dto);

            dto.MersisNo = "2";
            await servis.SicilKaydetAsync(dto);

            var kayit = Assert.Single(db.FirmaSicilBilgileri.Where(s => s.FirmaId == FirmaA));
            Assert.Equal("2", kayit.MersisNo);
        }

        [Theory]
        [InlineData("")]
        [InlineData("123")]
        [InlineData("123456789012")]
        public async Task Gecersiz_vkn_reddedilir(string vkn)
        {
            using var db = Context();
            var servis = Servis(db);

            var dto = await servis.SicilGetAsync();
            dto.VergiKimlikNo = vkn;

            await Assert.ThrowsAsync<FirmaBilgiKuralException>(() => servis.SicilKaydetAsync(dto));
        }

        [Fact]
        public async Task Unvansiz_sicil_reddedilir()
        {
            using var db = Context();
            var servis = Servis(db);

            var dto = await servis.SicilGetAsync();
            dto.Unvan = "   ";

            await Assert.ThrowsAsync<FirmaBilgiKuralException>(() => servis.SicilKaydetAsync(dto));
        }

        // ---- Kapsam ----

        [Fact]
        public async Task Baska_firmanin_verisi_gorunmez()
        {
            using var db = Context();

            await Servis(db, FirmaA).OrtaklarKaydetAsync(new List<FirmaOrtakDto>
            {
                new() { Ad = "AHMET YILMAZ", PayOrani = 100m, PayTutari = 1000m }
            });

            var digerFirma = await Servis(db, FirmaB).OrtaklarGetAsync();

            Assert.Empty(digerFirma.Ortaklar);
        }

        [Fact]
        public async Task Kapsam_secilmeden_okuma_reddedilir()
        {
            using var db = Context();
            var servis = new FirmaBilgiService(db, new SabitBankaFirmaKapsami(0));

            // Sessiz varsayılan yok: kapsamsız istek "hiç kayıt yok" gibi görünüp
            // kullanıcıyı yanıltırdı.
            await Assert.ThrowsAsync<FirmaBilgiKuralException>(() => servis.OrtaklarGetAsync());
        }

        // ---- Ortaklık ----

        [Fact]
        public async Task Ortaklar_kaydedilir_ve_toplamlar_hesaplanir()
        {
            using var db = Context();
            var servis = Servis(db);

            var sonuc = await servis.OrtaklarKaydetAsync(new List<FirmaOrtakDto>
            {
                new() { Ad = "AHMET YILMAZ", TcknVkn = "12345678901", PayOrani = 60m, PayTutari = 600_000m },
                new() { Ad = "MEHMET DEMİR", TcknVkn = "10987654321", PayOrani = 40m, PayTutari = 400_000m }
            });

            Assert.Equal(2, sonuc.Ortaklar.Count);
            Assert.Equal(100m, sonuc.ToplamPayOrani);
            Assert.Equal(1_000_000m, sonuc.ToplamPayTutari);
            Assert.False(sonuc.PayOraniUyarisi);
        }

        [Theory]
        [InlineData(60, 30, true)]    // eksik
        [InlineData(60, 50, true)]    // fazla
        [InlineData(60, 40, false)]   // tam
        [InlineData(33.33, 66.67, false)] // kuruş farkı toleransı içinde
        public async Task Toplam_yuz_degilse_uyarilir_ama_kayit_engellenmez(
            decimal birinci, decimal ikinci, bool uyariBekleniyor)
        {
            using var db = Context();
            var servis = Servis(db);

            var sonuc = await servis.OrtaklarKaydetAsync(new List<FirmaOrtakDto>
            {
                new() { Ad = "A", PayOrani = birinci },
                new() { Ad = "B", PayOrani = ikinci }
            });

            Assert.Equal(uyariBekleniyor, sonuc.PayOraniUyarisi);
            // Uyarı kaydı engellemez: geçiş dönemlerinde tablo tutmayabiliyor.
            Assert.Equal(2, sonuc.Ortaklar.Count);
        }

        [Fact]
        public async Task Ortak_yoksa_uyari_verilmez()
        {
            using var db = Context();

            var sonuc = await Servis(db).OrtaklarGetAsync();

            // Boş tabloda "toplam %0" anlamsız bir alarm olurdu.
            Assert.False(sonuc.PayOraniUyarisi);
        }

        [Fact]
        public async Task Gonderilmeyen_ortak_silinir()
        {
            using var db = Context();
            var servis = Servis(db);

            var ilk = await servis.OrtaklarKaydetAsync(new List<FirmaOrtakDto>
            {
                new() { Ad = "AHMET YILMAZ", PayOrani = 60m },
                new() { Ad = "MEHMET DEMİR", PayOrani = 40m }
            });

            var kalan = ilk.Ortaklar.Where(o => o.Ad == "AHMET YILMAZ").ToList();
            kalan[0].PayOrani = 100m;

            var sonuc = await servis.OrtaklarKaydetAsync(kalan);

            // Ekrandan silinen satır sunucuda da silinir; aksi hâlde toplam tutmazdı.
            Assert.Single(sonuc.Ortaklar);
            Assert.Equal(100m, sonuc.ToplamPayOrani);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        public async Task Aralik_disi_pay_orani_reddedilir(decimal oran)
        {
            using var db = Context();

            await Assert.ThrowsAsync<FirmaBilgiKuralException>(
                () => Servis(db).OrtaklarKaydetAsync(new List<FirmaOrtakDto> { new() { Ad = "A", PayOrani = oran } }));
        }

        [Fact]
        public async Task Adsiz_ortak_reddedilir()
        {
            using var db = Context();

            await Assert.ThrowsAsync<FirmaBilgiKuralException>(
                () => Servis(db).OrtaklarKaydetAsync(new List<FirmaOrtakDto> { new() { Ad = " ", PayOrani = 100m } }));
        }

        [Fact]
        public async Task Gecersiz_uzunluktaki_kimlik_no_reddedilir()
        {
            using var db = Context();

            await Assert.ThrowsAsync<FirmaBilgiKuralException>(
                () => Servis(db).OrtaklarKaydetAsync(
                    new List<FirmaOrtakDto> { new() { Ad = "A", TcknVkn = "123", PayOrani = 100m } }));
        }

        // ---- İmza yetkilileri ----

        [Fact]
        public async Task Suresi_dolmus_yetkili_isaretlenir_ama_silinmez()
        {
            using var db = Context();
            var servis = Servis(db);

            var sonuc = await servis.YetkililerKaydetAsync(new List<FirmaImzaYetkilisiDto>
            {
                new()
                {
                    Ad = "ESKİ YETKİLİ",
                    Gorev = "Genel Müdür",
                    TemsilSekli = TemsilSekli.Munferit,
                    YetkiBaslangic = new DateTime(2020, 1, 1),
                    YetkiBitis = DateTime.Today.AddDays(-1)
                },
                new()
                {
                    Ad = "GÜNCEL YETKİLİ",
                    TemsilSekli = TemsilSekli.Musterek,
                    YetkiBaslangic = new DateTime(2024, 1, 1)
                }
            });

            // Geçmişe dönük belge kontrolü için kayıt duruyor, yalnız işaretleniyor.
            Assert.Equal(2, sonuc.Count);
            Assert.True(sonuc.Single(y => y.Ad == "ESKİ YETKİLİ").SuresiDoldu);
            Assert.False(sonuc.Single(y => y.Ad == "GÜNCEL YETKİLİ").SuresiDoldu);
        }

        [Fact]
        public void Bitis_gunu_dahil_gecerli_sayilir()
        {
            var bugun = new DateTime(2026, 8, 29);

            var bugunBiten = FirmaBilgiService.Dto(
                new FirmaImzaYetkilisi { Ad = "A", YetkiBitis = bugun }, bugun);

            var dunBiten = FirmaBilgiService.Dto(
                new FirmaImzaYetkilisi { Ad = "B", YetkiBitis = bugun.AddDays(-1) }, bugun);

            Assert.False(bugunBiten.SuresiDoldu);
            Assert.True(dunBiten.SuresiDoldu);
        }

        [Fact]
        public void Bitis_tarihi_bossa_yetki_suresizdir()
        {
            var dto = FirmaBilgiService.Dto(new FirmaImzaYetkilisi { Ad = "A" }, DateTime.Today);

            Assert.False(dto.SuresiDoldu);
        }

        [Fact]
        public async Task Bitis_baslangictan_once_olamaz()
        {
            using var db = Context();

            await Assert.ThrowsAsync<FirmaBilgiKuralException>(
                () => Servis(db).YetkililerKaydetAsync(new List<FirmaImzaYetkilisiDto>
                {
                    new()
                    {
                        Ad = "A",
                        YetkiBaslangic = new DateTime(2026, 5, 1),
                        YetkiBitis = new DateTime(2026, 4, 1)
                    }
                }));
        }

        [Fact]
        public async Task Gecersiz_tckn_reddedilir()
        {
            using var db = Context();

            await Assert.ThrowsAsync<FirmaBilgiKuralException>(
                () => Servis(db).YetkililerKaydetAsync(
                    new List<FirmaImzaYetkilisiDto> { new() { Ad = "A", Tckn = "1234" } }));
        }

        // ---- Belgeler ----

        [Fact]
        public async Task Pdf_belge_eklenir_ve_listelenir()
        {
            using var db = Context();
            var servis = Servis(db);

            await servis.BelgeEkleAsync(new FirmaBelgesiOlusturDto
            {
                Tur = FirmaBelgeTuru.ImzaSirkuleri,
                FileId = 50,
                FileName = "sirkuler.pdf",
                ContentType = "application/pdf",
                Length = 4096,
                Aciklama = "2026"
            }, "test.kullanici");

            var belgeler = await servis.BelgelerGetAsync();

            var belge = Assert.Single(belgeler);
            Assert.Equal(FirmaBelgeTuru.ImzaSirkuleri, belge.Tur);
            Assert.Equal("2026", belge.Aciklama);
            Assert.Equal("test.kullanici", belge.YukleyenKullanici);
        }

        [Fact]
        public async Task Ayni_turden_ikinci_belge_eskisini_silmez()
        {
            using var db = Context();
            var servis = Servis(db);

            await servis.BelgeEkleAsync(new FirmaBelgesiOlusturDto
            {
                Tur = FirmaBelgeTuru.VergiLevhasi, FileId = 60, FileName = "2025.pdf", Length = 1024
            }, null);

            await servis.BelgeEkleAsync(new FirmaBelgesiOlusturDto
            {
                Tur = FirmaBelgeTuru.VergiLevhasi, FileId = 61, FileName = "2026.pdf", Length = 1024
            }, null);

            // Vergi levhası her yıl yenileniyor; eskisi kayıtta kalmalı (beyanname
            // eklerinden ayrılan nokta).
            Assert.Equal(2, (await servis.BelgelerGetAsync()).Count);
        }

        [Fact]
        public async Task Pdf_olmayan_belge_reddedilir()
        {
            using var db = Context();

            await Assert.ThrowsAsync<FirmaBilgiKuralException>(
                () => Servis(db).BelgeEkleAsync(new FirmaBelgesiOlusturDto
                {
                    Tur = FirmaBelgeTuru.Diger, FileId = 1, FileName = "x.png", ContentType = "image/png", Length = 10
                }, null));
        }

        [Fact]
        public async Task Sinirdan_buyuk_belge_reddedilir()
        {
            using var db = Context();

            await Assert.ThrowsAsync<FirmaBilgiKuralException>(
                () => Servis(db).BelgeEkleAsync(new FirmaBelgesiOlusturDto
                {
                    Tur = FirmaBelgeTuru.Diger, FileId = 1, FileName = "x.pdf",
                    Length = FirmaBilgiService.EnFazlaBayt + 1
                }, null));
        }

        [Fact]
        public async Task Silinen_belgenin_dosya_kimligi_geri_doner()
        {
            using var db = Context();
            var servis = Servis(db);

            var belge = await servis.BelgeEkleAsync(new FirmaBelgesiOlusturDto
            {
                Tur = FirmaBelgeTuru.FaaliyetBelgesi, FileId = 77, FileName = "faaliyet.pdf", Length = 512
            }, null);

            var fileId = await servis.BelgeSilAsync(belge.Id);

            Assert.Equal(77, fileId);
            Assert.Empty(await servis.BelgelerGetAsync());
        }

        [Fact]
        public async Task Baska_firmanin_belgesi_silinemez()
        {
            using var db = Context();

            var belge = await Servis(db, FirmaA).BelgeEkleAsync(new FirmaBelgesiOlusturDto
            {
                Tur = FirmaBelgeTuru.Diger, FileId = 90, FileName = "a.pdf", Length = 100
            }, null);

            await Assert.ThrowsAsync<FirmaBilgiKuralException>(
                () => Servis(db, FirmaB).BelgeSilAsync(belge.Id));
        }
    }
}
