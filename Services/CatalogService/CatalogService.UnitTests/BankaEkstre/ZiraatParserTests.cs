using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Ziraat ayrıştırıcısı. Asıl sınanan şey dosyanın kendisi: gerçek ekstrenin
    /// <c>styles.xml</c>'i bozuk ve biçim tablosunu okuyan kütüphaneler orada patlıyor.
    /// Değerler sağlam olduğu için ayrıştırıcı ham XML yoluna düşüp dosyayı okuyabilmeli
    /// — ve düştüğünü de söylemeli.
    ///
    /// Ham açıklamalar gerçek 7 aylık ekstreden birebir alınmıştır.
    /// </summary>
    public class ZiraatParserTests
    {
        private readonly ZiraatVadesizParser _parser = new();

        // ---- Gerçek dosyadan ham açıklamalar ----

        private const string VadeliKasa = "62286065-5010";

        private const string VadeliVirman =
            "707-62286065-5003 No.lu Vds. Hes.tan 62286065-5022 -Hes.Açılış";

        private const string HesaplarArasi =
            "Gönd: PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ HESAPLAR ARASI E.F.T. Ziraat Bankası TL";

        private const string BurakGunel =
            "Enpara Bank A.Ş./TR380015700000000105549208-BURAK GÜNEL/2026000000008 NL. FT. ÖDEMESİ";

        private const string Hanedan =
            "31.12.2026 Sorumlu Tedarik Zinciri Güvence Denetimi 1. Taksit HANEDAN DÖVİZ VE ALTIN TİC";

        private const string DovizSatis =
            "0707GKDS26000982 Ref,USD 2000TL.92919,40 KMV Matrahı İnternet Döviz Satış İşlemi";

        private static MemoryStream Ekstre() => UcBankaTestOrtami.ZiraatEkstresi(
            new object?[] { "26.08.2026", "F08179", VadeliVirman, -50000.00m },
            new object?[] { "26.08.2026", "F08180", BurakGunel, -12500.75m },
            new object?[] { "25.08.2026", "F08175", Hanedan, 34000.00m });

        // ---- Dosya ve kolonlar ----

        [Fact]
        public void Basliklar_isimle_bulunur_ve_satirlar_ayrisir()
        {
            using var dosya = Ekstre();

            var sonuc = _parser.Ayristir(dosya);

            Assert.Empty(sonuc.Uyarilar);
            Assert.Equal(3, sonuc.Satirlar.Count);
            Assert.Equal(3, sonuc.AciklamaKolonu);
            Assert.Equal(13, sonuc.Satirlar[0].KaynakSatirNo);
            Assert.Equal(new DateTime(2026, 8, 26), sonuc.Satirlar[0].Tarih);
            Assert.Equal("F08179", sonuc.Satirlar[0].Referans);
        }

        [Fact]
        public void Yon_tutarin_isaretinden_okunur()
        {
            using var dosya = Ekstre();

            var satirlar = _parser.Ayristir(dosya).Satirlar;

            Assert.Equal(Yon.Cikan, satirlar[0].Yon);
            Assert.Equal(50000.00m, satirlar[0].Tutar);
            Assert.Equal(Yon.Giren, satirlar[2].Yon);
            // İşlem tipi kolonu yok; niteliği açıklama belirliyor.
            Assert.Equal(string.Empty, satirlar[0].IslemTipi);
        }

        // ---- Bozuk stil tablosu ----

        [Fact]
        public void Bozuk_stil_tablosuna_ragmen_satirlar_ayrisir()
        {
            using var saglam = Ekstre();
            using var bozuk = UcBankaTestOrtami.StilTablosuBozuk(saglam);

            var sonuc = _parser.Ayristir(bozuk);

            // openpyxl'in "expected <class 'openpyxl.styles.fills.Fill'>" ile durduğu yer.
            // Değerler sağlam olduğu için ayrıştırma tam olmalı.
            Assert.Equal(3, sonuc.Satirlar.Count);
            Assert.Equal(BurakGunel, sonuc.Satirlar[1].HamAciklama);
            Assert.Equal(12500.75m, sonuc.Satirlar[1].Tutar);
        }

        [Fact]
        public void Yedek_okuyucuya_dusuldugu_uyariya_yazilir()
        {
            using var saglam = Ekstre();
            using var bozuk = UcBankaTestOrtami.StilTablosuBozuk(saglam);

            var sonuc = _parser.Ayristir(bozuk);

            // Sessizce düşülmez: hangi okuyucunun neden başarısız olduğu görünür kalmalı.
            Assert.Contains(sonuc.Uyarilar, u => u.Contains("ClosedXML"));
        }

        [Fact]
        public void Ham_xml_okuyucusu_bozuk_dosyayi_dogrudan_okur()
        {
            using var saglam = Ekstre();
            using var bozuk = UcBankaTestOrtami.StilTablosuBozuk(saglam);

            var tablo = HamXlsxOkuyucu.Oku(bozuk);

            // Başlık satırı 12, veri 13'ten; yedek yol kolon yerleşimini de korumalı.
            var baslik = tablo.Satirlar.Single(s => s.SatirNo == 12);
            Assert.Equal("Açıklama", baslik.Hucre(3).Metin);

            var ilkVeri = tablo.Satirlar.Single(s => s.SatirNo == 13);
            Assert.Equal(VadeliVirman, ilkVeri.Hucre(3).Metin);
            Assert.Equal(-50000.00, ilkVeri.Hucre(4).Sayi);
        }

        // ---- Unvan çıkarma ----

        [Fact]
        public void Iban_sonrasindaki_ad_unvan_olarak_alinir()
        {
            // IBAN'ın ÖNÜNDEKİ ad karşı tarafın bankası ("Enpara Bank A.Ş."), unvan değil;
            // o kalıp için desen tanımlanmadı.
            Assert.Equal("BURAK GÜNEL", Cikar(BurakGunel).Unvan);
        }

        [Fact]
        public void Aciklama_sonundaki_buyuk_harfli_unvan_alinir()
        {
            Assert.Equal("HANEDAN DÖVİZ VE ALTIN TİC", Cikar(Hanedan).Unvan);
        }

        [Fact]
        public void Bankalar_arasi_satirda_hesap_sahibi_elenir()
        {
            var sonuc = Cikar(HesaplarArasi);

            Assert.Null(sonuc.Unvan);
            Assert.True(sonuc.HesapSahibiElendi);
        }

        [Theory]
        [InlineData(VadeliKasa)]
        [InlineData(VadeliVirman)]
        [InlineData(DovizSatis)]
        public void Unvan_tasimayan_satirlarda_unvan_uydurulmaz(string aciklama)
        {
            Assert.Null(Cikar(aciklama).Unvan);
        }

        private static UnvanSonuc Cikar(string aciklama)
            => new UnvanCikarici().Cikar(aciklama, UcBankaTestOrtami.ZiraatDesenleri(),
                                         UcBankaTestOrtami.HesapSahibi);
    }
}
