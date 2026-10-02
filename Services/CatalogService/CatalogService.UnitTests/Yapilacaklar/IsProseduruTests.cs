using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.Sistemler;
using CatalogService.Api.Features.Sistemler.Domain;
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
    /// İş prosedürü (Prompt 8): program, alıcı, nasıl yapılır, ekler, ön adım bağı, geçmiş.
    /// Sabit tarih 28.09.2026 — <see cref="YapilacaklarTests"/> ile aynı ortam.
    /// </summary>
    public class IsProseduruTests
    {
        private static readonly DateTime Bugun = new(2026, 9, 28);
        private const int Carriere = 5;
        private const int Ben = 16;

        private static CatalogContext Context(string? mizanFormati = "Luca")
        {
            var db = BankaEkstreTestOrtami.YeniContext();

            db.Firmalar.Add(new Firma
            {
                Id = Carriere, Unvan = "CARRIERE B.V. TR", KisaAd = "CARRIERE", VergiKimlikNo = "1234567890", Aktif = true,
                SorumluKullaniciId = Ben, SorumluKullaniciAdi = "cengiz", MizanFormati = mizanFormati
            });
            db.FirmaSicilBilgileri.Add(new FirmaSicilBilgisi
            {
                Id = 1, FirmaId = Carriere,
                MukellefiyetTurleri = "0003 - GELİR VERGİSİ S. (MUHTASAR), 0015 - KATMA DEĞER VERGİSİ"
            });
            db.SaveChanges();

            VergiTakvimiSeed.SeedAsync(db, Bugun).GetAwaiter().GetResult();
            SistemSeed.SeedAsync(db).GetAwaiter().GetResult();
            return db;
        }

        /// <summary>Ortak listedeki sistemin Id'si (seed: Luca, ORKA, e-Beyanname GİB…).</summary>
        private static int SistemId(CatalogContext db, string ad)
            => db.Sistemler.AsNoTracking().Single(s => s.Ad == ad).Id;

        private static FirmaIsiKaydetDto BordroLucada(CatalogContext db)
        {
            var dto = Bordro();
            dto.SistemId = SistemId(db, "Luca");
            return dto;
        }

        private static YapilacaklarService Servis(CatalogContext db)
            => new(db, new SahteSaat(new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero)), new SabitKullanici(Ben),
                   new FirmaOlayYazici(db, new SabitKullanici(Ben), NullLogger<FirmaOlayYazici>.Instance));

        private static FirmaIsiKaydetDto Bordro() => new()
        {
            Baslik = "Bordroyu hazırla ve Hollanda'ya gönder",
            Tekrar = IsTekrari.Aylik,
            GunKurali = IsGunKurali.AyinGunu,
            AyinGunu = 25,
            MenuYolu = "Bordro > Raporlar > Ücret Bordrosu",
            NasilYapilir = "Puantajı Luca'ya gir\r\nBordroyu hesapla\r\n\r\nÜcret bordrosunu PDF al\n  Excel özetini çıkar  \nHollanda'ya e-posta ile gönder\n",
            Alicilar = new()
            {
                new() { AdSoyad = "Jan de Vries", Eposta = "jan@carriere.nl", Rol = "Finans, Hollanda", AliciTipi = AliciTipi.Kime },
                new() { AdSoyad = "Ayşe Yılmaz", Eposta = "ayse@carriere.com.tr", Rol = "İK", AliciTipi = AliciTipi.Bilgi }
            }
        };

        private static FirmaIsiEkiOlusturDto Ek(string ad, long boyut = 1024, int fileId = 501)
            => new() { FileId = fileId, DosyaAdi = ad, ContentType = "application/octet-stream", Boyut = boyut };

        // ---- Program, alıcı, nasıl yapılır ----

        [Fact]
        public async Task Bordro_isi_programi_alicilari_ve_bes_adimiyla_kaydedilir()
        {
            using var db = Context();
            var servis = Servis(db);

            var isi = await servis.IsEkleAsync(Carriere, BordroLucada(db));
            var kart = await servis.FirmaKartiAsync(Carriere);

            var tanim = Assert.Single(kart.OzelTanimlar);
            Assert.Equal(SistemId(db, "Luca"), tanim.SistemId);
            Assert.Equal("Luca", tanim.SistemAdi); // listedeki mor program çipi
            Assert.Equal(new[] { "Jan de Vries", "Ayşe Yılmaz" }, tanim.Alicilar.Select(a => a.AdSoyad));
            Assert.Equal(new[] { AliciTipi.Kime, AliciTipi.Bilgi }, tanim.Alicilar.Select(a => a.AliciTipi));
            Assert.Equal(new DateTime(2026, 9, 25), Assert.Single(kart.Ozel).SonGun);

            var panel = await servis.ProsedurAsync(Carriere, IsKaynagi.Ozel, isi.Id, tumGecmis: false);
            Assert.Equal("Bordro > Raporlar > Ücret Bordrosu", panel.MenuYolu);
            Assert.Equal(new[]
            {
                "Puantajı Luca'ya gir", "Bordroyu hesapla", "Ücret bordrosunu PDF al", "Excel özetini çıkar",
                "Hollanda'ya e-posta ile gönder"
            }, panel.Adimlar);
            Assert.Equal(2, panel.Alicilar.Count);
            Assert.Equal("Luca", panel.MizanFormati);
        }

        [Fact]
        public async Task Alici_listesi_butun_olarak_yazilir_gonderilmeyen_silinir()
        {
            using var db = Context();
            var servis = Servis(db);
            var isi = await servis.IsEkleAsync(Carriere, Bordro());

            var dto = Bordro();
            // E-posta zorunlu değil. (Prompt 14: aynı adla e-postasız yazılan alıcı firmanın o KİŞİSİNE eşleşir ve
            // kişinin e-postasını taşır — e-posta kişide tek yerde; bu yüzden burada yeni bir ad kullanılıyor.)
            dto.Alicilar = new() { new() { AdSoyad = "Ayşe Kaya", AliciTipi = AliciTipi.Kime } };
            await servis.IsGuncelleAsync(Carriere, isi.Id, dto);

            var alici = Assert.Single(await db.FirmaIsiAlicilari.ToListAsync());
            Assert.Null(alici.Eposta);

            // null = alıcılara dokunma.
            dto.Alicilar = null;
            await servis.IsGuncelleAsync(Carriere, isi.Id, dto);
            Assert.Equal(1, await db.FirmaIsiAlicilari.CountAsync());
        }

        [Fact]
        public async Task Gecersiz_eposta_ve_bos_alici_reddedilir()
        {
            using var db = Context();
            var servis = Servis(db);

            var dto = Bordro();
            dto.Alicilar = new() { new() { AdSoyad = "Jan", Eposta = "jan(at)carriere.nl" } };
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => servis.IsEkleAsync(Carriere, dto));

            dto.Alicilar = new() { new() { AdSoyad = "  " } };
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => servis.IsEkleAsync(Carriere, dto));
        }

        [Fact]
        public async Task Pasif_sistem_yeni_iste_secilemez_mevcut_is_bozulmaz()
        {
            using var db = Context();
            var servis = Servis(db);
            var isi = await servis.IsEkleAsync(Carriere, BordroLucada(db));

            var luca = await db.Sistemler.SingleAsync(s => s.Ad == "Luca");
            luca.Aktif = false;
            await db.SaveChangesAsync();

            // Mevcut iş: aynı sistemle güncellenebilir, çip çıkmaya devam eder.
            var guncel = await servis.IsGuncelleAsync(Carriere, isi.Id, BordroLucada(db).Baslikli("Bordro"));
            Assert.Equal("Luca", guncel.SistemAdi);

            // Yeni iş: pasif sistem seçilemez ve seçeneklerde yalnız kullanıldığı için durur.
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => servis.IsEkleAsync(Carriere, BordroLucada(db)));
            var secenek = Assert.Single((await servis.FirmaKartiAsync(Carriere)).Sistemler, s => s.Ad == "Luca");
            Assert.False(secenek.Aktif);
        }

        [Theory]
        [InlineData("Luca", "Luca")]
        [InlineData("ORKA — döviz kolonlu", "ORKA")]
        [InlineData("ORKA — döviz kolonsuz", "ORKA")]
        [InlineData("Logo", "Logo")]
        [InlineData("Mikro", "Mikro")]
        [InlineData("Belirsiz", null)]
        [InlineData(null, null)]
        public void Mizan_formatinin_programi(string? format, string? beklenen)
            => Assert.Equal(beklenen, MizanFormatlari.ProgramAdi(format));

        [Fact]
        public async Task Varsayilan_sistem_firmanin_muhasebe_programi_ise_kilitli_degil()
        {
            using var db = Context(mizanFormati: "ORKA — döviz kolonlu");
            var servis = Servis(db);

            // Muhasebe programı atanmamış: öneri mizan formatının programından (ORKA).
            Assert.Equal(SistemId(db, "ORKA"), (await servis.FirmaKartiAsync(Carriere)).VarsayilanSistemId);

            // Kullanılan sistemler kartında muhasebe = ORKA; bordro işi yine Luca'da tutulabilir.
            db.FirmaSistemleri.Add(new FirmaSistemi { FirmaId = Carriere, SistemId = SistemId(db, "ORKA"), Sira = 1 });
            await db.SaveChangesAsync();

            var isi = await servis.IsEkleAsync(Carriere, BordroLucada(db));
            var panel = await servis.ProsedurAsync(Carriere, IsKaynagi.Ozel, isi.Id, false);

            Assert.Equal("Luca", panel.SistemAdi);
            Assert.Equal("ORKA", panel.FarkliMuhasebeProgrami); // "Firmanın muhasebe programı ORKA" notu
            var kart = await servis.FirmaKartiAsync(Carriere);
            Assert.Equal(SistemId(db, "ORKA"), kart.VarsayilanSistemId);
            Assert.True(kart.Sistemler.First().Firmanin); // firmanınki listede önde
        }

        // ---- Ekler ----

        [Fact]
        public async Task Png_ve_xlsx_eklenir_silinince_sayi_duser_belgeler_etkilenmez()
        {
            using var db = Context();
            db.FirmaBelgeleri.Add(new FirmaBelgesi
            {
                FirmaId = Carriere, Tur = FirmaBelgeTuru.VergiLevhasi, FileId = 900, FileName = "levha.pdf", Length = 10
            });
            await db.SaveChangesAsync();
            var servis = Servis(db);
            var isi = await servis.IsEkleAsync(Carriere, Bordro());

            var png = await servis.EkEkleAsync(Carriere, isi.Id, Ek("bordro-ozet.png", fileId: 501));
            await servis.EkEkleAsync(Carriere, isi.Id, Ek("Bordro Eylül.xlsx", fileId: 502));

            Assert.Equal(2, Assert.Single((await servis.FirmaKartiAsync(Carriere)).OzelTanimlar).EkSayisi);
            Assert.Equal(2, (await servis.ProsedurAsync(Carriere, IsKaynagi.Ozel, isi.Id, false)).Ekler.Count);

            var fileId = await servis.EkSilAsync(Carriere, isi.Id, png.Id);

            Assert.Equal(501, fileId);
            Assert.Equal(1, Assert.Single((await servis.FirmaKartiAsync(Carriere)).OzelTanimlar).EkSayisi);

            // Belgeler kartı ayrı tablo: dokunulmadı.
            var belge = Assert.Single(await db.FirmaBelgeleri.ToListAsync());
            Assert.Equal(900, belge.FileId);
        }

        [Theory]
        [InlineData("program.exe")]
        [InlineData("arsiv.zip")]
        [InlineData("uzantisiz")]
        public async Task Izin_verilmeyen_ek_turu_reddedilir(string ad)
        {
            using var db = Context();
            var servis = Servis(db);
            var isi = await servis.IsEkleAsync(Carriere, Bordro());

            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => servis.EkEkleAsync(Carriere, isi.Id, Ek(ad)));
        }

        [Fact]
        public async Task Ek_boyut_siniri_belgeler_kartiyla_ayni()
        {
            using var db = Context();
            var servis = Servis(db);
            var isi = await servis.IsEkleAsync(Carriere, Bordro());

            Assert.Equal(Api.Features.FirmaBilgileri.Services.FirmaBilgiService.EnFazlaBayt, YapilacaklarService.EkEnFazlaBayt);

            await servis.EkEkleAsync(Carriere, isi.Id, Ek("tam.pdf", YapilacaklarService.EkEnFazlaBayt));
            await Assert.ThrowsAsync<YapilacaklarKuralException>(
                () => servis.EkEkleAsync(Carriere, isi.Id, Ek("buyuk.pdf", YapilacaklarService.EkEnFazlaBayt + 1)));
        }

        [Fact]
        public async Task Is_silinince_ek_fileIdleri_doner_ve_on_adim_bagi_bosalir()
        {
            using var db = Context();
            var servis = Servis(db);
            var bordro = await servis.IsEkleAsync(Carriere, Bordro());
            await servis.EkEkleAsync(Carriere, bordro.Id, Ek("a.pdf", fileId: 601));

            var hazirlik = new FirmaIsiKaydetDto
            {
                Baslik = "Puantajı topla", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AyinGunu, AyinGunu = 20,
                OnAdimiOlduguIsId = bordro.Id
            };
            var h = await servis.IsEkleAsync(Carriere, hazirlik);
            Assert.Equal("Bordroyu hazırla ve Hollanda'ya gönder", h.OnAdimiBaslik);

            var fileIdleri = await servis.IsSilAsync(Carriere, bordro.Id);

            Assert.Equal(new[] { 601 }, fileIdleri);
            Assert.Equal(0, await db.FirmaIsiEkleri.CountAsync());
            Assert.Equal(0, await db.FirmaIsiAlicilari.CountAsync());
            Assert.Null(Assert.Single((await servis.FirmaKartiAsync(Carriere)).OzelTanimlar).OnAdimiOlduguIsId);
        }

        // ---- Ön adım bağı ----

        [Fact]
        public async Task Kdv_hesabi_yasal_kdv_beyannamesinin_on_adimi_olur()
        {
            using var db = Context();
            var servis = Servis(db);

            var kdvHesabi = await servis.IsEkleAsync(Carriere, new FirmaIsiKaydetDto
            {
                Baslik = "KDV hesabını çıkar ve onaya gönder",
                Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AyinGunu, AyinGunu = 15,
                OnAdimiYasalKodu = "0015", OnAdimiYasalTekrar = IsTekrari.Aylik
            });

            Assert.Equal("KDV beyannamesi (KDV-1)", kdvHesabi.OnAdimiBaslik);

            var kart = await servis.FirmaKartiAsync(Carriere);
            var prosedur = Assert.Single(kart.YasalProsedurler);
            Assert.Equal("0015", prosedur.YasalMukellefiyetKodu);
            Assert.Equal(prosedur.Id, Assert.Single(kart.OzelTanimlar).OnAdimiOlduguIsId);

            // Prosedür satırı özel iş sayılmaz; yasal satırlar değişmedi.
            Assert.Equal("KDV hesabını çıkar ve onaya gönder", Assert.Single(kart.Ozel).Baslik);
            Assert.Equal(new[] { "0003", "0015" }, kart.Yasal.Select(s => s.MukellefiyetKodu).Distinct().OrderBy(k => k));
            Assert.DoesNotContain((await servis.ListeAsync(IsFiltresi.Ozel)).Gruplar, g => g.Baslik.StartsWith("KDV beyannamesi"));
        }

        [Fact]
        public async Task On_adim_kendisi_ya_da_dongu_olamaz()
        {
            using var db = Context();
            var servis = Servis(db);
            var a = await servis.IsEkleAsync(Carriere, Bordro());

            var bDto = Bordro().Baslikli("Puantajı topla");
            bDto.OnAdimiOlduguIsId = a.Id;
            var b = await servis.IsEkleAsync(Carriere, bDto);

            var kendisi = Bordro();
            kendisi.OnAdimiOlduguIsId = a.Id;
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => servis.IsGuncelleAsync(Carriere, a.Id, kendisi));

            var dongu = Bordro();
            dongu.OnAdimiOlduguIsId = b.Id; // a → b → a
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => servis.IsGuncelleAsync(Carriere, a.Id, dongu));
        }

        // ---- Yasal işte prosedür ----

        [Fact]
        public async Task Yasal_ise_program_ve_alici_eklenir_ad_ve_tarih_degismez()
        {
            using var db = Context();
            var servis = Servis(db);
            var kdv = (await servis.FirmaKartiAsync(Carriere)).Yasal.Single(s => s.MukellefiyetKodu == "0015");

            var prosedur = await servis.YasalProsedurKaydetAsync(Carriere, "0015", IsTekrari.Aylik, new IsProsedurKaydetDto
            {
                SistemId = SistemId(db, "e-Beyanname GİB"),
                MenuYolu = "Beyanname Düzenleme > KDV1",
                Alicilar = new() { new() { AdSoyad = "Jan de Vries", Eposta = "jan@carriere.nl", AliciTipi = AliciTipi.Bilgi } }
            });

            Assert.Equal("KDV beyannamesi (KDV-1)", prosedur.Baslik);
            Assert.Equal("e-Beyanname GİB", prosedur.SistemAdi);

            var panel = await servis.ProsedurAsync(Carriere, IsKaynagi.Yasal, kdv.KaynakId, false);
            Assert.Equal(prosedur.Id, panel.IsId);
            Assert.Single(panel.Alicilar);

            // Ad ve tarih kilitli: genel güncelleme ve silme reddedilir.
            var degistir = Bordro().Baslikli("KDV başka ad");
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => servis.IsGuncelleAsync(Carriere, prosedur.Id, degistir));
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => servis.IsSilAsync(Carriere, prosedur.Id));

            var sonra = (await servis.FirmaKartiAsync(Carriere)).Yasal.Single(s => s.MukellefiyetKodu == "0015");
            Assert.Equal(kdv.Baslik, sonra.Baslik);
            Assert.Equal(kdv.SonGun, sonra.SonGun);
        }

        [Fact]
        public async Task Yasal_prosedur_ac_idempotent_mukellefiyeti_olmayana_acilmaz()
        {
            using var db = Context();
            var servis = Servis(db);

            var bir = await servis.YasalProsedurAcAsync(Carriere, "0015", IsTekrari.Aylik);
            var iki = await servis.YasalProsedurAcAsync(Carriere, "0015", IsTekrari.Aylik);

            Assert.Equal(bir.Id, iki.Id);
            Assert.Equal(1, await db.FirmaIsleri.CountAsync());

            // 0010 (kurumlar) bu firmada yok.
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => servis.YasalProsedurAcAsync(Carriere, "0010", IsTekrari.Yillik));
        }

        [Fact]
        public async Task Yasal_prosedur_satiri_isaretlenemez_ozel_is_gibi()
        {
            using var db = Context();
            var servis = Servis(db);
            var prosedur = await servis.YasalProsedurAcAsync(Carriere, "0015", IsTekrari.Aylik);

            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => servis.IsaretleAsync(new IsIsaretleDto
            {
                Yapildi = true,
                Isler = { new IsIsaretDto { KaynakTip = IsKaynagi.Ozel, KaynakId = prosedur.Id, FirmaId = Carriere, DonemAnahtari = "2026-09" } }
            }));
        }

        // ---- Geçmiş ----

        [Fact]
        public async Task Gecmis_yeniden_eskiye_yapilmayan_donem_isaretli()
        {
            using var db = Context();
            db.FirmaIsleri.Add(new FirmaIsi
            {
                Id = 70, FirmaId = Carriere, Baslik = "Stok sayımı", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AySonu,
                OlusturmaZamani = new DateTime(2026, 7, 15, 9, 0, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync();
            var servis = Servis(db);

            var temmuz = (await servis.FirmaKartiAsync(Carriere)).Ozel.First();
            await servis.IsaretleAsync(new IsIsaretleDto { Yapildi = true, Isler = { IsIsaretDto(temmuz) } });

            var panel = await servis.ProsedurAsync(Carriere, IsKaynagi.Ozel, 70, false);

            Assert.Equal(new[] { "2026-08", "2026-07" }, panel.Gecmis.Select(g => g.DonemAnahtari));
            Assert.True(panel.Gecmis[0].Yapilmadi);
            Assert.True(panel.Gecmis[1].Yapildi);
            Assert.Equal(2, panel.GecmisToplam);
        }

        [Fact]
        public async Task Gecmis_son_alti_donem_tumu_istenince_hepsi()
        {
            using var db = Context();
            db.FirmaIsleri.Add(new FirmaIsi
            {
                Id = 71, FirmaId = Carriere, Baslik = "Kasa sayımı", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AySonu,
                OlusturmaZamani = new DateTime(2025, 12, 10, 9, 0, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync();
            var servis = Servis(db);

            var kisa = await servis.ProsedurAsync(Carriere, IsKaynagi.Ozel, 71, tumGecmis: false);
            var tum = await servis.ProsedurAsync(Carriere, IsKaynagi.Ozel, 71, tumGecmis: true);

            Assert.Equal(6, kisa.Gecmis.Count);
            Assert.Equal(9, kisa.GecmisToplam); // Aralık 2025 – Ağustos 2026
            Assert.Equal("2026-08", kisa.Gecmis[0].DonemAnahtari);
            Assert.Equal(9, tum.Gecmis.Count);
        }

        [Fact]
        public void Adimlar_bos_satirlari_atlar()
        {
            Assert.Empty(YapilacaklarKurucu.Adimlar(null));
            Assert.Empty(YapilacaklarKurucu.Adimlar(" \n \r\n"));
            Assert.Equal(new[] { "bir", "iki" }, YapilacaklarKurucu.Adimlar("bir\r\n\r\n  iki  "));
        }

        private static IsIsaretDto IsIsaretDto(IsSatiriDto s) => new()
        {
            KaynakTip = s.KaynakTip, KaynakId = s.KaynakId, FirmaId = s.FirmaId, DonemAnahtari = s.DonemAnahtari
        };
    }
}
