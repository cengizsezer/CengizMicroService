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
    /// İş kategorileri (Firma Panosu Aşama 2): seed, kayıt kuralları, kategorisiz iş listesi ve toplu atama.
    /// Çözüm sırası <see cref="ChecklistKartTests"/>'te (saf). Sabit tarih 28.09.2026.
    /// </summary>
    public class IsKategoriTests
    {
        private const int Dgr = 3, Cubic = 4, Lime = 5;
        private const int Ben = 16;

        private static CatalogContext Context()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            db.Firmalar.AddRange(
                new Firma { Id = Dgr, Unvan = "DGR A.Ş.", KisaAd = "DGR", VergiKimlikNo = "2951070824", Aktif = true },
                new Firma { Id = Cubic, Unvan = "CUBIC LTD.", KisaAd = "CUBIC", VergiKimlikNo = "1111111111", Aktif = true },
                new Firma { Id = Lime, Unvan = "LIME LTD.", KisaAd = "LIME", VergiKimlikNo = "2222222222", Aktif = true });
            db.FirmaSicilBilgileri.AddRange(
                new FirmaSicilBilgisi { Id = 1, FirmaId = Dgr, MukellefiyetTurleri = "0003 - MUHTASAR, 0015 - KATMA DEĞER VERGİSİ" },
                new FirmaSicilBilgisi { Id = 2, FirmaId = Cubic, MukellefiyetTurleri = "0003 - MUHTASAR" });
            db.SaveChanges();

            VergiTakvimiSeed.SeedAsync(db, new DateTime(2026, 9, 28)).GetAwaiter().GetResult();
            IsKategorisiSeed.SeedAsync(db).GetAwaiter().GetResult();
            return db;
        }

        private static IsKategoriService Kategoriler(CatalogContext db) => new(db);

        private static YapilacaklarService Isler(CatalogContext db)
            => new(db, new SahteSaat(new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero)), new SabitKullanici(Ben),
                   new FirmaOlayYazici(db, new SabitKullanici(Ben), NullLogger<FirmaOlayYazici>.Instance));

        private static int Id(CatalogContext db, string ad) => db.IsKategorileri.AsNoTracking().Single(k => k.Ad == ad).Id;

        private static FirmaIsi OzelIs(int id, int firmaId, string baslik, IsTekrari tekrar = IsTekrari.Aylik,
                                       int? kategoriId = null, int? tarifId = null) => new()
        {
            Id = id, FirmaId = firmaId, Baslik = baslik, Tekrar = tekrar, GunKurali = IsGunKurali.DonemSonu,
            KategoriId = kategoriId, IsTarifiId = tarifId,
            OlusturmaZamani = DateTime.SpecifyKind(new DateTime(2026, 7, 1), DateTimeKind.Utc)
        };

        // ---- Seed ----

        [Fact]
        public async Task Seed_dort_kategori_sirasiyla_yalniz_tablo_bosken()
        {
            using var db = Context();   // seed bir kez çalıştı

            Assert.Equal(new[] { "Banka & Cari", "Kayıt & Kapanış", "Bordro", "Beyanname" },
                         (await Kategoriler(db).ListeAsync()).Select(k => k.Ad));
            Assert.True((await Kategoriler(db).ListeAsync()).Single(k => k.Ad == "Beyanname").YasalVarsayilan);

            // Kullanıcı birini silerse ikinci açılış geri getirmez.
            await Kategoriler(db).SilAsync(Id(db, "Bordro"));
            Assert.Equal(0, await IsKategorisiSeed.SeedAsync(db));
            Assert.Equal(3, await db.IsKategorileri.CountAsync());
        }

        // ---- Kategori kayıt kuralları ----

        [Fact]
        public async Task Ekleme_sona_dizer_ayni_ad_ve_Diger_reddedilir()
        {
            using var db = Context();
            var k = await Kategoriler(db).EkleAsync(new IsKategorisiKaydetDto { Ad = "  Sgk " });
            Assert.Equal("Sgk", k.Ad);
            Assert.Equal(5, k.Sira);

            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => Kategoriler(db).EkleAsync(new() { Ad = "bordro" }));
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => Kategoriler(db).EkleAsync(new() { Ad = "DİĞER" }));
        }

        [Fact]
        public async Task Siralama_yeni_siraya_gore()
        {
            using var db = Context();
            var idler = (await Kategoriler(db).ListeAsync()).Select(k => k.Id).Reverse().ToList();

            var liste = await Kategoriler(db).SiralaAsync(idler);

            Assert.Equal(new[] { "Beyanname", "Bordro", "Kayıt & Kapanış", "Banka & Cari" }, liste.Select(k => k.Ad));
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => Kategoriler(db).SiralaAsync(idler.Take(2).ToList()));
        }

        [Fact]
        public async Task Kullanilan_kategori_silinmez_pasife_alinabilir_pasif_atanamaz()
        {
            using var db = Context();
            var bordro = Id(db, "Bordro");
            db.FirmaIsleri.Add(OzelIs(1, Dgr, "bordro", kategoriId: bordro));
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<YapilacaklarKuralException>(() => Kategoriler(db).SilAsync(bordro));
            Assert.Contains("pasife alın", ex.Message);

            await Kategoriler(db).GuncelleAsync(bordro, new IsKategorisiKaydetDto { Ad = "Bordro", Aktif = false });

            // Pasif kategori yeni işe, eşlemeye, toplu atamaya verilemez…
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => Isler(db).IsEkleAsync(Dgr, new FirmaIsiKaydetDto
            {
                Baslik = "SGK", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.DonemSonu, KategoriId = bordro
            }));
            await Assert.ThrowsAsync<YapilacaklarKuralException>(
                () => Kategoriler(db).MukellefiyetKaydetAsync("0015", new MukellefiyetEslemeKaydetDto { KategoriId = bordro }));

            // …ama kaydında zaten olan iş, kategoriye dokunmadan güncellenebilir.
            await Isler(db).IsGuncelleAsync(Dgr, 1, new FirmaIsiKaydetDto
            {
                Baslik = "bordro", Aciklama = "maaş", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.DonemSonu, KategoriId = bordro
            });
            Assert.Equal(bordro, (await db.FirmaIsleri.AsNoTracking().SingleAsync(i => i.Id == 1)).KategoriId);
        }

        // ---- Yasal varsayılan ----

        private static async Task<string> YasalBolumAsync(CatalogContext db)
            => (await Isler(db).FirmaPanosuAsync(new FirmaPanosuFiltresi { FirmaId = Dgr }))
                .Donemler.SelectMany(d => d.Kartlar).SelectMany(k => k.Bolumler)
                .Where(b => b.Maddeler.Any(m => m.Satir.KaynakTip == IsKaynagi.Yasal))
                .Select(b => b.Ad).Distinct().Single();

        [Fact]
        public async Task Yasal_varsayilan_seedde_Beyanname_ad_degisince_yasal_isler_yerinde_kalir()
        {
            using var db = Context();
            var beyanname = Id(db, "Beyanname");
            Assert.Equal(beyanname, Assert.Single(await Kategoriler(db).ListeAsync(), k => k.YasalVarsayilan).Id);
            Assert.Equal("Beyanname", await YasalBolumAsync(db));

            await Kategoriler(db).GuncelleAsync(beyanname, new IsKategorisiKaydetDto { Ad = "Vergi beyanları", Aktif = true });

            Assert.Equal("Vergi beyanları", await YasalBolumAsync(db));
            Assert.True((await db.IsKategorileri.AsNoTracking().SingleAsync(k => k.Id == beyanname)).VarsayilanYasal);
        }

        [Fact]
        public async Task Yasal_varsayilan_tasinir_eskisinden_kaldirilir_pasife_tasinmaz()
        {
            using var db = Context();
            var beyanname = Id(db, "Beyanname");
            var bordro = Id(db, "Bordro");

            var liste = await Kategoriler(db).YasalVarsayilanYapAsync(bordro);

            Assert.Equal(bordro, Assert.Single(liste, k => k.YasalVarsayilan).Id);
            Assert.False((await db.IsKategorileri.AsNoTracking().SingleAsync(k => k.Id == beyanname)).VarsayilanYasal);
            Assert.Equal("Bordro", await YasalBolumAsync(db));

            // Eski varsayılan artık sıradan kategori: pasife alınabilir, kullanılmıyorsa silinir.
            await Kategoriler(db).GuncelleAsync(beyanname, new IsKategorisiKaydetDto { Ad = "Beyanname", Aktif = false });
            await Kategoriler(db).SilAsync(beyanname);

            // Pasif kategori varsayılan yapılamaz.
            var banka = Id(db, "Banka & Cari");
            await Kategoriler(db).GuncelleAsync(banka, new IsKategorisiKaydetDto { Ad = "Banka & Cari", Aktif = false });
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => Kategoriler(db).YasalVarsayilanYapAsync(banka));
            Assert.Equal(bordro, Assert.Single(await Kategoriler(db).ListeAsync(), k => k.YasalVarsayilan).Id);
        }

        [Fact]
        public async Task Yasal_varsayilan_pasife_alinamaz_silinemez()
        {
            using var db = Context();
            var beyanname = Id(db, "Beyanname");

            var ex = await Assert.ThrowsAsync<YapilacaklarKuralException>(() =>
                Kategoriler(db).GuncelleAsync(beyanname, new IsKategorisiKaydetDto { Ad = "Beyanname", Aktif = false }));
            Assert.Contains("pasife alınamaz", ex.Message);
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => Kategoriler(db).SilAsync(beyanname));

            var k = await db.IsKategorileri.AsNoTracking().SingleAsync(x => x.Id == beyanname);
            Assert.True(k.Aktif);
            Assert.True(k.VarsayilanYasal);
        }

        [Fact]
        public async Task Yeni_kategori_yasal_varsayilan_olmaz()
        {
            using var db = Context();
            var k = await Kategoriler(db).EkleAsync(new IsKategorisiKaydetDto { Ad = "Beyanname 2" });
            Assert.False(k.YasalVarsayilan);
            Assert.Single(await Kategoriler(db).ListeAsync(), x => x.YasalVarsayilan);
        }

        // ---- Yasal eşleme ----

        [Fact]
        public async Task Mukellefiyet_eslemesi_takvimdeki_kodlar_bos_birakma_eslemeyi_kaldirir()
        {
            using var db = Context();
            var bordro = Id(db, "Bordro");

            var liste = await Kategoriler(db).MukellefiyetlerAsync();
            Assert.Contains(liste, e => e.MukellefiyetKodu == "0015" && e.KategoriId == null);

            var e = await Kategoriler(db).MukellefiyetKaydetAsync("0003", new MukellefiyetEslemeKaydetDto { KategoriId = bordro });
            Assert.Equal(bordro, e.KategoriId);
            Assert.Equal(1, await db.MukellefiyetKategorileri.CountAsync());

            await Kategoriler(db).MukellefiyetKaydetAsync("0003", new MukellefiyetEslemeKaydetDto { KategoriId = null });
            Assert.Equal(0, await db.MukellefiyetKategorileri.CountAsync());

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => Kategoriler(db).MukellefiyetKaydetAsync("9999", new MukellefiyetEslemeKaydetDto { KategoriId = bordro }));
        }

        // ---- Kategorisiz işler ve toplu atama ----

        private static async Task<CatalogContext> IslerleAsync()
        {
            var db = Context();
            var banka = Id(db, "Banka & Cari");
            var tarif = new IsTarifi { Id = 50, Ad = "Fatura girişi", KategoriId = Id(db, "Kayıt & Kapanış") };
            db.IsTarifleri.Add(tarif);
            db.FirmaIsleri.AddRange(
                OzelIs(1, Dgr, "bordro"),
                OzelIs(2, Cubic, "BORDRO "),
                OzelIs(3, Lime, "Bordro", kategoriId: banka),                 // kendi kategorisi var → listede yok
                OzelIs(4, Dgr, "bordro", IsTekrari.Yillik),                   // farklı tekrar → ayrı grup
                OzelIs(5, Dgr, "Fatura girişi", tarifId: 50),                 // tariften kategori → listede yok
                OzelIs(6, Cubic, "Fatura girişi"),
                // Prosedür satırı (yasal) — kategorisiz iş değildir.
                new FirmaIsi { Id = 7, FirmaId = Dgr, Baslik = "bordro", Tekrar = IsTekrari.Aylik, YasalMukellefiyetKodu = "0003" });
            await db.SaveChangesAsync();
            return db;
        }

        [Fact]
        public async Task Kategorisiz_isler_baslik_tekrar_gruplu_firma_sayisiyla_prosedur_haric()
        {
            using var db = await IslerleAsync();

            var gruplar = await Kategoriler(db).KategorisizAsync();

            Assert.Equal(3, gruplar.Count);
            var bordro = gruplar.Single(g => g.Tekrar == IsTekrari.Aylik && g.Baslik.ToUpperInvariant() == "BORDRO");
            Assert.Equal(2, bordro.FirmaSayisi);   // DGR + CUBIC; LIME'nin kategorisi var, prosedür satırı sayılmaz
            Assert.Equal(2, bordro.IsSayisi);
            Assert.Equal("O|BORDRO|2", bordro.Anahtar);
            Assert.Single(gruplar, g => g.Tekrar == IsTekrari.Yillik);
            Assert.Equal(1, gruplar.Single(g => g.Baslik == "Fatura girişi").IsSayisi);   // tariften gelen hariç

            // Pasif kategoriye bağlı iş de kategorisiz sayılır (çözüm Diğer).
            var banka = Id(db, "Banka & Cari");
            await Kategoriler(db).GuncelleAsync(banka, new IsKategorisiKaydetDto { Ad = "Banka & Cari", Aktif = false });
            Assert.Equal(3, (await Kategoriler(db).KategorisizAsync()).Single(g => g.Anahtar == "O|BORDRO|2").IsSayisi);
        }

        [Fact]
        public async Task Toplu_atama_yalniz_secili_grubun_kategorisiz_islerine()
        {
            using var db = await IslerleAsync();
            var bordro = Id(db, "Bordro");
            var banka = Id(db, "Banka & Cari");

            var sonuc = await Kategoriler(db).TopluAtaAsync(new TopluKategoriAtaDto { Anahtar = "O|BORDRO|2", KategoriId = bordro });

            Assert.Equal(2, sonuc.Guncellenen);
            var isler = await db.FirmaIsleri.AsNoTracking().ToDictionaryAsync(i => i.Id);
            Assert.Equal(bordro, isler[1].KategoriId);
            Assert.Equal(bordro, isler[2].KategoriId);
            Assert.Equal(banka, isler[3].KategoriId);   // kendi kategorisi olan değişmez
            Assert.Null(isler[4].KategoriId);           // farklı tekrar
            Assert.Null(isler[6].KategoriId);           // başka grup
            Assert.Null(isler[7].KategoriId);           // prosedür satırı

            Assert.DoesNotContain(await Kategoriler(db).KategorisizAsync(), g => g.Anahtar == "O|BORDRO|2");
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => Kategoriler(db).TopluAtaAsync(new TopluKategoriAtaDto { Anahtar = "O|BORDRO|2", KategoriId = bordro }));
        }

        [Fact]
        public async Task Pano_kartlari_kategoriyle_bolumlenir()
        {
            using var db = await IslerleAsync();
            await Kategoriler(db).TopluAtaAsync(new TopluKategoriAtaDto { Anahtar = "O|BORDRO|2", KategoriId = Id(db, "Bordro") });

            var pano = await Isler(db).FirmaPanosuAsync(new FirmaPanosuFiltresi { FirmaId = Dgr });

            var bolumler = pano.Donemler.SelectMany(d => d.Kartlar).SelectMany(k => k.Bolumler).ToList();
            Assert.Contains(bolumler, b => b.Ad == "Bordro");
            Assert.Contains(bolumler, b => b.Ad == "Kayıt & Kapanış");
            Assert.Contains(bolumler, b => b.Ad == "Beyanname");
            Assert.Contains(bolumler, b => b.Anahtar == IsKategoriCozucu.DigerAnahtari);   // yıllık bordro: kategorisiz
            Assert.All(bolumler.Where(b => b.Maddeler.Any(m => m.Satir.Baslik == "bordro" && m.Satir.Tekrar == IsTekrari.Aylik)),
                       b => Assert.Equal("Bordro", b.Ad));
            Assert.All(bolumler.Where(b => b.Maddeler.Any(m => m.Satir.Baslik == "Fatura girişi")),
                       b => Assert.Equal("Kayıt & Kapanış", b.Ad));
            Assert.All(bolumler.Where(b => b.Maddeler.Any(m => m.Satir.KaynakTip == IsKaynagi.Yasal)),
                       b => Assert.Equal("Beyanname", b.Ad));
        }
    }
}
