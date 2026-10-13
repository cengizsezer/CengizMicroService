using System.Reflection;
using System.Text.Json;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.Parolalar.Domain;
using CatalogService.Api.Features.Parolalar.Dtos;
using CatalogService.Api.Features.Parolalar.Services;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.Ajanlar;
using CatalogService.UnitTests.BankaEkstre;
using CatalogService.UnitTests.Muhasebe;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.Parolalar
{
    /// <summary>
    /// Yönetim › Parolalar: şifreler veritabanında şifreli; liste şifre döndürmez; tek alan ucu her çağrıda erişim kaydı
    /// yazar (şifre metni olmadan); düzenlemede boş şifre değişmez; SGK firma + şube tekil; pasif firmanın kaydı kalır.
    /// </summary>
    public class ParolalarTests
    {
        private const int Lime = 5, Dgr = 3, Eski = 9;
        private const string GizliParola = "Gizli-Parola-7781";
        private const string GizliSifre = "Gizli-Sifre-4402";

        private static readonly DateTimeOffset Simdi = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);

        private static CatalogContext Context()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            db.Firmalar.AddRange(
                new Firma { Id = Lime, Unvan = "LIME TEKSTİL LTD.", KisaAd = "LIME", VergiKimlikNo = "2222222222", Aktif = true },
                new Firma { Id = Dgr, Unvan = "DGR İÇ VE DIŞ TİC.", KisaAd = "", VergiKimlikNo = "2951070824", Aktif = true },
                new Firma { Id = Eski, Unvan = "ESKİ MÜŞTERİ A.Ş.", KisaAd = "ESKİ", VergiKimlikNo = "1111111111", Aktif = false });
            db.SaveChanges();
            return db;
        }

        private static ParolaService Servis(CatalogContext db)
            => new(db, new ParolaSifreleyici(new EphemeralDataProtectionProvider()), new SahteSaat(Simdi), new SabitKullanici(16));

        private static ParolaKaydetDto Dvd(int? firmaId = Lime, string? parola = GizliParola, string? sifre = GizliSifre) => new()
        {
            Tur = ParolaTuru.DijitalVergiDairesi,
            FirmaId = firmaId,
            KullaniciAdi = "10000005",
            Parola = parola,
            Sifre = sifre
        };

        private static ParolaKaydetDto Sgk(int? firmaId, string? sube, string? firmaAdi = null) => new()
        {
            Tur = ParolaTuru.Sgk,
            FirmaId = firmaId,
            FirmaAdi = firmaAdi,
            Sube = sube,
            KullaniciKoduEk = "22",
            KullaniciAdi = "10000000022",
            SistemSifresi = "Sistem-1",
            IsyeriSifresi = "Isyeri-2"
        };

        [Fact]
        public async Task Sifreler_veritabaninda_sifreli_duz_metin_hicbir_sutunda_yok()
        {
            using var db = Context();
            var s = Servis(db);
            var dto = await s.EkleAsync(Dvd());

            var satir = await db.Parolalar.AsNoTracking().SingleAsync(p => p.Id == dto.Id);
            Assert.False(string.IsNullOrEmpty(satir.ParolaSifreli));
            Assert.False(string.IsNullOrEmpty(satir.SifreSifreli));
            Assert.NotEqual(satir.ParolaSifreli, satir.SifreSifreli);

            var tumMetin = JsonSerializer.Serialize(satir);
            Assert.DoesNotContain(GizliParola, tumMetin);
            Assert.DoesNotContain(GizliSifre, tumMetin);

            // Çözülünce aslı geri gelir.
            Assert.Equal(GizliParola, (await s.AlanAsync(dto.Id, "parola", "goruntule")).Deger);
            Assert.Equal(GizliSifre, (await s.AlanAsync(dto.Id, "sifre", "kopyala")).Deger);
        }

        [Fact]
        public async Task Liste_yanitinda_sifre_ne_duz_ne_sifreli_var_yalniz_dolu_bilgisi()
        {
            using var db = Context();
            var s = Servis(db);
            var dto = await s.EkleAsync(Dvd());
            var satir = await db.Parolalar.AsNoTracking().SingleAsync(p => p.Id == dto.Id);

            var liste = await s.ListeAsync();
            var json = JsonSerializer.Serialize(liste);

            Assert.DoesNotContain(GizliParola, json);
            Assert.DoesNotContain(GizliSifre, json);
            Assert.DoesNotContain(satir.ParolaSifreli!, json);
            Assert.DoesNotContain(satir.SifreSifreli!, json);

            var d = Assert.Single(liste);
            Assert.True(d.ParolaDolu);
            Assert.True(d.SifreDolu);
            Assert.False(d.SistemSifresiDolu);

            // DTO'da şifre taşıyabilecek alan hiç yok (yalnız bool "Dolu" alanları).
            var supheli = typeof(ParolaDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(string)
                            && (p.Name.Contains("Sifre", StringComparison.OrdinalIgnoreCase)
                                || p.Name.Contains("Parola", StringComparison.OrdinalIgnoreCase)))
                .Select(p => p.Name);
            Assert.Empty(supheli);
        }

        [Fact]
        public async Task Alan_ucu_degeri_dondurur_her_cagri_erisim_kaydi_yazar_sifre_metni_olmadan()
        {
            using var db = Context();
            var s = Servis(db);
            var dto = await s.EkleAsync(Dvd());

            var deger = await s.AlanAsync(dto.Id, "parola", "goruntule");
            await s.AlanAsync(dto.Id, "SIFRE", "kopyala");

            Assert.Equal("parola", deger.Alan);
            Assert.Equal(GizliParola, deger.Deger);

            var kayitlar = await db.ParolaErisimKayitlari.AsNoTracking().OrderBy(k => k.Id).ToListAsync();
            Assert.Equal(new[] { "ekle", "goruntule", "kopyala" }, kayitlar.Select(k => k.Islem));
            Assert.Equal(new string?[] { null, "parola", "sifre" }, kayitlar.Select(k => k.Alan));
            Assert.All(kayitlar, k =>
            {
                Assert.Equal(dto.Id, k.ParolaId);
                Assert.Equal(16, k.KullaniciId);
                Assert.Equal("test", k.Kullanici);
                Assert.Equal("LIME", k.FirmaAdi);
                Assert.Equal(new SahteSaat(Simdi).GetLocalNow().DateTime, k.Tarih);   // yerel duvar saati
            });

            var json = JsonSerializer.Serialize(kayitlar);
            Assert.DoesNotContain(GizliParola, json);
            Assert.DoesNotContain(GizliSifre, json);
        }

        [Fact]
        public async Task Alan_ucu_gecersiz_islem_ve_ture_ait_olmayan_alan_reddedilir_kayit_yazilmaz()
        {
            using var db = Context();
            var s = Servis(db);
            var dto = await s.EkleAsync(Dvd());

            Assert.Equal("islem", (await Assert.ThrowsAsync<ParolaKuralException>(() => s.AlanAsync(dto.Id, "parola", "indir"))).Field);
            Assert.Equal("islem", (await Assert.ThrowsAsync<ParolaKuralException>(() => s.AlanAsync(dto.Id, "parola", null))).Field);
            Assert.Equal("alan", (await Assert.ThrowsAsync<ParolaKuralException>(() => s.AlanAsync(dto.Id, "sistemSifresi", "goruntule"))).Field);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => s.AlanAsync(999, "parola", "goruntule"));

            Assert.Equal(1, await db.ParolaErisimKayitlari.CountAsync());   // yalnız "ekle"
        }

        [Fact]
        public async Task Duzenlemede_bos_birakilan_sifre_degismez_yazilan_degisir()
        {
            using var db = Context();
            var s = Servis(db);
            var dto = await s.EkleAsync(Dvd());
            var once = await db.Parolalar.AsNoTracking().SingleAsync();

            var guncel = Dvd(parola: "", sifre: "Yeni-Sifre-9");
            guncel.Not = "Ay sonu kontrol";
            var sonuc = await s.GuncelleAsync(dto.Id, guncel);

            var sonra = await db.Parolalar.AsNoTracking().SingleAsync();
            Assert.Equal(once.ParolaSifreli, sonra.ParolaSifreli);                       // boş → aynen
            Assert.Equal(GizliParola, (await s.AlanAsync(dto.Id, "parola", "goruntule")).Deger);
            Assert.Equal("Yeni-Sifre-9", (await s.AlanAsync(dto.Id, "sifre", "goruntule")).Deger);
            Assert.Equal("Ay sonu kontrol", sonuc.Not);

            // null da boş sayılır; hiçbir şifre değişmezse düzenle kaydının Alan'ı boş.
            await s.GuncelleAsync(dto.Id, Dvd(parola: null, sifre: null));
            Assert.Equal("Yeni-Sifre-9", (await s.AlanAsync(dto.Id, "sifre", "goruntule")).Deger);

            var duzenlemeler = await db.ParolaErisimKayitlari.Where(k => k.Islem == "duzenle").OrderBy(k => k.Id).ToListAsync();
            Assert.Equal(new string?[] { "sifre", null }, duzenlemeler.Select(k => k.Alan));
        }

        [Fact]
        public async Task Yeni_kayitta_sifreler_ve_tur_alanlari_zorunlu()
        {
            using var db = Context();
            var s = Servis(db);

            Assert.Equal("parola", (await Assert.ThrowsAsync<ParolaKuralException>(() => s.EkleAsync(Dvd(parola: "")))).Field);
            Assert.Equal("kullaniciAdi", (await Assert.ThrowsAsync<ParolaKuralException>(
                () => s.EkleAsync(new ParolaKaydetDto { Tur = ParolaTuru.DijitalVergiDairesi, FirmaId = Lime, Parola = "a", Sifre = "b" }))).Field);

            var ent = new ParolaKaydetDto { Tur = ParolaTuru.Entegrator, FirmaId = Lime, Entegrator = "Bilinmeyen", KullaniciAdi = "u", Sifre = "x" };
            Assert.Equal("entegrator", (await Assert.ThrowsAsync<ParolaKuralException>(() => s.EkleAsync(ent))).Field);

            ent.Entegrator = "uyumsoft";
            ent.PortalAdresi = "portal.uyumsoft.com.tr";
            Assert.Equal("portalAdresi", (await Assert.ThrowsAsync<ParolaKuralException>(() => s.EkleAsync(ent))).Field);

            ent.PortalAdresi = "https://portal.uyumsoft.com.tr";
            var kayit = await s.EkleAsync(ent);
            Assert.Equal("Uyumsoft", kayit.Entegrator);                                // listedeki yazımla
            Assert.Null(kayit.Sube);

            Assert.Equal(1, await db.Parolalar.CountAsync());
        }

        [Fact]
        public async Task Sgk_ayni_firma_ve_sube_ikinci_kez_girilemez()
        {
            using var db = Context();
            var s = Servis(db);

            var merkez = await s.EkleAsync(Sgk(Lime, null));
            await Assert.ThrowsAsync<ParolaCakismaException>(() => s.EkleAsync(Sgk(Lime, "  ")));        // boş şube = boş şube

            await s.EkleAsync(Sgk(Lime, "Bursa"));
            await Assert.ThrowsAsync<ParolaCakismaException>(() => s.EkleAsync(Sgk(Lime, "  bursa ")));  // harf / boşluk farkı sayılmaz
            await s.EkleAsync(Sgk(Lime, "İzmir"));
            await s.EkleAsync(Sgk(Dgr, "Bursa"));                                                       // başka firma serbest

            // Elle yazılan firmada da ad + şube tekil.
            await s.EkleAsync(Sgk(null, null, "Yeni Müşteri Ltd"));
            await Assert.ThrowsAsync<ParolaCakismaException>(() => s.EkleAsync(Sgk(null, null, "YENİ  MÜŞTERİ LTD")));

            // Kaydın kendisini düzenlemek çakışma değil; başka kaydın şubesine taşımak çakışma.
            await s.GuncelleAsync(merkez.Id, Sgk(Lime, null));
            await Assert.ThrowsAsync<ParolaCakismaException>(() => s.GuncelleAsync(merkez.Id, Sgk(Lime, "BURSA")));

            // Diğer türlerde tekillik yok: aynı firmaya iki entegratör kaydı.
            var ent = new ParolaKaydetDto { Tur = ParolaTuru.Entegrator, FirmaId = Lime, Entegrator = "eLogo", KullaniciAdi = "u", Sifre = "x" };
            await s.EkleAsync(ent);
            await s.EkleAsync(ent);

            Assert.Equal(5, await db.Parolalar.CountAsync(p => p.Tur == ParolaTuru.Sgk));
        }

        [Fact]
        public async Task Pasife_alinan_firmanin_kaydi_silinmez_listede_pasif_isaretli_gorunur()
        {
            using var db = Context();
            var s = Servis(db);
            var dto = await s.EkleAsync(Dvd(Lime));

            var firma = await db.Firmalar.SingleAsync(f => f.Id == Lime);
            firma.Aktif = false;
            firma.KisaAd = "LIME YENİ";
            await db.SaveChangesAsync();

            var d = Assert.Single(await s.ListeAsync());
            Assert.Equal(dto.Id, d.Id);
            Assert.True(d.FirmaPasif);
            Assert.False(d.ElleYazilan);
            Assert.Equal("LIME YENİ", d.FirmaAdi);                                     // Firmalarım'daki güncel ad

            // Kendi pasif firmasıyla düzenlenebilir; yeni kayıtta / başka kayda pasif firma seçilemez.
            await s.GuncelleAsync(dto.Id, Dvd(Lime, parola: null, sifre: null));
            Assert.Equal("firmaId", (await Assert.ThrowsAsync<ParolaKuralException>(() => s.EkleAsync(Dvd(Lime)))).Field);
            Assert.Equal("firmaId", (await Assert.ThrowsAsync<ParolaKuralException>(() => s.EkleAsync(Dvd(Eski)))).Field);
            Assert.Equal("firmaId", (await Assert.ThrowsAsync<ParolaKuralException>(() => s.EkleAsync(Dvd(404)))).Field);
        }

        [Fact]
        public async Task Firmalarimda_olmayan_firma_elle_yazilir_kisa_ad_yoksa_unvan()
        {
            using var db = Context();
            var s = Servis(db);

            var elleDto = Dvd(firmaId: null);
            elleDto.FirmaAdi = "  Yeni Müşteri A.Ş. ";
            var elle = await s.EkleAsync(elleDto);
            Assert.True(elle.ElleYazilan);
            Assert.Null(elle.FirmaId);
            Assert.Equal("Yeni Müşteri A.Ş.", elle.FirmaAdi);
            Assert.False(elle.FirmaPasif);

            Assert.Equal("firmaAdi", (await Assert.ThrowsAsync<ParolaKuralException>(() => s.EkleAsync(Dvd(firmaId: null)))).Field);

            var dgr = await s.EkleAsync(Dvd(Dgr));
            Assert.Equal("DGR İÇ VE DIŞ TİC.", dgr.FirmaAdi);                          // kısa ad boş → unvan
            Assert.False(dgr.ElleYazilan);
        }

        [Fact]
        public async Task Silme_kaydi_siler_erisim_kayitlari_kalir()
        {
            using var db = Context();
            var s = Servis(db);
            var dto = await s.EkleAsync(Dvd());
            await s.AlanAsync(dto.Id, "parola", "goruntule");

            await s.SilAsync(dto.Id);

            Assert.Empty(await db.Parolalar.ToListAsync());
            var kayitlar = await db.ParolaErisimKayitlari.OrderBy(k => k.Id).ToListAsync();
            Assert.Equal(new[] { "ekle", "goruntule", "sil" }, kayitlar.Select(k => k.Islem));
            Assert.All(kayitlar, k => Assert.Equal(dto.Id, k.ParolaId));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => s.SilAsync(dto.Id));
        }

        [Fact]
        public async Task Turu_degistirilemez_baska_turun_alanlari_yok_sayilir()
        {
            using var db = Context();
            var s = Servis(db);
            var dvd = Dvd();
            dvd.Sube = "Bursa";
            dvd.Entegrator = "eLogo";
            var dto = await s.EkleAsync(dvd);

            Assert.Null(dto.Sube);
            Assert.Null(dto.Entegrator);
            Assert.Null((await db.Parolalar.SingleAsync()).TekillikAnahtari);

            var sgk = Sgk(Lime, null);
            Assert.Equal("tur", (await Assert.ThrowsAsync<ParolaKuralException>(() => s.GuncelleAsync(dto.Id, sgk))).Field);
        }
    }
}
