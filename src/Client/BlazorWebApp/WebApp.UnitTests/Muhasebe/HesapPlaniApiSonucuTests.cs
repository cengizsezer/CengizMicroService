using System.Net;
using System.Text;
using WebApp.Application.Services;

namespace WebApp.UnitTests.Muhasebe
{
    /// <summary>
    /// Hesap planı isteğinin sonucu: "istek başarısız" ile "kayıt yok" ayrı ayrı
    /// bildirilmeli. Eski sürüm her hatayı yutup boş liste döndüğü için 401/500/zaman aşımı
    /// ekranda "Hesap planı boş." diye görünüyordu — prod'daki boş sayfanın neden hatasız
    /// göründüğünün sebebi buydu (KARARLAR §83).
    /// </summary>
    public class HesapPlaniApiSonucuTests
    {
        private sealed class SabitHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _kod;
            private readonly string _govde;

            public SabitHandler(HttpStatusCode kod, string govde = "")
            {
                _kod = kod;
                _govde = govde;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
                => Task.FromResult(new HttpResponseMessage(_kod)
                {
                    Content = new StringContent(_govde, Encoding.UTF8, "application/json")
                });
        }

        private static MuhasebeApi Api(HttpStatusCode kod, string govde = "")
            => new(new HttpClient(new SabitHandler(kod, govde)) { BaseAddress = new Uri("http://test.local") });

        [Fact]
        public async Task Bos_liste_donerse_istek_basarili_sayilir()
        {
            var (liste, basarili) = await Api(HttpStatusCode.OK, "[]").GetHesapPlaniSonucAsync();

            Assert.True(basarili);
            Assert.Empty(liste);
        }

        [Fact]
        public async Task Dolu_liste_donerse_kayitlar_gelir()
        {
            const string govde = """[{"id":1,"ustHesapId":null,"kod":"1","ad":"DÖNEN VARLIKLAR"}]""";

            var (liste, basarili) = await Api(HttpStatusCode.OK, govde).GetHesapPlaniSonucAsync();

            Assert.True(basarili);
            Assert.Single(liste);
            Assert.Equal("1", liste[0].Kod);
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task Istek_basarisizsa_bos_liste_ile_birlikte_bildirilir(HttpStatusCode kod)
        {
            var (liste, basarili) = await Api(kod).GetHesapPlaniSonucAsync();

            Assert.False(basarili);
            Assert.Empty(liste);
        }

        /// <summary>
        /// Eski metot davranışını korur (diğer çağıranlar bozulmasın): hata yine yutulur ve
        /// boş liste döner — fark, artık sonucun ayrıca sorulabilmesi.
        /// </summary>
        [Fact]
        public async Task Eski_metot_hata_durumunda_bos_liste_dondurmeye_devam_eder()
        {
            var liste = await Api(HttpStatusCode.InternalServerError).GetHesapPlaniAsync();

            Assert.Empty(liste);
        }
    }
}
