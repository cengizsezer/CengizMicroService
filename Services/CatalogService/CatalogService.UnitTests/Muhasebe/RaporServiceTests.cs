using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Features.Muhasebe.Dtos;
using CatalogService.Api.Features.Muhasebe.Services;
using CatalogService.Api.Infrastructure.Accessor;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.Muhasebe
{
    /// <summary>Rapor / bakiye iş kuralları (18–21) için birim testleri.</summary>
    public class RaporServiceTests
    {
        private static FisService FisServisi(CatalogContext db) =>
            new(db, new SabitKullanici(), new FixedTenantAccessor(MuhasebeTestOrtami.TenantNo));

        private static RaporService Servis(CatalogContext db) => new(db);

        private static FisSatirYazDto Satir(int hesapId, decimal borc = 0, decimal alacak = 0, int? masrafMerkeziId = null) => new()
        {
            HesapId = hesapId,
            Borc = borc,
            Alacak = alacak,
            MasrafMerkeziId = masrafMerkeziId
        };

        private static FisYazDto Fis(DateTime tarih, bool kesinlestir, params FisSatirYazDto[] satirlar) => new()
        {
            Tarih = tarih,
            FisTuru = FisTuru.Mahsup,
            Aciklama = "Rapor testi",
            Kesinlestir = kesinlestir,
            Satirlar = satirlar.ToList()
        };

        /// <summary>Test kitabındaki hesap kimlikleri.</summary>
        private sealed record Kitap(int Kasa, int Bankalar, int Akbank, int Garanti, int Gider, int Saticilar);

        /// <summary>
        /// Hazır THP'nin üzerine pasif karakterli bir sınıf (3 → 32 → 320) ve 102'nin altına
        /// iki banka muavini ekler; ardından aşağıdaki kesinleşmiş fişleri yazar:
        /// <list type="bullet">
        /// <item>05.01 açılış — Kasa 20.000 B / Satıcılar 20.000 A</item>
        /// <item>10.01 gider  — 770 1.000 B / Satıcılar 1.000 A</item>
        /// <item>15.02 —  Akbank 5.000 B / Kasa 5.000 A</item>
        /// <item>20.03 —  Garanti 3.000 B / Kasa 3.000 A</item>
        /// <item>25.03 —  Satıcılar 400 B / Kasa 400 A</item>
        /// </list>
        /// Ayrıca 01.04 tarihli bir taslak fiş (770 250 B / Kasa 250 A) bırakır.
        /// </summary>
        private static async Task<Kitap> KitapKurAsync(CatalogContext db)
        {
            var hesapPlani = new HesapPlaniService(db);
            var fisler = FisServisi(db);

            var sinif3 = await hesapPlani.CreateAsync(new HesapPlaniCreateDto
            {
                Segment = "3",
                Ad = "KISA VADELİ YABANCI KAYNAKLAR"
            });
            var grup32 = await hesapPlani.CreateAsync(new HesapPlaniCreateDto
            {
                UstHesapId = sinif3!.Id,
                Segment = "2",
                Ad = "Ticari Borçlar"
            });
            var saticilar = await hesapPlani.CreateAsync(new HesapPlaniCreateDto
            {
                UstHesapId = grup32!.Id,
                Segment = "0",
                Ad = "Satıcılar",
                HareketGorur = true
            });

            var bankalar = await MuhasebeTestOrtami.HesapAsync(db, "102");
            var akbank = await hesapPlani.CreateAsync(new HesapPlaniCreateDto
            {
                UstHesapId = bankalar.Id,
                Segment = "1",
                Ad = "Akbank",
                HareketGorur = true
            });
            var garanti = await hesapPlani.CreateAsync(new HesapPlaniCreateDto
            {
                UstHesapId = bankalar.Id,
                Segment = "2",
                Ad = "Garanti",
                HareketGorur = true
            });

            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");
            var gider = await MuhasebeTestOrtami.HesapAsync(db, "770");

            var kitap = new Kitap(kasa.Id, bankalar.Id, akbank!.Id, garanti!.Id, gider.Id, saticilar!.Id);

            await fisler.CreateAsync(Fis(new DateTime(2026, 1, 5), true,
                Satir(kitap.Kasa, borc: 20000m), Satir(kitap.Saticilar, alacak: 20000m)));

            await fisler.CreateAsync(Fis(new DateTime(2026, 1, 10), true,
                Satir(kitap.Gider, borc: 1000m), Satir(kitap.Saticilar, alacak: 1000m)));

            await fisler.CreateAsync(Fis(new DateTime(2026, 2, 15), true,
                Satir(kitap.Akbank, borc: 5000m), Satir(kitap.Kasa, alacak: 5000m)));

            await fisler.CreateAsync(Fis(new DateTime(2026, 3, 20), true,
                Satir(kitap.Garanti, borc: 3000m), Satir(kitap.Kasa, alacak: 3000m)));

            await fisler.CreateAsync(Fis(new DateTime(2026, 3, 25), true,
                Satir(kitap.Saticilar, borc: 400m), Satir(kitap.Kasa, alacak: 400m)));

            // Kural 21 için: taslak fiş raporlara girmemeli.
            await fisler.CreateAsync(Fis(new DateTime(2026, 4, 1), false,
                Satir(kitap.Gider, borc: 250m), Satir(kitap.Kasa, alacak: 250m)));

            return kitap;
        }

        private static MizanSatirDto SatirBul(MizanDto mizan, string kod) =>
            mizan.Satirlar.First(s => s.Kod == kod);

        // ---- Kural 20: bakiye yönü karaktere göre ----

        [Theory]
        [InlineData(HesapKarakter.Aktif, 1000, 400, 600)]
        [InlineData(HesapKarakter.Gider, 1000, 400, 600)]
        [InlineData(HesapKarakter.Maliyet, 1000, 400, 600)]
        [InlineData(HesapKarakter.Pasif, 400, 1000, 600)]
        [InlineData(HesapKarakter.Gelir, 400, 1000, 600)]
        public void BakiyeKurali_KarakterinYonunuUyguluyor(HesapKarakter karakter, int borc, int alacak, int beklenen)
        {
            Assert.Equal(beklenen, BakiyeKurali.Bakiye(karakter, borc, alacak));
        }

        [Fact]
        public void BakiyeKurali_TersYondekiKalaniNegatifDonuyor()
        {
            // Aktif hesapta alacak kalanı → negatif bakiye; yön kalanın fiilî tarafını gösterir.
            Assert.Equal(-250m, BakiyeKurali.Bakiye(HesapKarakter.Aktif, 750m, 1000m));
            Assert.Equal(BakiyeYonu.Alacak, BakiyeKurali.Yon(750m, 1000m));
            Assert.Equal(BakiyeYonu.Borc, BakiyeKurali.Yon(1000m, 750m));
            Assert.Equal(BakiyeYonu.Yok, BakiyeKurali.Yon(1000m, 1000m));
        }

        [Fact]
        public async Task AktifHesapta_BakiyeBorcEksiAlacak_PasiftesineTersi()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var kitap = await KitapKurAsync(db);
            var mizan = await Servis(db).GetMizanAsync(new RaporFiltreDto(), null);

            // Kasa (Aktif): 20.000 B − 8.400 A = 11.600 borç kalanı
            var kasa = SatirBul(mizan, "100");
            Assert.Equal(HesapKarakter.Aktif, kasa.Karakter);
            Assert.Equal(20000m, kasa.ToplamBorc);
            Assert.Equal(8400m, kasa.ToplamAlacak);
            Assert.Equal(11600m, kasa.Bakiye);
            Assert.Equal(BakiyeYonu.Borc, kasa.Yon);
            Assert.Equal(11600m, kasa.BorcBakiye);
            Assert.Equal(0m, kasa.AlacakBakiye);

            // Satıcılar (Pasif): 21.000 A − 400 B = 20.600 alacak kalanı
            var saticilar = SatirBul(mizan, "320");
            Assert.Equal(HesapKarakter.Pasif, saticilar.Karakter);
            Assert.Equal(400m, saticilar.ToplamBorc);
            Assert.Equal(21000m, saticilar.ToplamAlacak);
            Assert.Equal(20600m, saticilar.Bakiye);
            Assert.Equal(BakiyeYonu.Alacak, saticilar.Yon);
            Assert.Equal(20600m, saticilar.AlacakBakiye);
            Assert.Equal(0m, saticilar.BorcBakiye);

            // 770 (Gider): borç yönlü
            var gider = SatirBul(mizan, "770");
            Assert.Equal(HesapKarakter.Gider, gider.Karakter);
            Assert.Equal(1000m, gider.Bakiye);
            Assert.NotEqual(0, kitap.Gider);
        }

        // ---- Kural 19: üst hesap bakiyesi alt ağacın toplamı ----

        [Fact]
        public async Task UstHesapBakiyesi_AltAgacinToplaminaEsit()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            await KitapKurAsync(db);
            var mizan = await Servis(db).GetMizanAsync(new RaporFiltreDto(), null);

            var akbank = SatirBul(mizan, "102.01");
            var garanti = SatirBul(mizan, "102.02");
            var bankalar = SatirBul(mizan, "102");
            var kasa = SatirBul(mizan, "100");
            var grup10 = SatirBul(mizan, "10");
            var sinif1 = SatirBul(mizan, "1");

            Assert.Equal(5000m, akbank.ToplamBorc);
            Assert.Equal(3000m, garanti.ToplamBorc);

            // 102 = 102.01 + 102.02
            Assert.Equal(akbank.ToplamBorc + garanti.ToplamBorc, bankalar.ToplamBorc);
            Assert.Equal(akbank.ToplamAlacak + garanti.ToplamAlacak, bankalar.ToplamAlacak);
            Assert.Equal(akbank.Bakiye + garanti.Bakiye, bankalar.Bakiye);

            // 10 = 100 + 102
            Assert.Equal(kasa.ToplamBorc + bankalar.ToplamBorc, grup10.ToplamBorc);
            Assert.Equal(kasa.ToplamAlacak + bankalar.ToplamAlacak, grup10.ToplamAlacak);
            Assert.Equal(kasa.Bakiye + bankalar.Bakiye, grup10.Bakiye);

            // 1 = 10 (sınıfın tek grubu)
            Assert.Equal(grup10.ToplamBorc, sinif1.ToplamBorc);
            Assert.Equal(grup10.ToplamAlacak, sinif1.ToplamAlacak);

            // Kendi hareketi olmayan ara hesap da alt ağacı sayesinde mizanda yer alır.
            Assert.False(bankalar.HareketGorur);
            Assert.Equal(8000m, bankalar.ToplamBorc);
        }

        [Fact]
        public async Task Ekstre_UstHesapIcin_AltAgacinHareketleriniTopluyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var kitap = await KitapKurAsync(db);
            var servis = Servis(db);

            var akbank = await servis.GetEkstreAsync(kitap.Akbank, new RaporFiltreDto());
            var garanti = await servis.GetEkstreAsync(kitap.Garanti, new RaporFiltreDto());
            var bankalar = await servis.GetEkstreAsync(kitap.Bankalar, new RaporFiltreDto());

            Assert.Equal(akbank!.ToplamBorc + garanti!.ToplamBorc, bankalar!.ToplamBorc);
            Assert.Equal(8000m, bankalar.ToplamBorc);
            Assert.Equal(2, bankalar.BorcHareketleri.Count);
            Assert.Empty(bankalar.AlacakHareketleri);
            Assert.Equal(BakiyeYonu.Borc, bankalar.Yon);

            // Satır hangi alt hesaptan geldiğini taşır; satıra tıklanınca fiş açılır.
            Assert.Contains(bankalar.BorcHareketleri, h => h.HesapKod == "102.01" && h.Tutar == 5000m);
            Assert.Contains(bankalar.BorcHareketleri, h => h.HesapKod == "102.02" && h.Tutar == 3000m);
            Assert.All(bankalar.BorcHareketleri, h => Assert.NotEqual(0, h.FisId));
            Assert.All(bankalar.BorcHareketleri, h => Assert.NotEmpty(h.FisNo));
        }

        [Fact]
        public async Task Ekstre_BorcVeAlacakKolonlariniAyiriyor_ToplamVeYonVeriyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var kitap = await KitapKurAsync(db);

            var kasa = await Servis(db).GetEkstreAsync(kitap.Kasa, new RaporFiltreDto());

            Assert.NotNull(kasa);
            Assert.Single(kasa!.BorcHareketleri);
            Assert.Equal(3, kasa.AlacakHareketleri.Count);
            Assert.Equal(20000m, kasa.ToplamBorc);
            Assert.Equal(8400m, kasa.ToplamAlacak);
            Assert.Equal(11600m, kasa.Bakiye);
            Assert.Equal(BakiyeYonu.Borc, kasa.Yon);

            // Başlangıç tarihi verilmediğinde rapor tüm geçmişi kapsar; devir yoktur.
            Assert.Equal(0m, kasa.DevirBorc);
            Assert.Equal(0m, kasa.DevirAlacak);
            Assert.Equal(0m, kasa.DevirBakiye);
            Assert.Equal(kasa.ToplamBorc, kasa.KapanisBorc);
            Assert.Equal(kasa.ToplamAlacak, kasa.KapanisAlacak);

            // Hareketler tarih sırasında gelir.
            var tarihler = kasa.AlacakHareketleri.Select(h => h.Tarih).ToList();
            Assert.Equal(tarihler.OrderBy(t => t).ToList(), tarihler);
        }

        // ---- Devir bakiyesi ----

        /// <summary>Kitaptaki mart dönemi: 20.03 Garanti 3.000 B / Kasa 3.000 A, 25.03 Satıcılar 400 B / Kasa 400 A.</summary>
        private static RaporFiltreDto Mart => new()
        {
            Bas = new DateTime(2026, 3, 1),
            Bit = new DateTime(2026, 3, 31)
        };

        [Fact]
        public async Task Ekstre_DevirBakiyesiVeriyor_KapanisDevirArtiDonemHareketleri()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var kitap = await KitapKurAsync(db);

            var kasa = await Servis(db).GetEkstreAsync(kitap.Kasa, Mart);

            // Devir: 05.01 açılış 20.000 B ve 15.02 5.000 A → dönem başından öncesi.
            Assert.Equal(20000m, kasa!.DevirBorc);
            Assert.Equal(5000m, kasa.DevirAlacak);
            Assert.Equal(15000m, kasa.DevirBakiye);

            // Dönem hareketleri devri içermez.
            Assert.Equal(0m, kasa.ToplamBorc);
            Assert.Equal(3400m, kasa.ToplamAlacak);
            Assert.Empty(kasa.BorcHareketleri);
            Assert.Equal(2, kasa.AlacakHareketleri.Count);

            // Kapanış = devir + dönem.
            Assert.Equal(20000m, kasa.KapanisBorc);
            Assert.Equal(8400m, kasa.KapanisAlacak);
            Assert.Equal(kasa.DevirBorc + kasa.ToplamBorc, kasa.KapanisBorc);
            Assert.Equal(kasa.DevirAlacak + kasa.ToplamAlacak, kasa.KapanisAlacak);
            Assert.Equal(11600m, kasa.Bakiye);
            Assert.Equal(BakiyeYonu.Borc, kasa.Yon);
        }

        [Fact]
        public async Task Ekstre_DevirBakiyesi_KaraktereGoreYonlu()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var kitap = await KitapKurAsync(db);

            // Satıcılar (Pasif): devirde 21.000 alacak, dönemde 400 borç.
            var saticilar = await Servis(db).GetEkstreAsync(kitap.Saticilar, Mart);

            Assert.Equal(HesapKarakter.Pasif, saticilar!.Karakter);
            Assert.Equal(0m, saticilar.DevirBorc);
            Assert.Equal(21000m, saticilar.DevirAlacak);
            Assert.Equal(21000m, saticilar.DevirBakiye);      // Pasif → Alacak − Borç

            Assert.Equal(400m, saticilar.ToplamBorc);
            Assert.Equal(20600m, saticilar.Bakiye);
            Assert.Equal(BakiyeYonu.Alacak, saticilar.Yon);
        }

        [Fact]
        public async Task Ekstre_UstHesabinDeviri_AltAgacinToplami()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var kitap = await KitapKurAsync(db);
            var servis = Servis(db);

            var akbank = await servis.GetEkstreAsync(kitap.Akbank, Mart);
            var garanti = await servis.GetEkstreAsync(kitap.Garanti, Mart);
            var bankalar = await servis.GetEkstreAsync(kitap.Bankalar, Mart);

            // Akbank'ın 15.02 hareketi devre, Garanti'nin 20.03 hareketi döneme düşer.
            Assert.Equal(5000m, akbank!.DevirBorc);
            Assert.Equal(0m, garanti!.DevirBorc);

            Assert.Equal(akbank.DevirBorc + garanti.DevirBorc, bankalar!.DevirBorc);
            Assert.Equal(akbank.DevirAlacak + garanti.DevirAlacak, bankalar.DevirAlacak);
            Assert.Equal(3000m, bankalar.ToplamBorc);
            Assert.Equal(8000m, bankalar.KapanisBorc);
            Assert.Equal(8000m, bankalar.Bakiye);
        }

        [Fact]
        public async Task Ekstre_Devir_TaslakFisleriSaymiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var fisler = FisServisi(db);

            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");
            var banka = await MuhasebeTestOrtami.HesapAsync(db, "102");

            await fisler.CreateAsync(Fis(new DateTime(2026, 1, 10), true,
                Satir(kasa.Id, borc: 1000m), Satir(banka.Id, alacak: 1000m)));

            // Devir tarihinden önce ama taslak → devre girmemeli (kural 21).
            await fisler.CreateAsync(Fis(new DateTime(2026, 1, 15), false,
                Satir(kasa.Id, borc: 500m), Satir(banka.Id, alacak: 500m)));

            await fisler.CreateAsync(Fis(new DateTime(2026, 2, 10), true,
                Satir(kasa.Id, borc: 200m), Satir(banka.Id, alacak: 200m)));

            var ekstre = await Servis(db).GetEkstreAsync(kasa.Id, new RaporFiltreDto
            {
                Bas = new DateTime(2026, 2, 1)
            });

            Assert.Equal(1000m, ekstre!.DevirBorc);
            Assert.Equal(200m, ekstre.ToplamBorc);
            Assert.Equal(1200m, ekstre.KapanisBorc);
            Assert.Equal(1200m, ekstre.Bakiye);
        }

        [Fact]
        public async Task Ekstre_DonemBasindanOncesiYoksa_DevirSifir()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var kitap = await KitapKurAsync(db);

            var kasa = await Servis(db).GetEkstreAsync(kitap.Kasa, new RaporFiltreDto
            {
                Bas = new DateTime(2026, 1, 1)
            });

            Assert.Equal(0m, kasa!.DevirBorc);
            Assert.Equal(0m, kasa.DevirAlacak);
            Assert.Equal(0m, kasa.DevirBakiye);
            Assert.Equal(11600m, kasa.Bakiye);
        }

        [Fact]
        public async Task Ekstre_BulunamayanHesapIcin_NullDonuyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            Assert.Null(await Servis(db).GetEkstreAsync(9999, new RaporFiltreDto()));
        }

        // ---- Mizan genel toplamı ----

        [Fact]
        public async Task Mizanda_GenelBorcToplami_GenelAlacakToplaminaEsit()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            await KitapKurAsync(db);

            var mizan = await Servis(db).GetMizanAsync(new RaporFiltreDto(), null);

            Assert.Equal(29400m, mizan.GenelToplam.ToplamBorc);
            Assert.Equal(mizan.GenelToplam.ToplamBorc, mizan.GenelToplam.ToplamAlacak);
            Assert.Equal(20600m, mizan.GenelToplam.BorcBakiye);
            Assert.Equal(mizan.GenelToplam.BorcBakiye, mizan.GenelToplam.AlacakBakiye);
            Assert.True(mizan.GenelToplam.Dengede);
        }

        // ---- YaprakMi ----

        [Fact]
        public async Task Mizan_YaprakMi_AltHesabiOlmayanlariIsaretliyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            await KitapKurAsync(db);
            var mizan = await Servis(db).GetMizanAsync(new RaporFiltreDto(), null);

            foreach (var kod in new[] { "1", "10", "102", "3", "32", "7", "77" })
                Assert.False(SatirBul(mizan, kod).YaprakMi, $"{kod} yaprak sayılmamalı");

            foreach (var kod in new[] { "100", "102.01", "102.02", "320", "770" })
                Assert.True(SatirBul(mizan, kod).YaprakMi, $"{kod} yaprak olmalı");
        }

        [Fact]
        public async Task Mizan_YaprakMi_HareketGorurdenBagimsiz()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var kitap = await KitapKurAsync(db);

            // Hareketi olan yaprak hesap pasife alınsa da yaprak kalır.
            await new HesapPlaniService(db).PasifeAlAsync(kitap.Akbank);
            var mizan = await Servis(db).GetMizanAsync(new RaporFiltreDto(), null);

            var akbank = SatirBul(mizan, "102.01");
            Assert.True(akbank.YaprakMi);
            Assert.False(akbank.Aktif);

            // 102 hem yaprak değil hem hareket görmüyor; iki bayrak farklı şeyleri anlatır.
            var bankalar = SatirBul(mizan, "102");
            Assert.False(bankalar.YaprakMi);
            Assert.False(bankalar.HareketGorur);
        }

        [Fact]
        public async Task Mizan_YaprakSatirlarinToplami_GenelToplamaEsit()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            await KitapKurAsync(db);
            var mizan = await Servis(db).GetMizanAsync(new RaporFiltreDto(), null);

            // Üst satırlar alt ağaç toplamı taşır; mükerrersiz toplam yalnızca yapraklardadır.
            var yapraklar = mizan.Satirlar.Where(s => s.YaprakMi).ToList();

            Assert.Equal(mizan.GenelToplam.ToplamBorc, yapraklar.Sum(s => s.ToplamBorc));
            Assert.Equal(mizan.GenelToplam.ToplamAlacak, yapraklar.Sum(s => s.ToplamAlacak));
            Assert.Equal(mizan.GenelToplam.BorcBakiye, yapraklar.Sum(s => s.BorcBakiye));
            Assert.Equal(mizan.GenelToplam.AlacakBakiye, yapraklar.Sum(s => s.AlacakBakiye));
        }

        // ---- Kural 21: yalnızca kesinleşmiş fişler; taslaklar ayrı ----

        [Fact]
        public async Task Mizan_TaslakFisleriIcermiyor_AyriOzetteGosteriyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            await KitapKurAsync(db);

            var mizan = await Servis(db).GetMizanAsync(new RaporFiltreDto(), null);

            // Taslak fiş 770'e 250 borç yazıyordu; mizanda görünmemeli.
            Assert.Equal(1000m, SatirBul(mizan, "770").ToplamBorc);
            Assert.Equal(1, mizan.Taslak.FisSayisi);
            Assert.Equal(250m, mizan.Taslak.ToplamBorc);
            Assert.Equal(250m, mizan.Taslak.ToplamAlacak);
        }

        [Fact]
        public async Task Ekstre_TaslakHareketleriAyriListeliyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var kitap = await KitapKurAsync(db);

            var gider = await Servis(db).GetEkstreAsync(kitap.Gider, new RaporFiltreDto());

            Assert.Single(gider!.BorcHareketleri);
            Assert.Equal(1000m, gider.ToplamBorc);

            Assert.Single(gider.TaslakHareketler);
            Assert.Equal(250m, gider.TaslakHareketler[0].Tutar);
            Assert.Equal(250m, gider.Taslak.ToplamBorc);
            Assert.Equal(1, gider.Taslak.FisSayisi);
        }

        [Fact]
        public async Task Taslak_Kesinlesince_MizanaGiriyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var kitap = await KitapKurAsync(db);
            var servis = Servis(db);

            var oncesi = await servis.GetMizanAsync(new RaporFiltreDto(), null);
            Assert.Equal(1000m, SatirBul(oncesi, "770").ToplamBorc);

            var taslak = (await FisServisi(db).GetListeAsync(new FisFiltreDto { Durum = FisDurum.Taslak })).Single();
            await FisServisi(db).KesinlestirAsync(taslak.Id);

            // Kural 18: bakiye saklanmadığı için rapor bir sonraki istekte kendiliğinden değişir.
            var sonrasi = await servis.GetMizanAsync(new RaporFiltreDto(), null);
            Assert.Equal(1250m, SatirBul(sonrasi, "770").ToplamBorc);
            Assert.Equal(0, sonrasi.Taslak.FisSayisi);
            Assert.True(sonrasi.GenelToplam.Dengede);
        }

        // ---- Filtreler ----

        [Fact]
        public async Task Mizan_SeviyeFiltresi_DahaDerinHesaplariElemiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            await KitapKurAsync(db);
            var servis = Servis(db);

            var kebire = await servis.GetMizanAsync(new RaporFiltreDto(), seviye: 3);
            Assert.All(kebire.Satirlar, s => Assert.True(s.Seviye <= 3));
            Assert.DoesNotContain(kebire.Satirlar, s => s.Kod == "102.01");
            Assert.Contains(kebire.Satirlar, s => s.Kod == "102");

            var muavine = await servis.GetMizanAsync(new RaporFiltreDto(), seviye: 4);
            Assert.Contains(muavine.Satirlar, s => s.Kod == "102.01");

            // Seviye filtresi genel toplamı değiştirmez; toplam hareketler üzerinden alınır.
            Assert.Equal(muavine.GenelToplam.ToplamBorc, kebire.GenelToplam.ToplamBorc);
            Assert.True(kebire.GenelToplam.Dengede);
        }

        [Fact]
        public async Task Mizan_TarihAraliginiUyguluyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            await KitapKurAsync(db);

            var mart = await Servis(db).GetMizanAsync(
                new RaporFiltreDto { Bas = new DateTime(2026, 3, 1), Bit = new DateTime(2026, 3, 31) }, null);

            // Aralıkta yalnızca 20.03 (Garanti 3.000) ve 25.03 (Satıcılar 400) fişleri var.
            Assert.Equal(3400m, mart.GenelToplam.ToplamBorc);
            Assert.Equal(3400m, mart.GenelToplam.ToplamAlacak);
            Assert.True(mart.GenelToplam.Dengede);

            Assert.Equal(3000m, SatirBul(mart, "102").ToplamBorc);
            Assert.DoesNotContain(mart.Satirlar, s => s.Kod == "102.01");   // şubat hareketi aralık dışı
            Assert.DoesNotContain(mart.Satirlar, s => s.Kod == "770");      // ocak hareketi aralık dışı
            Assert.Equal(400m, SatirBul(mart, "320").ToplamBorc);
            Assert.Equal(0m, SatirBul(mart, "320").ToplamAlacak);
        }

        // ---- Pasif hesap ----

        [Fact]
        public async Task PasifeAlinanHesap_RaporlardaGorunmeyeDevamEdiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var kitap = await KitapKurAsync(db);

            await new HesapPlaniService(db).PasifeAlAsync(kitap.Akbank);

            var mizan = await Servis(db).GetMizanAsync(new RaporFiltreDto(), null);
            var akbank = SatirBul(mizan, "102.01");

            Assert.False(akbank.Aktif);
            Assert.Equal(5000m, akbank.ToplamBorc);
            Assert.Equal(8000m, SatirBul(mizan, "102").ToplamBorc);
            Assert.True(mizan.GenelToplam.Dengede);
        }

        // ---- Masraf merkezi ----

        [Fact]
        public async Task MasrafMerkeziRaporu_MerkezBazindaTopluyor_HesapKirilimiVeriyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();

            db.MasrafMerkezleri.Add(new MasrafMerkezi { Kod = "01", Ad = "Ev" });
            db.MasrafMerkezleri.Add(new MasrafMerkezi { Kod = "02", Ad = "Araç" });
            await db.SaveChangesAsync();

            var ev = await db.MasrafMerkezleri.FirstAsync(m => m.Kod == "01");
            var arac = await db.MasrafMerkezleri.FirstAsync(m => m.Kod == "02");

            var gider = await MuhasebeTestOrtami.HesapAsync(db, "770");
            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");
            var fisler = FisServisi(db);

            await fisler.CreateAsync(Fis(new DateTime(2026, 2, 1), true,
                Satir(gider.Id, borc: 1200m, masrafMerkeziId: ev.Id),
                Satir(kasa.Id, alacak: 1200m)));

            await fisler.CreateAsync(Fis(new DateTime(2026, 2, 10), true,
                Satir(gider.Id, borc: 800m, masrafMerkeziId: arac.Id),
                Satir(kasa.Id, alacak: 800m)));

            await fisler.CreateAsync(Fis(new DateTime(2026, 2, 20), true,
                Satir(gider.Id, borc: 300m, masrafMerkeziId: ev.Id),
                Satir(kasa.Id, alacak: 300m)));

            var rapor = await Servis(db).GetMasrafMerkeziAsync(new RaporFiltreDto());

            Assert.Equal(2, rapor.Satirlar.Count);

            var evSatir = rapor.Satirlar.First(s => s.Kod == "01");
            Assert.Equal("Ev", evSatir.Ad);
            Assert.Equal(1500m, evSatir.ToplamBorc);
            Assert.Equal(0m, evSatir.ToplamAlacak);
            Assert.Equal(1500m, evSatir.Bakiye);
            Assert.Equal("770", evSatir.Hesaplar.Single().Kod);
            Assert.Equal(1500m, evSatir.Hesaplar.Single().Borc);

            Assert.Equal(800m, rapor.Satirlar.First(s => s.Kod == "02").ToplamBorc);

            // Kasa satırlarında masraf merkezi yok → dağıtılmamış.
            Assert.NotNull(rapor.Dagitilmamis);
            Assert.Equal(2300m, rapor.Dagitilmamis!.ToplamAlacak);
            Assert.Equal(2300m, rapor.ToplamBorc);
            Assert.Equal(2300m, rapor.ToplamAlacak);
        }

        [Fact]
        public async Task MasrafMerkeziRaporu_TaslakFisleriIcermiyor_TarihiUyguluyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();

            db.MasrafMerkezleri.Add(new MasrafMerkezi { Kod = "01", Ad = "Ev" });
            await db.SaveChangesAsync();
            var ev = await db.MasrafMerkezleri.FirstAsync();

            var gider = await MuhasebeTestOrtami.HesapAsync(db, "770");
            var kasa = await MuhasebeTestOrtami.HesapAsync(db, "100");
            var fisler = FisServisi(db);

            await fisler.CreateAsync(Fis(new DateTime(2026, 2, 1), true,
                Satir(gider.Id, borc: 1000m, masrafMerkeziId: ev.Id),
                Satir(kasa.Id, alacak: 1000m)));

            await fisler.CreateAsync(Fis(new DateTime(2026, 5, 1), true,
                Satir(gider.Id, borc: 400m, masrafMerkeziId: ev.Id),
                Satir(kasa.Id, alacak: 400m)));

            await fisler.CreateAsync(Fis(new DateTime(2026, 2, 5), false,
                Satir(gider.Id, borc: 999m, masrafMerkeziId: ev.Id),
                Satir(kasa.Id, alacak: 999m)));

            var subat = await Servis(db).GetMasrafMerkeziAsync(new RaporFiltreDto
            {
                Bas = new DateTime(2026, 2, 1),
                Bit = new DateTime(2026, 2, 28)
            });

            Assert.Equal(1000m, subat.Satirlar.Single().ToplamBorc);
        }

        // ---- Kural 18: bakiye saklanmaz ----

        [Fact]
        public async Task Bakiye_HicbirTablodaSaklanmiyor_HerIsteklteYenidenHesaplaniyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var kitap = await KitapKurAsync(db);
            var servis = Servis(db);

            var oncesi = await servis.GetMizanAsync(new RaporFiltreDto(), null);
            Assert.Equal(11600m, SatirBul(oncesi, "100").Bakiye);

            await FisServisi(db).CreateAsync(Fis(new DateTime(2026, 6, 1), true,
                Satir(kitap.Kasa, borc: 1000m), Satir(kitap.Saticilar, alacak: 1000m)));

            var sonrasi = await servis.GetMizanAsync(new RaporFiltreDto(), null);
            Assert.Equal(12600m, SatirBul(sonrasi, "100").Bakiye);
            Assert.Equal(21600m, SatirBul(sonrasi, "320").Bakiye);
            Assert.True(sonrasi.GenelToplam.Dengede);

            // Hesap planı tablosunda tutulan bir bakiye alanı yok; kaynak yalnızca FisSatir.
            Assert.DoesNotContain(
                db.Model.FindEntityType(typeof(HesapPlani))!.GetProperties(),
                p => p.Name.Contains("Bakiye", StringComparison.OrdinalIgnoreCase));
        }
    }
}
