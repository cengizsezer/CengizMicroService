using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Features.Firmalar;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.BankaEkstre;

namespace CatalogService.UnitTests.Anasayfa
{
    /// <summary>
    /// Prompt 6B düzeltmeleri: sıkı mükellefiyet kodu ayıklama, kırmızı "!" kuralı ve cari
    /// dönem, kod çipleri, raya dönen uyarılar, geçerli yetkili yok, şablon kısıtı.
    /// </summary>
    public class Prompt6bDuzeltmeTests
    {
        private static readonly DateTime Bugun = new(2026, 9, 28);

        /// <summary>GİB'in gösterdiği biçim, olduğu gibi.</summary>
        private const string DgrMetni =
            "0003 - GELİR VERGİSİ S. (MUHTASAR) , 0010 - KURUMLAR VERGİSİ , 0015 - KATMA DEĞER VERGİSİ, " +
            "0033 - DAMGA VERGİSİ, 0040 - DAMGA VERGİSİ (BEYANNAMELİ DAMGA VERGİSİ MÜKELLEFİ)";

        // ---- 1. Kod ayıklama ----

        [Fact]
        public void GIB_metninden_tam_olarak_bes_kod_cikar()
        {
            var sonuc = MukellefiyetSiniflandirici.Ayikla(DgrMetni);

            Assert.Equal(new[] { "0003", "0010", "0015", "0033", "0040" }, sonuc.Kodlar.Select(k => k.Kod));
            Assert.Null(sonuc.Kalan);
            Assert.Equal("KURUMLAR VERGİSİ", sonuc.Kodlar[1].Ad);
        }

        [Fact]
        public void Yil_gecen_metinden_yil_kod_olarak_cikmaz()
        {
            var sonuc = MukellefiyetSiniflandirici.Ayikla("0015 - KDV, 2021 yılından itibaren e-defter");

            Assert.Equal(new[] { "0015" }, sonuc.Kodlar.Select(k => k.Kod));
            Assert.DoesNotContain("2021", sonuc.Kodlar.Select(k => k.Kod));
            Assert.Equal("2021 yılından itibaren e-defter", sonuc.Kalan);   // yutulmadı
        }

        [Fact]
        public void Tiresiz_bitisik_ve_yalniz_kod_bicimleri()
        {
            Assert.Equal(new[] { "0010" }, MukellefiyetSiniflandirici.Kodlar("0010-KURUMLAR VERGİSİ"));
            Assert.Equal(new[] { "0003", "0010" }, MukellefiyetSiniflandirici.Kodlar("0003, 0010"));

            // 6C: tire şartı kalktı — tiresiz "0010 KURUMLAR VERGİSİ" de kod (bkz. Prompt6cTests).
        }

        [Fact]
        public void Parantez_ici_numara_kod_sayilmaz()
            => Assert.Empty(MukellefiyetSiniflandirici.Kodlar("(0123 sayılı karar)"));

        [Fact]
        public void Bilinmeyen_kod_reddedilmez_ama_siniflandirmada_kullanilmaz()
        {
            var sonuc = MukellefiyetSiniflandirici.Ayikla("0099 - BİLİNMEYEN VERGİ");

            Assert.Equal("0099", Assert.Single(sonuc.Kodlar).Kod);
            Assert.Equal(VergiTuru.Belirsiz, MukellefiyetSiniflandirici.Oner("0099 - BİLİNMEYEN VERGİ").VergiTuru);
        }

        [Fact]
        public void Kurallar_0003_ve_0010_aynen()
        {
            var oneri = MukellefiyetSiniflandirici.Oner(DgrMetni);
            Assert.Equal(VergiTuru.KurumlarVergisi, oneri.VergiTuru);
            Assert.Equal("0010", oneri.KaynakKod);

            Assert.Equal(VergiTuru.Belirsiz, MukellefiyetSiniflandirici.Oner("0003 - GELİR VERGİSİ S. (MUHTASAR)").VergiTuru);
        }

        // ---- 3. Çipler ----

