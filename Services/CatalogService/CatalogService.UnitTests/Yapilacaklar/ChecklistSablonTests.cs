using CatalogService.Api.Features.Anasayfa.Services;
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
    /// Checklist şablonları (Firma Panosu Aşama 3): şablon = iş tanımı paketi; uygulanınca firmaya özel iş yazar.
    /// Önizleme ve yazma aynı plandan (ChecklistSablonPlanlayici). Sabit tarih 13.10.2026.
    /// </summary>
    public class ChecklistSablonTests
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
            db.SaveChanges();
            IsKategorisiSeed.SeedAsync(db).GetAwaiter().GetResult();
            return db;
        }

        private static readonly DateTimeOffset Simdi = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);

        private static ChecklistSablonService Sablonlar(CatalogContext db)
            => new(db, new SahteSaat(Simdi), new SabitKullanici(Ben),
                   new FirmaOlayYazici(db, new SabitKullanici(Ben), NullLogger<FirmaOlayYazici>.Instance));

        private static YapilacaklarService Isler(CatalogContext db)
            => new(db, new SahteSaat(Simdi), new SabitKullanici(Ben),
                   new FirmaOlayYazici(db, new SabitKullanici(Ben), NullLogger<FirmaOlayYazici>.Instance));

        private static int Kat(CatalogContext db, string ad) => db.IsKategorileri.AsNoTracking().Single(k => k.Ad == ad).Id;

        private static ChecklistSablonMaddesiDto Madde(string baslik, IsTekrari tekrar = IsTekrari.Aylik, int? kategoriId = null,
                                                       IsGunKurali kural = IsGunKurali.DonemSonu, int? gun = null, int? tarifId = null)
            => new() { Baslik = baslik, Tekrar = tekrar, GunKurali = kural, AyinGunu = gun, KategoriId = kategoriId, IsTarifiId = tarifId };

        /// <summary>"Ay sonu kapanış": banka, bordro (ayın 25'i), KDV hazırlık, geçici vergi (üç aylık).</summary>
        private static async Task<ChecklistSablonuDto> Kapanis(CatalogContext db)
            => await Sablonlar(db).EkleAsync(new ChecklistSablonuKaydetDto
            {
                Ad = "Ay sonu kapanış",
                Aciklama = "Her ay tekrarlanan kapanış işleri",
                Maddeler =
                {
                    Madde("Banka mutabakatı", kategoriId: Kat(db, "Banka & Cari")),
                    Madde("Bordro", kategoriId: Kat(db, "Bordro"), kural: IsGunKurali.AyinGunu, gun: 25),
                    Madde("KDV hazırlık", kategoriId: Kat(db, "Kayıt & Kapanış")),
                    Madde("Geçici vergi hazırlık", IsTekrari.UcAylik)
                }
            });

        private static SablondanIstekDto Istek(int sablonId, string? ilkDonem, params int[] firmalar)
            => new() { SablonId = sablonId, FirmaIdleri = firmalar.ToList(), IlkDonem = ilkDonem };

        // ---- Önizleme = gerçek yazma ----

        [Fact]
        public async Task Onizleme_gercek_yazmayla_ayni_ve_yazmaz()
        {
            using var db = Context();
            var sablon = await Kapanis(db);
            await Isler(db).IsEkleAsync(Lime, new FirmaIsiKaydetDto { Baslik = " bordro ", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AySonu });

            var onizleme = await Sablonlar(db).SablondanOnizleAsync(Istek(sablon.Id, "2026-10", Lime, Cubic));
            Assert.Equal(1, await db.FirmaIsleri.CountAsync());   // önizleme yazmaz
            Assert.False(onizleme.Yazildi);

            var sonuc = await Sablonlar(db).SablondanOlusturAsync(Istek(sablon.Id, "2026-10", Lime, Cubic));
            Assert.True(sonuc.Yazildi);

            Assert.Equal(
                onizleme.Firmalar.Select(f => (f.FirmaId, string.Join("|", f.Eklenenler), string.Join("|", f.Atlananlar.Select(a => a.Baslik)))),
                sonuc.Firmalar.Select(f => (f.FirmaId, string.Join("|", f.Eklenenler), string.Join("|", f.Atlananlar.Select(a => a.Baslik)))));
            Assert.Equal(onizleme.Kategoriler.Select(k => (k.Ad, k.Sayi)), sonuc.Kategoriler.Select(k => (k.Ad, k.Sayi)));

            // Yazılanlar önizlemenin söylediği kadar.
            foreach (var f in sonuc.Firmalar)
                Assert.Equal(f.Eklenenler.Count, await db.FirmaIsleri.CountAsync(i => i.FirmaId == f.FirmaId && i.SablonId == sablon.Id));

            // Kategori bölümleri şablon sırasına değil kategori sırasına göre, "Diğer" en sonda.
            Assert.Equal(new[] { ("Banka & Cari", 1), ("Kayıt & Kapanış", 1), ("Bordro", 1), ("Diğer", 1) },
                         onizleme.Kategoriler.Select(k => (k.Ad, k.Sayi)));
        }

        // ---- Atlama kuralı ----

        [Fact]
        public async Task Ayni_baslik_ve_tekrar_atlanir_farkli_tekrar_eklenir_pasif_is_de_sayilir()
        {
            using var db = Context();
            var sablon = await Kapanis(db);
            var isler = Isler(db);
            await isler.IsEkleAsync(Lime, new FirmaIsiKaydetDto { Baslik = "BORDRO", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AySonu });
            await isler.IsEkleAsync(Lime, new FirmaIsiKaydetDto { Baslik = "KDV hazırlık", Tekrar = IsTekrari.Yillik, GunKurali = IsGunKurali.DonemSonu });
            await isler.IsEkleAsync(Lime, new FirmaIsiKaydetDto { Baslik = "Banka mutabakatı", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AySonu, Aktif = false });

            var sonuc = await Sablonlar(db).SablondanOlusturAsync(Istek(sablon.Id, null, Lime));

            var lime = Assert.Single(sonuc.Firmalar);
            Assert.Equal(new[] { "Banka mutabakatı", "Bordro" }, lime.Atlananlar.Select(a => a.Baslik));
            Assert.All(lime.Atlananlar, a => Assert.Equal(ChecklistSablonPlanlayici.AyniIsSebebi, a.Sebep));
            Assert.Equal(new[] { "KDV hazırlık", "Geçici vergi hazırlık" }, lime.Eklenenler);   // yıllık KDV ≠ aylık

            // Israr yok: atlanan madde ikinci iş olarak yazılmadı.
            Assert.Equal(1, await db.FirmaIsleri.CountAsync(i => i.FirmaId == Lime && i.Baslik.ToUpper() == "BORDRO"));

            // Aynı şablonu ikinci kez uygulamak hiçbir şey eklemez.
            var tekrar = await Sablonlar(db).SablondanOlusturAsync(Istek(sablon.Id, null, Lime));
            Assert.Empty(tekrar.Firmalar.Single().Eklenenler);
            Assert.Equal(4, tekrar.Firmalar.Single().Atlananlar.Count);
        }

        // ---- Çoklu firma ----

        [Fact]
        public async Task Coklu_firma_her_firma_kendi_mevcuduna_gore_ve_isler_dogru_kurulur()
        {
            using var db = Context();
            var sablon = await Kapanis(db);
            await Isler(db).IsEkleAsync(Dgr, new FirmaIsiKaydetDto { Baslik = "Bordro", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AySonu });

            var sonuc = await Sablonlar(db).SablondanOlusturAsync(Istek(sablon.Id, "2026-10", Lime, Dgr, Cubic, Lime));

            // Firma adına göre sıralı, tekrarlanan Id bir kez.
            Assert.Equal(new[] { "CUBIC", "DGR", "LIME" }, sonuc.Firmalar.Select(f => f.FirmaAdi));
            Assert.Equal(new[] { 4, 3, 4 }, sonuc.Firmalar.Select(f => f.Eklenenler.Count));
            Assert.Equal("Bordro", sonuc.Firmalar.Single(f => f.FirmaId == Dgr).Atlananlar.Single().Baslik);

            var bordro = await db.FirmaIsleri.SingleAsync(i => i.FirmaId == Cubic && i.Baslik == "Bordro");
            Assert.Equal((IsGunKurali.AyinGunu, (int?)25, (int?)Kat(db, "Bordro"), (int?)sablon.Id),
                         (bordro.GunKurali, bordro.AyinGunu, bordro.KategoriId, bordro.SablonId));
            Assert.Null(bordro.SorumluKullaniciId);   // firmanın sorumlusu
            Assert.True(bordro.Aktif);
            Assert.Equal(Ben.ToString(), bordro.OlusturanKullaniciId);

            // Olay kaydı yalnız iş eklenen firmalara.
            Assert.Equal(3, await db.FirmaOlayKayitlari.CountAsync(o => o.Aciklama.StartsWith("Şablondan")));
        }

        [Fact]
        public async Task Kartta_maddelerin_sablonu_etiket_olur_sablonsuz_kartta_etiket_yok()
        {
            using var db = Context();
            var sablon = await Kapanis(db);
            await Sablonlar(db).SablondanOlusturAsync(Istek(sablon.Id, "2026-09", Lime));
            await Isler(db).IsEkleAsync(Cubic, new FirmaIsiKaydetDto { Baslik = "Rapor", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AySonu, IlkDonem = "2026-09" });

            var pano = await Isler(db).FirmaPanosuAsync(new FirmaPanosuFiltresi());
            var kartlar = pano.Donemler.SelectMany(d => d.Kartlar).ToList();

            Assert.All(kartlar.Where(k => k.FirmaId == Lime), k => Assert.Equal(new[] { "Ay sonu kapanış" }, k.Sablonlar));
            Assert.All(kartlar.Where(k => k.FirmaId == Cubic), k => Assert.Empty(k.Sablonlar));
        }

        // ---- Başlangıç dönemi ----

        [Fact]
        public async Task IlkDonem_hepsine_yazilir_eklenme_ayina_esitse_bos_gecersiz_reddedilir()
        {
            using var db = Context();
            var sablon = await Kapanis(db);

            await Sablonlar(db).SablondanOlusturAsync(Istek(sablon.Id, "2026-09", Lime));
            Assert.All(await db.FirmaIsleri.Where(i => i.FirmaId == Lime).ToListAsync(), i => Assert.Equal("2026-09", i.IlkDonem));

            // Ekim'de eklenip Ekim'den başlayan: eski kural (null) — "…'dan beri" notu çıkmasın.
            await Sablonlar(db).SablondanOlusturAsync(Istek(sablon.Id, "2026-10", Cubic));
            Assert.All(await db.FirmaIsleri.Where(i => i.FirmaId == Cubic).ToListAsync(), i => Assert.Null(i.IlkDonem));

            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => Sablonlar(db).SablondanOnizleAsync(Istek(sablon.Id, "2026-13", Dgr)));
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => Sablonlar(db).SablondanOnizleAsync(Istek(sablon.Id, null)));
        }

        // ---- Pasif kategori ----

        [Fact]
        public async Task Pasif_kategori_tasinmaz_is_kategorisiz_yazilir()
        {
            using var db = Context();
            var sablon = await Kapanis(db);
            var bordroKat = Kat(db, "Bordro");
            await new IsKategoriService(db).GuncelleAsync(bordroKat, new IsKategorisiKaydetDto { Ad = "Bordro", Aktif = false });

            var onizleme = await Sablonlar(db).SablondanOnizleAsync(Istek(sablon.Id, null, Lime));
            Assert.DoesNotContain(onizleme.Kategoriler, k => k.Ad == "Bordro");
            Assert.Equal(2, onizleme.Kategoriler.Single(k => k.Ad == "Diğer").Sayi);

            await Sablonlar(db).SablondanOlusturAsync(Istek(sablon.Id, null, Lime));
            var bordro = await db.FirmaIsleri.SingleAsync(i => i.FirmaId == Lime && i.Baslik == "Bordro");
            Assert.Null(bordro.KategoriId);
            Assert.NotNull((await db.FirmaIsleri.SingleAsync(i => i.FirmaId == Lime && i.Baslik == "Banka mutabakatı")).KategoriId);

            // Şablonun kendisi pasif kategoriyi korur (kayıt yeniden kaydedilebilir), yeni madde pasif kategori alamaz.
            var kayitli = await Sablonlar(db).GetirAsync(sablon.Id);
            Assert.True(kayitli.Maddeler.Single(m => m.Baslik == "Bordro").KategoriPasif);
            await Sablonlar(db).GuncelleAsync(sablon.Id, new ChecklistSablonuKaydetDto { Ad = kayitli.Ad, Maddeler = kayitli.Maddeler });
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => Sablonlar(db).EkleAsync(new ChecklistSablonuKaydetDto
            {
                Ad = "Yeni", Maddeler = { Madde("SGK", kategoriId: bordroKat) }
            }));
        }

        // ---- Firmadan şablon ----

        [Fact]
        public async Task Firmadan_sablon_baslik_tekrar_gun_kategori_tarif_kopyalanir_prosedur_ve_firmaya_ozgu_alanlar_kopyalanmaz()
        {
            using var db = Context();
            var tarif = new IsTarifi { Ad = "Bordro", NasilYapilir = "Puantaj" };
            db.IsTarifleri.Add(tarif);
            await db.SaveChangesAsync();

            var isler = Isler(db);
            var bordro = await isler.IsEkleAsync(Dgr, new FirmaIsiKaydetDto
            {
                Baslik = "Bordro", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AyinGunu, AyinGunu = 25,
                KategoriId = Kat(db, "Bordro"), SorumluKullaniciId = 99, SorumluKullaniciAdi = "zeynep",
                Alicilar = new() { new() { AdSoyad = "Jan", Eposta = "jan@x.nl", AliciTipi = AliciTipi.Kime } }
            });
            (await db.FirmaIsleri.SingleAsync(i => i.Id == bordro.Id)).IsTarifiId = tarif.Id;
            await db.SaveChangesAsync();
            await isler.IsEkleAsync(Dgr, new FirmaIsiKaydetDto { Baslik = "Yıllık rapor", Tekrar = IsTekrari.Yillik, GunKurali = IsGunKurali.DonemSonu });
            await isler.IsEkleAsync(Dgr, new FirmaIsiKaydetDto { Baslik = "Açılış", Tekrar = IsTekrari.TekSefer, TekSeferTarih = new DateTime(2026, 11, 2) });
            await isler.IsEkleAsync(Dgr, new FirmaIsiKaydetDto { Baslik = "Eski iş", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AySonu, Aktif = false });
            // Yasal prosedür satırı (0015 KDV) — özel iş değil.
            db.FirmaIsleri.Add(new FirmaIsi { FirmaId = Dgr, Baslik = "KDV", YasalMukellefiyetKodu = "0015", Tekrar = IsTekrari.Aylik });
            await db.SaveChangesAsync();

            var onizleme = await Sablonlar(db).FirmadanOnizleAsync(Dgr);
            Assert.Equal(new[] { "Bordro", "Yıllık rapor" }, onizleme.Maddeler.Select(m => m.Baslik));
            Assert.Equal(new[] { "Açılış", "Eski iş" }, onizleme.Disarida.Select(d => d.Baslik));   // prosedür satırı hiç listelenmez

            var sablon = await Sablonlar(db).FirmadanOlusturAsync(new FirmadanSablonDto { FirmaId = Dgr, Ad = "DGR paketi" });
            Assert.Equal(onizleme.Maddeler.Select(m => (m.Baslik, m.Tekrar, m.GunKurali, m.AyinGunu, m.KategoriId, m.IsTarifiId)),
                         sablon.Maddeler.Select(m => (m.Baslik, m.Tekrar, m.GunKurali, m.AyinGunu, m.KategoriId, m.IsTarifiId)));
            var m = sablon.Maddeler[0];
            Assert.Equal((IsGunKurali.AyinGunu, (int?)25, (int?)Kat(db, "Bordro"), (int?)tarif.Id), (m.GunKurali, m.AyinGunu, m.KategoriId, m.IsTarifiId));

            // Şablondan oluşan iş: sorumlu, alıcı, ek, tamamlama yok.
            await Sablonlar(db).SablondanOlusturAsync(Istek(sablon.Id, null, Lime));
            var kopya = await db.FirmaIsleri.Include(i => i.Alicilar).Include(i => i.Ekler).SingleAsync(i => i.FirmaId == Lime && i.Baslik == "Bordro");
            Assert.Null(kopya.SorumluKullaniciId);
            Assert.Empty(kopya.Alicilar);
            Assert.Empty(kopya.Ekler);
            Assert.Equal(tarif.Id, kopya.IsTarifiId);
            Assert.Equal(0, await db.IsTamamlamalari.CountAsync(t => t.FirmaId == Lime));
            Assert.Equal(0, await db.FirmaIsleri.CountAsync(i => i.FirmaId == Lime && i.YasalMukellefiyetKodu != null));
        }

        [Fact]
        public async Task Ozel_isi_olmayan_firmadan_sablon_olmaz()
        {
            using var db = Context();
            db.FirmaIsleri.Add(new FirmaIsi { FirmaId = Dgr, Baslik = "KDV", YasalMukellefiyetKodu = "0015", Tekrar = IsTekrari.Aylik });
            await db.SaveChangesAsync();

            Assert.Empty((await Sablonlar(db).FirmadanOnizleAsync(Dgr)).Maddeler);
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() =>
                Sablonlar(db).FirmadanOlusturAsync(new FirmadanSablonDto { FirmaId = Dgr, Ad = "Boş" }));
            Assert.Equal(0, await db.ChecklistSablonlari.CountAsync());
        }

        // ---- CRUD ----

        [Fact]
        public async Task Kullanilmis_sablon_silinmez_pasife_alinir_kullanilmamis_silinir()
        {
            using var db = Context();
            var kullanilan = await Kapanis(db);
            var bos = await Sablonlar(db).EkleAsync(new ChecklistSablonuKaydetDto { Ad = "Deneme", Maddeler = { Madde("X") } });
            await Sablonlar(db).SablondanOlusturAsync(Istek(kullanilan.Id, null, Lime));

            var s1 = await Sablonlar(db).SilAsync(kullanilan.Id);
            Assert.True(s1.PasifeAlindi);
            Assert.False(s1.Silindi);
            Assert.False((await db.ChecklistSablonlari.SingleAsync(s => s.Id == kullanilan.Id)).Aktif);
            Assert.Equal(4, await db.FirmaIsleri.CountAsync(i => i.SablonId == kullanilan.Id));   // işler yerinde
            Assert.Equal(4, (await Sablonlar(db).GetirAsync(kullanilan.Id)).KullanimSayisi);

            // Pasif şablondan iş oluşturulmaz.
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => Sablonlar(db).SablondanOnizleAsync(Istek(kullanilan.Id, null, Cubic)));

            var s2 = await Sablonlar(db).SilAsync(bos.Id);
            Assert.True(s2.Silindi);
            Assert.False(await db.ChecklistSablonlari.AnyAsync(s => s.Id == bos.Id));
            Assert.Equal(0, await db.ChecklistSablonMaddeleri.CountAsync(m => m.SablonId == bos.Id));
        }

        [Fact]
        public async Task Sablonu_degistirmek_olusmus_isleri_degistirmez()
        {
            using var db = Context();
            var sablon = await Kapanis(db);
            await Sablonlar(db).SablondanOlusturAsync(Istek(sablon.Id, null, Lime));
            var once = await db.FirmaIsleri.AsNoTracking().Where(i => i.FirmaId == Lime).OrderBy(i => i.Id)
                .Select(i => new { i.Baslik, i.GunKurali, i.AyinGunu, i.KategoriId }).ToListAsync();

            await Sablonlar(db).GuncelleAsync(sablon.Id, new ChecklistSablonuKaydetDto
            {
                Ad = "Ay sonu kapanış v2",
                Maddeler = { Madde("Bordro", kural: IsGunKurali.AyinGunu, gun: 15), Madde("Yeni madde") }
            });

            var sonra = await db.FirmaIsleri.AsNoTracking().Where(i => i.FirmaId == Lime).OrderBy(i => i.Id)
                .Select(i => new { i.Baslik, i.GunKurali, i.AyinGunu, i.KategoriId }).ToListAsync();
            Assert.Equal(once, sonra);

            var guncel = await Sablonlar(db).GetirAsync(sablon.Id);
            Assert.Equal(new[] { "Bordro", "Yeni madde" }, guncel.Maddeler.Select(m => m.Baslik));
            Assert.Equal(2, await db.ChecklistSablonMaddeleri.CountAsync());
        }

        [Fact]
        public async Task Kayit_kurallari_ad_tekil_bos_madde_tek_seferlik_ve_cift_madde_reddedilir_sira_korunur()
        {
            using var db = Context();
            var s = Sablonlar(db);
            await Kapanis(db);

            async Task Red(ChecklistSablonuKaydetDto dto) => await Assert.ThrowsAsync<YapilacaklarKuralException>(() => s.EkleAsync(dto));

            await Red(new() { Ad = " ay SONU kapanış ", Maddeler = { Madde("X") } });
            await Red(new() { Ad = "Boş", Maddeler = { } });
            await Red(new() { Ad = "Tek", Maddeler = { Madde("Açılış", IsTekrari.TekSefer) } });
            await Red(new() { Ad = "Çift", Maddeler = { Madde("Bordro"), Madde(" BORDRO ") } });
            await Red(new() { Ad = "Gün", Maddeler = { Madde("Bordro", kural: IsGunKurali.AyinGunu, gun: 32) } });

            var sirali = await s.EkleAsync(new() { Ad = "Sıra", Maddeler = { Madde("C"), Madde("A"), Madde("B"), Madde("A", IsTekrari.Yillik) } });
            Assert.Equal(new[] { "C", "A", "B", "A" }, sirali.Maddeler.Select(m => m.Baslik));
        }

        [Fact]
        public async Task Sablonda_kullanilan_kategori_silinmez()
        {
            using var db = Context();
            await Kapanis(db);
            var kategoriler = new IsKategoriService(db);

            Assert.Equal(1, (await kategoriler.ListeAsync()).Single(k => k.Ad == "Bordro").SablonMaddeSayisi);
            var ex = await Assert.ThrowsAsync<YapilacaklarKuralException>(() => kategoriler.SilAsync(Kat(db, "Bordro")));
            Assert.Contains("şablon maddesi", ex.Message);
        }
    }
}
