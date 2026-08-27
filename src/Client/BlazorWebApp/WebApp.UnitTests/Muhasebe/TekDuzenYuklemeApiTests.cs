using System.Net;
using System.Text;
using WebApp.Application.Services;

namespace WebApp.UnitTests.Muhasebe
{
    /// <summary>
    /// "Tek düzen hesap planını yükle" düğmesinin API katmanı: sunucunun açıkladığı mesaj
    /// çağırana ulaşmalı. Sessiz başarısızlık bu modülde bir kez teşhisi zorlaştırdı
    /// (KARARLAR §83/§84).
    /// </summary>
    public class TekDuzenYuklemeApiTests
    {
        private sealed class SabitHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _kod;
            private readonly string _govde;

            public SabitHandler(HttpStatusCode kod, string govde)
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

        private sealed class PatlayanHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
                => throw new HttpRequestException("ağ yok");
        }

        private static MuhasebeApi Api(HttpMessageHandler handler)
            => new(new HttpClient(handler) { BaseAddress = new Uri("http://test.local") });

        private static MuhasebeApi Api(HttpStatusCode kod, string govde)
            => Api(new SabitHandler(kod, govde));

        [Fact]
        public async Task Basarili_yuklemede_adet_ve_mesaj_gelir()
        {
            var api = Api(HttpStatusCode.OK, """{"adet":293,"message":"Tekdüzen hesap planı yüklendi (293 hesap)."}""");

            var (basarili, adet, mesaj) = await api.TekDuzenPlaniYukleAsync();

            Assert.True(basarili);
            Assert.Equal(293, adet);
            Assert.Contains("293", mesaj);
        }

        [Fact]
        public async Task Plan_zaten_doluysa_sunucunun_mesaji_gosterilir()
        {
            var api = Api(HttpStatusCode.Conflict, """{"message":"Bu firmanın hesap planı zaten dolu; yükleme yapılmadı."}""");

            var (basarili, _, mesaj) = await api.TekDuzenPlaniYukleAsync();

            Assert.False(basarili);
            Assert.Contains("zaten dolu", mesaj);
        }

        [Fact]
        public async Task Sablon_dosyasi_yoksa_sunucunun_aciklamasi_gosterilir()
        {
            var api = Api(HttpStatusCode.InternalServerError,
                          """{"message":"Tekdüzen hesap planı şablonu sunucuda bulunamadı (thp-standart.json)."}""");

            var (basarili, _, mesaj) = await api.TekDuzenPlaniYukleAsync();

            Assert.False(basarili);
            Assert.Contains("thp-standart.json", mesaj);
        }

        [Fact]
        public async Task Govde_bos_ise_durum_kodu_mesaja_yansir()
        {
            var api = Api(HttpStatusCode.Unauthorized, string.Empty);

            var (basarili, _, mesaj) = await api.TekDuzenPlaniYukleAsync();

            Assert.False(basarili);
            Assert.Contains("401", mesaj);
        }

        [Fact]
        public async Task Sunucuya_ulasilamazsa_anlasilir_mesaj_doner()
        {
            var api = Api(new PatlayanHandler());

            var (basarili, _, mesaj) = await api.TekDuzenPlaniYukleAsync();

            Assert.False(basarili);
            Assert.Contains("ulaşılamadı", mesaj);
        }
    }
}
