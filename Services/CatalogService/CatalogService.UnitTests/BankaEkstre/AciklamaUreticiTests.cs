using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>Şablon seçimi, yer tutucu doldurma, Title Case ve 50 karakter sınırı.</summary>
    public class AciklamaUreticiTests
    {
        private readonly AciklamaUretici _uretici = new();
        private static readonly List<AciklamaSablonu> Sablonlar = BankaEkstreTestOrtami.Sablonlar();

        private SatirBaglami Baglam(string islemTipi, string hamAciklama = "", string? unvan = null, string? banka = null)
        {
            var baglam = new SatirBaglami
            {
                IslemTipi = islemTipi,
                HamAciklama = hamAciklama,
                Unvan = unvan,
                BankaAdi = banka,
                Yon = Yon.Giren
            };
            baglam.Sablon = _uretici.SablonBul(islemTipi, Sablonlar);
            return baglam;
        }

        [Fact]
        public void Sablonu_doldurur_ve_baslik_bicimine_cevirir()
        {
            // Türkçe kültürüyle büyütülür: "GİYİM" → "Giyim" (noktalı i korunur).
            var aciklama = _uretici.Uret(Baglam("Gelen EFT Otomatik Yatan", unvan: "DAĞI GİYİM SANAYİ"));

            Assert.Equal("Gelen Eft - Dağı Giyim Sanayi", aciklama);
        }

        [Fact]
        public void Elli_karakteri_asmaz()
        {
            var uzunUnvan = "ULUSLARARASI TASIMACILIK VE LOJISTIK HIZMETLERI TICARET";

            var aciklama = _uretici.Uret(Baglam("Gelen EFT Otomatik Yatan", unvan: uzunUnvan));

            Assert.True(aciklama.Length <= AciklamaUretici.EnFazlaUzunluk,
                $"Açıklama {aciklama.Length} karakter: ORKA 50 karakterde kesiyor.");
        }

        [Fact]
        public void Unvan_yoksa_yer_tutucu_ve_ayrac_duser()
        {
            var aciklama = _uretici.Uret(Baglam("Gelen EFT Otomatik Yatan", unvan: null));

            Assert.Equal("Gelen Eft", aciklama);
            Assert.DoesNotContain("{UNVAN}", aciklama);
        }

        [Fact]
        public void Yer_tutucusuz_sablon_oldugu_gibi_kullanilir()
            => Assert.Equal("Banka Gideri", _uretici.Uret(Baglam("MKK Masrafı")));

        [Fact]
        public void Bankalar_arasi_harekette_banka_adi_kullanilir()
        {
            var aciklama = _uretici.Uret(Baglam("Virman", banka: "Akbank"));

            Assert.Equal("Hesaplararası Virman - Akbank", aciklama);
        }

        [Fact]
        public void Plakayi_aciklamadan_alir()
        {
            var aciklama = _uretici.Uret(Baglam("HGS Bakiye Yükle", hamAciklama: "34 ABC 123 plakalı araç HGS yükleme"));

            Assert.Equal("Hgs Bakiye Yüklemesi - 34 Abc 123", aciklama);
        }

        [Fact]
        public void Vergi_turunu_aciklamadan_alir()
        {
            var aciklama = _uretici.Uret(Baglam("Vergi Tahsilatı", hamAciklama: "KDV beyannamesi tahsilatı"));

            Assert.Equal("Vergi Ödemesi - Kdv", aciklama);
        }

        [Fact]
        public void Sablon_yoksa_islem_tipi_ve_unvandan_uretilir()
        {
            // Uydurma yapılmaz; bankanın kendi metni düzenlenerek kullanılır.
            var aciklama = _uretici.Uret(Baglam("Bilinmeyen İşlem", unvan: "ALFA TİCARET"));

            Assert.Equal("Bilinmeyen İşlem - Alfa Ticaret", aciklama);
        }

        [Fact]
        public void Icerir_eslesmesi_calisir()
        {
            var sablonlar = new List<AciklamaSablonu>
            {
                new() { IslemTipiDeseni = "Otoyolu Bakiye Yükle", EslesmeTuru = EslesmeTuru.Icerir,
                        Sablon = "Hgs Bakiye Yüklemesi - {PLAKA}", Sira = 10, Aktif = true }
            };

            var sablon = _uretici.SablonBul("Kuzey Marmara Otoyolu Bakiye Yükle", sablonlar);

            Assert.NotNull(sablon);
        }
    }
}
