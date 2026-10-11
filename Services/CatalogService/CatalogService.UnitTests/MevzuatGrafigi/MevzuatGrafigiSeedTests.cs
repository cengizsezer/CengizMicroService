using CatalogService.Api.Features.MevzuatGrafigi;
using CatalogService.Api.Features.MevzuatGrafigi.Domain;
using CatalogService.Api.Features.MevzuatGrafigi.Dtos;
using CatalogService.Api.Features.MevzuatGrafigi.Services;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.BankaEkstre;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;

namespace CatalogService.UnitTests.MevzuatGrafigi
{
    /// <summary>
    /// Mevzuat Grafiği örnek içerik seed'i: mockup verisi kurallara uyar, konumlar mockup'taki
    /// layout() ile aynıdır, seed yalnız kart tablosu boşken ve tek sefer çalışır.
    /// </summary>
    public class MevzuatGrafigiSeedTests
    {
        private static CatalogContext Context() => BankaEkstreTestOrtami.YeniContext();

        /// <summary>Aynı InMemory veritabanına yeni context (servisin yeniden başlaması).</summary>
        private static CatalogContext Context(CatalogContext ayniVeritabani) =>
            BankaEkstreTestOrtami.YeniContext(ayniVeritabani.GetService<IDbContextOptions>()
                .FindExtension<InMemoryOptionsExtension>()!.StoreName);

        /// <summary>
        /// Beklenen konumlar mockup'taki layout() fonksiyonu çalıştırılarak çıkarıldı
        /// (mevzuat-grafigi.html, views.forEach(layout) sonrası v.pos).
        /// </summary>
        private static readonly (string Kart, double X, double Y)[] IhracatKonumlari =
        {
            ("f1", 40, 265), ("f3", 40, 415), ("i1", 330, 115), ("i2", 330, 265), ("i3", 330, 415), ("i4", 330, 565),
            ("s1", 620, 115), ("s2", 620, 265), ("s5", 620, 415), ("d1", 620, 565),
            ("k1", 910, 115), ("k2", 910, 265), ("k5", 910, 415), ("k7", 910, 565),
            ("y1", 1200, 40), ("y2", 1200, 190), ("y3", 1200, 340), ("y4", 1200, 490), ("y5", 1200, 640),
        };

        private static readonly (string Kart, double X, double Y)[] TeknokentKonumlari =
        {
            ("f2", 40, 340), ("i5", 330, 265), ("i2", 330, 415),
            ("s3", 620, 40), ("s4", 620, 190), ("s1", 620, 340), ("d1", 620, 490), ("d2", 620, 640),
            ("k4", 910, 40), ("k8", 910, 190), ("k1", 910, 340), ("k5", 910, 490), ("k6", 910, 640),
            ("y6", 1200, 190), ("y4", 1200, 340), ("y3", 1200, 490),
        };

        private static string Baslik(string anahtar) => MevzuatGrafigiSeed.Kartlar.Single(k => k.Anahtar == anahtar).Baslik;

        [Fact]
        public void Seed_verisi_baglanti_ve_dislama_kurallarina_uyar()
        {
            Assert.Empty(MevzuatGrafigiSeed.Denetle());
        }

        [Fact]
        public void Denetim_kurala_uymayan_kayitlari_tek_tek_raporlar()
        {
            var kartlar = new[]
            {
                ("f", MevzuatKartTuru.Firma, "Örnek A.Ş.", "", ""),
                ("i", MevzuatKartTuru.Islem, "İşlem", "", ""),
                ("s", MevzuatKartTuru.Istisna, "İstisna", "", ""),
                ("k", MevzuatKartTuru.Kanun, "Kanun", "", ""),
            };
            var ihlaller = MevzuatGrafigiSeed.Denetle(
                kartlar,
                new[] { ("f", "i"), ("f", "i"), ("f", "k"), ("i", "x") },
                new[] { ("s", "k", "Not") },
                new[] { ("Tuval", new[] { "f", "f", "y" }) });

            Assert.Contains(ihlaller, m => m == "Tekrar eden bağlantı: f → i");
            Assert.Contains(ihlaller, m => m.StartsWith("f → k: Firma kartı Kanun kartına bağlanamaz."));
            Assert.Contains(ihlaller, m => m == "Tanımsız kart: x");
            Assert.Contains(ihlaller, m => m.StartsWith("s - k: Dışlama yalnız İstisna ve İndirim"));
            Assert.Contains(ihlaller, m => m == "\"Tuval\" tuvalinde kart tekrar ediyor: f");
            Assert.Contains(ihlaller, m => m == "Tanımsız kart: y");
            Assert.Equal(6, ihlaller.Count);
        }

