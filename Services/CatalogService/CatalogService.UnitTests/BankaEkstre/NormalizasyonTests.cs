using CatalogService.Api.Features.BankaEkstre.Services;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>Türkçe karakter sadeleştirmesi, gürültü kelime temizliği ve kod yardımcıları.</summary>
    public class NormalizasyonTests
    {
        [Theory]
        [InlineData("İstanbul", "ISTANBUL")]
        [InlineData("ŞİŞLİ", "SISLI")]
        [InlineData("Çığır", "CIGIR")]
        [InlineData("Öztürk", "OZTURK")]
        [InlineData("Güneş", "GUNES")]
        public void Turkce_karakterleri_sadelestirir(string girdi, string beklenen)
            => Assert.Equal(beklenen, Normalizasyon.TurkceSadelestir(girdi));

        [Fact]
        public void Gurultu_kelimelerini_atar()
        {
            var normal = Normalizasyon.UnvanNormalize("DAĞI GİYİM SANAYİ VE TİCARET ANONİM ŞİRKETİ");

            Assert.Equal("DAGI GIYIM", normal);
        }

        [Fact]
        public void Alfanumerik_disini_bosluga_cevirir()
        {
            var normal = Normalizasyon.UnvanNormalize("PKF-İSTANBUL, Y.M.M.");

            // Kısaltmadaki noktalar silinir; "Y.M.M." tek parça kalır.
            Assert.Equal("PKF ISTANBUL YMM", normal);
        }

        [Fact]
        public void Noktali_sirket_kisaltmasi_gurultu_sayilir()
        {
            // "A.Ş." → "AS" → gürültü listesinde; nokta boşluğa çevrilseydi
            // "A" ve "S" ayrı kelime kalır, eşleştirme skorunu düşürürdü.
            Assert.Equal("DAGI GIYIM", Normalizasyon.UnvanNormalize("DAĞI GİYİM SANAYİ A.Ş."));
        }

        [Fact]
        public void Baslik_bicimi_noktali_kisaltmayi_bozmaz()
            => Assert.Equal("Dağı Giyim A.Ş.", Normalizasyon.BaslikBicimi("DAĞI GİYİM A.Ş."));

        [Fact]
        public void Tamami_gurultuyse_bos_donmez()
        {
            // Aksi hâlde "LTD ŞTİ" gibi bir unvan tamamen kaybolur ve eşleştirme kör kalırdı.
            var normal = Normalizasyon.UnvanNormalize("LTD ŞTİ");

            Assert.Equal("LTD STI", normal);
        }

        [Fact]
        public void Ayni_aciklama_ayni_hashe_duser()
        {
            var a = Normalizasyon.AciklamaHash("0000123 sorgu numaralı DAĞI GİYİM tarafından");
            var b = Normalizasyon.AciklamaHash("0000999 sorgu numaralı DAGI GIYIM tarafından");

            // Sayılar atıldığı için aynı gönderici her seferinde aynı anahtara düşer.
            Assert.Equal(a, b);
            Assert.NotEmpty(a);
        }

        [Fact]
        public void Farkli_aciklama_farkli_hashe_duser()
        {
            var a = Normalizasyon.AciklamaHash("DAĞI GİYİM tarafından");
            var b = Normalizasyon.AciklamaHash("KEMAL TEKSTİL tarafından");

            Assert.NotEqual(a, b);
        }

        [Fact]
        public void Hesap_kodu_bosluklu_kalir()
        {
            Assert.Equal("120 D22", Normalizasyon.HesapKoduNormalize("  120   d22 "));
            Assert.Equal("102 1 1 01", Normalizasyon.HesapKoduNormalize("102 1 1 01"));
        }

        [Fact]
        public void Ana_grup_ve_baslangic_harfi()
        {
            Assert.Equal("120", Normalizasyon.AnaGrup("120 D22"));
            Assert.Equal("D", Normalizasyon.BaslangicHarfi("120 D22"));
            Assert.Equal("329", Normalizasyon.AnaGrup("329 K08"));
            Assert.Equal("K", Normalizasyon.BaslangicHarfi("329 K08"));
            // Tamamı sayısal kodda harf yoktur.
            Assert.Null(Normalizasyon.BaslangicHarfi("102 1 1 01"));
        }

        [Fact]
        public void Ibani_metinden_cikarir()
        {
            Assert.Equal("TR330006200012300006673953",
                Normalizasyon.IbanBul("TR33 0006 2000 1230 0006 6739 53 nolu hesaba"));

            Assert.Null(Normalizasyon.IbanBul("TR33 0006 **** **** **** 6739 53"));
            Assert.Null(Normalizasyon.IbanBul("açıklama içinde IBAN yok"));
        }

        [Fact]
        public void Baslik_bicimi_her_kelimeyi_buyutur()
            => Assert.Equal("Gelen Eft - Dağı Giyim", Normalizasyon.BaslikBicimi("GELEN EFT - DAĞI GİYİM"));

        [Fact]
        public void Kirpma_sinirla_ve_bosluklari_sadelestirir()
        {
            Assert.Equal("Gelen Eft", Normalizasyon.Kirp("  Gelen    Eft  ", 50));
            Assert.Equal(10, Normalizasyon.Kirp(new string('A', 80), 10).Length);
        }

        [Fact]
        public void Vkn_anahtari_yalniz_10_veya_11_hane_kabul_eder()
        {
            Assert.Equal("1234567890", Normalizasyon.VknAnahtar("123 456 7890"));
            Assert.Equal(string.Empty, Normalizasyon.VknAnahtar("12345"));
        }
    }
}
