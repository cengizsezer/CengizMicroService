using CatalogService.Api.Features.Anasayfa.Domain;
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
    /// Firma işleri ve Yapılacaklar. Bütün testler SABİT tarihte (28.09.2026) çalışır:
    /// saat servise <see cref="SahteSaat"/> olarak verilir, gerçek saate bağlı test yok.
    /// </summary>
    public class YapilacaklarTests
    {
        private static readonly DateTime Bugun = new(2026, 9, 28);

        private const int Dgr = 3;
        private const int Ben = 16;
        private const int Baskasi = 17;

        private const string DgrMukellefiyet =
            "0003 - GELİR VERGİSİ S. (MUHTASAR), 0010 - KURUMLAR VERGİSİ, 0015 - KATMA DEĞER VERGİSİ, " +
            "0033 - GEÇİCİ VERGİ, 0040 - DAMGA VERGİSİ";

        private static SahteSaat Saat() => new(new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero));

        private static CatalogContext Context(int ekFirma = 0, string? ekMukellefiyet = "0015 - KATMA DEĞER VERGİSİ")
        {
            var db = BankaEkstreTestOrtami.YeniContext();

            db.Firmalar.Add(new Firma
            {
                Id = Dgr, Unvan = "DGR A.Ş.", KisaAd = "DGR", VergiKimlikNo = "2951070824", Aktif = true,
                SorumluKullaniciId = Ben, SorumluKullaniciAdi = "cengiz"
            });
            db.FirmaSicilBilgileri.Add(new FirmaSicilBilgisi { Id = 1, FirmaId = Dgr, MukellefiyetTurleri = DgrMukellefiyet });

            for (var i = 0; i < ekFirma; i++)
            {
                var id = 100 + i;
                db.Firmalar.Add(new Firma
                {
                    Id = id, Unvan = $"Firma {i:00}", KisaAd = $"F{i:00}", VergiKimlikNo = $"10000000{i:00}", Aktif = true,
                    SorumluKullaniciId = Baskasi, SorumluKullaniciAdi = "diger"
                });
                db.FirmaSicilBilgileri.Add(new FirmaSicilBilgisi { Id = 10 + i, FirmaId = id, MukellefiyetTurleri = ekMukellefiyet });
            }

            db.SaveChanges();

            // Takvim, seed'in kendisiyle ve aynı sabit günle doldurulur.
            VergiTakvimiSeed.SeedAsync(db, Bugun).GetAwaiter().GetResult();
            return db;
        }

        private static YapilacaklarService Servis(CatalogContext db, int kullanici = Ben)
            => new(db, Saat(), new SabitKullanici(kullanici),
                   new FirmaOlayYazici(db, new SabitKullanici(kullanici), NullLogger<FirmaOlayYazici>.Instance));

        private static FirmaIsiKaydetDto RaporEpostasi() => new()
        {
            Baslik = "Yönetim raporu e-postası",
            Tekrar = IsTekrari.Aylik,
            GunKurali = IsGunKurali.AyinGunu,
            AyinGunu = 1
        };

        private static IsIsaretleDto Isaret(bool yapildi, params IsSatiriDto[] satirlar) => new()
        {
            Yapildi = yapildi,
            Isler = satirlar.Select(s => new IsIsaretDto
            {
                KaynakTip = s.KaynakTip, KaynakId = s.KaynakId, FirmaId = s.FirmaId, DonemAnahtari = s.DonemAnahtari
            }).ToList()
        };

        // ---- Tarih kuralları ----

        [Theory]
        [InlineData(2026, 1, 31)]
        [InlineData(2026, 4, 30)]   // 30 çeken ay
        [InlineData(2026, 9, 30)]
        [InlineData(2026, 2, 28)]   // şubat
        [InlineData(2028, 2, 29)]   // artık yıl şubatı
        public void Ayin_31i_kurali_ay_sonuna_duser(int yil, int ay, int beklenenGun)
        {
            var donem = new IsDonemi(IsTekrari.Aylik, yil, ay);

            var sonGun = IsTakvimHesabi.SonGun(IsGunKurali.AyinGunu, 31, donem);

            Assert.Equal(new DateTime(yil, ay, beklenenGun), sonGun);
        }

        [Fact]
        public void Ay_sonu_ve_donem_sonu_ucaylikta_farkli()
        {
            var q3 = new IsDonemi(IsTekrari.UcAylik, 2026, 3);

            Assert.Equal(new DateTime(2026, 7, 31), IsTakvimHesabi.SonGun(IsGunKurali.AySonu, null, q3));
            Assert.Equal(new DateTime(2026, 9, 30), IsTakvimHesabi.SonGun(IsGunKurali.DonemSonu, null, q3));
            Assert.Equal("2026-Q3", q3.Anahtar);
            Assert.Equal(q3, IsDonemi.Coz(IsTekrari.UcAylik, "2026-Q3"));
            Assert.Null(IsDonemi.Coz(IsTekrari.Aylik, "2026-Q3"));
        }

        [Theory]
        [InlineData(2026, 9, 1, IsDurumu.Gecikti, 27)]
        [InlineData(2026, 9, 28, IsDurumu.BuHafta, 0)]
        [InlineData(2026, 10, 5, IsDurumu.BuHafta, 7)]
        [InlineData(2026, 10, 6, IsDurumu.Sonra, 0)]
        public void Durum_hesabi(int y, int a, int g, IsDurumu beklenen, int gun)
        {
            var (durum, sayi) = IsTakvimHesabi.Durum(new DateTime(y, a, g), Bugun, tamam: false);
            Assert.Equal(beklenen, durum);
            Assert.Equal(gun, sayi);
        }

        [Fact]
        public void Durum_bu_ay_haftadan_sonra_ayni_ay_icinde()
        {
            var (durum, _) = IsTakvimHesabi.Durum(new DateTime(2026, 9, 30), new DateTime(2026, 9, 10), tamam: false);
            Assert.Equal(IsDurumu.BuAy, durum);
            Assert.Equal(IsDurumu.Tamam, IsTakvimHesabi.Durum(new DateTime(2026, 9, 1), Bugun, tamam: true).Durum);
        }

        // ---- Seed ----

        [Fact]
        public async Task Seed_idempotent_elle_duzeltilen_tarihe_dokunmaz()
        {
            using var db = Context();
            var toplam = await db.VergiTakvimi.CountAsync();

            var kdvEylul = await db.VergiTakvimi.SingleAsync(t => t.MukellefiyetKodu == "0015" && t.Yil == 2026 && t.DonemNo == 9);
            kdvEylul.SonGun = new DateTime(2026, 11, 2); // uzatma
            await db.SaveChangesAsync();

            var sonuc = await VergiTakvimiSeed.SeedAsync(db, Bugun);

            Assert.Equal(0, sonuc.Eklenen);
            Assert.Equal(toplam, await db.VergiTakvimi.CountAsync());
            var satir = await db.VergiTakvimi.SingleAsync(t => t.Id == kdvEylul.Id);
            Assert.Equal(new DateTime(2026, 11, 2), satir.SonGun);
            Assert.True(satir.ElleDuzenlendi);   // seed değeri değil → elle sayıldı, bir daha dokunulmaz
        }

        /// <summary>
        /// Prompt 7B: hafta sonu kaydırması KALDIRILDI — tahakkuk fişi nominal tarihi yazıyor, kaydırma geç tarih
        /// gösteriyordu. (Önceki test "pazartesiye kaydırır" diyordu; bu prompt o davranışı bilerek tersine çevirdi.)
        /// </summary>
        [Fact]
        public void Seed_nominal_tarih_uretir_hafta_sonu_kaydirmasi_yok()
        {
            var satirlar = VergiTakvimiSeed.Uret(Bugun);

            // 2 yıl × (3 aylık iş × 12 + geçici 3 + kurumlar 1) = 80.
            Assert.Equal(80, satirlar.Count);

            VergiTakvimi Bul(string kod, int yil, int no) => satirlar.Single(t => t.MukellefiyetKodu == kod && t.Yil == yil && t.DonemNo == no);

            Assert.Equal(new DateTime(2026, 9, 26), Bul("0003", 2026, 8).SonGun);   // muhtasar 08/2026 · cumartesi
            Assert.Equal(new DateTime(2026, 5, 17), Bul("0033", 2026, 1).SonGun);   // geçici 2026/1 · pazar
            Assert.Equal(new DateTime(2026, 9, 28), Bul("0015", 2026, 8).SonGun);   // KDV 08/2026
            Assert.Equal(new DateTime(2026, 9, 26), Bul("0040", 2026, 8).SonGun);   // damga · izleyen ayın 26'sı
            Assert.Equal(new DateTime(2026, 8, 17), Bul("0033", 2026, 2).SonGun);
            Assert.Equal(new DateTime(2026, 11, 17), Bul("0033", 2026, 3).SonGun);
            Assert.Equal(new DateTime(2027, 4, 30), Bul("0010", 2026, 1).SonGun);   // kurumlar

            // Bugün 28.09: muhtasar ağustosun son günü (26.09) geçti → pasif; KDV ağustos (28.09) aktif.
            Assert.False(Bul("0003", 2026, 8).Aktif);
            Assert.True(Bul("0015", 2026, 8).Aktif);
            Assert.False(Bul("0015", 2026, 7).Aktif);

            Assert.Contains(satirlar, t => t.SonGun.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        }

        [Fact]
        public async Task Seed_eski_kaydirmali_satiri_nominale_ceker_elle_duzenleneni_korur()
        {
            using var db = Context();
            var muhtasar = await db.VergiTakvimi.SingleAsync(t => t.MukellefiyetKodu == "0003" && t.Yil == 2026 && t.DonemNo == 8);
            var gecici = await db.VergiTakvimi.SingleAsync(t => t.MukellefiyetKodu == "0033" && t.Yil == 2026 && t.DonemNo == 1);
            var damga = await db.VergiTakvimi.SingleAsync(t => t.MukellefiyetKodu == "0040" && t.Yil == 2026 && t.DonemNo == 8);
            muhtasar.SonGun = new DateTime(2026, 9, 28);      // eski seed'in kaydırmalı değeri
            gecici.SonGun = new DateTime(2026, 5, 18);        // eski seed'in kaydırmalı değeri
            damga.SonGun = new DateTime(2026, 9, 28);
            damga.ElleDuzenlendi = true;                       // ekrandan düzenlenmiş
            muhtasar.Aktif = true;
            await db.SaveChangesAsync();
            await Servis(db).IsaretleAsync(new IsIsaretleDto
            {
                Yapildi = true,
                Isler = { new IsIsaretDto { KaynakTip = IsKaynagi.Yasal, KaynakId = muhtasar.Id, FirmaId = Dgr, DonemAnahtari = "2026-08" } }
            });

            var sonuc = await VergiTakvimiSeed.SeedAsync(db, Bugun);

            Assert.Equal(2, sonuc.Yenilenen);
            Assert.Equal(new DateTime(2026, 9, 26), (await db.VergiTakvimi.FindAsync(muhtasar.Id))!.SonGun);
            Assert.Equal(new DateTime(2026, 5, 17), (await db.VergiTakvimi.FindAsync(gecici.Id))!.SonGun);
            Assert.Equal(new DateTime(2026, 9, 28), (await db.VergiTakvimi.FindAsync(damga.Id))!.SonGun);   // korundu
            Assert.True((await db.VergiTakvimi.FindAsync(muhtasar.Id))!.Aktif);                            // aktiflik korunur
            Assert.Equal(1, await db.IsTamamlamalari.CountAsync(t => t.KaynakId == muhtasar.Id));           // işaret kaybolmadı

            // İkinci çalıştırma değişiklik yapmaz.
            var ikinci = await VergiTakvimiSeed.SeedAsync(db, Bugun);
            Assert.Equal((0, 0, 0), (ikinci.Eklenen, ikinci.Yenilenen, ikinci.ElleSayilan));
        }

        [Fact]
        public async Task Ekrandan_duzenlenen_satir_elle_isaretlenir_seed_dokunmaz()
        {
            using var db = Context();
            var servis = new VergiTakvimiService(db, Saat());
            var kdv = await db.VergiTakvimi.SingleAsync(t => t.MukellefiyetKodu == "0015" && t.Yil == 2026 && t.DonemNo == 10);

            await servis.GuncelleAsync(kdv.Id, new VergiTakvimiKaydetDto
            {
                MukellefiyetKodu = "0015", Ad = kdv.Ad, Tekrar = kdv.Tekrar, Yil = 2026, DonemNo = 10,
                SonGun = new DateTime(2026, 11, 30), Aktif = true
            });
            await VergiTakvimiSeed.SeedAsync(db, Bugun);

            var satir = await db.VergiTakvimi.FindAsync(kdv.Id);
            Assert.True(satir!.ElleDuzenlendi);
            Assert.Equal(new DateTime(2026, 11, 30), satir.SonGun);
        }

        // ---- DGR: türetilen yasal işler ----

        [Fact]
        public async Task Dgr_kodlarindan_bes_yasal_is_turetilir()
        {
            using var db = Context();

            var kart = await Servis(db).FirmaKartiAsync(Dgr);

            Assert.Equal(new[] { "0003", "0010", "0015", "0033", "0040" }, kart.MukellefiyetKodlari.OrderBy(k => k));
            Assert.Empty(kart.TakvimdeOlmayanKodlar);

            var adlar = kart.Yasal.Select(s => $"{s.MukellefiyetKodu} {s.Baslik} · {s.DonemEtiketi} · {s.SonGun:dd.MM.yyyy} · {s.Durum}")
                                  .ToList();
            // Prompt 7B: nominal tarih. Muhtasar ve damga ağustosun son günü 26.09 (cumartesi) — seed anında (28.09)
            // geçmiş, pasif yazıldı; kuyrukta sıradaki dönem (eylül, 26.10) görünür. Önceki sürüm 28.09'a kaydırıyordu.
            Assert.Equal(new[]
            {
                "0003 Muhtasar ve prim hizmet beyannamesi · Eylül 2026 · 26.10.2026 · Sonra",
                "0010 Kurumlar vergisi beyannamesi · 2026 · 30.04.2027 · Sonra",
                "0015 KDV beyannamesi (KDV-1) · Ağustos 2026 · 28.09.2026 · BuHafta",
                "0033 Geçici vergi beyannamesi · 2026 / 3. çeyrek · 17.11.2026 · Sonra",
                "0040 Damga vergisi beyannamesi · Eylül 2026 · 26.10.2026 · Sonra"
            }, adlar);

            Assert.Equal(0, kart.GeciktiSayisi);
            Assert.Equal(1, kart.BuAySayisi);   // yalnız KDV (28.09)
        }

        [Fact]
        public async Task Mukellefiyetten_cikan_firmanin_yasal_isi_kendiliginden_duser()
        {
            using var db = Context();
            var sicil = await db.FirmaSicilBilgileri.SingleAsync(s => s.FirmaId == Dgr);
            sicil.MukellefiyetTurleri = "0003 - MUHTASAR, 0010 - KURUMLAR VERGİSİ";
            await db.SaveChangesAsync();

            var kart = await Servis(db).FirmaKartiAsync(Dgr);

            Assert.Equal(new[] { "0003", "0010" }, kart.Yasal.Select(s => s.MukellefiyetKodu).OrderBy(k => k));
        }

        // ---- Özel iş: gecikti → işaretle → sonraki dönem → işareti kaldır ----

        [Fact]
        public async Task Rapor_epostasi_ayin_1i_28_eylulde_eklenince_gecikti()
        {
            using var db = Context();
            var servis = Servis(db);

            await servis.IsEkleAsync(Dgr, RaporEpostasi());
            var ozel = (await servis.FirmaKartiAsync(Dgr)).Ozel;

            var satir = Assert.Single(ozel);
            Assert.Equal(new DateTime(2026, 9, 1), satir.SonGun);
            Assert.Equal(IsDurumu.Gecikti, satir.Durum);
            Assert.Equal(27, satir.Gun);
            Assert.Equal("2026-09", satir.DonemAnahtari);
            Assert.Equal(Ben, satir.SorumluKullaniciId); // boş sorumlu = firmanın sorumlusu
        }

        [Fact]
        public async Task Isaretlenince_ustu_cizili_kalir_1_ekim_gorunur_kaldirilinca_geri_gecikti()
        {
            using var db = Context();
            var servis = Servis(db);
            await servis.IsEkleAsync(Dgr, RaporEpostasi());
            var eylul = Assert.Single((await servis.FirmaKartiAsync(Dgr)).Ozel);

            await servis.IsaretleAsync(Isaret(true, eylul));
            var sonra = (await servis.FirmaKartiAsync(Dgr)).Ozel;

            Assert.Equal(2, sonra.Count);
            Assert.True(sonra[0].Tamamlandi);
            Assert.Equal(IsDurumu.Tamam, sonra[0].Durum);
            Assert.Equal(new DateTime(2026, 9, 1), sonra[0].SonGun);
            Assert.False(sonra[1].Tamamlandi);
            Assert.Equal(new DateTime(2026, 10, 1), sonra[1].SonGun);
            Assert.Equal(IsDurumu.BuHafta, sonra[1].Durum);
            Assert.Equal(3, sonra[1].Gun);
            Assert.Equal(1, await db.IsTamamlamalari.CountAsync());

            await servis.IsaretleAsync(Isaret(false, eylul));
            var geri = Assert.Single((await servis.FirmaKartiAsync(Dgr)).Ozel);

            Assert.Equal(IsDurumu.Gecikti, geri.Durum);
            Assert.Equal(new DateTime(2026, 9, 1), geri.SonGun);
            Assert.Equal(0, await db.IsTamamlamalari.CountAsync()); // işaret kaldırılınca satır silinir
        }

        [Fact]
        public async Task Isaretleme_olay_kaydina_yazilir()
        {
            using var db = Context();
            var servis = Servis(db);
            await servis.IsEkleAsync(Dgr, RaporEpostasi());
            var eylul = Assert.Single((await servis.FirmaKartiAsync(Dgr)).Ozel);

            await servis.IsaretleAsync(Isaret(true, eylul));

            var olaylar = await db.FirmaOlayKayitlari.Where(o => o.FirmaId == Dgr).ToListAsync();
            Assert.Contains(olaylar, o => o.OlayTipi == FirmaOlayTipi.IsEklendi);
            Assert.Contains(olaylar, o => o.OlayTipi == FirmaOlayTipi.IsYapildi && o.Aciklama.Contains("Eylül 2026"));
        }

        [Fact]
        public async Task Iki_ay_gecikmisse_ikisi_de_ayri_gecikmis_satir()
        {
            using var db = Context();
            db.FirmaIsleri.Add(new FirmaIsi
            {
                FirmaId = Dgr, Baslik = "Stok sayımı", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AySonu,
                OlusturmaZamani = new DateTime(2026, 7, 15, 9, 0, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync();

            var ozel = (await Servis(db).FirmaKartiAsync(Dgr)).Ozel;

            Assert.Equal(new[] { new DateTime(2026, 7, 31), new DateTime(2026, 8, 31) }, ozel.Select(s => s.SonGun));
            Assert.All(ozel, s => Assert.Equal(IsDurumu.Gecikti, s.Durum));
        }

        [Fact]
        public async Task Ozel_is_silinince_tamamlamalari_da_silinir()
        {
            using var db = Context();
            var servis = Servis(db);
            var isi = await servis.IsEkleAsync(Dgr, RaporEpostasi());
            await servis.IsaretleAsync(Isaret(true, Assert.Single((await servis.FirmaKartiAsync(Dgr)).Ozel)));

            await servis.IsSilAsync(Dgr, isi.Id);

            Assert.Empty((await servis.FirmaKartiAsync(Dgr)).Ozel);
            Assert.Equal(0, await db.IsTamamlamalari.CountAsync());
        }

        [Fact]
        public async Task Yasal_is_mukellefiyeti_olmayan_firmaya_isaretlenemez()
        {
            using var db = Context(ekFirma: 1, ekMukellefiyet: "0003 - MUHTASAR");
            var servis = Servis(db);
            var kdv = (await servis.FirmaKartiAsync(Dgr)).Yasal.Single(s => s.MukellefiyetKodu == "0015");

            var ex = await Assert.ThrowsAsync<YapilacaklarKuralException>(() => servis.IsaretleAsync(new IsIsaretleDto
            {
                Yapildi = true,
                Isler = { new IsIsaretDto { KaynakTip = IsKaynagi.Yasal, KaynakId = kdv.KaynakId, FirmaId = 100, DonemAnahtari = kdv.DonemAnahtari } }
            }));
            Assert.Contains("0015", ex.Message);
        }

        // ---- Yapılacaklar: aynı iş tek satır ----

        [Fact]
        public async Task Ayni_yasal_isin_9_firmasi_tek_satir()
        {
            using var db = Context(ekFirma: 8); // DGR + 8 firma, hepsinde 0015
            var servis = Servis(db);

            var liste = await servis.ListeAsync(IsFiltresi.Tumu);

            var kdv = Assert.Single(liste.Gruplar, g => g.Baslik == "KDV beyannamesi (KDV-1)");
            Assert.Equal(9, kdv.FirmaSayisi);
            Assert.Equal(new DateTime(2026, 9, 28), kdv.SonGun);
            Assert.Equal(IsDurumu.BuHafta, kdv.Durum);
            Assert.Equal(9, kdv.Firmalar.Select(f => f.FirmaId).Distinct().Count());
        }

        [Fact]
        public async Task Tek_satirdaki_isaret_hepsini_isaretler()
        {
            using var db = Context(ekFirma: 8);
            var servis = Servis(db);
            var kdv = (await servis.ListeAsync(IsFiltresi.Tumu)).Gruplar.Single(g => g.Baslik == "KDV beyannamesi (KDV-1)");

            await servis.IsaretleAsync(Isaret(true, kdv.Firmalar.ToArray()));

            var gruplar = (await servis.ListeAsync(IsFiltresi.Tumu)).Gruplar
                .Where(g => g.Baslik == "KDV beyannamesi (KDV-1)").ToList();

            var yapildi = Assert.Single(gruplar, g => g.SonGun == new DateTime(2026, 9, 28));
            Assert.Equal(IsDurumu.Tamam, yapildi.Durum);
            Assert.Equal(9, yapildi.TamamlananSayisi);

            // Sıradaki dönem (Eylül KDV'si, 28.10.2026) yine tek satır.
            var sonraki = Assert.Single(gruplar, g => g.SonGun == new DateTime(2026, 10, 28));
            Assert.Equal(9, sonraki.FirmaSayisi);
            Assert.Equal(IsDurumu.Sonra, sonraki.Durum);
        }

        [Fact]
        public async Task Ayni_baslik_ve_gunlu_ozel_isler_de_gruplanir()
        {
            using var db = Context(ekFirma: 2);
            var servis = Servis(db);
            await servis.IsEkleAsync(Dgr, RaporEpostasi());
            await servis.IsEkleAsync(100, RaporEpostasi().Baslikli("yönetim raporu e-postası "));
            await servis.IsEkleAsync(101, RaporEpostasi());

            var grup = Assert.Single((await servis.ListeAsync(IsFiltresi.Ozel)).Gruplar);

            Assert.Equal(3, grup.FirmaSayisi);
            Assert.Equal(IsDurumu.Gecikti, grup.Durum);
        }

        [Fact]
        public async Task Filtreler_bende_yasal_ozel()
        {
            using var db = Context(ekFirma: 1);
            var servis = Servis(db, kullanici: Ben);
            await servis.IsEkleAsync(100, RaporEpostasi()); // firmanın sorumlusu başkası

            var bende = await servis.ListeAsync(IsFiltresi.Bende);
            Assert.All(bende.Gruplar.SelectMany(g => g.Firmalar), s => Assert.Equal(Dgr, s.FirmaId));

            var yasal = await servis.ListeAsync(IsFiltresi.Yasal);
            Assert.All(yasal.Gruplar, g => Assert.Equal(IsKaynagi.Yasal, g.KaynakTip));

            var ozel = await servis.ListeAsync(IsFiltresi.Ozel);
            var g = Assert.Single(ozel.Gruplar);
            Assert.Equal(100, Assert.Single(g.Firmalar).FirmaId);
        }

        // ---- Anasayfa şeridi ----

        [Fact]
        public async Task Liste_firma_filtresi_gruplamadan_once_ve_diger_filtrelerle_birlikte()
        {
            using var db = Context(ekFirma: 2);
            var servis = Servis(db);

            var hepsi = await servis.ListeAsync(IsFiltresi.Tumu);
            var dgr = await servis.ListeAsync(IsFiltresi.Tumu, firmaId: Dgr);

            // Bütün firmalarda KDV satırı 3 firmayı kapsar; filtreyle tek firma.
            Assert.Contains(hepsi.Gruplar, g => g.FirmaSayisi > 1);
            Assert.All(dgr.Gruplar, g => Assert.All(g.Firmalar, f => Assert.Equal(Dgr, f.FirmaId)));
            Assert.True(dgr.Gruplar.Count > 0);

            // "Bende + firma": DGR'nin sorumlusu Ben; diğer firmanın sorumlusu başkası → boş.
            Assert.NotEmpty((await servis.ListeAsync(IsFiltresi.Bende, firmaId: Dgr)).Gruplar);
            Assert.Empty((await servis.ListeAsync(IsFiltresi.Bende, firmaId: 100)).Gruplar);
        }

        [Fact]
        public async Task Gecikmis_is_yokken_serit_sayisi_sifir()
        {
            using var db = Context(ekFirma: 8);

            var ozet = await Servis(db).OzetAsync();

            Assert.Equal(0, ozet.Gecikti); // istemci şeridi Gecikti > 0 iken çizer
            Assert.Equal(1, ozet.BuHafta); // KDV (28.09) — satır sayısı, firma değil. Muhtasar/damga 26.09 nominal (7B), seed'de pasif
        }

        [Fact]
        public async Task Gecikmis_is_varken_serit_sayisi_satir_sayisidir()
        {
            using var db = Context(ekFirma: 2);
            var servis = Servis(db);
            await servis.IsEkleAsync(Dgr, RaporEpostasi());
            await servis.IsEkleAsync(100, RaporEpostasi());

            var ozet = await servis.OzetAsync();

            Assert.Equal(1, ozet.Gecikti); // iki firmada aynı iş = tek satır
        }
    }

    internal static class FirmaIsiKaydetDtoUzanti
    {
        public static FirmaIsiKaydetDto Baslikli(this FirmaIsiKaydetDto dto, string baslik)
        {
            dto.Baslik = baslik;
            return dto;
        }
    }
}