        [Fact]
        public void Yerlesim_mockup_layout_fonksiyonuyla_ayni()
        {
            var tuvaller = MevzuatGrafigiSeed.Tuvaller;
            foreach (var (tuval, beklenen) in new[] { (tuvaller[0], IhracatKonumlari), (tuvaller[1], TeknokentKonumlari) })
            {
                var yer = MevzuatGrafigiSeed.Yerlesim(tuval.Kartlar);
                Assert.Equal(beklenen.Length, yer.Count);
                foreach (var (kart, x, y) in beklenen)
                    Assert.Equal((x, y), yer[kart]);
            }
        }

        [Fact]
        public async Task Bos_tabloya_mockup_icerigini_yazar()
        {
            using var db = Context();

            Assert.Equal(new MevzuatGrafigiSeed.MevzuatSeedSonucu(MevzuatGrafigiSeed.MevzuatSeedIslemi.Tam, 28, 28, 2, 2),
                         await MevzuatGrafigiSeed.SeedAsync(db));

            Assert.Equal(28, await db.MevzuatKartlar.CountAsync());
            Assert.Equal(28, await db.MevzuatBaglantilar.CountAsync());
            Assert.Equal(2, await db.MevzuatDislamalar.CountAsync());
            Assert.Empty(db.MevzuatSartTakipleri);

            var g = await new MevzuatGrafigiService(db).GetAsync();
            Assert.Equal(new[] { ("İhracat & İthalat", 1, 19), ("Teknokent & Ar-Ge", 2, 16) },
                         g.Tuvaller.Select(t => (t.Ad, t.Sira, t.Yerlesimler.Count)).ToArray());

            // Firma kartları örnek kalır.
            Assert.All(g.Kartlar.Where(k => k.Tur == MevzuatKartTuru.Firma), k => Assert.StartsWith("Örnek ", k.Baslik));
            Assert.Equal(3, g.Kartlar.Count(k => k.Tur == MevzuatKartTuru.Firma));

            // Dışlama A < B sıralı (servisle aynı).
            Assert.All(g.Dislamalar, d => Assert.True(d.AKartId < d.BKartId));
        }

        [Fact]
        public async Task Yazilan_konumlar_mockup_ile_ayni()
        {
            using var db = Context();
            await MevzuatGrafigiSeed.SeedAsync(db);
            var g = await new MevzuatGrafigiService(db).GetAsync();
            var basligi = g.Kartlar.ToDictionary(k => k.Id, k => k.Baslik);

            foreach (var (tuvalAdi, beklenen) in new[] { ("İhracat & İthalat", IhracatKonumlari), ("Teknokent & Ar-Ge", TeknokentKonumlari) })
            {
                var yer = g.Tuvaller.Single(t => t.Ad == tuvalAdi).Yerlesimler
                    .ToDictionary(y => basligi[y.KartId], y => (y.X, y.Y));
                foreach (var (kart, x, y) in beklenen)
                    Assert.Equal((x, y), yer[Baslik(kart)]);
            }
        }

        [Fact]
        public async Task Bagli_kayitlar_dogru_kartlari_gosterir()
        {
            using var db = Context();
            await MevzuatGrafigiSeed.SeedAsync(db);
            var g = await new MevzuatGrafigiService(db).GetAsync();
            var basligi = g.Kartlar.ToDictionary(k => k.Id, k => k.Baslik);

            var baglantilar = g.Baglantilar.Select(b => (basligi[b.KaynakKartId], basligi[b.HedefKartId])).ToHashSet();
            Assert.Equal(MevzuatGrafigiSeed.Baglantilar.Select(b => (Baslik(b.Kaynak), Baslik(b.Hedef))).ToHashSet(), baglantilar);

            var dislama = g.Dislamalar.Single(d => d.Not == "Aynı harcama için mükerrer yararlanılmaz");
            Assert.Equal(new[] { "Ar-Ge İndirimi", "Teknokent Kazanç İstisnası" },
                         new[] { basligi[dislama.AKartId], basligi[dislama.BKartId] }.OrderBy(x => x, StringComparer.Ordinal));
        }

