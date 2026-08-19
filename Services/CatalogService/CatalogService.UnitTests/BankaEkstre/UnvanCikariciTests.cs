using CatalogService.Api.Features.BankaEkstre.Services;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Ölçülen altı desenin her biri için en az bir örnek. Desenler sırayla denenir,
    /// ilk yakalayan kazanır; bu yüzden örnekler yalnız hedef deseni tetikleyecek şekilde seçildi.
    /// </summary>
    public class UnvanCikariciTests
    {
        private readonly UnvanCikarici _cikarici = new();
        private static readonly List<Api.Features.BankaEkstre.Domain.UnvanDeseni> Desenler =
            BankaEkstreTestOrtami.Desenler();

        [Fact]
        public void Desen1_sorgu_numarali_tarafindan()
        {
            var unvan = _cikarici.Cikar(
                "0000123 sorgu numaralı DAGI GIYIM SANAYI VE TICARET A.S. tarafından gönderilmiştir",
                Desenler);

            Assert.Equal("DAGI GIYIM SANAYI VE TICARET A.S.", unvan);
        }

        [Fact]
        public void Desen2_nolu_hesab()
        {
            var unvan = _cikarici.Cikar(
                "TR330006200012300006673953 nolu KEMAL TEKSTIL LIMITED hesabına havale",
                Desenler);

            Assert.Equal("KEMAL TEKSTIL LIMITED", unvan);
        }

        [Fact]
        public void Desen3_sorgu_nolu_kalan_metin()
        {
            var unvan = _cikarici.Cikar(
                "20260115 sorgu no'lu 5511223 PARK PLAZA YONETIMI",
                Desenler);

            Assert.Equal("PARK PLAZA YONETIMI", unvan);
        }

        [Fact]
        public void Desen4_nolu_buyuk_harfli_unvan()
        {
            // "hesab" geçmediği için desen 2 tutmaz; desen 4 devreye girer.
            var unvan = _cikarici.Cikar(
                "123456 nolu PKF ISTANBUL YEMINLI MALI MUSAVIRLIK",
                Desenler);

            Assert.Equal("PKF ISTANBUL YEMINLI MALI MUSAVIRLIK", unvan);
        }

        [Fact]
        public void Desen5_egik_cizgi_oncesi_unvan()
        {
            var unvan = _cikarici.Cikar(
                "MERT INSAAT SANAYI / ISTANBUL SUBESI",
                Desenler);

            Assert.Equal("MERT INSAAT SANAYI", unvan);
        }

        [Fact]
        public void Desen6_parantez_oncesi_metin()
        {
            var unvan = _cikarici.Cikar(
                "Beta Yazılım Hizmetleri (ödeme referansı 99123)",
                Desenler);

            Assert.Equal("Beta Yazılım Hizmetleri", unvan);
        }

        [Fact]
        public void Hicbir_desen_tutmazsa_null_doner()
        {
            var unvan = _cikarici.Cikar("kredi karti borc odemesi", Desenler);

            Assert.Null(unvan);
        }

        [Fact]
        public void Bos_aciklama_null_doner()
        {
            Assert.Null(_cikarici.Cikar(null, Desenler));
            Assert.Null(_cikarici.Cikar("   ", Desenler));
        }

        [Fact]
        public void Bozuk_desen_ayristirmayi_dusurmez()
        {
            var desenler = new List<Api.Features.BankaEkstre.Domain.UnvanDeseni>
            {
                new() { Desen = "([", Sira = 10, Aktif = true, GrupNo = 1 },
                new() { Desen = @"^(.+?)\s*\(", Sira = 20, Aktif = true, GrupNo = 1 }
            };

            Assert.Equal("Alfa Ticaret", _cikarici.Cikar("Alfa Ticaret (referans)", desenler));
        }
    }
}
