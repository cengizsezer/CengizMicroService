using CatalogService.Api.Features.Declarations;
using CatalogService.Api.Features.Declarations.Entities;
using CatalogService.Api.Features.Declarations.Services;

namespace CatalogService.UnitTests.Beyannameler
{
    /// <summary>
    /// Beyanname türü eşleştirmesi. Tür bugüne kadar ekrandaki sabit listeden seçilen
    /// serbest metindi; kayıtlarda farklı yazımlar bir arada duruyor ve kolonu bulunamayan
    /// kayıt matriste hiç görünmüyor. Testler bu yüzden <b>yazım çeşitlerini</b> sınıyor.
    /// </summary>
    public class BeyannameTuruEsleyiciTests
    {
        private static List<BeyannameTuru> Turler()
        {
            var sira = 0;
            return BeyannameTuruSeed.Turler.Select(t => new BeyannameTuru
            {
                Id = ++sira,
                Deger = t.Deger,
                Kod = t.Kod,
                Ad = t.Ad,
                Sira = sira * 10,
                Aktif = true
            }).ToList();
        }

        [Fact]
        public void Saklanan_degerin_tamami_eslesir()
        {
            var tur = BeyannameTuruEsleyici.Esle(Turler(), "0015 KDV-1");

            Assert.NotNull(tur);
            Assert.Equal("KDV (1 No.lu)", tur!.Ad);
        }

        [Theory]
        // Ayraç ve boşluk farkları önemsiz.
        [InlineData("0015 KDV 1")]
        [InlineData("0015  kdv-1")]
        [InlineData("0015 KDV_1")]
        public void Yazim_farklari_ayni_ture_duser(string yazim)
        {
            var tur = BeyannameTuruEsleyici.Esle(Turler(), yazim);

            Assert.Equal("0015", tur?.Kod);
        }

        [Fact]
        public void Turkce_buyuk_kucuk_harf_tuzagina_dusmez()
        {
            // Invariant kültür 'ı' → 'I' ve 'i' → 'İ' dönüşümünü yapmaz; düz
            // OrdinalIgnoreCase karşılaştırması burada ayrışıyordu.
            var tur = BeyannameTuruEsleyici.Esle(Turler(), "0033 gecici vergi");

            Assert.Equal("0033", tur?.Kod);
        }

        [Fact]
        public void Yalnizca_vergi_kodu_yazilmissa_kodla_eslesir()
        {
            // Kayıt "0010 KURUMLAR" diye yazılmış olsa da kod aynı.
            var tur = BeyannameTuruEsleyici.Esle(Turler(), "0010 KURUMLAR");

            Assert.Equal("Kurumlar Vergisi", tur?.Ad);
        }

        [Fact]
        public void Kodsuz_saklanan_tur_kodla_da_bulunur()
        {
            // Eski listede "SGK" kodsuzdu; kayıtlarda "4101 SGK PRİMİ" de olabiliyor.
            Assert.Equal("SGK Primi", BeyannameTuruEsleyici.Esle(Turler(), "SGK")?.Ad);
            Assert.Equal("SGK Primi", BeyannameTuruEsleyici.Esle(Turler(), "4101 SGK PRİMİ")?.Ad);
        }

        [Fact]
        public void Okunur_adla_da_eslesir()
        {
            Assert.Equal("0003", BeyannameTuruEsleyici.Esle(Turler(), "Gelir Vergisi Stopajı")?.Kod);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("9999 TANIMSIZ VERGİ")]
        public void Taninmayan_metin_null_doner(string? metin)
        {
            // Tahmin edilmez: eşleşmeyen tür matriste "eşleşmeyen" diye raporlanır.
            Assert.Null(BeyannameTuruEsleyici.Esle(Turler(), metin));
        }

        [Fact]
        public void Tanim_listesi_bossa_eslesme_denenmez()
        {
            Assert.Null(BeyannameTuruEsleyici.Esle(new List<BeyannameTuru>(), "0015 KDV-1"));
        }
    }
}
