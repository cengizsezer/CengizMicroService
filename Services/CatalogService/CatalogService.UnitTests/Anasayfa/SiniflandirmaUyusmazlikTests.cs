using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Features.Firmalar;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.BankaEkstre;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.Anasayfa
{
    /// <summary>
    /// Kodlardan çıkan öneri kayıtlı (otomatik) sınıflandırmayla çelişince: değer korunur,
    /// ama uyarı grubunda görünür; düzeltilince ya da elle onaylanınca düşer.
    /// </summary>
    public class SiniflandirmaUyusmazlikTests
    {
        private static readonly DateTime Bugun = new(2026, 9, 29);

        /// <summary>0010'dan otomatik kurumlar + bilanço almış, sonra metni 0010'suz kalmış firma.</summary>
        private static async Task<CatalogContext> CelisenFirma()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            db.Firmalar.Add(new Firma { Id = 1, Unvan = "DGR A.Ş.", KisaAd = "DGR", VergiKimlikNo = "2951070824", Aktif = true });
            db.FirmaSicilBilgileri.Add(new FirmaSicilBilgisi { Id = 1, FirmaId = 1, MukellefiyetTurleri = "0003 - MUHTASAR, 0010 - KURUMLAR VERGİSİ" });
            db.SaveChanges();
            await FirmaSiniflandirmaSeed.SeedAsync(db);

            db.FirmaSicilBilgileri.Single().MukellefiyetTurleri = "0003 - MUHTASAR, 0015 - KDV";
            db.SaveChanges();
            await FirmaSiniflandirmaSeed.SeedAsync(db);   // değeri geri ALMAZ (6C)
            return db;
        }

        private static Task<FirmaPaneliDto> Panel(CatalogContext db)
            => new FirmaPaneliService(db, () => Bugun).PanelAsync(1);

        [Fact]
        public async Task Celiskide_deger_korunur_uyari_grubunda_gorunur_rayda_sayilir()
        {
            using var db = await CelisenFirma();

            var panel = await Panel(db);
            var firma = await db.Firmalar.AsNoTracking().SingleAsync();

            Assert.Equal(VergiTuru.KurumlarVergisi, firma.VergiTuru);        // korundu

            var uyari = Assert.Single(FirmaPaneliKurucu.RaydakiUyarilar(panel.Secili!.Uyarilar));
            Assert.Equal(FirmaUyariTuru.SiniflandirmaUyusmuyor, uyari.Tur);
            Assert.StartsWith("Mükellefiyet kodları sınıflandırmayla uyuşmuyor — kodlarda 0010 yok, kayıtlı değer Kurumlar vergisi",
                              uyari.Mesaj);
            Assert.Equal(1, panel.Firmalar.Single().UyariSayisi);
            Assert.NotNull(panel.Secili.Siniflandirma.UyusmazlikMesaji);
        }

        [Fact]
        public async Task Elle_onaylaninca_uyari_duser_deger_degismez()
        {
            using var db = await CelisenFirma();

            await new FirmaKunyeService(db).SiniflandirmaKaydetAsync(1, new FirmaSiniflandirmaKaydetDto
            {
                VergiTuru = VergiTuru.KurumlarVergisi, DefterUsulu = DefterUsulu.BilancoEsasi, Onayla = true
            });

            var panel = await Panel(db);
            Assert.Empty(FirmaPaneliKurucu.RaydakiUyarilar(panel.Secili!.Uyarilar));
            Assert.Equal(0, panel.Firmalar.Single().UyariSayisi);

            var firma = await db.Firmalar.AsNoTracking().SingleAsync();
            Assert.Equal(VergiTuru.KurumlarVergisi, firma.VergiTuru);
            Assert.Equal(SiniflandirmaKaynagi.Elle, firma.VergiTuruKaynagi);
        }

        [Fact]
        public async Task Onaysiz_degisikliksiz_kayit_uyariyi_dusurmez()
        {
            using var db = await CelisenFirma();

            // Formu açıp hiçbir şeyi değiştirmeden kaydetmek onay SAYILMAZ.
            await new FirmaKunyeService(db).SiniflandirmaKaydetAsync(1, new FirmaSiniflandirmaKaydetDto
            {
                VergiTuru = VergiTuru.KurumlarVergisi, DefterUsulu = DefterUsulu.BilancoEsasi
            });

            Assert.Single(FirmaPaneliKurucu.RaydakiUyarilar((await Panel(db)).Secili!.Uyarilar));
        }

        [Fact]
        public async Task Duzeltilince_uyari_duser()
        {
            using var db = await CelisenFirma();

            await new FirmaKunyeService(db).SiniflandirmaKaydetAsync(1, new FirmaSiniflandirmaKaydetDto
            {
                VergiTuru = VergiTuru.GelirVergisi, DefterUsulu = DefterUsulu.BilancoEsasi
            });

            // Vergi türü değişti (Elle); defter usulü otomatik kaldı ve hâlâ 0010'suz → yalnız o kalır.
            var uyari = Assert.Single(FirmaPaneliKurucu.RaydakiUyarilar((await Panel(db)).Secili!.Uyarilar));
            Assert.Contains("kayıtlı defter usulü Bilanço esası", uyari.Mesaj);
            Assert.DoesNotContain("kayıtlı değer", uyari.Mesaj);
        }

        [Fact]
        public async Task Mukellefiyet_metni_duzelince_uyari_duser()
        {
            using var db = await CelisenFirma();
            db.FirmaSicilBilgileri.Single().MukellefiyetTurleri = "0003 - MUHTASAR, 0010 - KURUMLAR VERGİSİ";
            db.SaveChanges();

            Assert.Empty(FirmaPaneliKurucu.RaydakiUyarilar((await Panel(db)).Secili!.Uyarilar));
        }

        [Fact]
        public void Elle_secilmis_deger_celisse_de_uyari_yok()
        {
            var firma = new Firma { VergiTuru = VergiTuru.GelirVergisi, VergiTuruKaynagi = SiniflandirmaKaynagi.Elle };

            Assert.Null(MukellefiyetSiniflandirici.Uyusmazlik(firma, "0010 - KURUMLAR VERGİSİ"));
        }

        [Fact]
        public void Kodlar_baska_turu_gosteriyorsa_mesaj_bunu_soyler()
        {
            var firma = new Firma { VergiTuru = VergiTuru.GelirVergisi, VergiTuruKaynagi = SiniflandirmaKaynagi.Otomatik };

            var mesaj = MukellefiyetSiniflandirici.Uyusmazlik(firma, "0001 - GELİR VERGİSİ, 0010 - KURUMLAR VERGİSİ");

            Assert.Equal("Mükellefiyet kodları sınıflandırmayla uyuşmuyor — kodlar Kurumlar vergisi gösteriyor (0010), " +
                         "kayıtlı değer Gelir vergisi", mesaj);
        }

        [Fact]
        public void Uyumlu_firmada_uyari_yok()
        {
            var firma = new Firma();
            MukellefiyetSiniflandirici.Uygula(firma, "0010 - KURUMLAR VERGİSİ");

            Assert.Null(MukellefiyetSiniflandirici.Uyusmazlik(firma, "0010 - KURUMLAR VERGİSİ"));
        }
    }
}