        [Fact]
        public void DGR_icin_bes_cip_0010_vurgulu()
        {
            var cipler = FirmaPaneliKurucu.MukellefiyetCipleri(DgrMetni, out var kalan);

            Assert.Equal(5, cipler.Count);
            Assert.Null(kalan);
            Assert.Equal("0010", Assert.Single(cipler, c => c.SiniflandirmaKaynagi).Kod);
            Assert.Equal("Muhtasar", cipler.Single(c => c.Kod == "0003").KisaAd);
            Assert.Equal("Damga vergisi", cipler.Single(c => c.Kod == "0033").KisaAd);   // metindeki addan
        }

        [Fact]
        public async Task Panelde_mukellefiyet_kodlari_ve_kalan_metin_geliyor()
        {
            using var db = Ortam(out _);
            db.FirmaSicilBilgileri.Add(new FirmaSicilBilgisi
            {
                Id = 1, FirmaId = 1, MukellefiyetTurleri = DgrMetni + ", eski kayıt notu"
            });
            db.SaveChanges();

            var m = (await Panel(db, 1)).Secili!.Mukellefiyet;

            Assert.Equal(5, m.Kodlar.Count);
            Assert.Equal("eski kayıt notu", m.KalanMetin);
        }

        // ---- 2. Kırmızı "!" ve cari dönem ----

        [Fact]
        public void Takvim_yilinda_cari_donem_takvim_yili()
        {
            var donem = FirmaPaneliKurucu.CariDonem(new Firma { HesapDonemi = HesapDonemi.TakvimYili }, Bugun);

            Assert.Equal(new DateTime(2026, 1, 1), donem.Bas);
            Assert.Equal(new DateTime(2026, 12, 31), donem.Bit);
            Assert.Equal(2026, donem.Yil);
        }

        [Fact]
        public void Ozel_hesap_doneminde_cari_donem_takvim_yilindan_farkli()
        {
            var firma = new Firma
            {
                HesapDonemi = HesapDonemi.OzelHesapDonemi,
                OzelDonemBas = new DateTime(2024, 7, 1),
                OzelDonemBit = new DateTime(2025, 6, 30)
            };

            // 28.09.2026 → 01.07.2026 – 30.06.2027 dönemi; kapandığı yıl 2027.
            var eylul = FirmaPaneliKurucu.CariDonem(firma, Bugun);
            Assert.Equal(new DateTime(2026, 7, 1), eylul.Bas);
            Assert.Equal(new DateTime(2027, 6, 30), eylul.Bit);
            Assert.Equal(2027, eylul.Yil);

            // 15.03.2026 → 01.07.2025 – 30.06.2026; kapandığı yıl 2026.
            var mart = FirmaPaneliKurucu.CariDonem(firma, new DateTime(2026, 3, 15));
            Assert.Equal(new DateTime(2025, 7, 1), mart.Bas);
            Assert.Equal(2026, mart.Yil);
        }

        [Fact]
        public void Hic_mizani_olmayan_kirmizi_gecmisi_olan_kirmizi_degil()
        {
            Assert.True(FirmaPaneliKurucu.MizanYok(Array.Empty<MizanYuklemesi>()));

            var gecmis = new[] { new MizanYuklemesi(1, 1, 2025, Bugun, null, null, null, null) };
            Assert.False(FirmaPaneliKurucu.MizanYok(gecmis));
            Assert.True(FirmaPaneliKurucu.CariMizanEksik(gecmis, 2026));
            Assert.False(FirmaPaneliKurucu.CariMizanEksik(Array.Empty<MizanYuklemesi>(), 2026));
        }

        [Fact]
        public void Ozel_donemli_firmanin_donem_ciplerinde_cari_yil_donemden()
        {
            var cipler = FirmaPaneliKurucu.Donemler(
                new[] { new MizanYuklemesi(1, 1, 2026, Bugun, null, null, null, null) }, cariYil: 2027);

            Assert.Equal(new[] { 2027, 2026 }, cipler.Select(c => c.Yil));
            Assert.False(cipler[0].Yuklu);
        }

        // ---- 4. Raya dönen uyarılar ----

