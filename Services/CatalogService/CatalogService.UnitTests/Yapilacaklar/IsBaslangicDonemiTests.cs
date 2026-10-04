using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.Yapilacaklar;
using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Features.Yapilacaklar.Services;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.Ajanlar;
using CatalogService.UnitTests.BankaEkstre;
using CatalogService.UnitTests.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatalogService.UnitTests.Yapilacaklar
{
    /// <summary>
    /// İşin başlangıç dönemi (Prompt 17). Saat 28.09.2026 — <see cref="DonemPanosuTests"/> ile aynı ortam.
    /// Ekim'de Eylül'ün işini girme senaryosu burada "Eylül'de Ağustos'unkini girme" olarak kurulur.
    /// </summary>
    public class IsBaslangicDonemiTests
    {
        private static readonly DateTime Bugun = new(2026, 9, 28);
        private const int Dgr = 3;
        private const int Cubic = 4;
        private const int Ben = 16;

        private static CatalogContext Context()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            db.Firmalar.AddRange(
                new Firma { Id = Dgr, Unvan = "DGR A.Ş.", KisaAd = "DGR", VergiKimlikNo = "2951070824", Aktif = true },
                new Firma { Id = Cubic, Unvan = "CUBIC LTD.", KisaAd = "CUBIC", VergiKimlikNo = "1111111111", Aktif = true });
            db.FirmaSicilBilgileri.AddRange(
                new FirmaSicilBilgisi { Id = 1, FirmaId = Dgr, MukellefiyetTurleri = "0003 - MUHTASAR, 0015 - KATMA DEĞER VERGİSİ" },
                new FirmaSicilBilgisi { Id = 2, FirmaId = Cubic, MukellefiyetTurleri = "0003 - MUHTASAR" });
            db.SaveChanges();

            VergiTakvimiSeed.SeedAsync(db, Bugun).GetAwaiter().GetResult();
            return db;
        }

        private static SahteSaat Saat() => new(new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero));

        private static YapilacaklarService Isler(CatalogContext db)
            => new(db, Saat(), new SabitKullanici(Ben),
                   new FirmaOlayYazici(db, new SabitKullanici(Ben), NullLogger<FirmaOlayYazici>.Instance));

        private static DonemPanosuService Pano(CatalogContext db) => new(db, Saat(), Isler(db), new SabitKullanici(Ben));

        private static FirmaIsiKaydetDto Is(string baslik, IsTekrari tekrar = IsTekrari.Aylik, string? ilkDonem = null) => new()
        {
            Baslik = baslik, Tekrar = tekrar, GunKurali = IsGunKurali.DonemSonu, IlkDonem = ilkDonem
        };

        private static DonemHucreDurumu Durum(DonemPanosuDto pano, int firmaId, string kolonBasligi)
        {
            var i = pano.Kolonlar.FindIndex(k => k.Baslik == kolonBasligi);
            return i < 0 ? DonemHucreDurumu.Yok : pano.Satirlar.Single(s => s.FirmaId == firmaId).Hucreler[i].Durum;
        }

        // ---- Model ----

        [Fact]
        public async Task Gecmis_ay_secilen_is_o_aydan_baslar_oncesinde_yok()
        {
            using var db = Context();
            var eklenen = await Isler(db).IsEkleAsync(Dgr, Is("Stok sayımı", ilkDonem: "2026-08"));
            var pano = Pano(db);

            Assert.Equal("2026-08", eklenen.IlkDonem);
            Assert.Equal("2026-08", eklenen.BaslangicDonemi);
            Assert.Equal(DonemHucreDurumu.Eksik, Durum(await pano.PanoAsync(2026, 8), Dgr, "Stok sayımı"));
            Assert.Equal(DonemHucreDurumu.Yok, Durum(await pano.PanoAsync(2026, 7), Dgr, "Stok sayımı"));
            Assert.Equal("2026-08", (await pano.PanoAsync(2026, 8)).Isler.Single(s => s.Baslik == "Stok sayımı").IlkDonem);
        }

        [Fact]
        public async Task Bos_ilk_donem_eski_davranis_eklendigi_aydan()
        {
            using var db = Context();
            var eklenen = await Isler(db).IsEkleAsync(Dgr, Is("Stok sayımı"));
            var pano = Pano(db);

            Assert.Null(eklenen.IlkDonem);
            Assert.Equal("2026-09", eklenen.BaslangicDonemi);
            Assert.Equal(DonemHucreDurumu.Yok, Durum(await pano.PanoAsync(2026, 8), Dgr, "Stok sayımı"));
            Assert.Equal(DonemHucreDurumu.Eksik, Durum(await pano.PanoAsync(2026, 9), Dgr, "Stok sayımı"));
        }

        [Fact]
        public async Task Eklenme_ayina_esit_ilk_donem_saklanmaz_ve_not_cikmaz()
        {
            using var db = Context();
            var eklenen = await Isler(db).IsEkleAsync(Dgr, Is("Stok sayımı", ilkDonem: "2026-09"));

            Assert.Null(eklenen.IlkDonem);
            Assert.Null((await db.FirmaIsleri.SingleAsync()).IlkDonem);
        }

        [Fact]
        public async Task Gelecek_ay_serbest()
        {
            using var db = Context();
            await Isler(db).IsEkleAsync(Dgr, Is("Yıl sonu hazırlığı", ilkDonem: "2026-11"));
            var pano = Pano(db);

            Assert.Equal(DonemHucreDurumu.Yok, Durum(await pano.PanoAsync(2026, 10), Dgr, "Yıl sonu hazırlığı"));
            Assert.Equal(DonemHucreDurumu.Eksik, Durum(await pano.PanoAsync(2026, 11), Dgr, "Yıl sonu hazırlığı"));
        }

        [Theory]
        [InlineData("2026-13")]
        [InlineData("09.2026")]
        [InlineData("2026-Q3")]
        [InlineData("abc")]
        public async Task Gecersiz_bicim_reddedilir(string ilkDonem)
        {
            using var db = Context();
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => Isler(db).IsEkleAsync(Dgr, Is("Stok sayımı", ilkDonem: ilkDonem)));
        }

        [Fact]
        public async Task Tek_seferlik_iste_ilk_donem_yok_sayilir()
        {
            using var db = Context();
            var dto = Is("Kuruluş evrakı", IsTekrari.TekSefer, "bozuk-deger");
            dto.TekSeferTarih = new DateTime(2026, 10, 15);
            var eklenen = await Isler(db).IsEkleAsync(Dgr, dto);

            Assert.Null(eklenen.IlkDonem);
            Assert.Null(eklenen.BaslangicDonemi);
            Assert.Equal(DonemHucreDurumu.Eksik, Durum(await Pano(db).PanoAsync(2026, 10), Dgr, "Kuruluş evrakı"));
        }

        [Fact]
        public async Task Uc_aylik_is_ilk_donemin_ceyreginden_baslar_gecerlilik_degismez()
        {
            using var db = Context();
            await Isler(db).IsEkleAsync(Dgr, Is("Geçici vergi hazırlığı", IsTekrari.UcAylik, "2026-05"));
            var pano = Pano(db);

            Assert.Equal(DonemHucreDurumu.Yok, Durum(await pano.PanoAsync(2026, 3), Dgr, "Geçici vergi hazırlığı"));   // Q1: henüz yok
            Assert.Equal(DonemHucreDurumu.Eksik, Durum(await pano.PanoAsync(2026, 6), Dgr, "Geçici vergi hazırlığı")); // Q2
            Assert.Equal(DonemHucreDurumu.Yok, Durum(await pano.PanoAsync(2026, 7), Dgr, "Geçici vergi hazırlığı"));   // çeyrek sonu değil
            Assert.Equal(DonemHucreDurumu.Eksik, Durum(await pano.PanoAsync(2026, 9), Dgr, "Geçici vergi hazırlığı")); // Q3
        }

        // ---- Kuyruk ----

        [Fact]
        public async Task Gecmis_doneme_yazilan_is_kuyrukta_gecikmis_gorunur()
        {
            using var db = Context();
            await Isler(db).IsEkleAsync(Dgr, Is("Stok sayımı", ilkDonem: "2026-08"));

            var satirlar = YapilacaklarKurucu.Satirlar(Bugun,
                new[] { new YapilacaklarKurucu.FirmaGirdisi(Dgr, "DGR", null, null, Array.Empty<string>()) },
                Array.Empty<VergiTakvimi>(), await db.FirmaIsleri.ToListAsync(), Array.Empty<IsTamamlama>());

            var agustos = Assert.Single(satirlar, s => s.DonemAnahtari == "2026-08");
            Assert.Equal(IsDurumu.Gecikti, agustos.Durum);
        }

        // ---- Toplu ekleme ve kopyalama ----

        [Fact]
        public async Task Toplu_eklemede_butun_satirlar_tek_baslangic_ayini_alir()
        {
            using var db = Context();
            var sonuc = await Isler(db).TopluEkleAsync(Dgr, new TopluEkleDto
            {
                Isler = { Is("Bordro"), Is("Banka ekstresi"), Is("Geçici vergi hazırlığı", IsTekrari.UcAylik) },
                IlkDonem = "2026-08"
            });

            Assert.Equal(3, sonuc.Eklenen);
            Assert.All(await db.FirmaIsleri.ToListAsync(), i => Assert.Equal("2026-08", i.IlkDonem));
        }

        [Fact]
        public async Task Toplu_guncellemede_mevcut_isin_baslangici_korunur()
        {
            using var db = Context();
            var servis = Isler(db);
            await servis.IsEkleAsync(Dgr, Is("Bordro", ilkDonem: "2026-06"));

            var sonuc = await servis.TopluEkleAsync(Dgr, new TopluEkleDto { Isler = { Is("Bordro") }, UzerineYazma = false, IlkDonem = "2026-08" });

            Assert.Equal(1, sonuc.Guncellenen);
            Assert.Equal("2026-06", (await db.FirmaIsleri.SingleAsync()).IlkDonem);
        }

        [Fact]
        public async Task Kopyada_kaynagin_baslangici_tasinmaz_hedefte_secilen_ay_gecerli()
        {
            using var db = Context();
            var servis = Isler(db);
            var kaynak = await servis.IsEkleAsync(Cubic, Is("Bordro", ilkDonem: "2026-03"));

            await servis.KopyalaAsync(Dgr, new IsKopyalaDto { KaynakFirmaId = Cubic, IsIdleri = { kaynak.Id }, IlkDonem = "2026-08" });
            var kopya = await db.FirmaIsleri.SingleAsync(i => i.FirmaId == Dgr);

            Assert.Equal("2026-08", kopya.IlkDonem);
        }

        [Fact]
        public async Task Bos_ilk_donemle_kopya_eklendigi_aydan_baslar()
        {
            using var db = Context();
            var servis = Isler(db);
            var kaynak = await servis.IsEkleAsync(Cubic, Is("Bordro", ilkDonem: "2026-03"));

            await servis.KopyalaAsync(Dgr, new IsKopyalaDto { KaynakFirmaId = Cubic, IsIdleri = { kaynak.Id } });

            Assert.Null((await db.FirmaIsleri.SingleAsync(i => i.FirmaId == Dgr)).IlkDonem);
        }
    }
}
