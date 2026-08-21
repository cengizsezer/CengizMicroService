using CatalogService.Api.Features.BankaEkstre.Services;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Eşleştirme anahtarlarının ayrıştırılması, saklama biçimi ve hesap adından üretilen
    /// öneri. Öneri bilerek dar: fazla anahtar, yanlış hesaba eşlemekten daha kötü değil
    /// ama kullanıcının düzeltmesi gereken bir gürültü.
    /// </summary>
    public class EslestirmeAnahtariTests
    {
        [Fact]
        public void Liste_bosluklari_kirpar_ve_tekrari_eler()
        {
            var anahtarlar = EslestirmeAnahtari.Ayristir("  Otomatik   Süpürme ,, Süpürme, otomatik süpürme ");

            Assert.Equal(new[] { "Otomatik Süpürme", "Süpürme" }, anahtarlar);
        }

        [Fact]
        public void Duzenle_bos_listeyi_null_yapar()
        {
            Assert.Null(EslestirmeAnahtari.Duzenle("   ,  , "));
            Assert.Null(EslestirmeAnahtari.Duzenle(null));
            Assert.Equal("Marifetli, Maslak", EslestirmeAnahtari.Duzenle(" Marifetli ,Maslak"));
        }

        [Fact]
        public void Normalize_anahtarlar_kisa_olanlari_eler()
        {
            var anahtarlar = EslestirmeAnahtari.NormalizeAnahtarlar("Otomatik Süpürme, TL, Blokaj");

            // "TL" iki harf: her açıklamada geçer, yok sayılır.
            Assert.Equal(new[] { "OTOMATIK SUPURME", "BLOKAJ" }, anahtarlar);
        }

        [Theory]
        [InlineData("Vakıfbank, Vadeli Tl - Otomatik Süpürme Hesabı", "Vakıfbank", "Otomatik Süpürme")]
        [InlineData("Ziraat Bankası, Günlük Kazanan Hesap - 5022", "Ziraat", "Günlük Kazanan")]
        [InlineData("Teb, Marifetli Tl - Maslak, 129-154401190", "TEB", "Marifetli, Maslak")]
        public void Oneri_banka_adini_ve_genel_kelimeleri_atar(string hesapAdi, string bankaAdi, string bekleniyor)
            => Assert.Equal(bekleniyor, EslestirmeAnahtari.Oner(hesapAdi, bankaAdi));

        [Fact]
        public void Oneri_ayirt_edici_kelime_kalmayinca_bos_doner()
        {
            // Ada bankanın adı ve genel kelimelerden başka bir şey yok.
            Assert.Null(EslestirmeAnahtari.Oner("Vakıfbank, Vadesiz Tl", "Vakıfbank"));
            Assert.Null(EslestirmeAnahtari.Oner(null, "Vakıfbank"));
        }

        [Fact]
        public void Oneri_kaydedilen_bicimde_uretilir()
        {
            // Öneri doğrudan alana yazılıyor; Duzenle'den geçmiş olmalı ki tekrar/boşluk kalmasın.
            var oneri = EslestirmeAnahtari.Oner("Akbank, Vadeli Tl Serbest Plus, Blokaj", "Akbank");

            Assert.Equal("Serbest Plus, Blokaj", oneri);
            Assert.Equal(oneri, EslestirmeAnahtari.Duzenle(oneri));
        }
    }
}
