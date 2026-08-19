using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Parser testleri: tarih/tutar/yön doğru çıkıyor mu, IBAN açıklamadan alınıyor mu,
    /// başlık bulunamadığında sabit indekslere düşüp uyarı yazıyor mu.
    /// </summary>
    public class VakifbankParserTests
    {
        private readonly VakifbankVadesizParser _parser = new();

        [Fact]
        public void Basliktan_kolonlari_bulur_ve_satirlari_ayristirir()
        {
            using var dosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "15.01.2026", "Gelen EFT Otomatik Yatan", 12500.75m, "EFT", "1234567890", "A", "0000123 sorgu numaralı DAGI GIYIM SANAYI A.S. tarafından gönderilmiştir" },
                new object[] { "16.01.2026", "Gönderilen havale", -3400.00m, "İnternet", "", "B", "TR33 0006 2000 1230 0006 6739 53 nolu KEMAL TEKSTIL LTD hesabına" });

            var sonuc = _parser.Ayristir(dosya);

            Assert.Empty(sonuc.Uyarilar);
            Assert.Equal(2, sonuc.Satirlar.Count);

            var ilk = sonuc.Satirlar[0];
            Assert.Equal(1, ilk.SiraNo);
            Assert.Equal(new DateTime(2026, 1, 15), ilk.Tarih);
            Assert.Equal(Yon.Giren, ilk.Yon);
            Assert.Equal(12500.75m, ilk.Tutar);
            Assert.Equal("Gelen EFT Otomatik Yatan", ilk.IslemTipi);
            Assert.Equal("1234567890", ilk.KarsiVkn);
            Assert.Equal("EFT", ilk.Kanal);

            var ikinci = sonuc.Satirlar[1];
            Assert.Equal(Yon.Cikan, ikinci.Yon);
            // Tutar her zaman pozitif saklanır; işaret Yon alanında durur.
            Assert.Equal(3400.00m, ikinci.Tutar);
        }

        [Fact]
        public void Aciklamadaki_ibani_cikarir()
        {
            using var dosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "16.01.2026", "Gönderilen havale", -100m, "", "", "B", "TR33 0006 2000 1230 0006 6739 53 nolu KEMAL TEKSTIL hesabına" });

            var satir = _parser.Ayristir(dosya).Satirlar.Single();

            Assert.Equal("TR330006200012300006673953", satir.KarsiIban);
        }

        [Fact]
        public void Maskeli_ibani_anahtar_olarak_kabul_etmez()
        {
            using var dosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "16.01.2026", "Gönderilen havale", -100m, "", "", "B", "TR33 0006 **** **** **** 6739 53 nolu hesaba" });

            var satir = _parser.Ayristir(dosya).Satirlar.Single();

            Assert.Null(satir.KarsiIban);
        }

        [Fact]
        public void Baslik_yoksa_sabit_indekslere_duser_ve_uyarir()
        {
            using var dosya = BankaEkstreTestOrtami.BasliksizEkstre(
                new object[] { "15.01.2026", "Gelen EFT Otomatik Yatan", 500m, "EFT", "", "A", "Test açıklaması" });

            var sonuc = _parser.Ayristir(dosya);

            var satir = Assert.Single(sonuc.Satirlar);
            Assert.Equal(new DateTime(2026, 1, 15), satir.Tarih);
            Assert.Equal(500m, satir.Tutar);
            Assert.Contains(sonuc.Uyarilar, u => u.Contains("sabit kolon indekslerine"));
        }

        [Fact]
        public void Isaretsiz_tutarda_yonu_borc_alacak_kolonundan_alir()
        {
            using var dosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "17.01.2026", "Gönderilen havale", 250m, "", "", "B", "Açıklama" },
                new object[] { "17.01.2026", "Gelen EFT Ödeme", 250m, "", "", "A", "Açıklama" });

            var sonuc = _parser.Ayristir(dosya);

            Assert.Equal(Yon.Cikan, sonuc.Satirlar[0].Yon);
            Assert.Equal(Yon.Giren, sonuc.Satirlar[1].Yon);
        }

        [Fact]
        public void Veri_olmayan_satirlari_atlar_ve_donemi_hesaplar()
        {
            using var dosya = BankaEkstreTestOrtami.BasliklıEkstre(
                new object[] { "20.01.2026", "Gelen EFT Ödeme", 100m, "", "", "A", "Açıklama" },
                new object[] { "", "TOPLAM", "", "", "", "", "" },
                new object[] { "05.01.2026", "Gönderilen havale", -50m, "", "", "B", "Açıklama" });

            var sonuc = _parser.Ayristir(dosya);

            Assert.Equal(2, sonuc.Satirlar.Count);
            Assert.Equal(new DateTime(2026, 1, 5), sonuc.DonemBaslangic);
            Assert.Equal(new DateTime(2026, 1, 20), sonuc.DonemBitis);
        }
    }
}
