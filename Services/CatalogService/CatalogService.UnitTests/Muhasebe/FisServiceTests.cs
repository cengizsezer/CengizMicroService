using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Features.Muhasebe.Dtos;
using CatalogService.Api.Features.Muhasebe.Services;
using CatalogService.Api.Infrastructure.Accessor;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.Muhasebe
{
    /// <summary>Fiş (yevmiye) iş kuralları (10–17) için birim testleri.</summary>
    public class FisServiceTests
    {
        private static readonly DateTime Tarih = new(2026, 3, 15);

        private static FisService Servis(CatalogContext db) =>
            new(db, new SabitKullanici(), new FixedTenantAccessor(MuhasebeTestOrtami.TenantNo));

        private static FisSatirYazDto Satir(int hesapId, decimal borc = 0, decimal alacak = 0) => new()
        {
            HesapId = hesapId,
            Borc = borc,
            Alacak = alacak
        };

        private static FisYazDto Fis(bool kesinlestir, params FisSatirYazDto[] satirlar) => new()
        {
            Tarih = Tarih,
            FisTuru = FisTuru.Mahsup,
            Aciklama = "Test fişi",
            Kesinlestir = kesinlestir,
            Satirlar = satirlar.ToList()
        };

        /// <summary>770 borçlu / 100 alacaklı, 1.000,00 TL'lik dengeli fiş isteği üretir.</summary>
        private static async Task<FisYazDto> DengeliFisAsync(CatalogContext db, bool kesinlestir = false, decimal tutar = 1000m)
        {
            var gider = await MuhasebeTestOrtami.HesapAsync(db, "770");
            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");

            return Fis(kesinlestir, Satir(gider.Id, borc: tutar), Satir(kasa.Id, alacak: tutar));
        }

        // ---- Kural 10: fiş en az iki satır içermeli ----

        [Fact]
        public async Task TekSatirlikFis_Kaydedilemiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);
            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");

            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(
                () => servis.CreateAsync(Fis(false, Satir(kasa.Id, borc: 100m))));

            Assert.Contains("en az iki satır", hata.Message);
            Assert.Equal(0, await db.Fisler.CountAsync());
        }

        // ---- Kural 11: borç toplamı = alacak toplamı ----

        [Fact]
        public async Task DengesizFis_Kaydedilemiyor_HataMesajiBorcAlacakVeFarkiIceriyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);
            var gider = await MuhasebeTestOrtami.HesapAsync(db, "770");
            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");

            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(
                () => servis.CreateAsync(Fis(false,
                    Satir(gider.Id, borc: 1250.50m),
                    Satir(kasa.Id, alacak: 1000m))));

            Assert.Contains("1.250,50", hata.Message);   // borç
            Assert.Contains("1.000,00", hata.Message);   // alacak
            Assert.Contains("250,50", hata.Message);     // fark
            Assert.Equal(0, await db.Fisler.CountAsync());
        }

        [Fact]
        public async Task DengeliFis_Kaydediliyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var fis = await servis.CreateAsync(await DengeliFisAsync(db));

            Assert.Equal(1000m, fis.ToplamBorc);
            Assert.Equal(1000m, fis.ToplamAlacak);
            Assert.Equal(FisDurum.Taslak, fis.Durum);
            Assert.Equal(2026, fis.DonemYil);
            Assert.Equal(new short[] { 1, 2 }, fis.Satirlar.Select(s => s.SiraNo).ToArray());
        }

        // ---- Kural 12: toplam tutar sıfır olamaz ----

        [Fact]
        public async Task ToplamiSifirOlanFis_Kaydedilemiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);
            var gider = await MuhasebeTestOrtami.HesapAsync(db, "770");
            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");

            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(
                () => servis.CreateAsync(Fis(false, Satir(gider.Id), Satir(kasa.Id))));

            Assert.Contains("sıfır olamaz", hata.Message);
            Assert.Equal(0, await db.Fisler.CountAsync());
        }

        // ---- Kural 13: bir satırda ya borç ya alacak dolu olur ----

        [Fact]
        public async Task BorcVeAlacagiBirlikteDoluSatir_Kaydedilemiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);
            var gider = await MuhasebeTestOrtami.HesapAsync(db, "770");
            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");

            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(
                () => servis.CreateAsync(Fis(false,
                    new FisSatirYazDto { HesapId = gider.Id, Borc = 100m, Alacak = 100m },
                    Satir(kasa.Id, alacak: 100m))));

            Assert.Contains("hem borç hem alacak", hata.Message);
            Assert.Equal(0, await db.Fisler.CountAsync());
        }

        [Fact]
        public async Task TutarsizSatir_Kaydedilemiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);
            var gider = await MuhasebeTestOrtami.HesapAsync(db, "770");
            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");
            var banka = await MuhasebeTestOrtami.HesapAsync(db, "102");

            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(
                () => servis.CreateAsync(Fis(false,
                    Satir(gider.Id, borc: 100m),
                    Satir(banka.Id),                       // boş satır
                    Satir(kasa.Id, alacak: 100m))));

            Assert.Contains("2. satırda tutar yok", hata.Message);
        }

        // ---- Kural 14: yalnızca hareket gören ve aktif hesaba fiş kesilebilir ----

        [Fact]
        public async Task HareketGormeyenHesaba_FisKesilemiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);
            var grup = await MuhasebeTestOrtami.HesapAsync(db, "10");   // grup, çocuğu var → hareket görmez
            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");

            Assert.False(grup.HareketGorur);

            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(
                () => servis.CreateAsync(Fis(false,
                    Satir(grup.Id, borc: 100m),
                    Satir(kasa.Id, alacak: 100m))));

            Assert.Contains("hareket görmüyor", hata.Message);
            Assert.Equal(0, await db.Fisler.CountAsync());
        }

        [Fact]
        public async Task PasifHesaba_FisKesilemiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);
            var gider = await MuhasebeTestOrtami.HesapAsync(db, "770");
            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");

            // Faz 2 hesap planı servisi ile pasife çekilir.
            await MuhasebeTestOrtami.HesapPlaniServisi(db).PasifeAlAsync(kasa.Id);

            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(
                () => servis.CreateAsync(Fis(false,
                    Satir(gider.Id, borc: 100m),
                    Satir(kasa.Id, alacak: 100m))));

            Assert.Contains("pasif", hata.Message);
            Assert.Equal(0, await db.Fisler.CountAsync());
        }

        [Fact]
        public async Task TaslakKesinlestirilirken_ArayaPasifeAlinanHesap_Yakalaniyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var taslak = await servis.CreateAsync(await DengeliFisAsync(db));
            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");
            await MuhasebeTestOrtami.HesapPlaniServisi(db).PasifeAlAsync(kasa.Id);

            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(() => servis.KesinlestirAsync(taslak.Id));

            Assert.Contains("pasif", hata.Message);
            Assert.Equal(FisDurum.Taslak, (await servis.GetByIdAsync(taslak.Id))!.Durum);
        }

        // ---- Kural 15: kesinleşmiş fiş güncellenemez ve silinemez ----

        [Fact]
        public async Task KesinlesmisFis_Guncellenemiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var fis = await servis.CreateAsync(await DengeliFisAsync(db, kesinlestir: true));
            var yeniIcerik = await DengeliFisAsync(db, tutar: 500m);

            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(
                () => servis.UpdateAsync(fis.Id, yeniIcerik));

            Assert.Contains("ters kayıt", hata.Message);
            Assert.Equal(1000m, (await servis.GetByIdAsync(fis.Id))!.ToplamBorc);
        }

        [Fact]
        public async Task KesinlesmisFis_Silinemiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var fis = await servis.CreateAsync(await DengeliFisAsync(db, kesinlestir: true));

            Assert.Equal(FisSilmeSonuc.Kesinlesmis, await servis.DeleteAsync(fis.Id));
            Assert.Equal(1, await db.Fisler.CountAsync());
        }

        [Fact]
        public async Task TaslakFis_GuncellenebiliyorVeSilinebiliyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var fis = await servis.CreateAsync(await DengeliFisAsync(db));
            var guncel = await servis.UpdateAsync(fis.Id, await DengeliFisAsync(db, tutar: 250m));

            Assert.Equal(250m, guncel!.ToplamBorc);
            Assert.Equal(fis.FisNo, guncel.FisNo);              // numara değişmez
            Assert.Equal(2, guncel.Satirlar.Count);
            Assert.Equal(2, await db.FisSatirlar.CountAsync()); // eski satırlar silindi

            Assert.Equal(FisSilmeSonuc.Silindi, await servis.DeleteAsync(fis.Id));
            Assert.Equal(0, await db.Fisler.CountAsync());
            Assert.Equal(0, await db.FisSatirlar.CountAsync());
        }

        [Fact]
        public async Task Kesinlestir_TaslagiKesinlesmisYapiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var fis = await servis.CreateAsync(await DengeliFisAsync(db));
            var kesin = await servis.KesinlestirAsync(fis.Id);

            Assert.Equal(FisDurum.Kesinlesmis, kesin!.Durum);
            Assert.NotNull(kesin.GuncellemeT);

            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(() => servis.KesinlestirAsync(fis.Id));
            Assert.Contains("zaten kesinleşmiş", hata.Message);
        }

        // ---- Ters kayıt ----

        [Fact]
        public async Task TersKayit_BorcVeAlacagiYerDegistirmisFisUretiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var kaynak = await servis.CreateAsync(await DengeliFisAsync(db, kesinlestir: true));
            var ters = await servis.TersKayitAsync(kaynak.Id, new TersKayitDto());

            Assert.NotNull(ters);
            Assert.NotEqual(kaynak.Id, ters!.Id);
            Assert.NotEqual(kaynak.FisNo, ters.FisNo);
            Assert.Equal(FisDurum.Taslak, ters.Durum);
            Assert.Contains(kaynak.FisNo, ters.Aciklama);

            var kaynakSatir = kaynak.Satirlar.OrderBy(s => s.SiraNo).ToList();
            var tersSatir = ters.Satirlar.OrderBy(s => s.SiraNo).ToList();

            Assert.Equal(kaynakSatir.Count, tersSatir.Count);
            for (var i = 0; i < kaynakSatir.Count; i++)
            {
                Assert.Equal(kaynakSatir[i].HesapId, tersSatir[i].HesapId);
                Assert.Equal(kaynakSatir[i].Borc, tersSatir[i].Alacak);
                Assert.Equal(kaynakSatir[i].Alacak, tersSatir[i].Borc);
            }

            Assert.Equal(kaynak.ToplamBorc, ters.ToplamAlacak);
            Assert.Equal(kaynak.ToplamAlacak, ters.ToplamBorc);

            // Kaynak fiş olduğu gibi durur; düzeltme silmeyle değil ters kayıtla yapılır.
            Assert.Equal(FisDurum.Kesinlesmis, (await servis.GetByIdAsync(kaynak.Id))!.Durum);
        }

        [Fact]
        public async Task TaslakFisin_TersKaydiAlinamiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var taslak = await servis.CreateAsync(await DengeliFisAsync(db));

            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(
                () => servis.TersKayitAsync(taslak.Id, new TersKayitDto()));

            Assert.Contains("yalnızca kesinleşmiş", hata.Message);
        }

        // ---- Kural 17: döviz satırında döviz tutarı ve kur zorunlu ----

        [Fact]
        public async Task DovizSatirinda_DovizVeKurZorunlu()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);
            var banka = await MuhasebeTestOrtami.HesapAsync(db, "102");
            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");

            var istek = Fis(false,
                new FisSatirYazDto { HesapId = banka.Id, Borc = 3500m, ParaBirimi = "USD" },
                Satir(kasa.Id, alacak: 3500m));

            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(() => servis.CreateAsync(istek));
            Assert.Contains("döviz tutarı ve kur zorunlu", hata.Message);

            // Döviz tutarı ve kur verilince kaydediliyor; Borc alanı TL karşılığıdır.
            istek.Satirlar[0].Doviz = 100m;
            istek.Satirlar[0].Kur = 35m;

            var fis = await servis.CreateAsync(istek);
            var dovizSatir = fis.Satirlar.First(s => s.ParaBirimi == "USD");

            Assert.Equal(3500m, dovizSatir.Borc);
            Assert.Equal(100m, dovizSatir.Doviz);
            Assert.Equal(35m, dovizSatir.Kur);

            // TL satırında döviz alanları taşınmaz.
            var tlSatir = fis.Satirlar.First(s => s.ParaBirimi == "TRY");
            Assert.Null(tlSatir.Doviz);
            Assert.Null(tlSatir.Kur);
        }

        // ---- Kural 16: fiş numarası firma + dönem bazında sıralı, boşluksuz ----

        [Fact]
        public async Task FisNumarasi_DonemBazindaSiraliUretiliyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var birinci = await servis.CreateAsync(await DengeliFisAsync(db));
            var ikinci = await servis.CreateAsync(await DengeliFisAsync(db));

            Assert.Equal("2026/000001", birinci.FisNo);
            Assert.Equal("2026/000002", ikinci.FisNo);

            // Farklı dönem kendi sırasından başlar.
            var oncekiDonem = await DengeliFisAsync(db);
            oncekiDonem.Tarih = new DateTime(2025, 12, 31);

            var ucuncu = await servis.CreateAsync(oncekiDonem);
            Assert.Equal("2025/000001", ucuncu.FisNo);
            Assert.Equal(2025, ucuncu.DonemYil);
        }

        [Fact]
        public async Task FisNumarasi_Eszamanli50Istekte_TekrarsizVeBosluksuz()
        {
            const int istekSayisi = 50;

            var veritabani = MuhasebeTestOrtami.YeniVeritabaniAdi();
            using var kurulum = await MuhasebeTestOrtami.HazirContextAsync(veritabani);

            var giderId = (await MuhasebeTestOrtami.HesapAsync(kurulum, "770")).Id;
            var kasaId = (await MuhasebeTestOrtami.HesapAsync(kurulum, "100")).Id;

            // Her istek gerçekteki gibi kendi scoped context'i ile çalışır.
            var istekler = Enumerable.Range(0, istekSayisi).Select(_ => Task.Run(async () =>
            {
                using var db = MuhasebeTestOrtami.YeniContext(veritabani);
                var fis = await Servis(db).CreateAsync(
                    Fis(true, Satir(giderId, borc: 100m), Satir(kasaId, alacak: 100m)));

                return fis.FisNo;
            }));

            var numaralar = await Task.WhenAll(istekler);

            var beklenen = Enumerable.Range(1, istekSayisi).Select(i => $"2026/{i:D6}").ToArray();

            Assert.Equal(istekSayisi, numaralar.Distinct().Count());
            Assert.Equal(beklenen, numaralar.OrderBy(n => n).ToArray());
        }

        // ---- Listeleme ----

        [Fact]
        public async Task Liste_Tarih_Durum_VeHesapFiltresiUyguluyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);
            var banka = await MuhasebeTestOrtami.HesapAsync(db, "102");
            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");

            var taslak = await servis.CreateAsync(await DengeliFisAsync(db));
            var kesin = await servis.CreateAsync(Fis(true, Satir(banka.Id, borc: 400m), Satir(kasa.Id, alacak: 400m)));

            var eskiFis = await DengeliFisAsync(db);
            eskiFis.Tarih = new DateTime(2026, 1, 5);
            await servis.CreateAsync(eskiFis);

            Assert.Equal(3, (await servis.GetListeAsync(new FisFiltreDto())).Count);

            var kesinler = await servis.GetListeAsync(new FisFiltreDto { Durum = FisDurum.Kesinlesmis });
            Assert.Equal(new[] { kesin.Id }, kesinler.Select(f => f.Id).ToArray());

            var martSonrasi = await servis.GetListeAsync(new FisFiltreDto { Bas = new DateTime(2026, 3, 1) });
            Assert.Equal(2, martSonrasi.Count);
            Assert.DoesNotContain(martSonrasi, f => f.Tarih.Month == 1);

            var bankaninkiler = await servis.GetListeAsync(new FisFiltreDto { HesapId = banka.Id });
            Assert.Equal(new[] { kesin.Id }, bankaninkiler.Select(f => f.Id).ToArray());

            var ozet = bankaninkiler[0];
            Assert.Equal(2, ozet.SatirSayisi);
            Assert.Equal(400m, ozet.ToplamBorc);
            Assert.Equal(400m, ozet.ToplamAlacak);
            Assert.NotEqual(0, taslak.Id);
        }

        [Fact]
        public async Task BulunamayanFis_NullDonuyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            Assert.Null(await servis.GetByIdAsync(9999));
            Assert.Null(await servis.UpdateAsync(9999, await DengeliFisAsync(db)));
            Assert.Null(await servis.KesinlestirAsync(9999));
            Assert.Null(await servis.TersKayitAsync(9999, new TersKayitDto()));
            Assert.Equal(FisSilmeSonuc.Bulunamadi, await servis.DeleteAsync(9999));
        }

        // ---- Hesap planı ile bağ ----

        [Fact]
        public async Task FisKesilenKullaniciHesabi_Silinemiyor_PasifeCekiliyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);
            var hesapPlani = MuhasebeTestOrtami.HesapPlaniServisi(db);

            var banka = await MuhasebeTestOrtami.HesapAsync(db, "102");
            var muavin = await hesapPlani.CreateAsync(new HesapPlaniCreateDto
            {
                UstHesapId = banka.Id,
                Segment = "1",
                Ad = "Akbank",
                HareketGorur = true
            });

            var gider = await MuhasebeTestOrtami.HesapAsync(db, "770");
            await servis.CreateAsync(Fis(true, Satir(gider.Id, borc: 300m), Satir(muavin!.Id, alacak: 300m)));

            // Faz 2'deki kural 8: hareketi olan hesap silinmez, pasife çekilir.
            Assert.Equal(HesapSilmeSonuc.HareketVar, await hesapPlani.DeleteAsync(muavin.Id));

            await hesapPlani.PasifeAlAsync(muavin.Id);
            Assert.False((await MuhasebeTestOrtami.HesapAsync(db, "102.01")).Aktif);
        }
    }
}