        [Fact]
        public async Task Ikinci_calisma_hicbir_sey_eklemez()
        {
            using var db = Context();
            await MevzuatGrafigiSeed.SeedAsync(db);

            Assert.Equal(MevzuatGrafigiSeed.MevzuatSeedSonucu.Atlandi, await MevzuatGrafigiSeed.SeedAsync(db));
            Assert.Equal(28, await db.MevzuatKartlar.CountAsync());
            Assert.Equal(2, await db.MevzuatTuvaller.CountAsync());
        }

        [Fact]
        public async Task Kart_tablosu_doluysa_atlanir()
        {
            using var db = Context();
            await new MevzuatGrafigiService(db).CreateKartAsync(new MevzuatKartSaveDto { Tur = MevzuatKartTuru.Kanun, Baslik = "Kullanıcı kartı" });

            Assert.Equal(MevzuatGrafigiSeed.MevzuatSeedSonucu.Atlandi, await MevzuatGrafigiSeed.SeedAsync(db));
            Assert.Equal(1, await db.MevzuatKartlar.CountAsync());
            Assert.Empty(db.MevzuatTuvaller);
            Assert.Empty(db.MevzuatBaglantilar);
        }

        [Fact]
        public async Task Seed_sonrasi_veri_servis_kurallariyla_duzenlenebilir()
        {
            using var db = Context();
            await MevzuatGrafigiSeed.SeedAsync(db);
            var s = new MevzuatGrafigiService(db);
            var g = await s.GetAsync();
            int Id(string anahtar) => g.Kartlar.Single(k => k.Baslik == Baslik(anahtar)).Id;

            // Seed'deki bağlantı servis açısından da "zaten var".
            var ex = await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() =>
                s.CreateBaglantiAsync(new MevzuatBaglantiSaveDto { KaynakKartId = Id("f1"), HedefKartId = Id("i1") }));
            Assert.Equal("Bu bağlantı zaten var.", ex.Message);

