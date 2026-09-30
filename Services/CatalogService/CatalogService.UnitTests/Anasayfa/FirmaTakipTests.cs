using CatalogService.Api.Features.Anasayfa.Domain;
using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Features.FirmaBilgileri.Services;
using CatalogService.Api.Features.FirmaKontrol.Domain;
using CatalogService.Api.Features.FirmaKontrol.Dtos;
using CatalogService.Api.Features.FirmaKontrol.Services;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.BankaEkstre;
using CatalogService.UnitTests.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatalogService.UnitTests.Anasayfa
{
    /// <summary>
    /// Anasayfa künyesi — Takip kartı: sorumlu, dönemler, notlar, son işlemler ve imza
    /// yetkilileri başlık rozeti.
    /// </summary>
    public class FirmaTakipTests
    {
        private static readonly DateTime Bugun = new(2026, 9, 28);

        private const int Dgr = 3;
        private const int Ben = 16;
        private const int Baskasi = 17;

        private static CatalogContext Context()
        {
            var db = BankaEkstreTestOrtami.YeniContext();

            db.Firmalar.Add(new Firma
            {
                Id = Dgr, Unvan = "DGR A.Ş.", KisaAd = "DGR", VergiKimlikNo = "2951070824", VergiDairesi = "SARIYER", Aktif = true
            });
            db.FirmaSicilBilgileri.Add(new FirmaSicilBilgisi { Id = 1, FirmaId = Dgr, MersisNo = null });
            db.FirmaImzaYetkilileri.Add(new FirmaImzaYetkilisi
            {
                Id = 1, FirmaId = Dgr, Ad = "Zeynep POTUR", YetkiBitis = new DateTime(2028, 12, 22)
            });

            db.SaveChanges();
            return db;
        }

        private static FirmaOlayYazici Yazici(CatalogContext db, int kullanici = Ben)
            => new(db, new SabitKullanici(kullanici), NullLogger<FirmaOlayYazici>.Instance);

        private static FirmaKunyeService Kunye(CatalogContext db, int kullanici = Ben)
            => new(db, Yazici(db, kullanici), new SabitKullanici(kullanici));

        private static Task<FirmaPaneliDto> Panel(CatalogContext db, int kullanici = Ben)
            => new FirmaPaneliService(db, () => Bugun).PanelAsync(Dgr, default, kullanici.ToString());

        // ---- Sorumlu ----

        [Fact]
        public async Task Sorumlu_atanmamissa_bos_ve_eksik_listesinde()
        {
            using var db = Context();

            var detay = (await Panel(db)).Secili!;

            Assert.Null(detay.Takip.SorumluKullaniciId);
            Assert.Contains(detay.Eksikler, e => e.Odak == "sorumlu");
        }

        [Fact]
        public async Task Sorumlu_atanir_olay_yazilir_eksikten_duser()
        {
            using var db = Context();

            await Kunye(db).SorumluAtaAsync(Dgr, new FirmaSorumluAtaDto { KullaniciId = 16, KullaniciAdi = "cengiz" });

            var detay = (await Panel(db)).Secili!;
            Assert.Equal(16, detay.Takip.SorumluKullaniciId);
            Assert.Equal("cengiz", detay.Takip.SorumluKullaniciAdi);
            Assert.DoesNotContain(detay.Eksikler, e => e.Odak == "sorumlu");

            var olay = Assert.Single(detay.Takip.SonOlaylar);
            Assert.Equal(FirmaOlayTipi.SorumluAtandi, olay.OlayTipi);
            Assert.Contains("cengiz", olay.Aciklama);
        }

        [Fact]
        public async Task Sorumlu_kaldirilabiliyor()
        {
            using var db = Context();
            var servis = Kunye(db);

            await servis.SorumluAtaAsync(Dgr, new FirmaSorumluAtaDto { KullaniciId = 16, KullaniciAdi = "cengiz" });
            await servis.SorumluAtaAsync(Dgr, new FirmaSorumluAtaDto { KullaniciId = null });

            var firma = await db.Firmalar.AsNoTracking().SingleAsync(f => f.Id == Dgr);
            Assert.Null(firma.SorumluKullaniciId);
            Assert.Null(firma.SorumluKullaniciAdi);
        }

        // ---- Dönemler ----

        [Fact]
        public async Task Donemler_yuklu_yillar_yesil_cari_yil_yoksa_gri()
        {
            using var db = Context();
            db.FirmaKontrolMizanAgaclari.Add(new FirmaKontrolMizanAgac { FirmaId = Dgr, Donem = 1, Yil = 2025, UploadedAt = Bugun });
            db.FirmaKontrolMizanAgaclari.Add(new FirmaKontrolMizanAgac { FirmaId = Dgr, Donem = 0, Yil = 2025, UploadedAt = Bugun }); // 2024
            db.SaveChanges();

            var donemler = (await Panel(db)).Secili!.Takip.Donemler;

            Assert.Equal(new[] { 2026, 2025, 2024 }, donemler.Select(d => d.Yil));
            Assert.Equal(new[] { false, true, true }, donemler.Select(d => d.Yuklu));
        }

        // ---- Notlar ----

        [Fact]
        public async Task Son_not_kartta_digerleri_listede()
        {
            using var db = Context();
            var servis = Kunye(db);

            await servis.NotEkleAsync(Dgr, new FirmaNotuEkleDto { Metin = "ilk" });
            await Task.Delay(5);
            await servis.NotEkleAsync(Dgr, new FirmaNotuEkleDto { Metin = "ikinci" });

            var takip = (await Panel(db)).Secili!.Takip;
            Assert.Equal("ikinci", takip.SonNot!.Metin);
            Assert.Equal(2, takip.NotSayisi);
            Assert.Equal(new[] { "ikinci", "ilk" }, (await servis.NotlarAsync(Dgr)).Select(n => n.Metin));
        }

        [Fact]
        public async Task Notu_yalniz_yazan_silebilir()
        {
            using var db = Context();

            var not = await Kunye(db, Ben).NotEkleAsync(Dgr, new FirmaNotuEkleDto { Metin = "benim notum" });

            // Başkası: listede silinemez görünüyor, silmeye kalkınca reddediliyor.
            var baskasininGozuyle = Assert.Single(await Kunye(db, Baskasi).NotlarAsync(Dgr));
            Assert.False(baskasininGozuyle.Silinebilir);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Kunye(db, Baskasi).NotSilAsync(Dgr, not.Id));

            // Yazan silebiliyor.
            Assert.True((await Panel(db, Ben)).Secili!.Takip.SonNot!.Silinebilir);
            await Kunye(db, Ben).NotSilAsync(Dgr, not.Id);
            Assert.Empty(db.FirmaNotlari);
        }

        [Fact]
        public async Task Bos_not_reddedilir()
        {
            using var db = Context();

            await Assert.ThrowsAsync<FirmaKunyeKuralException>(() =>
                Kunye(db).NotEkleAsync(Dgr, new FirmaNotuEkleDto { Metin = "   " }));
        }

        // ---- Son işlemler (olay kaydı) ----

        [Fact]
        public async Task Kayit_yoksa_son_islemler_bos()
        {
            using var db = Context();

            Assert.Empty((await Panel(db)).Secili!.Takip.SonOlaylar);
        }

        [Fact]
        public async Task Kartta_son_bes_olay_yeniden_eskiye()
        {
            using var db = Context();
            for (var i = 1; i <= 7; i++)
            {
                db.FirmaOlayKayitlari.Add(new FirmaOlayKaydi
                {
                    FirmaId = Dgr, OlayTipi = FirmaOlayTipi.NotEklendi, Aciklama = $"olay {i}", Zaman = Bugun.AddMinutes(i)
                });
            }
            db.SaveChanges();

            var olaylar = (await Panel(db)).Secili!.Takip.SonOlaylar;

            Assert.Equal(new[] { "olay 7", "olay 6", "olay 5", "olay 4", "olay 3" }, olaylar.Select(o => o.Aciklama));
            Assert.Equal(7, (await Kunye(db).OlaylarAsync(Dgr)).Count);   // "Tüm hareketler"
        }

        [Fact]
        public async Task Mizan_yukleme_olayi_yaziliyor()
        {
            using var db = Context();

            await new FirmaKontrolMizanService(db, Yazici(db)).KaydetAsync(Dgr, new MizanKaydetRequest
            {
                Donem = 1, Yil = 2026,
                Satirlar = { new MizanHamSatirDto { Kod = "100", Bakiye = 1 } },
                Dugumler = { new MizanHamDugumDto { Kod = "100", BorcBakiye = 1 } }
            });

            var olay = Assert.Single(db.FirmaOlayKayitlari);
            Assert.Equal(FirmaOlayTipi.MizanYuklendi, olay.OlayTipi);
            Assert.Contains("2026", olay.Aciklama);
            Assert.Equal("16", olay.KullaniciId);
        }

        [Fact]
        public async Task Etiket_kurali_ekleme_ve_silme_olay_yaziyor()
        {
            using var db = Context();
            var servis = new EtiketService(db, Yazici(db));

            var boyut = await servis.BoyutEkleAsync(Dgr, new EtiketBoyutuYazDto { Ad = "Hat", Kapsam = EtiketKapsami.BuFirma, KapsamHesaplari = "6*" });
            var deger = await servis.DegerEkleAsync(boyut.Id, new EtiketDegeriYazDto { Ad = "A" });
            var kural = await servis.KuralEkleAsync(boyut.Id, new EtiketKuraliYazDto { Desen = "600*", DegerId = deger.Id });
            await servis.KuralSilAsync(kural.Id);

            Assert.Equal(new[] { FirmaOlayTipi.EtiketKuraliEklendi, FirmaOlayTipi.EtiketKuraliSilindi },
                         db.FirmaOlayKayitlari.OrderBy(o => o.Id).Select(o => o.OlayTipi));
        }

        [Fact]
        public async Task Sicil_kaydi_mukellefiyet_ve_sicil_olaylarini_ayri_yaziyor()
        {
            using var db = Context();
            var servis = new FirmaBilgiService(db, new SabitBankaFirmaKapsami(Dgr), Yazici(db));

            var dto = await servis.SicilGetAsync();
            dto.MersisNo = "0295107082400019";             // sicil
            dto.MukellefiyetTurleri = "0003, 0010";        // mükellefiyet (+ otomatik sınıflandırma)
            await servis.SicilKaydetAsync(dto);

            var tipler = db.FirmaOlayKayitlari.Select(o => o.OlayTipi).ToList();
            Assert.Contains(FirmaOlayTipi.MukellefiyetGuncellendi, tipler);
            Assert.Contains(FirmaOlayTipi.SicilGuncellendi, tipler);
            Assert.Contains(FirmaOlayTipi.SiniflandirmaGuncellendi, tipler);

            // Değişiklik yoksa olay yok.
            var sayi = db.FirmaOlayKayitlari.Count();
            await servis.SicilKaydetAsync(await servis.SicilGetAsync());
            Assert.Equal(sayi, db.FirmaOlayKayitlari.Count());
        }

        [Fact]
        public async Task Olay_yazilamazsa_asil_islem_bozulmuyor()
        {
            using var db = Context();
            // Gerçek yazıcı, içeride patlayan bir bağımlılıkla: hata yutulmalı, loglanmalı.
            var yazici = new FirmaOlayYazici(db, new PatlayanKullanici(), NullLogger<FirmaOlayYazici>.Instance);
            var servis = new FirmaKunyeService(db, yazici, new SabitKullanici(Ben));

            await servis.SorumluAtaAsync(Dgr, new FirmaSorumluAtaDto { KullaniciId = 16, KullaniciAdi = "cengiz" });

            Assert.Equal(16, (await db.Firmalar.AsNoTracking().SingleAsync(f => f.Id == Dgr)).SorumluKullaniciId);
            Assert.Empty(db.FirmaOlayKayitlari);

            // Context kirlenmedi: sonraki yazma işlemi de sorunsuz.
            await servis.NotEkleAsync(Dgr, new FirmaNotuEkleDto { Metin = "sonra" });
            Assert.Single(db.FirmaNotlari);
        }

        [Fact]
        public async Task Uzun_aciklama_kesiliyor()
        {
            using var db = Context();

            await Yazici(db).YazAsync(Dgr, FirmaOlayTipi.NotEklendi, new string('x', 900));

            Assert.Equal(500, db.FirmaOlayKayitlari.Single().Aciklama.Length);
        }

        /// <summary>Kimliği okunurken patlayan kullanıcı — olay yazıcısının hata yolu için.</summary>
        private sealed class PatlayanKullanici : CatalogService.Api.Infrastructure.Auth.IHttpCurrentUser
        {
            public bool IsAuthenticated => throw new InvalidProgramException("kimlik okunamadı");
            public string UserId => throw new InvalidProgramException("kimlik okunamadı");
            public string? UserName => throw new InvalidProgramException("kimlik okunamadı");
            public string? Email => null;
        }

        // ---- İmza yetkilileri rozeti ----

        [Fact]
        public async Task DGR_Zeynep_POTUR_yetkisi_uzun_rozet_cikmaz()
        {
            using var db = Context();

            Assert.Null((await Panel(db)).Secili!.YetkiRozeti);
        }

        [Fact]
        public void Yetki_rozeti_365_ve_90_gun_esikleri()
        {
            var yetkililer = new List<FirmaImzaYetkilisi>
            {
                new() { Ad = "A", YetkiBitis = Bugun.AddDays(364) },
                new() { Ad = "B", YetkiBitis = Bugun.AddDays(365) },     // sayılmaz
                new() { Ad = "C", YetkiBitis = Bugun.AddDays(-5) },      // dolmuş, sayılmaz
                new() { Ad = "D", YetkiBitis = null }                    // süresiz
            };

            var amber = FirmaPaneliKurucu.YetkiRozeti(yetkililer, Bugun)!;
            Assert.Equal(1, amber.Sayi);
            Assert.False(amber.Kritik);

            yetkililer.Add(new FirmaImzaYetkilisi { Ad = "E", YetkiBitis = Bugun.AddDays(89) });
            var kirmizi = FirmaPaneliKurucu.YetkiRozeti(yetkililer, Bugun)!;
            Assert.Equal(2, kirmizi.Sayi);
            Assert.True(kirmizi.Kritik);
        }
    }
}
