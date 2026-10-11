using CatalogService.Api.Features.MevzuatGrafigi.Controllers;
using CatalogService.Api.Features.MevzuatGrafigi.Domain;
using CatalogService.Api.Features.MevzuatGrafigi.Dtos;
using CatalogService.Api.Features.MevzuatGrafigi.Services;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.BankaEkstre;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.MevzuatGrafigi
{
    /// <summary>
    /// Mevzuat Grafiği: bağlantı sırası kuralı, tekrar eden bağlantının reddi, kart silinince
    /// temizlik ve yanındaki kurallar (dışlama yönsüzlüğü, şart takip türleri, tuvalden kaldırma).
    /// </summary>
    public class MevzuatGrafigiServiceTests
    {
        private static CatalogContext Context() => BankaEkstreTestOrtami.YeniContext();
        private static MevzuatGrafigiService Servis(CatalogContext db) => new(db);

        private static async Task<int> Kart(MevzuatGrafigiService s, MevzuatKartTuru tur, string baslik)
            => (await s.CreateKartAsync(new MevzuatKartSaveDto { Tur = tur, Baslik = baslik })).Id;

        private static Task<MevzuatBaglantiDto> Bagla(MevzuatGrafigiService s, int a, int b)
            => s.CreateBaglantiAsync(new MevzuatBaglantiSaveDto { KaynakKartId = a, HedefKartId = b });

        // ---- Bağlantı sırası ----

        public static IEnumerable<object[]> IzinliCiftler() => new[]
        {
            new object[] { MevzuatKartTuru.Firma,   MevzuatKartTuru.Islem },
            new object[] { MevzuatKartTuru.Firma,   MevzuatKartTuru.Istisna }, // doğrudan teşvik
            new object[] { MevzuatKartTuru.Firma,   MevzuatKartTuru.Indirim }, // doğrudan teşvik
            new object[] { MevzuatKartTuru.Islem,   MevzuatKartTuru.Istisna },
            new object[] { MevzuatKartTuru.Islem,   MevzuatKartTuru.Indirim },
            new object[] { MevzuatKartTuru.Istisna, MevzuatKartTuru.Kanun },
            new object[] { MevzuatKartTuru.Istisna, MevzuatKartTuru.Sart },
            new object[] { MevzuatKartTuru.Indirim, MevzuatKartTuru.Kanun },
            new object[] { MevzuatKartTuru.Indirim, MevzuatKartTuru.Sart },
        };

        [Fact]
        public void Kural_tablosu_yalniz_dokuz_cifte_izin_verir()
        {
            var izinli = IzinliCiftler().Select(x => ((MevzuatKartTuru)x[0], (MevzuatKartTuru)x[1])).ToHashSet();
            var turler = Enum.GetValues<MevzuatKartTuru>();

            foreach (var k in turler)
                foreach (var h in turler)
                    Assert.Equal(izinli.Contains((k, h)), BaglantiKurali.IzinliMi(k, h));
            Assert.Equal(9, izinli.Count);
        }

        [Theory]
        [InlineData(MevzuatKartTuru.Istisna)]
        [InlineData(MevzuatKartTuru.Indirim)]
        public async Task Firma_dogrudan_tesvike_baglanir_islem_uzerinden_baglantiyla_birlikte(MevzuatKartTuru tesvik)
        {
            using var db = Context();
            var s = Servis(db);
            var f = await Kart(s, MevzuatKartTuru.Firma, "Örnek A.Ş.");
            var i = await Kart(s, MevzuatKartTuru.Islem, "Hizmet İhracatı");
            var t = await Kart(s, tesvik, "Teşvik");
            var y = await Kart(s, MevzuatKartTuru.Sart, "Şart");

            await Bagla(s, f, i);
            await Bagla(s, i, t);
            var dogrudan = await Bagla(s, f, t); // aynı teşvik hem İşlem üzerinden hem doğrudan
            await Bagla(s, t, y);

            Assert.Equal((f, t), (dogrudan.KaynakKartId, dogrudan.HedefKartId));
            Assert.Equal(4, await db.MevzuatBaglantilar.CountAsync());
            var ex = await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() => Bagla(s, f, t));
            Assert.Equal("Bu bağlantı zaten var.", ex.Message);
        }

        [Theory]
        [InlineData(MevzuatKartTuru.Istisna)]
        [InlineData(MevzuatKartTuru.Indirim)]
        public async Task Tesvik_firmaya_ters_yonde_baglanamaz(MevzuatKartTuru tesvik)
        {
            using var db = Context();
            var s = Servis(db);
            var f = await Kart(s, MevzuatKartTuru.Firma, "Örnek A.Ş.");
            var t = await Kart(s, tesvik, "Teşvik");

            var ex = await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() => Bagla(s, t, f));
            Assert.StartsWith($"{BaglantiKurali.Etiket(tesvik)} kartı Firma kartına bağlanamaz.", ex.Message);
        }

        [Theory]
        [MemberData(nameof(IzinliCiftler))]
        public async Task Izinli_sira_kaydedilir(MevzuatKartTuru kaynak, MevzuatKartTuru hedef)
        {
            using var db = Context();
            var s = Servis(db);
            var a = await Kart(s, kaynak, "A");
            var b = await Kart(s, hedef, "B");

            var e = await Bagla(s, a, b);

            Assert.Equal(a, e.KaynakKartId);
            Assert.Equal(b, e.HedefKartId);
            Assert.Equal(1, await db.MevzuatBaglantilar.CountAsync());
        }

        [Theory]
        [InlineData(MevzuatKartTuru.Firma, MevzuatKartTuru.Kanun)]
        [InlineData(MevzuatKartTuru.Firma, MevzuatKartTuru.Sart)]
        [InlineData(MevzuatKartTuru.Firma, MevzuatKartTuru.Firma)]
        [InlineData(MevzuatKartTuru.Islem, MevzuatKartTuru.Firma)]   // ters yön
        [InlineData(MevzuatKartTuru.Islem, MevzuatKartTuru.Sart)]    // basamak atlama
        [InlineData(MevzuatKartTuru.Istisna, MevzuatKartTuru.Indirim)]
        [InlineData(MevzuatKartTuru.Kanun, MevzuatKartTuru.Sart)]    // uç kart çıkış vermez
        [InlineData(MevzuatKartTuru.Sart, MevzuatKartTuru.Istisna)]
        public async Task Sira_disi_baglanti_reddedilir(MevzuatKartTuru kaynak, MevzuatKartTuru hedef)
        {
            using var db = Context();
            var s = Servis(db);
            var a = await Kart(s, kaynak, "A");
            var b = await Kart(s, hedef, "B");

            var ex = await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() => Bagla(s, a, b));

            Assert.False(ex.Bulunamadi);
            Assert.Contains("bağlanamaz", ex.Message);
            Assert.Contains(BaglantiKurali.SiraMetni, ex.Message);
            Assert.Equal(0, await db.MevzuatBaglantilar.CountAsync());
        }

        [Fact]
        public async Task Ret_mesaji_turkce_tur_adlarini_kullanir()
        {
            using var db = Context();
            var s = Servis(db);
            var a = await Kart(s, MevzuatKartTuru.Islem, "Mal İhracatı");
            var b = await Kart(s, MevzuatKartTuru.Sart, "3 Ay İçinde İhraç");

            var ex = await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() => Bagla(s, a, b));

            Assert.StartsWith("İşlem kartı Şart kartına bağlanamaz.", ex.Message);
        }

        [Fact]
        public async Task Kontrolcu_kural_ihlalini_400_ve_mesajla_doner()
        {
            using var db = Context();
            var s = Servis(db);
            var a = await Kart(s, MevzuatKartTuru.Firma, "Örnek A.Ş.");
            var b = await Kart(s, MevzuatKartTuru.Kanun, "KDVK md.11");

            var sonuc = await new MevzuatGrafigiController(s)
                .CreateBaglanti(new MevzuatBaglantiSaveDto { KaynakKartId = a, HedefKartId = b }, default);

            var bad = Assert.IsType<BadRequestObjectResult>(sonuc);
            Assert.Contains("bağlanamaz", bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value) as string);
        }

        [Fact]
        public async Task Olmayan_karta_baglanti_404_doner()
        {
            using var db = Context();
            var s = Servis(db);
            var a = await Kart(s, MevzuatKartTuru.Firma, "Örnek A.Ş.");

            var sonuc = await new MevzuatGrafigiController(s)
                .CreateBaglanti(new MevzuatBaglantiSaveDto { KaynakKartId = a, HedefKartId = 999 }, default);

            Assert.IsType<NotFoundObjectResult>(sonuc);
        }

        // ---- Tekrar ve kendine bağlantı ----

        [Fact]
        public async Task Tekrar_eden_baglanti_reddedilir()
        {
            using var db = Context();
            var s = Servis(db);
            var f = await Kart(s, MevzuatKartTuru.Firma, "Örnek A.Ş.");
            var i = await Kart(s, MevzuatKartTuru.Islem, "Mal İhracatı");
            await Bagla(s, f, i);

            var ex = await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() => Bagla(s, f, i));

            Assert.Equal("Bu bağlantı zaten var.", ex.Message);
            Assert.Equal(1, await db.MevzuatBaglantilar.CountAsync());
        }

        [Fact]
        public async Task Kart_kendine_baglanamaz()
        {
            using var db = Context();
            var s = Servis(db);
            var i = await Kart(s, MevzuatKartTuru.Islem, "Mal İhracatı");

            var ex = await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() => Bagla(s, i, i));

            Assert.Equal("Kart kendine bağlanamaz.", ex.Message);
        }

        // ---- Dışlama ----

        [Fact]
        public async Task Dislama_yonsuzdur_B_A_tekrari_reddedilir()
        {
            using var db = Context();
            var s = Servis(db);
            var a = await Kart(s, MevzuatKartTuru.Istisna, "Teknokent Kazanç İstisnası");
            var b = await Kart(s, MevzuatKartTuru.Indirim, "Ar-Ge İndirimi");

            var d = await s.CreateDislamaAsync(new MevzuatDislamaSaveDto { AKartId = b, BKartId = a, Not = "Mükerrer olmaz" });

            Assert.Equal(Math.Min(a, b), d.AKartId);
            Assert.Equal(Math.Max(a, b), d.BKartId);
            await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(
                () => s.CreateDislamaAsync(new MevzuatDislamaSaveDto { AKartId = a, BKartId = b }));
            Assert.Equal(1, await db.MevzuatDislamalar.CountAsync());
        }

        [Fact]
        public void Dislama_kural_tablosu_yalniz_tesvik_ciftlerine_izin_verir()
        {
            var tesvik = new[] { MevzuatKartTuru.Istisna, MevzuatKartTuru.Indirim };
            var turler = Enum.GetValues<MevzuatKartTuru>();

            foreach (var a in turler)
                foreach (var b in turler)
                    Assert.Equal(tesvik.Contains(a) && tesvik.Contains(b),
                                 DislamaKurali.TesvikMi(a) && DislamaKurali.TesvikMi(b));
        }

        [Theory]
        [InlineData(MevzuatKartTuru.Istisna, MevzuatKartTuru.Istisna)]
        [InlineData(MevzuatKartTuru.Istisna, MevzuatKartTuru.Indirim)]
        [InlineData(MevzuatKartTuru.Indirim, MevzuatKartTuru.Istisna)]
        [InlineData(MevzuatKartTuru.Indirim, MevzuatKartTuru.Indirim)]
        public async Task Tesvik_ciftleri_arasinda_dislama_kaydedilir(MevzuatKartTuru a, MevzuatKartTuru b)
        {
            using var db = Context();
            var s = Servis(db);
            var x = await Kart(s, a, "A");
            var y = await Kart(s, b, "B");

            await s.CreateDislamaAsync(new MevzuatDislamaSaveDto { AKartId = x, BKartId = y, Not = "Mükerrer olmaz" });

            Assert.Equal(1, await db.MevzuatDislamalar.CountAsync());
        }

        [Theory]
        [InlineData(MevzuatKartTuru.Istisna, MevzuatKartTuru.Kanun)]
        [InlineData(MevzuatKartTuru.Indirim, MevzuatKartTuru.Sart)]
        [InlineData(MevzuatKartTuru.Firma, MevzuatKartTuru.Istisna)]
        [InlineData(MevzuatKartTuru.Islem, MevzuatKartTuru.Indirim)]
        [InlineData(MevzuatKartTuru.Kanun, MevzuatKartTuru.Kanun)]
        [InlineData(MevzuatKartTuru.Firma, MevzuatKartTuru.Islem)]
        public async Task Tesvik_disi_kartla_dislama_reddedilir(MevzuatKartTuru a, MevzuatKartTuru b)
        {
            using var db = Context();
            var s = Servis(db);
            var x = await Kart(s, a, "A");
            var y = await Kart(s, b, "B");

            var ex = await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() =>
                s.CreateDislamaAsync(new MevzuatDislamaSaveDto { AKartId = x, BKartId = y }));

            Assert.False(ex.Bulunamadi);
            Assert.Equal(DislamaKurali.RetMesaji(a, b), ex.Message);
            Assert.StartsWith("Dışlama yalnız İstisna ve İndirim kartları arasında kurulabilir", ex.Message);
            Assert.Equal(0, await db.MevzuatDislamalar.CountAsync());
        }

        [Fact]
        public async Task Kontrolcu_tesvik_disi_dislamayi_400_ile_doner()
        {
            using var db = Context();
            var s = Servis(db);
            var a = await Kart(s, MevzuatKartTuru.Istisna, "KDV İhracat İstisnası");
            var b = await Kart(s, MevzuatKartTuru.Kanun, "KDV Kanunu md.11");

            var sonuc = await new MevzuatGrafigiController(s)
                .CreateDislama(new MevzuatDislamaSaveDto { AKartId = a, BKartId = b }, default);

            var bad = Assert.IsType<BadRequestObjectResult>(sonuc);
            Assert.Equal("Dışlama yalnız İstisna ve İndirim kartları arasında kurulabilir; İstisna - Kanun olmaz.",
                         bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value) as string);
        }

        [Theory]
        [InlineData(MevzuatKartTuru.Kanun)]
        [InlineData(MevzuatKartTuru.Sart)]
        [InlineData(MevzuatKartTuru.Islem)]
        [InlineData(MevzuatKartTuru.Firma)]
        public async Task Dislamasi_olan_kartin_turu_tesvik_disina_cikarilamaz(MevzuatKartTuru yeniTur)
        {
            using var db = Context();
            var s = Servis(db);
            var ist = await Kart(s, MevzuatKartTuru.Istisna, "Teknokent Kazanç İstisnası");
            var ind = await Kart(s, MevzuatKartTuru.Indirim, "Ar-Ge İndirimi");
            await s.CreateDislamaAsync(new MevzuatDislamaSaveDto { AKartId = ist, BKartId = ind });

            var ex = await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() =>
                s.UpdateKartAsync(ind, new MevzuatKartSaveDto { Tur = yeniTur, Baslik = "Ar-Ge İndirimi" }));

            Assert.Contains("dışlamaları var", ex.Message);
            Assert.Equal(MevzuatKartTuru.Indirim, (await db.MevzuatKartlar.AsNoTracking().SingleAsync(x => x.Id == ind)).Tur);
        }

        [Fact]
        public async Task Dislamasi_olan_kart_Istisna_ile_Indirim_arasinda_degisebilir()
        {
            using var db = Context();
            var s = Servis(db);
            var ist = await Kart(s, MevzuatKartTuru.Istisna, "Teknokent Kazanç İstisnası");
            var ind = await Kart(s, MevzuatKartTuru.Indirim, "Ar-Ge İndirimi");
            await s.CreateDislamaAsync(new MevzuatDislamaSaveDto { AKartId = ist, BKartId = ind });

            var guncel = await s.UpdateKartAsync(ind, new MevzuatKartSaveDto { Tur = MevzuatKartTuru.Istisna, Baslik = "Ar-Ge İndirimi" });

            Assert.Equal(MevzuatKartTuru.Istisna, guncel!.Tur);
        }

        // ---- Kart silme temizliği ----

        [Fact]
        public async Task Kart_silinince_baglantilari_dislamalari_yerlesimleri_ve_sart_takipleri_silinir()
        {
            using var db = Context();
            var s = Servis(db);
            var f = await Kart(s, MevzuatKartTuru.Firma, "Örnek İhracat A.Ş.");
            var i = await Kart(s, MevzuatKartTuru.Islem, "Mal İhracatı");
            var ist = await Kart(s, MevzuatKartTuru.Istisna, "KDV İhracat İstisnası");
            var ind = await Kart(s, MevzuatKartTuru.Indirim, "Hizmet İhracatı Kazanç İndirimi");
            var k = await Kart(s, MevzuatKartTuru.Kanun, "KDV Kanunu md.11");
            var y = await Kart(s, MevzuatKartTuru.Sart, "Kapanmış Gümrük Beyannamesi");

            await Bagla(s, f, i);
            await Bagla(s, i, ist);     // silinecek kartın gelen bağlantısı
            await Bagla(s, ist, k);     // giden
            await Bagla(s, ist, y);     // giden
            await Bagla(s, i, ind);     // ilgisiz, kalmalı
            await s.CreateDislamaAsync(new MevzuatDislamaSaveDto { AKartId = ind, BKartId = ist });

            var t1 = await s.CreateTuvalAsync(new MevzuatTuvalSaveDto { Ad = "İhracat & İthalat" });
            var t2 = await s.CreateTuvalAsync(new MevzuatTuvalSaveDto { Ad = "Teknokent & Ar-Ge" });
            foreach (var id in new[] { f, i, ist, k })
                await s.TuvaleKartEkleAsync(t1.Id, new MevzuatYerlesimDto { KartId = id, X = 10, Y = 20 });
            await s.TuvaleKartEkleAsync(t2.Id, new MevzuatYerlesimDto { KartId = ist, X = 0, Y = 0 });

            await s.SartTakipKaydetAsync(new MevzuatSartTakipDto { FirmaKartId = f, SartKartId = y, Tamamlandi = true });

            Assert.True(await s.DeleteKartAsync(ist));

            var g = await s.GetAsync();
            Assert.DoesNotContain(g.Kartlar, x => x.Id == ist);
            Assert.Equal(5, g.Kartlar.Count);
            Assert.DoesNotContain(g.Baglantilar, x => x.KaynakKartId == ist || x.HedefKartId == ist);
            Assert.Equal(2, g.Baglantilar.Count);
            Assert.Empty(g.Dislamalar);
            Assert.All(g.Tuvaller, t => Assert.DoesNotContain(t.Yerlesimler, x => x.KartId == ist));
            Assert.Equal(new[] { f, i, k }, g.Tuvaller.Single(t => t.Id == t1.Id).Yerlesimler.Select(x => x.KartId).OrderBy(x => x));
            Assert.Empty(g.Tuvaller.Single(t => t.Id == t2.Id).Yerlesimler);
            // Silinen kart Firma/Şart değil; f-y şart takibi yerinde kalır.
            Assert.Single(g.SartTakipleri);
        }

        [Fact]
        public async Task Firma_ya_da_sart_karti_silinince_sart_takibi_de_silinir()
        {
            using var db = Context();
            var s = Servis(db);
            var f = await Kart(s, MevzuatKartTuru.Firma, "Örnek A.Ş.");
            var y1 = await Kart(s, MevzuatKartTuru.Sart, "3 Ay İçinde İhraç");
            var y2 = await Kart(s, MevzuatKartTuru.Sart, "KDV İade Talebi");
            await s.SartTakipKaydetAsync(new MevzuatSartTakipDto { FirmaKartId = f, SartKartId = y1, Tamamlandi = true });
            await s.SartTakipKaydetAsync(new MevzuatSartTakipDto { FirmaKartId = f, SartKartId = y2, Tamamlandi = false });

            await s.DeleteKartAsync(y1);
            Assert.Equal(y2, (await db.MevzuatSartTakipleri.SingleAsync()).SartKartId);

            await s.DeleteKartAsync(f);
            Assert.Empty(db.MevzuatSartTakipleri);
        }

        [Fact]
        public async Task Olmayan_kart_silinemez()
        {
            using var db = Context();
            Assert.False(await Servis(db).DeleteKartAsync(42));
        }

        // ---- Tuval ----

        [Fact]
        public async Task Tuvalden_kaldirma_yalniz_yerlesimi_siler()
        {
            using var db = Context();
            var s = Servis(db);
            var f = await Kart(s, MevzuatKartTuru.Firma, "Örnek A.Ş.");
            var i = await Kart(s, MevzuatKartTuru.Islem, "Mal İhracatı");
            await Bagla(s, f, i);
            var t1 = await s.CreateTuvalAsync(new MevzuatTuvalSaveDto { Ad = "Bir" });
            var t2 = await s.CreateTuvalAsync(new MevzuatTuvalSaveDto { Ad = "İki" });
            await s.TuvaleKartEkleAsync(t1.Id, new MevzuatYerlesimDto { KartId = i });
            await s.TuvaleKartEkleAsync(t2.Id, new MevzuatYerlesimDto { KartId = i });

            Assert.True(await s.TuvaldenKartKaldirAsync(t1.Id, i));

            Assert.True(await db.MevzuatKartlar.AnyAsync(x => x.Id == i));
            Assert.Equal(1, await db.MevzuatBaglantilar.CountAsync());
            Assert.Equal(t2.Id, (await db.MevzuatTuvalKartlari.SingleAsync()).TuvalId);
        }

        [Fact]
        public async Task Toplu_yerlesim_tuvalde_olmayan_karti_reddeder_ve_hicbirini_yazmaz()
        {
            using var db = Context();
            var s = Servis(db);
            var f = await Kart(s, MevzuatKartTuru.Firma, "Örnek A.Ş.");
            var i = await Kart(s, MevzuatKartTuru.Islem, "Mal İhracatı");
            var t = await s.CreateTuvalAsync(new MevzuatTuvalSaveDto { Ad = "Bir" });
            await s.TuvaleKartEkleAsync(t.Id, new MevzuatYerlesimDto { KartId = f, X = 1, Y = 1 });

            await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() => s.YerlesimGuncelleAsync(t.Id, new()
            {
                new() { KartId = f, X = 100, Y = 200 },
                new() { KartId = i, X = 5, Y = 5 },
            }));
            Assert.Equal(1, (await db.MevzuatTuvalKartlari.AsNoTracking().SingleAsync()).X);

            var sonuc = await s.YerlesimGuncelleAsync(t.Id, new() { new() { KartId = f, X = 100, Y = 200 } });
            Assert.Equal(100, sonuc!.Single().X);
        }

        // ---- Şart takip ve tür değişikliği ----

        [Fact]
        public async Task Sart_takip_yalniz_firma_ve_sart_turuyle_kaydedilir()
        {
            using var db = Context();
            var s = Servis(db);
            var f = await Kart(s, MevzuatKartTuru.Firma, "Örnek A.Ş.");
            var k = await Kart(s, MevzuatKartTuru.Kanun, "KDVK md.11");
            var y = await Kart(s, MevzuatKartTuru.Sart, "KDV İade Talebi");

            await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() =>
                s.SartTakipKaydetAsync(new MevzuatSartTakipDto { FirmaKartId = k, SartKartId = y }));
            await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() =>
                s.SartTakipKaydetAsync(new MevzuatSartTakipDto { FirmaKartId = f, SartKartId = k }));

            await s.SartTakipKaydetAsync(new MevzuatSartTakipDto { FirmaKartId = f, SartKartId = y, Tamamlandi = true });
            await s.SartTakipKaydetAsync(new MevzuatSartTakipDto { FirmaKartId = f, SartKartId = y, Tamamlandi = false });

            var takip = await db.MevzuatSartTakipleri.SingleAsync();
            Assert.False(takip.Tamamlandi);
        }

        [Fact]
        public async Task Bagli_kartin_turu_sirayi_bozacak_sekilde_degistirilemez()
        {
            using var db = Context();
            var s = Servis(db);
            var f = await Kart(s, MevzuatKartTuru.Firma, "Örnek A.Ş.");
            var i = await Kart(s, MevzuatKartTuru.Islem, "Mal İhracatı");
            await Bagla(s, f, i);

            await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() =>
                s.UpdateKartAsync(i, new MevzuatKartSaveDto { Tur = MevzuatKartTuru.Kanun, Baslik = "Mal İhracatı" }));
            Assert.Equal(MevzuatKartTuru.Islem, (await db.MevzuatKartlar.AsNoTracking().SingleAsync(x => x.Id == i)).Tur);

            // Firma → İşlem bağlantısındaki İşlem artık teşvike çevrilebilir (Firma → teşvik izinli)...
            var istisna = await s.UpdateKartAsync(i, new MevzuatKartSaveDto { Tur = MevzuatKartTuru.Istisna, Baslik = "Mal İhracatı" });
            Assert.Equal(MevzuatKartTuru.Istisna, istisna!.Tur);
            var indirim = await s.UpdateKartAsync(i, new MevzuatKartSaveDto { Tur = MevzuatKartTuru.Indirim, Baslik = "Mal İhracatı" });
            Assert.Equal(MevzuatKartTuru.Indirim, indirim!.Tur);
            // ...ama Şart'a çevrilemez (Firma → Şart yok); mesaj yeni sırayı söyler.
            var ex = await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() =>
                s.UpdateKartAsync(i, new MevzuatKartSaveDto { Tur = MevzuatKartTuru.Sart, Baslik = "Mal İhracatı" }));
            Assert.Contains("Firma → İşlem veya doğrudan İstisna / İndirim → Kanun / Şart", ex.Message);

            // Bağlantısız kartın türü serbestçe değişir.
            var bos = await Kart(s, MevzuatKartTuru.Kanun, "Yeni");
            var guncel = await s.UpdateKartAsync(bos, new MevzuatKartSaveDto { Tur = MevzuatKartTuru.Sart, Baslik = "Yeni" });
            Assert.Equal(MevzuatKartTuru.Sart, guncel!.Tur);
        }

        [Fact]
        public async Task Firmaya_dogrudan_bagli_tesvik_firma_disi_kaynaga_gore_tur_degistiremez()
        {
            using var db = Context();
            var s = Servis(db);
            var f = await Kart(s, MevzuatKartTuru.Firma, "Örnek A.Ş.");
            var t = await Kart(s, MevzuatKartTuru.Istisna, "KDV İhracat İstisnası");
            var k = await Kart(s, MevzuatKartTuru.Kanun, "KDVK md.11");
            await Bagla(s, f, t);
            await Bagla(s, t, k);

            // İşlem'e çevirmek Firma → İşlem'i tutar ama İşlem → Kanun'u bozar.
            await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() =>
                s.UpdateKartAsync(t, new MevzuatKartSaveDto { Tur = MevzuatKartTuru.Islem, Baslik = "KDV İhracat İstisnası" }));
            // Kanun'a çevirmek Firma → Kanun yaratır.
            await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() =>
                s.UpdateKartAsync(t, new MevzuatKartSaveDto { Tur = MevzuatKartTuru.Kanun, Baslik = "KDV İhracat İstisnası" }));
            // İstisna ↔ İndirim serbest.
            var g = await s.UpdateKartAsync(t, new MevzuatKartSaveDto { Tur = MevzuatKartTuru.Indirim, Baslik = "KDV İhracat İstisnası" });
            Assert.Equal(MevzuatKartTuru.Indirim, g!.Tur);
        }

        [Fact]
        public async Task Gecersiz_tur_ve_bos_baslik_reddedilir()
        {
            using var db = Context();
            var s = Servis(db);

            await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() =>
                s.CreateKartAsync(new MevzuatKartSaveDto { Tur = 0, Baslik = "X" }));
            await Assert.ThrowsAsync<MevzuatGrafigiHatasi>(() =>
                s.CreateKartAsync(new MevzuatKartSaveDto { Tur = MevzuatKartTuru.Firma, Baslik = "  " }));
        }
    }
}