            // Seed'deki dışlama ters yönden de tekrar sayılır.
            await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() =>
                s.CreateDislamaAsync(new MevzuatDislamaSaveDto { AKartId = Id("d1"), BKartId = Id("s3") }));
        }

        // ---- Yarım seed tamamlama ----

        /// <summary>
        /// 2026-10-10'daki yarım seed: yalnız seed kartları yazılmış, üstüne kullanıcı kendi
        /// tuvalini (Sira 1), kartını ve seed dışı bir bağlantıyı eklemiş.
        /// </summary>
        private static async Task YarimSeedKur(CatalogContext db, params string[] haric)
        {
            var simdi = DateTime.Now;
            db.MevzuatKartlar.AddRange(MevzuatGrafigiSeed.Kartlar.Where(k => !haric.Contains(k.Anahtar)).Select(k => new MevzuatKart
            {
                Tur = k.Tur, Baslik = k.Baslik, Madde = k.Madde, Ozet = k.Ozet, OlusturmaTarihi = simdi,
            }));
            var kullanici = new MevzuatKart { Tur = MevzuatKartTuru.Firma, Baslik = "87454", OlusturmaTarihi = simdi };
            db.MevzuatKartlar.Add(kullanici);
            await db.SaveChangesAsync();

            var lime = new MevzuatTuval { Ad = "lime", Sira = 1 };
            db.MevzuatTuvaller.Add(lime);
            var hizmet = await db.MevzuatKartlar.SingleAsync(k => k.Baslik == "Hizmet İhracatı");
            db.MevzuatBaglantilar.Add(new MevzuatBaglanti { KaynakKartId = kullanici.Id, HedefKartId = hizmet.Id });
            await db.SaveChangesAsync();
            db.MevzuatTuvalKartlari.Add(new MevzuatTuvalKarti { TuvalId = lime.Id, KartId = kullanici.Id, X = 1, Y = 2 });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        private static async Task<HashSet<(string, string)>> BaglantiBasliklari(CatalogContext db)
        {
            var b = await db.MevzuatKartlar.ToDictionaryAsync(k => k.Id, k => k.Baslik);
            return (await db.MevzuatBaglantilar.ToListAsync()).Select(x => (b[x.KaynakKartId], b[x.HedefKartId])).ToHashSet();
        }

        [Fact]
        public async Task Yarim_seed_izi_tamamlanir_kart_eklenmez_kullanici_verisi_korunur()
        {
            using var db = Context();
            await YarimSeedKur(db);

            var sonuc = await MevzuatGrafigiSeed.SeedAsync(db);

            Assert.Equal(new MevzuatGrafigiSeed.MevzuatSeedSonucu(MevzuatGrafigiSeed.MevzuatSeedIslemi.Tamamlama, 0, 28, 2, 2, 0), sonuc);
            Assert.Equal(29, await db.MevzuatKartlar.CountAsync());
            Assert.Equal(29, await db.MevzuatBaglantilar.CountAsync()); // 28 seed + kullanıcınınki
            Assert.Equal(2, await db.MevzuatDislamalar.CountAsync());

            var g = await new MevzuatGrafigiService(db).GetAsync();
            Assert.Equal(new[] { ("lime", 1, 1), ("İhracat & İthalat", 2, 19), ("Teknokent & Ar-Ge", 3, 16) },
                         g.Tuvaller.Select(t => (t.Ad, t.Sira, t.Yerlesimler.Count)).ToArray());
            Assert.Equal((1.0, 2.0), g.Tuvaller[0].Yerlesimler.Select(y => (y.X, y.Y)).Single());
            Assert.Contains(("87454", "Hizmet İhracatı"), await BaglantiBasliklari(db));

            // Konumlar tam seed'dekiyle aynı.
            var basligi = g.Kartlar.ToDictionary(k => k.Id, k => k.Baslik);
            var yer = g.Tuvaller.Single(t => t.Ad == "Teknokent & Ar-Ge").Yerlesimler.ToDictionary(y => basligi[y.KartId], y => (y.X, y.Y));
            foreach (var (kart, x, y) in TeknokentKonumlari) Assert.Equal((x, y), yer[Baslik(kart)]);
        }

        [Fact]
        public async Task Tamamlama_bir_kez_calisir_sonra_iz_kalmaz()
        {
            using var db = Context();
            await YarimSeedKur(db);
            Assert.NotNull(await MevzuatGrafigiSeed.YarimSeedKartlariAsync(db));

            Assert.Equal(MevzuatGrafigiSeed.MevzuatSeedIslemi.Tamamlama, (await MevzuatGrafigiSeed.SeedAsync(db)).Islem);

            // Tamamlama tuvalleri ve bağlantıları yazdı: koşulun iki ayağı da artık sağlanmıyor.
            Assert.Null(await MevzuatGrafigiSeed.YarimSeedKartlariAsync(db));
            using var yeniden = Context(db);
            Assert.Equal(MevzuatGrafigiSeed.MevzuatSeedSonucu.Atlandi, await MevzuatGrafigiSeed.SeedAsync(yeniden));
            Assert.Equal(29, await yeniden.MevzuatBaglantilar.CountAsync());
            Assert.Equal(2, await yeniden.MevzuatDislamalar.CountAsync());
            Assert.Equal(3, await yeniden.MevzuatTuvaller.CountAsync());
            Assert.Equal(36, await yeniden.MevzuatTuvalKartlari.CountAsync());
        }

        [Fact]
        public async Task Tamamlama_silinmis_seed_kartini_ve_baglilarini_geri_eklemez()
        {
            using var db = Context();
            await YarimSeedKur(db, "d2"); // kullanıcı "Ar-Ge İndirimi"ni silmiş

            var sonuc = await MevzuatGrafigiSeed.SeedAsync(db);

            Assert.Equal(new MevzuatGrafigiSeed.MevzuatSeedSonucu(MevzuatGrafigiSeed.MevzuatSeedIslemi.Tamamlama, 0, 27, 1, 2, 1), sonuc);
            Assert.False(await db.MevzuatKartlar.AnyAsync(k => k.Baslik == "Ar-Ge İndirimi"));
            Assert.Equal("Aynı kazanç için iki kez yararlanılmaz", (await db.MevzuatDislamalar.SingleAsync()).Not);
            var teknokent = await db.MevzuatTuvaller.Include(t => t.Kartlar).SingleAsync(t => t.Ad == "Teknokent & Ar-Ge");
            Assert.Equal(15, teknokent.Kartlar.Count);
        }

        [Fact]
        public async Task Tek_seed_baglantisi_bile_varsa_tamamlama_calismaz()
        {
            using var db = Context();
            await YarimSeedKur(db);
            // Kullanıcı kendi tuvalinde seed'deki "Hizmet İhracatı → KDV İhracat İstisnası"nı çizmiş.
            var hizmet = await db.MevzuatKartlar.SingleAsync(k => k.Baslik == "Hizmet İhracatı");
            var istisna = await db.MevzuatKartlar.SingleAsync(k => k.Baslik == "KDV İhracat İstisnası");
            db.MevzuatBaglantilar.Add(new MevzuatBaglanti { KaynakKartId = hizmet.Id, HedefKartId = istisna.Id });
            await db.SaveChangesAsync();

            Assert.Equal(MevzuatGrafigiSeed.MevzuatSeedSonucu.Atlandi, await MevzuatGrafigiSeed.SeedAsync(db));
            Assert.Equal(2, await db.MevzuatBaglantilar.CountAsync());
            Assert.Equal(1, await db.MevzuatTuvaller.CountAsync());
        }

        [Fact]
        public async Task Seed_tuvallerinden_biri_adiyla_varsa_tamamlama_calismaz()
        {
            using var db = Context();
            await YarimSeedKur(db);
            db.MevzuatTuvaller.Add(new MevzuatTuval { Ad = "Teknokent & Ar-Ge", Sira = 2 });
            await db.SaveChangesAsync();

            Assert.Equal(MevzuatGrafigiSeed.MevzuatSeedSonucu.Atlandi, await MevzuatGrafigiSeed.SeedAsync(db));
            Assert.Equal(1, await db.MevzuatBaglantilar.CountAsync());
            Assert.Empty(db.MevzuatDislamalar);
        }

        [Fact]
        public async Task Kullanici_seed_baglantisini_sildi_servis_yeniden_basladi_baglanti_geri_gelmedi()
        {
            using var db = Context();
            await MevzuatGrafigiSeed.SeedAsync(db);
            var s = new MevzuatGrafigiService(db);
            var g = await s.GetAsync();
            int Id(string anahtar) => g.Kartlar.Single(k => k.Baslik == Baslik(anahtar)).Id;
            var silinen = g.Baglantilar.Single(b => b.KaynakKartId == Id("f1") && b.HedefKartId == Id("i1"));
            Assert.True(await s.DeleteBaglantiAsync(silinen.Id));

            // Yeniden başlama: aynı veritabanında yeni context, açılış seed'i yeniden çalışır.
            using var yeniden = Context(db);
            Assert.Equal(MevzuatGrafigiSeed.MevzuatSeedSonucu.Atlandi, await MevzuatGrafigiSeed.SeedAsync(yeniden));

            Assert.Equal(27, await yeniden.MevzuatBaglantilar.CountAsync());
            Assert.DoesNotContain((Baslik("f1"), Baslik("i1")), await BaglantiBasliklari(yeniden));
        }

        [Fact]
        public async Task Seed_baglantilari_dislamalari_ve_bir_karti_silinse_de_tuvaller_durdukca_geri_gelmez()
        {
            using var db = Context();
            await MevzuatGrafigiSeed.SeedAsync(db);
            var s = new MevzuatGrafigiService(db);
            var g = await s.GetAsync();
            foreach (var b in g.Baglantilar) await s.DeleteBaglantiAsync(b.Id);
            foreach (var d in g.Dislamalar) await s.DeleteDislamaAsync(d.Id);
            await s.DeleteKartAsync(g.Kartlar.Single(k => k.Baslik == Baslik("y6")).Id);

            using var yeniden = Context(db);
            Assert.Equal(MevzuatGrafigiSeed.MevzuatSeedSonucu.Atlandi, await MevzuatGrafigiSeed.SeedAsync(yeniden));
            Assert.Empty(yeniden.MevzuatBaglantilar);
            Assert.Empty(yeniden.MevzuatDislamalar);
            Assert.Equal(27, await yeniden.MevzuatKartlar.CountAsync());
        }
    }
}
