using System.Net;
using System.Text;
using WebApp.Application.Services;
using WebApp.Application.Services.Interfaces;
using WebApp.Shared.Dto.BankaEkstre;

namespace WebApp.UnitTests.BankaEkstre
{
    /// <summary>
    /// Firma kapsamının <b>istekten</b> geldiğini sınar (KARARLAR §99).
    ///
    /// Bu testler eskiden <c>BankaOtomasyonOturumuTests</c> idi ve "seçim kalıcı mı,
    /// yenilemede kaynağından doğrulanıyor mu" sorularını soruyordu. O oturum kaldırıldı:
    /// firma artık bir bağlam değil, çağrının parametresi. Dolayısıyla sınanan iddia da
    /// değişti — <b>çağıranın verdiği firma adrese aynen yansıyor mu?</b>
    ///
    /// Adres üzerinden sınanmasının sebebi, hatanın buradan çıkacak olması: yanlış
    /// <c>firmaId</c> ile giden bir yazma, kaydı başka bir firmanın defterine yazar ve
    /// hiçbir ekranda hata görünmez.
    /// </summary>
    public class FirmaKapsamiIstektenGelirTests
    {
        private sealed class AdresYakalayan : HttpMessageHandler
        {
            public string? SonAdres { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                SonAdres = request.RequestUri?.PathAndQuery;

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                });
            }
        }

        private static (BankaEkstreApi Api, AdresYakalayan Yakalayan) Kur()
        {
            var yakalayan = new AdresYakalayan();
            var api = new BankaEkstreApi(new HttpClient(yakalayan) { BaseAddress = new Uri("http://test.local") });
            return (api, yakalayan);
        }

        [Fact]
        public async Task Firma_verilirse_adrese_firmaId_eklenir()
        {
            var (api, yakalayan) = Kur();

            await api.GetHesaplarAsync(firmaId: 4);

            Assert.Contains("firmaId=4", yakalayan.SonAdres);
        }

        /// <summary>
        /// "Tüm firmalar" okuması: <c>firmaId</c> HİÇ gönderilmez. <c>firmaId=0</c>
        /// gönderilseydi sunucu geçersiz değer sayıp 400 dönerdi.
        /// </summary>
        [Fact]
        public async Task Tum_firmalar_okumasinda_firmaId_gonderilmez()
        {
            var (api, yakalayan) = Kur();

            await api.GetHesaplarAsync(IBankaEkstreApi.TumFirmalar);

            Assert.DoesNotContain("firmaId", yakalayan.SonAdres);
        }

        /// <summary>
        /// Kapsam diğer sorgu parametrelerinin yanında durur; onların yerini almaz.
        /// </summary>
        [Fact]
        public async Task Kapsam_diger_parametrelerle_birlikte_gider()
        {
            var (api, yakalayan) = Kur();

            await api.EslesmeleriAraAsync(firmaId: 7, q: "DAGI", enFazla: 50);

            Assert.Contains("firmaId=7", yakalayan.SonAdres);
            Assert.Contains("enFazla=50", yakalayan.SonAdres);
            Assert.Contains("q=DAGI", yakalayan.SonAdres);
        }

        /// <summary>
        /// Yazma çağrısı çağıranın verdiği firmayı taşır. Ekranda "tüm firmalar" seçili
        /// olsa bile kaydın firması ayrı bir değerdir — bu testin koruduğu şey tam olarak
        /// bu ayrım.
        /// </summary>
        [Fact]
        public async Task Yazmada_kaydin_firmasi_gider()
        {
            var (api, yakalayan) = Kur();

            await api.KisiYonlendirmeEkleAsync(firmaId: 3, new KisiYonlendirmeYazDto
            {
                Isim = "Abdulkadir Sayıcı",
                HesapKodu = "331 02"
            });

            Assert.Contains("firmaId=3", yakalayan.SonAdres);
        }

        /// <summary>
        /// Farklı firmalar aynı istemciden art arda çağrılabilir ve birbirine sızmaz:
        /// saklanan bir kapsam olmadığı için "önceki firmanın değeri kaldı" durumu yok.
        /// </summary>
        [Fact]
        public async Task Ardisik_cagrilar_birbirinin_firmasini_tasimaz()
        {
            var (api, yakalayan) = Kur();

            await api.GetHesaplarAsync(firmaId: 4);
            Assert.Contains("firmaId=4", yakalayan.SonAdres);

            await api.GetHesaplarAsync(firmaId: 9);
            Assert.Contains("firmaId=9", yakalayan.SonAdres);
            Assert.DoesNotContain("firmaId=4", yakalayan.SonAdres);

            await api.GetHesaplarAsync(IBankaEkstreApi.TumFirmalar);
            Assert.DoesNotContain("firmaId", yakalayan.SonAdres);
        }
    }
}
