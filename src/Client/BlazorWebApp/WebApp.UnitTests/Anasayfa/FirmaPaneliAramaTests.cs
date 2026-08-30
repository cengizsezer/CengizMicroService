using WebApp.Pages.Anasayfa;
using WebApp.Shared.Dto.Anasayfa;

namespace WebApp.UnitTests.Anasayfa
{
    /// <summary>
    /// Firma panelinin arama kutusu. Süzme istemcide: liste zaten tek çağrıda geldi,
    /// her harfte sunucuya gitmenin karşılığı yok.
    ///
    /// Türkçe harf duyarsızlığı burada asıl mesele: kullanıcı "citadel" yazınca CİTADEL
    /// çıkmazsa arama kutusu işe yaramaz.
    /// </summary>
    public class FirmaPaneliAramaTests
    {
        private static List<FirmaPaneliOzetDto> Firmalar() => new()
        {
            new FirmaPaneliOzetDto { FirmaId = 1, Ad = "ALPHA", Unvan = "ALPHA AHŞAP SANAYİ A.Ş.", VergiKimlikNo = "7721471008" },
            new FirmaPaneliOzetDto { FirmaId = 2, Ad = "CİTADEL", Unvan = "CİTADEL GAYRİMENKUL A.Ş.", VergiKimlikNo = "7280624888" },
            new FirmaPaneliOzetDto { FirmaId = 3, Ad = "PROGROUP", Unvan = "PROGROUP LOJİSTİK LTD. ŞTİ.", VergiKimlikNo = "6110455512" }
        };

        private static int[] Idler(string? arama)
            => FirmaPaneliArama.Suz(Firmalar(), arama).Select(f => f.FirmaId).ToArray();

        [Fact]
        public void Arama_bossa_liste_oldugu_gibi_kaliyor()
        {
            Assert.Equal(new[] { 1, 2, 3 }, Idler(null));
            Assert.Equal(new[] { 1, 2, 3 }, Idler("   "));
        }

        [Fact]
        public void Firma_adiyla_suzuyor()
        {
            Assert.Equal(new[] { 3 }, Idler("progroup"));
        }

        [Fact]
        public void Ad_aramasi_turkce_harfe_takilmiyor()
        {
            // "citadel" → CİTADEL, "sti" → ŞTİ., "ahsap" → AHŞAP
            Assert.Equal(new[] { 2 }, Idler("citadel"));
            Assert.Equal(new[] { 3 }, Idler("sti"));
            Assert.Equal(new[] { 1 }, Idler("ahsap"));
        }

        [Fact]
        public void Unvanin_ortasindan_da_buluyor()
        {
            Assert.Equal(new[] { 2 }, Idler("gayrimenkul"));
        }

        [Fact]
        public void Vkn_ile_suzuyor()
        {
            Assert.Equal(new[] { 1 }, Idler("7721471008"));
            Assert.Equal(new[] { 3 }, Idler("61104"));
        }

        [Fact]
        public void Vkn_aramasinda_ayirac_karakterler_yok_sayiliyor()
        {
            // Kullanıcı numarayı boşlukla/noktayla yapıştırırsa arama bozulmasın.
            Assert.Equal(new[] { 2 }, Idler("728 062 4888"));
        }

        [Fact]
        public void Uymayan_arama_bos_liste_donduruyor()
        {
            Assert.Empty(Idler("zzz"));
        }

        [Fact]
        public void Maskeleme_bastan_dort_sondan_uc_haneyi_aciyor()
        {
            // 11 haneli TCKN: 1234****901
            Assert.Equal("1234****901", MaskeliKimlik.Maskele("12345678901"));

            // 10 haneli VKN: 1234***008
            Assert.Equal("7721***008", MaskeliKimlik.Maskele("7721471008"));

            // Kısa/biçimsiz değer olduğu gibi kalıyor; yarım maskelenmiş metin
            // kullanıcıya "veri bozuk" izlenimi verirdi.
            Assert.Equal("1234", MaskeliKimlik.Maskele("1234"));
        }
    }
}
