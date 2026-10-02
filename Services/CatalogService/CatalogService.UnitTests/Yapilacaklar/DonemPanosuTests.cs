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
    /// Dönem panosu (Prompt 10). Sabit tarih 28.09.2026 — <see cref="YapilacaklarTests"/> ile aynı
    /// ortam ve aynı seed (son günü geçmiş takvim satırları pasif).
    /// </summary>
    public class DonemPanosuTests
    {
        private static readonly DateTime Bugun = new(2026, 9, 28);
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
                new FirmaSicilBilgisi { Id = 1, FirmaId = Dgr,
                    MukellefiyetTurleri = "0003 - MUHTASAR, 0015 - KATMA DEĞER VERGİSİ, 0033 - GEÇİCİ VERGİ, 0040 - DAMGA VERGİSİ" },
                new FirmaSicilBilgisi { Id = 2, FirmaId = Cubic, MukellefiyetTurleri = "0003 - MUHTASAR" });
            db.SaveChanges();

            VergiTakvimiSeed.SeedAsync(db, Bugun).GetAwaiter().GetResult();
            return db;
        }

        private static SahteSaat Saat() => new(new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero));

        private static YapilacaklarService Isler(CatalogContext db)
            => new(db, Saat(), new SabitKullanici(Ben),
                   new FirmaOlayYazici(db, new SabitKullanici(Ben), NullLogger<FirmaOlayYazici>.Instance));

        private static DonemPanosuService Pano(CatalogContext db) => new(db, Saat(), Isler(db), new SabitKullanici(Ben));

        private static FirmaIsi OzelIs(int firmaId, string baslik, IsTekrari tekrar, DateTime eklenme) => new()
        {
            FirmaId = firmaId, Baslik = baslik, Tekrar = tekrar, GunKurali = IsGunKurali.DonemSonu,
            OlusturmaZamani = DateTime.SpecifyKind(eklenme, DateTimeKind.Utc)
        };

        private static IsIsaretDto Hucre(DonemPanosuDto pano, int firmaId, string kolonBasligi)
        {
            var i = pano.Kolonlar.FindIndex(k => k.Baslik == kolonBasligi);
            var h = pano.Satirlar.Single(s => s.FirmaId == firmaId).Hucreler[i];
            return new IsIsaretDto { KaynakTip = h.KaynakTip, KaynakId = h.KaynakId, FirmaId = firmaId, DonemAnahtari = h.DonemAnahtari };
        }

        private static DonemHucreDurumu Durum(DonemPanosuDto pano, int firmaId, string kolonBasligi)
        {
            var i = pano.Kolonlar.FindIndex(k => k.Baslik == kolonBasligi);
            return i < 0 ? DonemHucreDurumu.Yok : pano.Satirlar.Single(s => s.FirmaId == firmaId).Hucreler[i].Durum;
        }

        private static DonemEkiOlusturDto Ek(IsIsaretDto h, string ad, int fileId = 700) => new()
        {
            KaynakTip = h.KaynakTip, KaynakId = h.KaynakId, FirmaId = h.FirmaId, DonemAnahtari = h.DonemAnahtari,
            FileId = fileId, DosyaAdi = ad, ContentType = "image/png", Boyut = 2048
        };

        // ---- Dönem kuralları ----

        [Theory]
        [InlineData(2026, 9, 28, 2026, 8)]
        [InlineData(2027, 1, 5, 2026, 12)]
        public void Varsayilan_donem_bir_onceki_ay(int y, int a, int g, int beklenenYil, int beklenenAy)
            => Assert.Equal((beklenenYil, beklenenAy), DonemPanosuService.VarsayilanDonem(new DateTime(y, a, g)));

        [Theory]
        [InlineData(8, "2026-08")]
        [InlineData(9, "2026-09,2026-Q3")]
        [InlineData(12, "2026-12,2026-Q4,2026")]
        public void Ayda_kapanan_donemler(int ay, string beklenen)
            => Assert.Equal(beklenen, string.Join(",", IsDonemi.AydaKapananlar(2026, ay).Select(d => d.Anahtar)));

        [Fact]
        public async Task Uc_ayda_bir_is_yalniz_ceyrek_sonu_ayinda_hucre_gosterir()
        {
            using var db = Context();
            db.FirmaIsleri.Add(OzelIs(Dgr, "Geçici vergi hazırlığı", IsTekrari.UcAylik, new DateTime(2026, 7, 2)));
            await db.SaveChangesAsync();
            var pano = Pano(db);

            var agustos = await pano.PanoAsync(2026, 8);
            var eylul = await pano.PanoAsync(2026, 9);

            Assert.DoesNotContain(agustos.Kolonlar, k => k.Baslik == "Geçici vergi hazırlığı");
            Assert.DoesNotContain(agustos.Kolonlar, k => k.MukellefiyetKodu == "0033");      // yasal geçici vergi de çeyrekte
            Assert.Equal(DonemHucreDurumu.Eksik, Durum(eylul, Dgr, "Geçici vergi hazırlığı"));
            Assert.Contains(eylul.Kolonlar, k => k.MukellefiyetKodu == "0033");
            Assert.Equal("2026-Q3", Hucre(eylul, Dgr, "Geçici vergi hazırlığı").DonemAnahtari);
        }

        [Fact]
        public async Task Is_eklenmeden_onceki_donem_eksik_degil_yok()
        {
            using var db = Context();
            db.FirmaIsleri.Add(OzelIs(Dgr, "Stok sayımı", IsTekrari.Aylik, new DateTime(2026, 7, 15)));
            await db.SaveChangesAsync();
            var pano = Pano(db);

            Assert.DoesNotContain((await pano.PanoAsync(2026, 6)).Kolonlar, k => k.Baslik == "Stok sayımı");
            Assert.Equal(DonemHucreDurumu.Eksik, Durum(await pano.PanoAsync(2026, 8), Dgr, "Stok sayımı"));
        }

        // ---- Matris ----

        [Fact]
        public async Task Yapildi_eksik_ve_bu_firmada_yok_uc_ayri_durum()
        {
            using var db = Context();
            db.FirmaIsleri.Add(OzelIs(Dgr, "Rapor gönderimi", IsTekrari.Aylik, new DateTime(2026, 7, 1)));
            await db.SaveChangesAsync();
            var pano = await Pano(db).PanoAsync(2026, 8);

            var kdv = pano.Kolonlar.Single(k => k.MukellefiyetKodu == "0015").Baslik;
            await Isler(db).IsaretleAsync(new IsIsaretleDto { Yapildi = true, Isler = { Hucre(pano, Dgr, kdv) } });
            pano = await Pano(db).PanoAsync(2026, 8);

            Assert.Equal(DonemHucreDurumu.Yapildi, Durum(pano, Dgr, kdv));
            Assert.Equal(DonemHucreDurumu.Yok, Durum(pano, Cubic, kdv));                 // CUBIC'te KDV yok
            Assert.Equal(DonemHucreDurumu.Eksik, Durum(pano, Dgr, "Rapor gönderimi"));
            Assert.Equal(DonemHucreDurumu.Yok, Durum(pano, Cubic, "Rapor gönderimi"));   // eksik DEĞİL
        }

        [Fact]
        public async Task Ust_serit_sayilari_matristen_gelir()
        {
            using var db = Context();
            db.FirmaIsleri.Add(OzelIs(Dgr, "Fatura girişi", IsTekrari.Aylik, new DateTime(2026, 7, 1)));
            db.FirmaIsleri.Add(OzelIs(Cubic, "Fatura girişi", IsTekrari.Aylik, new DateTime(2026, 7, 1)));
            await db.SaveChangesAsync();
            var pano = await Pano(db).PanoAsync(2026, 8);
            await Isler(db).IsaretleAsync(new IsIsaretleDto { Yapildi = true, Isler = { Hucre(pano, Cubic, "Fatura girişi") } });
            pano = await Pano(db).PanoAsync(2026, 8);

            var hucreler = pano.Satirlar.SelectMany(s => s.Hucreler).ToList();
            Assert.Equal(hucreler.Count(h => h.Durum != DonemHucreDurumu.Yok), pano.Toplam);
            Assert.Equal(hucreler.Count(h => h.Durum == DonemHucreDurumu.Yapildi), pano.Yapilan);
            Assert.Equal(pano.Toplam - pano.Yapilan, pano.Eksikler.Sum(e => e.FirmaSayisi));

            // Aynı başlıklı özel iş iki firmada tek kolon; eksik özeti işe göre.
            Assert.Single(pano.Kolonlar, k => k.Baslik == "Fatura girişi");
            Assert.Equal(1, pano.Eksikler.Single(e => e.Baslik == "Fatura girişi").FirmaSayisi);
            Assert.Equal("Ağustos 2026", pano.Etiket);
        }

        // ---- Hücre paneli ----

        [Fact]
        public async Task Not_ve_kanit_doneme_baglanir()
        {
            using var db = Context();
            db.FirmaIsleri.Add(OzelIs(Dgr, "Banka işleme", IsTekrari.Aylik, new DateTime(2026, 7, 1)));
            await db.SaveChangesAsync();
            var pano = Pano(db);
            var h = Hucre(await pano.PanoAsync(2026, 8), Dgr, "Banka işleme");

            await Isler(db).IsaretleAsync(new IsIsaretleDto { Yapildi = true, Isler = { h } });
            await pano.NotKaydetAsync(new DonemNotuDto
            {
                KaynakTip = h.KaynakTip, KaynakId = h.KaynakId, FirmaId = Dgr, DonemAnahtari = h.DonemAnahtari,
                Not = "Garanti ekstresi müşteriden geç geldi."
            });
            await pano.EkEkleAsync(Ek(h, "ekran-goruntusu.png"));
            await pano.EkEkleAsync(Ek(h, "Ağustos.xlsx", 701));
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => pano.EkEkleAsync(Ek(h, "virus.exe")));

            var hucre = await pano.HucreAsync(h);
            Assert.True(hucre.Yapildi);
            Assert.Equal("Garanti ekstresi müşteriden geç geldi.", hucre.Not);
            Assert.Equal(new[] { "ekran-goruntusu.png", "Ağustos.xlsx" }, hucre.Ekler.Select(e => e.DosyaAdi));
            Assert.Equal("DGR · Ağustos 2026 · Banka işleme", $"{hucre.FirmaAdi} · {hucre.DonemEtiketi} · {hucre.Baslik}");

            var matris = await pano.PanoAsync(2026, 8);
            var i = matris.Kolonlar.FindIndex(k => k.Baslik == "Banka işleme");
            Assert.Equal(2, matris.Satirlar.Single(s => s.FirmaId == Dgr).Hucreler[i].EkSayisi);  // köşede ataç
        }

        [Fact]
        public async Task Gecen_donem_notu_ve_eki_sonraki_ayda_gorunur_yoksa_cikmaz()
        {
            using var db = Context();
            db.FirmaIsleri.Add(OzelIs(Dgr, "Gelir tablosu", IsTekrari.Aylik, new DateTime(2026, 7, 1)));
            await db.SaveChangesAsync();
            var pano = Pano(db);

            var agustos = Hucre(await pano.PanoAsync(2026, 8), Dgr, "Gelir tablosu");
            var eylul = Hucre(await pano.PanoAsync(2026, 9), Dgr, "Gelir tablosu");

            // Temmuz'da kayıt yok → Ağustos panelinde "Geçen dönem" bölümü çıkmaz.
            Assert.Null((await pano.HucreAsync(agustos)).GecenDonem);

            await Isler(db).IsaretleAsync(new IsIsaretleDto { Yapildi = true, Isler = { agustos } });
            Assert.Null((await pano.HucreAsync(eylul)).GecenDonem);          // işaret var ama not/ek yok

            await pano.NotKaydetAsync(new DonemNotuDto
            {
                KaynakTip = agustos.KaynakTip, KaynakId = agustos.KaynakId, FirmaId = Dgr, DonemAnahtari = agustos.DonemAnahtari,
                Not = "Luca'dan gelir tablosunu alıp şablonun 2. sayfasına yapıştırdım. Kur satırını elle güncelle."
            });
            await pano.EkEkleAsync(Ek(agustos, "sablon.xlsx"));

            var gecen = (await pano.HucreAsync(eylul)).GecenDonem;
            Assert.NotNull(gecen);
            Assert.Equal("Ağustos 2026", gecen!.DonemEtiketi);
            Assert.StartsWith("Luca'dan gelir tablosunu", gecen.Not);
            Assert.Equal("sablon.xlsx", Assert.Single(gecen.Ekler).DosyaAdi);
        }

        [Fact]
        public async Task Yasal_iste_gecen_donem_onceki_takvim_satirindan()
        {
            using var db = Context();
            var pano = Pano(db);
            var eylulPano = await pano.PanoAsync(2026, 9);
            var kdvBaslik = eylulPano.Kolonlar.Single(k => k.MukellefiyetKodu == "0015").Baslik;
            var agustos = Hucre(await pano.PanoAsync(2026, 8), Dgr, kdvBaslik);
            var eylul = Hucre(eylulPano, Dgr, kdvBaslik);

            Assert.NotEqual(agustos.KaynakId, eylul.KaynakId);   // her dönemin ayrı takvim satırı

            await Isler(db).IsaretleAsync(new IsIsaretleDto { Yapildi = true, Isler = { agustos } });
            await pano.NotKaydetAsync(new DonemNotuDto
            {
                KaynakTip = IsKaynagi.Yasal, KaynakId = agustos.KaynakId, FirmaId = Dgr, DonemAnahtari = agustos.DonemAnahtari,
                Not = "İndirimli oran satırı ayrı girildi."
            });

            Assert.Equal("İndirimli oran satırı ayrı girildi.", (await pano.HucreAsync(eylul)).GecenDonem?.Not);
        }

        // ---- İşaret geri alma ----

        [Fact]
        public async Task Isaret_kaldirma_kaniti_korur_kayit_silme_hepsini_ve_dosyalari_goturur()
        {
            using var db = Context();
            db.FirmaIsleri.Add(OzelIs(Dgr, "Bordro", IsTekrari.Aylik, new DateTime(2026, 7, 1)));
            await db.SaveChangesAsync();
            var pano = Pano(db);
            var isler = Isler(db);
            var h = Hucre(await pano.PanoAsync(2026, 8), Dgr, "Bordro");

            // Prosedür eki (Prompt 8) — dönem işlemlerinden etkilenmemeli.
            await isler.EkEkleAsync(Dgr, h.KaynakId, new FirmaIsiEkiOlusturDto { FileId = 900, DosyaAdi = "menu.png", Boyut = 10 });

            await isler.IsaretleAsync(new IsIsaretleDto { Yapildi = true, Isler = { h } });
            await pano.EkEkleAsync(Ek(h, "bordro.pdf", 801));
            await pano.NotKaydetAsync(new DonemNotuDto
            {
                KaynakTip = h.KaynakTip, KaynakId = h.KaynakId, FirmaId = Dgr, DonemAnahtari = h.DonemAnahtari, Not = "gönderildi"
            });

            // İşaret kaldırma (kart, Yapılacaklar ya da pano): yalnız zaman boşalır, not ve kanıt KALIR.
            var silinen = await isler.IsaretleAsync(new IsIsaretleDto { Yapildi = false, Isler = { h } });

            Assert.Empty(silinen);
            var kayit = await db.IsTamamlamalari.AsNoTracking().SingleAsync();
            Assert.Null(kayit.TamamlanmaZamani);
            Assert.Equal("gönderildi", kayit.Not);
            Assert.Equal(1, await db.IsTamamlamaEkleri.CountAsync());
            Assert.Equal(DonemHucreDurumu.Eksik, Durum(await pano.PanoAsync(2026, 8), Dgr, "Bordro"));

            // "Bu dönem kaydını sil": hepsi gider, dosya kimlikleri FileApi için döner.
            var dosyalar = await pano.KayitSilAsync(h);

            Assert.Equal(new[] { 801 }, dosyalar);
            Assert.Equal(0, await db.IsTamamlamalari.CountAsync());
            Assert.Equal(0, await db.IsTamamlamaEkleri.CountAsync());
            Assert.Equal(900, (await db.FirmaIsiEkleri.SingleAsync()).FileId);   // prosedür eki yerinde
        }

        // ---- Prompt 10B: geçmiş dönemler ve eksik döneme not ----

        [Fact]
        public async Task Haziran_2026_pasif_takvim_satirlari_panoda_hucre_uretir()
        {
            using var db = Context();
            Assert.False((await db.VergiTakvimi.SingleAsync(t => t.MukellefiyetKodu == "0015" && t.Yil == 2026 && t.DonemNo == 6)).Aktif);

            var haziran = await Pano(db).PanoAsync(2026, 6);

            var kdv = haziran.Kolonlar.Single(k => k.MukellefiyetKodu == "0015").Baslik;
            Assert.Equal(DonemHucreDurumu.Eksik, Durum(haziran, Dgr, kdv));         // "—" değil
            Assert.Equal(DonemHucreDurumu.Yok, Durum(haziran, Cubic, kdv));         // CUBIC'te KDV hâlâ yok
            Assert.Contains(haziran.Kolonlar, k => k.MukellefiyetKodu == "0033");   // Q2 geçici vergi haziranda
        }

        [Fact]
        public async Task Yapilacaklarda_gecmis_pasif_donemler_hala_is_uretmez()
        {
            using var db = Context();

            var kart = await Isler(db).FirmaKartiAsync(Dgr);

            // KDV kuyruğu Ağustos'tan başlar (Temmuz ve öncesi pasif); gecikmiş iş yok.
            Assert.Equal(0, kart.GeciktiSayisi);
            Assert.DoesNotContain(kart.Yasal, s => s.DonemAnahtari == "2026-06");
        }

        [Fact]
        public async Task Isaretsiz_doneme_not_ve_kanit_yazilir_eksik_sayilir_ayirt_edilir()
        {
            using var db = Context();
            db.FirmaIsleri.Add(OzelIs(Dgr, "Rapor", IsTekrari.Aylik, new DateTime(2026, 7, 1)));
            await db.SaveChangesAsync();
            var pano = Pano(db);
            var once = await pano.PanoAsync(2026, 8);
            var h = Hucre(once, Dgr, "Rapor");

            await pano.NotKaydetAsync(new DonemNotuDto
            {
                KaynakTip = h.KaynakTip, KaynakId = h.KaynakId, FirmaId = Dgr, DonemAnahtari = h.DonemAnahtari,
                Not = "Ağustos'ta rapor gitmedi, müşteri veriyi geç verdi."
            });
            await pano.EkEkleAsync(Ek(h, "musteri-maili.png", 811));

            var kayit = await db.IsTamamlamalari.AsNoTracking().SingleAsync();
            Assert.Null(kayit.TamamlanmaZamani);                                   // yapılmış sayılmadı

            var sonra = await pano.PanoAsync(2026, 8);
            var i = sonra.Kolonlar.FindIndex(k => k.Baslik == "Rapor");
            var hucre = sonra.Satirlar.Single(s => s.FirmaId == Dgr).Hucreler[i];
            Assert.Equal(DonemHucreDurumu.Eksik, hucre.Durum);
            Assert.True(hucre.NotVar);
            Assert.Equal(1, hucre.EkSayisi);
            Assert.Equal(once.Yapilan, sonra.Yapilan);                             // not yazmak işi yapmış saymaz
            Assert.Equal(sonra.Toplam - sonra.Yapilan, sonra.Eksikler.Sum(e => e.FirmaSayisi));

            var panel = await pano.HucreAsync(h);
            Assert.False(panel.Yapildi);
            Assert.True(panel.KayitVar);
            Assert.StartsWith("Ağustos'ta rapor gitmedi", panel.Not);

            // Yapılacaklar ve prosedür geçmişi: hâlâ yapılmadı.
            Assert.Contains((await Isler(db).FirmaKartiAsync(Dgr)).Ozel, s => s.DonemAnahtari == "2026-08" && !s.Tamamlandi);
            var gecmis = await Isler(db).ProsedurAsync(Dgr, IsKaynagi.Ozel, h.KaynakId, tumGecmis: true);
            Assert.True(gecmis.Gecmis.Single(g => g.DonemAnahtari == "2026-08").Yapilmadi);
        }

        [Fact]
        public async Task Sonradan_isaretlenince_not_ve_kanit_ayni_kayitta_kalir()
        {
            using var db = Context();
            db.FirmaIsleri.Add(OzelIs(Dgr, "Rapor", IsTekrari.Aylik, new DateTime(2026, 7, 1)));
            await db.SaveChangesAsync();
            var pano = Pano(db);
            var h = Hucre(await pano.PanoAsync(2026, 8), Dgr, "Rapor");
            await pano.NotKaydetAsync(new DonemNotuDto
            {
                KaynakTip = h.KaynakTip, KaynakId = h.KaynakId, FirmaId = Dgr, DonemAnahtari = h.DonemAnahtari, Not = "geç geldi"
            });
            await pano.EkEkleAsync(Ek(h, "kanit.pdf", 812));
            var id = (await db.IsTamamlamalari.AsNoTracking().SingleAsync()).Id;

            await Isler(db).IsaretleAsync(new IsIsaretleDto { Yapildi = true, Isler = { h } });

            var kayit = await db.IsTamamlamalari.AsNoTracking().SingleAsync();
            Assert.Equal(id, kayit.Id);
            Assert.NotNull(kayit.TamamlanmaZamani);
            Assert.Equal("geç geldi", kayit.Not);
            Assert.Equal(1, await db.IsTamamlamaEkleri.CountAsync());
            Assert.Equal(DonemHucreDurumu.Yapildi, Durum(await pano.PanoAsync(2026, 8), Dgr, "Rapor"));
        }

        [Fact]
        public async Task Not_bosaltilinca_kanitsiz_yapilmamis_kayit_kalmaz()
        {
            using var db = Context();
            db.FirmaIsleri.Add(OzelIs(Dgr, "Rapor", IsTekrari.Aylik, new DateTime(2026, 7, 1)));
            await db.SaveChangesAsync();
            var pano = Pano(db);
            var h = Hucre(await pano.PanoAsync(2026, 8), Dgr, "Rapor");
            DonemNotuDto Not(string? metin) => new()
            {
                KaynakTip = h.KaynakTip, KaynakId = h.KaynakId, FirmaId = Dgr, DonemAnahtari = h.DonemAnahtari, Not = metin
            };

            await pano.NotKaydetAsync(Not("geçici"));
            Assert.Equal(1, await db.IsTamamlamalari.CountAsync());

            await pano.NotKaydetAsync(Not("  "));
            Assert.Equal(0, await db.IsTamamlamalari.CountAsync());

            // Notsuz kanıt silinince de boş kayıt kalmaz.
            var ek = await pano.EkEkleAsync(Ek(h, "a.png", 813));
            Assert.Equal(813, await pano.EkSilAsync(ek.Id));
            Assert.Equal(0, await db.IsTamamlamalari.CountAsync());
        }

        [Fact]
        public async Task Is_silinince_donem_kanitlarinin_dosyalari_da_doner()
        {
            using var db = Context();
            db.FirmaIsleri.Add(OzelIs(Dgr, "Bordro", IsTekrari.Aylik, new DateTime(2026, 7, 1)));
            await db.SaveChangesAsync();
            var pano = Pano(db);
            var isler = Isler(db);
            var h = Hucre(await pano.PanoAsync(2026, 8), Dgr, "Bordro");
            await isler.IsaretleAsync(new IsIsaretleDto { Yapildi = true, Isler = { h } });
            await pano.EkEkleAsync(Ek(h, "bordro.pdf", 802));

            var dosyalar = await isler.IsSilAsync(Dgr, h.KaynakId);

            Assert.Contains(802, dosyalar);
            Assert.Equal(0, await db.IsTamamlamaEkleri.CountAsync());
        }

        // ---- Takip panosu: sekme şeridi ve Özet (Prompt 14) ----

        [Fact]
        public void Sekmeler_turetilir_icinde_bulunulan_ay_ve_onceki_aylar()
        {
            var aylar = DonemSekmeleri.Aylar(new DateTime(2027, 2, 10)).Select(a => a.Anahtar).ToList();

            Assert.Equal(DonemSekmeleri.Sayi, aylar.Count);
            Assert.Equal("2026-09", aylar[0]);                 // yıl sınırı geçilir
            Assert.Equal("2027-02", aylar[^1]);                // en sağda içinde bulunulan ay
        }

        [Theory]
        [InlineData(0, EksikTonu.Yok)]
        [InlineData(1, EksikTonu.Az)]
        [InlineData(3, EksikTonu.Az)]
        [InlineData(4, EksikTonu.Cok)]
        [InlineData(12, EksikTonu.Cok)]
        public void Eksik_tonu_tek_esikten(int eksik, EksikTonu beklenen)
            => Assert.Equal(beklenen, DonemSekmeleri.Ton(eksik));

        [Fact]
        public async Task Ozet_sayilari_ayin_matrisiyle_tutar_ve_varsayilan_onceki_ay()
        {
            using var db = Context();
            db.FirmaIsleri.Add(OzelIs(Dgr, "Fatura girişi", IsTekrari.Aylik, new DateTime(2026, 7, 1)));
            db.FirmaIsleri.Add(OzelIs(Cubic, "Fatura girişi", IsTekrari.Aylik, new DateTime(2026, 7, 1)));
            await db.SaveChangesAsync();
            var pano = Pano(db);
            await Isler(db).IsaretleAsync(new IsIsaretleDto
            {
                Yapildi = true, Isler = { Hucre(await pano.PanoAsync(2026, 8), Cubic, "Fatura girişi") }
            });

            var ozet = await pano.OzetAsync();

            Assert.Equal("2026-08", ozet.VarsayilanAnahtar);   // bugün 28.09 → Ağustos
            Assert.Equal(new[] { "2026-04", "2026-05", "2026-06", "2026-07", "2026-08", "2026-09" },
                         ozet.Donemler.Select(d => d.Anahtar));
            Assert.True(ozet.Donemler[^1].IcindeBulunulan);

            for (var i = 0; i < ozet.Donemler.Count; i++)
            {
                var d = ozet.Donemler[i];
                var matris = await pano.PanoAsync(d.Yil, d.Ay);
                Assert.Equal(matris.Toplam, d.Toplam);
                Assert.Equal(matris.Yapilan, d.Yapilan);
                Assert.Equal(matris.Toplam - matris.Yapilan, d.Eksik);
                Assert.Equal(DonemSekmeleri.Ton(d.Eksik), d.Ton);
                foreach (var s in matris.Satirlar)
                {
                    var h = ozet.Satirlar.Single(x => x.FirmaId == s.FirmaId).Hucreler[i];
                    Assert.Equal((s.Gecerli, s.Yapilan), (h.Gecerli, h.Yapilan));
                }
            }

            var agustos = ozet.Donemler.Single(d => d.Anahtar == "2026-08");
            var cubic = ozet.Satirlar.Single(s => s.FirmaId == Cubic).Hucreler[ozet.Donemler.IndexOf(agustos)];
            Assert.Equal(1, cubic.Yapilan);
            Assert.Contains(agustos.Eksikler, e => e.Baslik == "Fatura girişi" && e.FirmaSayisi == 1);
        }

        [Fact]
        public async Task Firma_filtresinde_sayilar_yalniz_o_firmayi_sayar()
        {
            using var db = Context();
            db.FirmaIsleri.Add(OzelIs(Dgr, "Fatura girişi", IsTekrari.Aylik, new DateTime(2026, 7, 1)));
            db.FirmaIsleri.Add(OzelIs(Cubic, "Fatura girişi", IsTekrari.Aylik, new DateTime(2026, 7, 1)));
            await db.SaveChangesAsync();
            var pano = Pano(db);

            var hepsi = await pano.PanoAsync(2026, 8);
            var cubic = await pano.PanoAsync(2026, 8, firmaId: Cubic);
            var cubicSatiri = hepsi.Satirlar.Single(s => s.FirmaId == Cubic);

            Assert.Equal(Cubic, cubic.FirmaId);
            Assert.Equal(Cubic, Assert.Single(cubic.Satirlar).FirmaId);
            Assert.Equal((cubicSatiri.Gecerli, cubicSatiri.Yapilan), (cubic.Toplam, cubic.Yapilan));
            Assert.All(cubic.Isler, s => Assert.Equal(Cubic, s.FirmaId));
            Assert.All(cubic.Eksikler, e => Assert.Equal(1, e.FirmaSayisi));
            Assert.True(hepsi.Toplam > cubic.Toplam);
        }

        [Fact]
        public async Task Satir_listesi_matrisin_hucreleri_ve_isaretlenince_sira_degismez()
        {
            using var db = Context();
            var bordro = OzelIs(Dgr, "Bordro", IsTekrari.Aylik, new DateTime(2026, 7, 1));
            bordro.GunKurali = IsGunKurali.AyinGunu;
            bordro.AyinGunu = 25;
            bordro.Alicilar.Add(new FirmaIsiAlicisi { AdSoyad = "J. van Dijk", AliciTipi = AliciTipi.Kime });
            bordro.Alicilar.Add(new FirmaIsiAlicisi { AdSoyad = "R. de Vries", AliciTipi = AliciTipi.Bilgi, Sira = 1 });
            db.FirmaIsleri.Add(bordro);
            await db.SaveChangesAsync();
            var pano = Pano(db);

            var once = await pano.PanoAsync(2026, 8);
            Assert.Equal(once.Satirlar.Sum(s => s.Gecerli), once.Isler.Count);   // Yok hücre satır olmaz

            var satir = once.Isler.Single(s => s.Baslik == "Bordro");
            Assert.Equal(new DateTime(2026, 8, 25), satir.SonGun);
            Assert.Equal(new[] { "J. van Dijk" }, satir.Kime);
            Assert.Equal(new[] { "R. de Vries" }, satir.Bilgi);
            Assert.Null(satir.SistemAdi);                                         // "program yok"
            Assert.NotNull(once.Isler.First(s => s.KaynakTip == IsKaynagi.Yasal).SonGun);

            await Isler(db).IsaretleAsync(new IsIsaretleDto
            {
                Yapildi = true,
                Isler = { new IsIsaretDto { KaynakTip = satir.KaynakTip, KaynakId = satir.KaynakId, FirmaId = Dgr, DonemAnahtari = satir.DonemAnahtari } }
            });
            var sonra = await pano.PanoAsync(2026, 8);

            Assert.Equal(once.Isler.Select(s => (s.FirmaId, s.KolonAnahtari)), sonra.Isler.Select(s => (s.FirmaId, s.KolonAnahtari)));
            Assert.Equal(DonemHucreDurumu.Yapildi, sonra.Isler.Single(s => s.Baslik == "Bordro").Durum);
        }
    }
}