        [Fact]
        public async Task Imza_yetkisi_45_gun_kalan_firmada_rayda_uyari_var()
        {
            using var db = Ortam(out var firma);
            db.FirmaImzaYetkilileri.Add(new FirmaImzaYetkilisi { Id = 1, FirmaId = 1, Ad = "A", YetkiBitis = Bugun.AddDays(45) });
            db.SaveChanges();

            var panel = await Panel(db, 1);
            var satir = panel.Firmalar.Single();

            Assert.Equal(1, satir.UyariSayisi);
            var uyari = Assert.Single(FirmaPaneliKurucu.RaydakiUyarilar(panel.Secili!.Uyarilar));
            Assert.Contains("45 gün sonra doluyor", uyari.Mesaj);
        }

        [Fact]
        public async Task Pay_orani_uyarisi_raya_sayiliyor_eksik_sicil_sayilmiyor()
        {
            using var db = Ortam(out _);
            db.FirmaOrtaklari.Add(new FirmaOrtak { Id = 1, FirmaId = 1, Ad = "O", PayOrani = 90 });
            db.SaveChanges();

            var panel = await Panel(db, 1);

            // Eksik sicil uyarısı da var (MERSİS/adres boş) ama o eksik listesinde; çift sayılmıyor.
            Assert.Contains(panel.Secili!.Uyarilar, u => u.Tur == FirmaUyariTuru.EksikSicilAlani);
            Assert.Equal(1, panel.Firmalar.Single().UyariSayisi);
        }

        // ---- 5. Geçerli yetkili yok ----

        [Fact]
        public async Task Tek_yetkilisinin_suresi_dolmus_firmada_gecerli_yetkili_yok()
        {
            using var db = Ortam(out _);
            db.FirmaImzaYetkilileri.Add(new FirmaImzaYetkilisi { Id = 1, FirmaId = 1, Ad = "Eski", YetkiBitis = Bugun.AddDays(-3) });
            db.SaveChanges();

            var panel = await Panel(db, 1);

            var uyari = Assert.Single(FirmaPaneliKurucu.RaydakiUyarilar(panel.Secili!.Uyarilar));
            Assert.Equal(FirmaUyariTuru.GecerliYetkiliYok, uyari.Tur);
            Assert.StartsWith("Geçerli imza yetkilisi yok", uyari.Mesaj);
            Assert.Equal(1, panel.Firmalar.Single().UyariSayisi);

            // Kart başlığı da söylüyor.
            Assert.True(panel.Secili.YetkiRozeti!.GecerliYok);
        }

        [Fact]
        public void Dolmus_eski_yetkili_gecerli_yetkilinin_yaninda_sayilmaz()
        {
            var rozet = FirmaPaneliKurucu.YetkiRozeti(new[]
            {
                new FirmaImzaYetkilisi { Ad = "Eski", YetkiBitis = Bugun.AddYears(-2) },
                new FirmaImzaYetkilisi { Ad = "Yeni", YetkiBitis = Bugun.AddYears(3) }
            }, Bugun);

            Assert.Null(rozet);
        }

        // ---- 6. Şablon ----

        [Fact]
        public void Sablon_yalniz_ORKA_ve_Luca_icin()
        {
            Assert.NotEmpty(FirmaKunyeService.MizanSablonu("ORKA — döviz kolonlu"));
            Assert.NotEmpty(FirmaKunyeService.MizanSablonu("ORKA — döviz kolonsuz"));
            Assert.NotEmpty(FirmaKunyeService.MizanSablonu("Luca"));

            var mikro = Assert.Throws<FirmaKunyeKuralException>(() => FirmaKunyeService.MizanSablonu("Mikro"));
            Assert.Contains("gerçek mizan çıktısı henüz görülmedi", mikro.Message);
            Assert.Throws<FirmaKunyeKuralException>(() => FirmaKunyeService.MizanSablonu("Logo"));
        }

        // ---- Ortam ----

        private static CatalogContext Ortam(out Firma firma)
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            firma = new Firma { Id = 1, Unvan = "TEST A.Ş.", KisaAd = "TEST", VergiKimlikNo = "1111111111", Aktif = true };
            db.Firmalar.Add(firma);
            db.SaveChanges();
            return db;
        }

        private static Task<FirmaPaneliDto> Panel(CatalogContext db, int firmaId)
            => new FirmaPaneliService(db, () => Bugun).PanelAsync(firmaId);
    }
}
