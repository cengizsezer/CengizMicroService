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
        public void Unvan_cekirdegi_tek_harfli_tokenlari_atar()
        {
            // Tek harf iki farklı cariyi birbirine yaklaştırmaktan başka bir şey yapmıyor.
            Assert.Equal("DAGI GIYIM", Normalizasyon.UnvanCekirdek("DAĞI X GİYİM SANAYİ A.Ş."));
        }

        [Fact]
        public void Ayni_cari_farkli_sorgu_numarasiyla_ayni_cekirdege_duser()
        {
            // Öğrenme anahtarının bütün meselesi bu: ham hash asla ikinci kez eşleşmiyordu.
            var a = Normalizasyon.UnvanCekirdek("DAĞI GİYİM SANAYİ VE TİCARET ANONİM ŞİRKETİ");
            var b = Normalizasyon.UnvanCekirdek("DAGI GIYIM SAN. TIC. A.Ş.");

            Assert.Equal("DAGI GIYIM", a);
            Assert.Equal(a, b);
        }

        [Fact]
        public void Farkli_cari_farkli_cekirdege_duser()
            => Assert.NotEqual(Normalizasyon.UnvanCekirdek("DAĞI GİYİM"),
                               Normalizasyon.UnvanCekirdek("KEMAL TEKSTİL"));

        [Fact]
        public void Unvansiz_satir_islem_tipinden_anahtar_alir()
        {
            Assert.Equal("ISLEM:MKK MASRAFI", Normalizasyon.IslemAnahtari("MKK Masrafı"));
            Assert.Equal(string.Empty, Normalizasyon.IslemAnahtari("   "));
        }

        [Fact]
        public void Kredi_taksit_satiri_kredi_hesap_numarasindan_anahtar_alir()
        {
            // İşlem tipi ("Taksitli Tahsilat") bütün kredilerde aynı; ayırt eden numara.
            Assert.Equal("KREDI:6501439328", Normalizasyon.KrediAnahtar(
                "6501439328  kredi hesap numaralı İşletme İhtiyaç Kredisi   Tam  Taksit  Tahsilatı  8  Taksit "));

            Assert.Equal(string.Empty, Normalizasyon.KrediAnahtar("0000123 sorgu numaralı DAĞI GİYİM tarafından"));
        }

        [Fact]
        public void Metindeki_butun_ibanlar_bulunur()
        {
            // Döviz ve virman satırlarında iki IBAN geçiyor; ilki hesabın kendisi.
            var ibanlar = Normalizasyon.IbanlariBul(
                "PKF ADAY TR40 0001 5001 5800 7298 4901 00 nolu hesabından " +
                "TR80 0001 5001 5804 8013 1394 00 nolu hesabına döviz alış");

            Assert.Equal(new[] { "TR400001500158007298490100", "TR800001500158048013139400" }, ibanlar);

            // IbanBul ilkini vermeye devam eder.
            Assert.Equal("TR400001500158007298490100", Normalizasyon.IbanBul(
                "PKF ADAY TR40 0001 5001 5800 7298 4901 00 nolu hesabından TR80 0001 5001 5804 8013 1394 00 nolu"));
        }

        [Fact]
        public void Iban_anahtari_bosluklu_ve_bitisik_yazimi_esitler()
            => Assert.Equal(Normalizasyon.IbanAnahtar("TR80 0001 5001 5804 8013 1394 00"),
                            Normalizasyon.IbanAnahtar("TR800001500158048013139400"));

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

        [Theory]
        [InlineData("OTOMATIK SUPURME PKF ADAY", "OTOMATIK SUPURME", true)]
        [InlineData("OTOMATIK SUPURME PKF ADAY", "SUPURME", true)]
        [InlineData("OTEBANK HESABINA VIRMAN", "TEB", false)]
        [InlineData("ZIRAAT TEB VIRMAN", "TEB", true)]
        [InlineData("VAKIFBANKA GONDERILDI", "VAKIFBANK", false)]
        [InlineData("", "TEB", false)]
        public void Ifade_tam_kelime_siniriyla_aranir(string metin, string ifade, bool bekleniyor)
            => Assert.Equal(bekleniyor, Normalizasyon.IfadeVarMi(metin, ifade));
    }
}
