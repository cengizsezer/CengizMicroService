using WebApp.Shared.Dto.BankaEkstre;

namespace WebApp.UnitTests.BankaEkstre
{
    /// <summary>
    /// Kategoriler görünümünün iki kolonu doğrudan DTO'dan okunuyor: hesap kodu metni ve
    /// "boş mu" bayrağı. Görünüm sade tutulduğu için tek vurgu kuralsız kategoridir —
    /// o satır kırmızı ve sayı yerine "yok" yazıyor.
    /// </summary>
    public class KategoriGorunumuTests
    {
        [Fact]
        public void Tek_kod_oldugu_gibi_yazilir()
        {
            var kategori = new KategoriKapsamDto { HesapKodlari = new List<string> { "770 03 005" }, KuralSayisi = 1 };

            Assert.Equal("770 03 005", kategori.KodMetni);
            Assert.False(kategori.Bos);
        }

        [Fact]
        public void Birden_fazla_kod_orta_noktayla_birlestirilir()
        {
            var kategori = new KategoriKapsamDto { HesapKodlari = new List<string> { "195", "196" }, KuralSayisi = 2 };

            Assert.Equal("195 · 196", kategori.KodMetni);
        }

        [Fact]
        public void Kuralsiz_kategori_bos_isaretlenir()
        {
            // Yeni banka eklenirken bu satırlar kontrol listesidir: kırmızı zemin + "yok".
            var kategori = new KategoriKapsamDto { KuralSayisi = 0 };

            Assert.True(kategori.Bos);
            Assert.Equal("—", kategori.KodMetni);
        }

        [Fact]
        public void Sablon_satirlari_kod_kolonunu_doldurmaz()
        {
            // Şablon açıklama üretir, karşı hesabı belirlemez; kodu da yoktur.
            var kategori = new KategoriKapsamDto
            {
                KuralSayisi = 1,
                Kurallar = new List<KategoriKuralDto>
                {
                    new() { Mekanizma = "şablon", Ad = "Hesaplar Arası EFT", HesapKodu = null }
                }
            };

            Assert.Equal("—", kategori.KodMetni);
            Assert.False(kategori.Bos);
        }
    }
}
