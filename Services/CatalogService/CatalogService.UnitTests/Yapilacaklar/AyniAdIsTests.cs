using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.Yapilacaklar;
using CatalogService.Api.Features.Yapilacaklar.Controllers;
using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Features.Yapilacaklar.Services;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.Ajanlar;
using CatalogService.UnitTests.BankaEkstre;
using CatalogService.UnitTests.Muhasebe;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatalogService.UnitTests.Yapilacaklar
{
    /// <summary>
    /// Aynı firmada aynı başlık + tekrarlı özel iş: İş Takip matrisi birini gösteremez. Gizlenen iş firma
    /// satırında listelenir (sayımlar değişmez), ekleme/güncelleme 409 + ısrar ile uyarır. Sabit tarih 28.09.2026.
    /// </summary>
    public class AyniAdIsTests
    {
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

            VergiTakvimiSeed.SeedAsync(db, new DateTime(2026, 9, 28)).GetAwaiter().GetResult();
            return db;
        }

        private static SahteSaat Saat() => new(new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero));

        private static YapilacaklarService Isler(CatalogContext db)
            => new(db, Saat(), new SabitKullanici(Ben),
                   new FirmaOlayYazici(db, new SabitKullanici(Ben), NullLogger<FirmaOlayYazici>.Instance));

        private static DonemPanosuService Pano(CatalogContext db) => new(db, Saat(), Isler(db), new SabitKullanici(Ben));

        /// <summary>Doğrudan tabloya: servis kontrolünden önce açılmış kopyaları temsil eder.</summary>
        private static FirmaIsi OzelIs(int id, int firmaId, string baslik, IsTekrari tekrar = IsTekrari.Aylik) => new()
        {
            Id = id, FirmaId = firmaId, Baslik = baslik, Tekrar = tekrar, GunKurali = IsGunKurali.DonemSonu,
            OlusturmaZamani = DateTime.SpecifyKind(new DateTime(2026, 7, 1), DateTimeKind.Utc)
        };

        private static FirmaIsiKaydetDto Dto(string baslik, IsTekrari tekrar = IsTekrari.Aylik, DateTime? tarih = null) => new()
        {
            Baslik = baslik, Tekrar = tekrar, GunKurali = IsGunKurali.DonemSonu, TekSeferTarih = tarih
        };

        // ---- 1. Gizlenen iş görünür ----

        [Fact]
        public async Task Ayni_firmada_iki_bordro_tek_kolon_digeri_uyari_listesinde()
        {
            using var db = Context();
            db.FirmaIsleri.AddRange(OzelIs(1, Dgr, "bordro"), OzelIs(3, Dgr, " BORDRO "), OzelIs(5, Cubic, "Bordro"));
            await db.SaveChangesAsync();

            var pano = await Pano(db).PanoAsync(2026, 8);

            // Firmalar arası birleştirme bilinçli: tek kolon, iki firmada da dolu.
            Assert.Single(pano.Kolonlar, k => k.KaynakTip == IsKaynagi.Ozel);
            var dgr = pano.Satirlar.Single(s => s.FirmaId == Dgr);
            var g = Assert.Single(dgr.GizlenenIsler);
            Assert.Equal(3, g.IsId);
            Assert.Equal(1, g.GosterilenIsId);
            Assert.Equal("BORDRO", g.Baslik);
            Assert.Empty(pano.Satirlar.Single(s => s.FirmaId == Cubic).GizlenenIsler);

            // Sayımlar değişmez: gizlenen iş hücre üretmez.
            Assert.Equal(pano.Satirlar.Sum(s => s.Hucreler.Count(h => h.Durum != DonemHucreDurumu.Yok)), pano.Toplam);
            Assert.DoesNotContain(pano.Isler, i => i.KaynakTip == IsKaynagi.Ozel && i.KaynakId == 3);

            // Özet de taşır (aylar boyunca tekil).
            var ozet = await Pano(db).OzetAsync();
            Assert.Equal(3, Assert.Single(ozet.Satirlar.Single(s => s.FirmaId == Dgr).GizlenenIsler).IsId);
        }

        [Fact]
        public void Farkli_tekrar_ayri_kolon_gizlenen_yok()
        {
            var firmalar = new[] { new YapilacaklarKurucu.FirmaGirdisi(Dgr, "DGR", null, null, new HashSet<string>()) };
            var pano = DonemPanosuKurucu.Kur(2026, 9, firmalar, Array.Empty<VergiTakvimi>(),
                new[] { OzelIs(1, Dgr, "bordro"), OzelIs(2, Dgr, "bordro", IsTekrari.UcAylik) }, Array.Empty<IsTamamlama>());

            Assert.Equal(2, pano.Kolonlar.Count);
            Assert.Empty(pano.Satirlar.Single().GizlenenIsler);
        }

        // ---- 2. Eklerken / güncellerken uyar ----

        [Fact]
        public async Task Yeni_ekleme_ayni_ad_409_israrla_kaydedilir()
        {
            using var db = Context();
            db.FirmaIsleri.Add(OzelIs(1, Dgr, "Bordro"));
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<YapilacaklarAyniAdException>(() => Isler(db).IsEkleAsync(Dgr, Dto("  BORDRO ")));
            Assert.Equal(1, ex.MevcutId);
            Assert.Equal("Bu firmada aynı adlı bir iş zaten var.", ex.Message);
            Assert.Equal(1, await db.FirmaIsleri.CountAsync());

            var dto = Dto("  BORDRO ");
            dto.Israr = true;
            var eklenen = await Isler(db).IsEkleAsync(Dgr, dto);
            Assert.Equal(2, await db.FirmaIsleri.CountAsync(i => i.FirmaId == Dgr));
            Assert.NotEqual(1, eklenen.Id);
        }

        [Fact]
        public async Task Controller_409_ve_mevcut_isi_doner()
        {
            using var db = Context();
            db.FirmaIsleri.Add(OzelIs(1, Dgr, "bordro"));
            await db.SaveChangesAsync();

            var sonuc = await new FirmaIsleriController(Isler(db)).Ekle(Dgr, Dto("Bordro"));

            var c = Assert.IsType<ConflictObjectResult>(sonuc.Result);
            var govde = Assert.IsType<IsAyniAdDto>(c.Value);
            Assert.Equal(1, govde.MevcutId);
            Assert.Equal("Baslik", govde.Field);
        }

        [Fact]
        public async Task Farkli_tekrar_farkli_firma_ve_prosedur_satiri_serbest()
        {
            using var db = Context();
            db.FirmaIsleri.AddRange(
                OzelIs(1, Dgr, "bordro", IsTekrari.UcAylik),
                OzelIs(2, Cubic, "bordro"),
                // Prosedür satırı (yasal) bu kontrolün dışında.
                new FirmaIsi { Id = 3, FirmaId = Dgr, Baslik = "bordro", Tekrar = IsTekrari.Aylik, YasalMukellefiyetKodu = "0015" });
            await db.SaveChangesAsync();

            await Isler(db).IsEkleAsync(Dgr, Dto("bordro"));
            Assert.Equal(3, await db.FirmaIsleri.CountAsync(i => i.FirmaId == Dgr));
        }

        [Fact]
        public async Task Tek_seferlik_yalniz_ayni_ayda_cakisir()
        {
            using var db = Context();
            db.FirmaIsleri.Add(new FirmaIsi
            {
                Id = 1, FirmaId = Dgr, Baslik = "Sayım", Tekrar = IsTekrari.TekSefer, TekSeferTarih = new DateTime(2026, 10, 5)
            });
            await db.SaveChangesAsync();

            await Isler(db).IsEkleAsync(Dgr, Dto("sayım", IsTekrari.TekSefer, new DateTime(2026, 11, 5)));
            await Assert.ThrowsAsync<YapilacaklarAyniAdException>(
                () => Isler(db).IsEkleAsync(Dgr, Dto("SAYIM", IsTekrari.TekSefer, new DateTime(2026, 10, 30))));
        }

        [Fact]
        public async Task Guncellemede_baslik_degisimi_409_degismeyen_baslik_serbest()
        {
            using var db = Context();
            db.FirmaIsleri.AddRange(OzelIs(1, Dgr, "bordro"), OzelIs(2, Dgr, "SGK"), OzelIs(3, Dgr, "bordro"));
            await db.SaveChangesAsync();

            // SGK → bordro: çakışma.
            var ex = await Assert.ThrowsAsync<YapilacaklarAyniAdException>(() => Isler(db).IsGuncelleAsync(Dgr, 2, Dto("Bordro")));
            Assert.Equal(1, ex.MevcutId);
            Assert.Equal("SGK", (await db.FirmaIsleri.AsNoTracking().SingleAsync(i => i.Id == 2)).Baslik);

            // Mevcut kopyanın başlığa dokunmayan düzenlemesi engellenmez (ör. açıklama).
            var dto = Dto("bordro");
            dto.Aciklama = "maaş";
            await Isler(db).IsGuncelleAsync(Dgr, 3, dto);

            // Tekrar değişimi de kontrol edilir: üç aylık "bordro" yok → serbest.
            await Isler(db).IsGuncelleAsync(Dgr, 3, Dto("bordro", IsTekrari.UcAylik));
            Assert.Equal(IsTekrari.UcAylik, (await db.FirmaIsleri.AsNoTracking().SingleAsync(i => i.Id == 3)).Tekrar);
        }
    }
}
