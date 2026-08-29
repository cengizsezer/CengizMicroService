using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.Declarations.Entities;

namespace CatalogService.UnitTests.Anasayfa
{
    /// <summary>
    /// Anasayfa kartlarının sayıları. Kurucu saf fonksiyon olduğu için "bugün" testte
    /// veriliyor: gerçek saate bağlı bir test, ayın sonunda kendiliğinden kırmızıya
    /// dönerdi.
    /// </summary>
    public class AnasayfaOzetKurucuTests
    {
        private static readonly DateTime Bugun = new(2026, 8, 29);

        private const int Yil = 2026;
        private const int Ay = 8;
        private const int Pencere = 15;

        private static Declaration Beyanname(int id, string firma, decimal tutar,
                                             PaymentStatus odeme = PaymentStatus.Pending,
                                             DateTime? vade = null,
                                             string tur = "0015 KDV-1") => new()
        {
            Id = id,
            CompanyName = firma,
            DeclarationType = tur,
            Year = Yil,
            Month = Ay,
            Amount = tutar,
            DueDate = vade ?? new DateTime(Yil, Ay, 26),
            PaymentStatus = odeme
        };

        private static Dictionary<int, string> Firmalar() => new()
        {
            [1] = "ALPHA",
            [3] = "CİTADEL",
            [7] = "PKF ADAY"
        };

        private static CatalogService.Api.Features.Anasayfa.Dtos.AnasayfaOzetDto Kur(
            IEnumerable<Declaration>? ayin = null,
            IEnumerable<Declaration>? yaklasan = null,
            IEnumerable<FirmaBankaOzetiDto>? banka = null)
            => AnasayfaOzetKurucu.Kur(Yil, Ay, Bugun, Pencere,
                (ayin ?? Enumerable.Empty<Declaration>()).ToList(),
                (yaklasan ?? Enumerable.Empty<Declaration>()).ToList(),
                Firmalar(),
                (banka ?? Enumerable.Empty<FirmaBankaOzetiDto>()).ToList());

        // ---- Bekleyen beyanname ----

        [Fact]
        public void Bekleyen_odenmemis_kayitlari_sayar()
        {
            var ozet = Kur(new[]
            {
                Beyanname(1, "ALPHA", 1000m),
                Beyanname(2, "ALPHA", 2000m, PaymentStatus.Planned),
                Beyanname(3, "CİTADEL", 5000m, PaymentStatus.Paid)
            });

            // "Bekleyen" ödemesi tamamlanmamış olan demek: planlanmış da bekliyordur.
            Assert.Equal(2, ozet.BekleyenBeyannameSayisi);
            Assert.Equal(3000m, ozet.BekleyenVergiTutari);

            Assert.Equal(3, ozet.ToplamBeyannameSayisi);
            Assert.Equal(8000m, ozet.ToplamVergiTutari);
        }

        [Fact]
        public void Kayit_yoksa_sayilar_sifir()
        {
            var ozet = Kur();

            Assert.Equal(0, ozet.BekleyenBeyannameSayisi);
            Assert.Equal(0m, ozet.BekleyenVergiTutari);
            Assert.Empty(ozet.YaklasanOdemeler);
            Assert.Empty(ozet.BankaOnayBekleyen);
        }

        // ---- Banka onay bekleyen ----

        [Fact]
        public void Banka_satiri_olmayan_firma_listelenmez()
        {
            var ozet = Kur(banka: new[]
            {
                new FirmaBankaOzetiDto { FirmaId = 1, OnayBekleyen = 0 },
                new FirmaBankaOzetiDto { FirmaId = 3, OnayBekleyen = 12 }
            });

            var satir = Assert.Single(ozet.BankaOnayBekleyen);
            Assert.Equal("CİTADEL", satir.FirmaAdi);
            Assert.Equal(12, satir.OnayBekleyen);
        }

        [Fact]
        public void Firmalar_en_cok_bekleyen_ustte_siralanir()
        {
            var ozet = Kur(banka: new[]
            {
                new FirmaBankaOzetiDto { FirmaId = 1, OnayBekleyen = 3 },
                new FirmaBankaOzetiDto { FirmaId = 3, OnayBekleyen = 40 },
                new FirmaBankaOzetiDto { FirmaId = 7, OnayBekleyen = 12 }
            });

            Assert.Equal(new[] { "CİTADEL", "PKF ADAY", "ALPHA" }, ozet.BankaOnayBekleyen.Select(s => s.FirmaAdi));
        }

