using CatalogService.Api.Features.BankaEkstre.Services;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Öğrenme anahtarı kalite kapısı (Tur 3, madde 5). Kapı yalnız <b>yeni</b> kayıtta
    /// çalışır; mevcut kaydın düzeltilmesi bu kuraldan bağımsızdır (bkz.
    /// <see cref="HesapEslesmeService.OgrenAsync"/>).
    /// </summary>
    public class AnahtarKalitesiTests
    {
        [Fact]
        public void Kesik_metinden_cikan_anahtar_alakasiz_hesaba_yazilmaz()
        {
            // Ölçülen bozuk kayıt: "SUN TEKS.SAN.VE TİC.A.Ş." metninden çıkan çekirdek,
            // adıyla hiç ilgisi olmayan bir firmaya bağlanmıştı.
            Assert.False(AnahtarKalitesi.Uygun("SUN TEKSSANVE TICAS", "Sungurlu Un Ve Yem Sanayi"));
        }

        [Fact]
        public void Ayni_firmanin_farkli_yazimi_yazilir()
        {
            // Ortak kelime yok ama iki ad da "SUNTEK" ile başlıyor.
            Assert.True(AnahtarKalitesi.Uygun("SUN TEKSSANVE TICAS", "Suntek Teknoloji Anonim Şirketi"));
        }

        [Theory]
        [InlineData("DAGI GIYIM", "Dağı Giyim Sanayi")]               // kapsama
        [InlineData("YURTICI KARGO", "Aras Kargo Yurtiçi Yurtdışı")]  // ortak kelime
        [InlineData("PARDUS PORTFOY", "Pardus Portföy Altın Fonu")]   // kapsama
        public void Adiyla_ortusen_anahtar_yazilir(string cekirdek, string hesapAdi)
            => Assert.True(AnahtarKalitesi.Uygun(cekirdek, hesapAdi));

        [Theory]
        [InlineData("NAOS")]        // tek kelime
        [InlineData("AS TIC")]      // iki kelime ama çok kısa
        public void Zayif_anahtar_yazilmaz(string cekirdek)
            => Assert.False(AnahtarKalitesi.Uygun(cekirdek, "Naos İstanbul Kozmetik"));

        [Theory]
        [InlineData("ISLEM:HGS BAKIYE YUKLE", "Hizmet Üretim Maliyeti")]
        [InlineData("KREDI:6501439328", "İşletme İhtiyaç Kredisi 6501439328")]
        public void Teknik_anahtarlar_kapiya_girmez(string cekirdek, string hesapAdi)
        {
            // Bunlar unvan değil satırın niteliği; hesap adıyla örtüşmeleri beklenmez.
            Assert.True(AnahtarKalitesi.Uygun(cekirdek, hesapAdi));
        }

        [Fact]
        public void Hesap_adi_bilinmiyorsa_kapi_uygulanmaz()
        {
            // Hesap planı hiç yüklenmemiş olabilir; olmayan veriye dayanıp öğrenme durmamalı.
            Assert.True(AnahtarKalitesi.Uygun("DAGI GIYIM", null));
        }

        [Fact]
        public void Gerekce_kullaniciya_gosterilecek_kadar_acik()
        {
            var neden = AnahtarKalitesi.Neden("SUN TEKSSANVE TICAS", "Sungurlu Un Ve Yem Sanayi");

            Assert.NotNull(neden);
            Assert.Contains("örtüşmüyor", neden);
        }
    }
}
