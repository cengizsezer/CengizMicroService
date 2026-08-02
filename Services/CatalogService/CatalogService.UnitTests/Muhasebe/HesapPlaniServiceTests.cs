using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Features.Muhasebe.Dtos;
using CatalogService.Api.Features.Muhasebe.Services;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.Api.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.Muhasebe
{
    /// <summary>Hesap planı iş kuralları (1–9) için birim testleri.</summary>
    public class HesapPlaniServiceTests
    {
        private static HesapPlaniService Servis(CatalogContext db) => new(db);

        private static HesapPlaniCreateDto Ekleme(int? ustId, string segment, string ad) => new()
        {
            UstHesapId = ustId,
            Segment = segment,
            Ad = ad,
            HareketGorur = true
        };

        /// <summary>102 → 102.01 → 102.01.01 zincirini kurar ve en alt düğümü döner.</summary>
        private static async Task<HesapPlaniDto> MuavinZinciriAsync(CatalogContext db, HesapPlaniService servis)
        {
            var bankalar = await MuhasebeTestOrtami.HesapAsync(db, "102");
            var muavin1 = await servis.CreateAsync(Ekleme(bankalar.Id, "1", "Akbank"));
            return (await servis.CreateAsync(Ekleme(muavin1!.Id, "1", "Akbank TL")))!;
        }

        // ---- Kural 1: yalnızca yaprak hesap hareket görür ----

        [Fact]
        public async Task CocukEklenen_HesabinHareketGorurAlani_OtomatikKapaniyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var bankalar = await MuhasebeTestOrtami.HesapAsync(db, "102");
            Assert.True(bankalar.HareketGorur);

            await servis.CreateAsync(Ekleme(bankalar.Id, "1", "Akbank"));

            var guncel = await MuhasebeTestOrtami.HesapAsync(db, "102");
            Assert.False(guncel.HareketGorur);
        }

        // ---- Kural 2: hareketi olan hesabın altına çocuk eklenemez ----

        [Fact]
        public async Task HareketiOlanHesabinAltina_CocukEklenemiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");
            await MuhasebeTestOrtami.HareketYazAsync(db, kasa.Id);

            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(
                () => servis.CreateAsync(Ekleme(kasa.Id, "1", "Merkez Kasa")));

            Assert.Contains("önce hareketleri alt hesaba taşıyın", hata.Message);
            Assert.Equal(0, await db.HesapPlanlari.CountAsync(h => h.UstHesapId == kasa.Id));
            Assert.True((await MuhasebeTestOrtami.HesapAsync(db, "100")).HareketGorur);
        }

        // ---- Kural 3: kod üst hesabın kodu ile başlar, tam kodu servis birleştirir ----

        [Fact]
        public async Task TamKod_UstHesabinKoduIleBaslayacakSekilde_ServisTarafindanBirlestiriliyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var bankalar = await MuhasebeTestOrtami.HesapAsync(db, "102");
            var muavin = await servis.CreateAsync(Ekleme(bankalar.Id, "1", "Akbank"));

            Assert.NotNull(muavin);
            Assert.Equal("102.01", muavin!.Kod);
            Assert.Equal("10201", muavin.KodDuz);
            Assert.Equal("01", muavin.SegmentKod);
            Assert.Equal(4, muavin.Seviye);
            Assert.Equal(HesapTuru.Muavin, muavin.HesapTuru);
            Assert.StartsWith(bankalar.Kod, muavin.Kod);
        }

        // ---- Kural 4: segment maskeyi aşamaz, sadece rakam, sola sıfır dolgulu ----

        [Fact]
        public async Task KisaGirilenSegment_MaskeyeGoreSolaSifirDolduruluyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var ust = await MuavinZinciriAsync(db, servis);   // 102.01.01, seviye 5
            var muavin = await servis.CreateAsync(Ekleme(ust.Id, "1", "Akbank vadesiz TL"));

            Assert.Equal("0001", muavin!.SegmentKod);
            Assert.Equal("102.01.01.0001", muavin.Kod);
            Assert.Equal(6, muavin.Seviye);
        }

        [Fact]
        public async Task RakamDisiKarakterIcerenSegment_Reddediliyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var bankalar = await MuhasebeTestOrtami.HesapAsync(db, "102");

            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(
                () => servis.CreateAsync(Ekleme(bankalar.Id, "A1", "Akbank")));

            Assert.Contains("yalnızca rakam", hata.Message);
        }

        [Fact]
        public async Task MaskedekiUzunlugunuAsanSegment_Reddediliyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var bankalar = await MuhasebeTestOrtami.HesapAsync(db, "102");

            // 4. seviyede maske 2 hane; 3 haneli segment kabul edilmez.
            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(
                () => servis.CreateAsync(Ekleme(bankalar.Id, "123", "Akbank")));

            Assert.Contains("en fazla 2 hane", hata.Message);
        }

        [Fact]
        public async Task AyniUstAltinda_AyniSegment_IkinciKezEklenemiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var bankalar = await MuhasebeTestOrtami.HesapAsync(db, "102");
            await servis.CreateAsync(Ekleme(bankalar.Id, "1", "Akbank"));

            // "01" ve "1" aynı segmente denk gelir (sola sıfır dolgu).
            var hata = await Assert.ThrowsAsync<DuplicateRecordException>(
                () => servis.CreateAsync(Ekleme(bankalar.Id, "01", "Yapı Kredi")));

            Assert.Contains("102.01", hata.Message);
            Assert.Equal(1, await db.HesapPlanlari.CountAsync(h => h.UstHesapId == bankalar.Id));
        }

        [Fact]
        public async Task SonrakiKod_IlkBosSegmentiDonduruyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var ust = await MuavinZinciriAsync(db, servis);   // 102.01.01, seviye 5
            await servis.CreateAsync(Ekleme(ust.Id, "1", "A"));
            await servis.CreateAsync(Ekleme(ust.Id, "2", "B"));
            await servis.CreateAsync(Ekleme(ust.Id, "4", "C"));

            var sonraki = await servis.GetSonrakiKodAsync(ust.Id);

            Assert.NotNull(sonraki);
            Assert.Equal("0003", sonraki!.Segment);
            Assert.Equal("102.01.01.0003", sonraki.Kod);
        }

        // ---- Kural 5: karakter üst hesaptan miras alınır ----

        [Fact]
        public async Task Karakter_UstHesaptanMirasAliniyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var giderKebiri = await MuhasebeTestOrtami.HesapAsync(db, "770");
            var muavin = await servis.CreateAsync(Ekleme(giderKebiri.Id, "1", "Kırtasiye"));
            var altMuavin = await servis.CreateAsync(Ekleme(muavin!.Id, "1", "Kağıt"));

            Assert.Equal(HesapKarakter.Gider, muavin.Karakter);
            Assert.Equal(HesapKarakter.Gider, altMuavin!.Karakter);
        }

        // ---- Kural 6: sistem hesabı kodu değişmez, silinemez ----

        [Fact]
        public async Task SistemHesabinin_AdiDegistirilebiliyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");

            var guncel = await servis.UpdateAsync(kasa.Id, new HesapPlaniUpdateDto
            {
                Ad = "Merkez Kasa",
                HareketGorur = kasa.HareketGorur
            });

            Assert.Equal("Merkez Kasa", guncel!.Ad);
            Assert.Equal("100", guncel.Kod);
        }

        [Fact]
        public async Task SistemHesabinin_KoduDegistirilemiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");

            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(
                () => servis.UpdateAsync(kasa.Id, new HesapPlaniUpdateDto
                {
                    Ad = "Kasa",
                    Segment = "5",
                    HareketGorur = kasa.HareketGorur
                }));

            Assert.Contains("kodu değiştirilemez", hata.Message);
            Assert.Equal("100", (await MuhasebeTestOrtami.HesapAsync(db, "100")).Kod);
        }

        [Fact]
        public async Task SistemHesabi_Silinemiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");

            Assert.Equal(HesapSilmeSonuc.SistemHesabi, await servis.DeleteAsync(kasa.Id));
            Assert.True(await db.HesapPlanlari.AnyAsync(h => h.Id == kasa.Id));
        }

        // ---- Kural 7: kullanıcı kebiri yalnızca boş kod aralıklarında açılır ----

        [Fact]
        public async Task BosKebirler_GrupAltindaKullanilmamisKodlariListeliyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var grup = await MuhasebeTestOrtami.HesapAsync(db, "10");   // 100 ve 102 dolu

            var boslar = await servis.GetBosKebirlerAsync(grup.Id);

            Assert.NotNull(boslar);
            var kodlar = boslar!.Select(b => b.Kod).ToList();
            Assert.DoesNotContain("100", kodlar);
            Assert.DoesNotContain("102", kodlar);
            Assert.Contains("101", kodlar);
            Assert.Contains("109", kodlar);
            Assert.Equal(8, kodlar.Count);
        }

        [Fact]
        public async Task DoluKebirKodu_IkinciKezAcilamiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var grup = await MuhasebeTestOrtami.HesapAsync(db, "10");

            await Assert.ThrowsAsync<DuplicateRecordException>(
                () => servis.CreateAsync(Ekleme(grup.Id, "2", "Özel Banka Hesabı")));
        }

        // ---- Kural 8: silme yok; hareketi olan pasife çekilir ----

        [Fact]
        public async Task HareketiOlanHesap_Silinemiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var bankalar = await MuhasebeTestOrtami.HesapAsync(db, "102");
            var muavin = await servis.CreateAsync(Ekleme(bankalar.Id, "1", "Akbank"));
            await MuhasebeTestOrtami.HareketYazAsync(db, muavin!.Id);

            Assert.Equal(HesapSilmeSonuc.HareketVar, await servis.DeleteAsync(muavin.Id));
            Assert.True(await db.HesapPlanlari.AnyAsync(h => h.Id == muavin.Id));
        }

        [Fact]
        public async Task HareketsizKullaniciHesabi_Silinebiliyor_UstHesapYenidenYaprakOluyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var bankalar = await MuhasebeTestOrtami.HesapAsync(db, "102");
            var muavin = await servis.CreateAsync(Ekleme(bankalar.Id, "1", "Akbank"));

            Assert.Equal(HesapSilmeSonuc.Silindi, await servis.DeleteAsync(muavin!.Id));
            Assert.False(await db.HesapPlanlari.AnyAsync(h => h.Id == muavin.Id));
            Assert.True((await MuhasebeTestOrtami.HesapAsync(db, "102")).HareketGorur);
        }

        [Fact]
        public async Task AltHesabiOlanHesap_Silinemiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var bankalar = await MuhasebeTestOrtami.HesapAsync(db, "102");
            var muavin = await servis.CreateAsync(Ekleme(bankalar.Id, "1", "Akbank"));
            await servis.CreateAsync(Ekleme(muavin!.Id, "1", "Akbank TL"));

            Assert.Equal(HesapSilmeSonuc.AltHesapVar, await servis.DeleteAsync(muavin.Id));
        }

        [Fact]
        public async Task PasifeAlma_AltAgaciDaKapsiyor_VeSecimListesindenDusuyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var bankalar = await MuhasebeTestOrtami.HesapAsync(db, "102");
            var muavin = await servis.CreateAsync(Ekleme(bankalar.Id, "1", "Akbank"));
            var altMuavin = await servis.CreateAsync(Ekleme(muavin!.Id, "1", "Akbank TL"));

            await servis.PasifeAlAsync(muavin.Id);

            Assert.False((await db.HesapPlanlari.FirstAsync(h => h.Id == muavin.Id)).Aktif);
            Assert.False((await db.HesapPlanlari.FirstAsync(h => h.Id == altMuavin!.Id)).Aktif);

            var secilebilir = await servis.GetHareketGorenlerAsync();
            Assert.DoesNotContain(secilebilir, h => h.Id == altMuavin!.Id);
        }

        // ---- Kural 9: Yol üst hesaptan türetilir ----

        [Fact]
        public async Task Yol_UstHesabinYoluVeIdsiIleTuretiliyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var bankalar = await MuhasebeTestOrtami.HesapAsync(db, "102");
            var muavin = await servis.CreateAsync(Ekleme(bankalar.Id, "1", "Akbank"));

            Assert.Equal($"{bankalar.Yol}{bankalar.Id}/", muavin!.Yol);
            Assert.StartsWith(bankalar.Yol, muavin.Yol);
        }

        // ---- Seçim listesi ve arama ----

        [Fact]
        public async Task HareketGorenler_SadeceHareketGorenVeAktifHesaplariDonduruyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var liste = await servis.GetHareketGorenlerAsync();

            Assert.All(liste, h => Assert.True(h.HareketGorur && h.Aktif));
            Assert.Contains(liste, h => h.Kod == "100");
            Assert.DoesNotContain(liste, h => h.Kod == "10");
        }

        [Fact]
        public async Task Arama_HemKodaHemIsmeGoreCalisiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var bankalar = await MuhasebeTestOrtami.HesapAsync(db, "102");
            await servis.CreateAsync(Ekleme(bankalar.Id, "1", "Akbank"));

            var kodIle = await servis.AraAsync("102.01");
            var isimIle = await servis.AraAsync("Akbank");

            Assert.Contains(kodIle, h => h.Kod == "102.01");
            Assert.Contains(isimIle, h => h.Kod == "102.01");
        }
    }
}