        /// <summary>
        /// Liste artık KIRPILMIYOR (KARARLAR §99): kullanıcı bütün firmaların durumunu
        /// anasayfada birlikte görmek istiyor. Bu test önceden kırpmayı doğruluyordu.
        /// Toplamın ayrı hesaplanması ise korundu — satırı olmayan firma listede çıkmaz
        /// ama toplama girer.
        /// </summary>
        [Fact]
        public void Butun_firmalar_listelenir_ve_toplam_hepsini_kapsar()
        {
            var ozetler = Enumerable.Range(1, 11)
                .Select(i => new FirmaBankaOzetiDto { FirmaId = i, OnayBekleyen = i })
                .ToList();

            // Onay bekleyeni olmayan firma listede çıkmaz, toplamı da değiştirmez.
            ozetler.Add(new FirmaBankaOzetiDto { FirmaId = 99, OnayBekleyen = 0 });

            var ozet = Kur(banka: ozetler);

            Assert.Equal(11, ozet.BankaOnayBekleyen.Count);
            Assert.Equal(ozetler.Sum(o => o.OnayBekleyen), ozet.BankaOnayBekleyenToplam);
        }

        [Fact]
        public void Adi_bulunamayan_firma_id_ile_gosterilir()
        {
            // Firma pasife alınmış olabilir; kart yine de sayıyı gösterir, satır kaybolmaz.
            var ozet = Kur(banka: new[] { new FirmaBankaOzetiDto { FirmaId = 99, OnayBekleyen = 5 } });

            Assert.Equal("Firma 99", ozet.BankaOnayBekleyen[0].FirmaAdi);
        }

        // ---- Yaklaşan ödemeler ----

        [Fact]
        public void Yaklasan_odemeler_tarihe_gore_siralanir_ve_gun_hesaplanir()
        {
            var ozet = Kur(yaklasan: new[]
            {
                Beyanname(1, "ALPHA", 100m, vade: Bugun.AddDays(5)),
                Beyanname(2, "CİTADEL", 200m, vade: Bugun.AddDays(1)),
                Beyanname(3, "PKF ADAY", 300m, vade: Bugun.AddDays(-2))
            });

            Assert.Equal(new[] { 3, 2, 1 }, ozet.YaklasanOdemeler.Select(o => o.DeclarationId));
            Assert.Equal(-2, ozet.YaklasanOdemeler[0].GunKaldi);
            Assert.True(ozet.YaklasanOdemeler[0].Gecikmis);
            Assert.False(ozet.YaklasanOdemeler[1].Gecikmis);
        }

        [Fact]
        public void Odenmis_kayit_yaklasan_listesine_girmez()
        {
            var ozet = Kur(yaklasan: new[]
            {
                Beyanname(1, "ALPHA", 100m, PaymentStatus.Paid, Bugun.AddDays(2)),
                Beyanname(2, "CİTADEL", 200m, PaymentStatus.Pending, Bugun.AddDays(3))
            });

            var odeme = Assert.Single(ozet.YaklasanOdemeler);
            Assert.Equal(2, odeme.DeclarationId);
        }

        [Fact]
        public void Yaklasan_liste_kirpilir()
        {
            var kayitlar = Enumerable.Range(1, AnasayfaOzetKurucu.EnFazlaOdeme + 5)
                .Select(i => Beyanname(i, "ALPHA", 100m, vade: Bugun.AddDays(i)))
                .ToList();

            var ozet = Kur(yaklasan: kayitlar);

            Assert.Equal(AnasayfaOzetKurucu.EnFazlaOdeme, ozet.YaklasanOdemeler.Count);
            // En yakın tarihliler kalır.
            Assert.Equal(1, ozet.YaklasanOdemeler[0].DeclarationId);
        }

        [Fact]
        public void Bugun_vadesi_dolan_gecikmis_sayilmaz()
        {
            var ozet = Kur(yaklasan: new[] { Beyanname(1, "ALPHA", 100m, vade: Bugun) });

            Assert.Equal(0, ozet.YaklasanOdemeler[0].GunKaldi);
            Assert.False(ozet.YaklasanOdemeler[0].Gecikmis);
        }

        [Fact]
        public void Donem_ve_pencere_ozette_tasinir()
        {
            var ozet = Kur();

            // Ekran "önümüzdeki N günde" derken bu değeri yazıyor; iki yerde ayrı
            // sabit tutulsaydı metin ile veri ayrışırdı.
            Assert.Equal(Yil, ozet.Yil);
            Assert.Equal(Ay, ozet.Ay);
            Assert.Equal(Pencere, ozet.OdemePenceresiGun);
        }
    }
}
