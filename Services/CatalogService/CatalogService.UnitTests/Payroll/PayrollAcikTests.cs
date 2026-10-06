using System.Net;
using System.Reflection;
using CatalogService.Api.Features.Payroll.Acik;
using CatalogService.Api.Features.Payroll.Commands.CalculatePayroll;
using CatalogService.Api.Features.Payroll.Controllers;
using CatalogService.Api.Features.Payroll.Dtos.Requests;
using CatalogService.Api.Features.Payroll.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CatalogService.UnitTests.Payroll
{
    /// <summary>
    /// Açık (girişsiz) bordro kapısı — KARARLAR §155. Kapsamın dar kaldığını, iki
    /// eşlemenin sapmadığını ve hız sınırının gerçek istemci IP'sine göre çalıştığını
    /// sabitler.
    /// </summary>
    public class PayrollAcikTests
    {
        // ---------------- Kapsam ----------------

        [Fact]
        public void Acik_controller_anonim_ve_hiz_sinirinin_arkasinda()
        {
            var t = typeof(PayrollAcikController);

            Assert.NotNull(t.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Null(t.GetCustomAttribute<AuthorizeAttribute>());
            Assert.Equal(BordroAcikHizSiniri.Politika,
                t.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
            Assert.Equal("api/catalog/payroll/acik", t.GetCustomAttribute<RouteAttribute>()?.Template);
        }

        private static List<MethodInfo> Uclar() => typeof(PayrollAcikController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ToList();

        private static string Imza(MethodInfo m)
        {
            var a = m.GetCustomAttributes<HttpMethodAttribute>().Single();
            return $"{a.HttpMethods.Single()} {a.Template}";
        }

        private static readonly string[] DosyaUclari =
        {
            "POST compare-distribution/export-excel",
            "POST compare-distribution/export-pdf",
            "POST export-excel",
            "POST export-pdf",
        };

        private static readonly string[] HesapUclari =
        {
            "GET law-types",
            "POST calculate",
            "POST compare-distribution",
        };

        private static List<string> Sirali(IEnumerable<string> x) => x.OrderBy(v => v, StringComparer.Ordinal).ToList();

        [Fact]
        public void Acik_controller_uygulama_ici_islevleri_aciyor_bootstrap_ve_parameters_haric()
        {
            var uclar = Sirali(Uclar().Select(Imza));

            Assert.Equal(Sirali(HesapUclari.Concat(DosyaUclari)), uclar);
            Assert.DoesNotContain(uclar, u => u.Contains("bootstrap") || u.Contains("parameters"));
        }

        [Fact]
        public void Acik_uclarin_hepsinin_girisli_controllerda_ayni_rota_ve_metotla_karsiligi_var()
        {
            // Açık kapı girişli kapının alt kümesi: yeni bir uç icat edilmedi.
            var girisli = typeof(PayrollController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(Imza)
                .ToHashSet();

            Assert.All(Uclar().Select(Imza), u => Assert.Contains(u, girisli));
        }

        [Fact]
        public void Yalniz_dort_dosya_ucu_dar_politikada_digerleri_hesap_politikasinda()
        {
            var dar = Uclar()
                .Where(m => m.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName == BordroAcikHizSiniri.DosyaPolitika)
                .Select(Imza);
            Assert.Equal(Sirali(DosyaUclari), Sirali(dar));

            // Aksiyon düzeyinde politikası olmayanlar controller'daki hesap politikasına düşer.
            // compare-distribution bilinçli olarak burada: pencere kendiliğinden çağırıyor.
            var hesap = Uclar()
                .Where(m => m.GetCustomAttribute<EnableRateLimitingAttribute>() is null)
                .Select(Imza);
            Assert.Equal(Sirali(HesapUclari), Sirali(hesap));
        }

        [Fact]
        public void Girisli_controller_hala_authorize()
        {
            var t = typeof(PayrollController);
            Assert.NotNull(t.GetCustomAttribute<AuthorizeAttribute>());
            Assert.Null(t.GetCustomAttribute<AllowAnonymousAttribute>());
        }

        // ---------------- Eşleme sapması ----------------

        [Fact]
        public void Acik_esleme_girisli_controllerin_eslemesiyle_birebir_ayni()
        {
            var istek = new CalculatePayrollRequest
            {
                Year = 2025,
                CalculationType = PayrollCalculationType.NetToGross,
                EmployeeType = PayrollEmployeeType.Retired,
                HasMandatoryBes = true,
                DisabilityType = PayrollDisabilityType.SecondDegree,
                IncludeMinimumWageExemption = false,
                IncludeStampTax = false,
                IncludeEmployerCost = true,
                StartMonth = 3,
                PreviousCumulativeTaxBase = 12345.67m,
                Months = new() { new PayrollMonthInputDto { Month = 3, Amount = 50000m } },
                LawCode = "05510",
                IsManufacturingSector = true,
                ArGeEgitimDurumu = PayrollArGeEgitimDurumu.Doktora95
            };

            var girisli = (CalculatePayrollCommand)typeof(PayrollController)
                .GetMethod("MapToCommand", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, new object[] { istek })!;
            var acik = CalculatePayrollEslemesi.KomutaCevir(istek);

            foreach (var p in typeof(CalculatePayrollCommand).GetProperties())
                Assert.True(Equals(p.GetValue(girisli), p.GetValue(acik)), $"{p.Name} iki eşlemede farklı");

            // İstekteki her alan komuta taşınıyor (yeni alan eklenip unutulursa kırılır).
            foreach (var p in typeof(CalculatePayrollRequest).GetProperties())
            {
                var k = typeof(CalculatePayrollCommand).GetProperty(p.Name);
                Assert.True(k is not null, $"{p.Name} komutta yok");
                Assert.True(Equals(p.GetValue(istek), k!.GetValue(acik)), $"{p.Name} eşlenmemiş");
            }
        }

        // ---------------- Gerçek istemci IP'si ----------------

        private static DefaultHttpContext Istek(string karsi, string? xRealIp = null, string? xff = null)
        {
            var http = new DefaultHttpContext();
            http.Connection.RemoteIpAddress = IPAddress.Parse(karsi);
            if (xRealIp is not null) http.Request.Headers["X-Real-IP"] = xRealIp;
            if (xff is not null) http.Request.Headers["X-Forwarded-For"] = xff;
            return http;
        }

        [Fact]
        public void Gateway_arkasinda_X_Real_IP_kullaniliyor()
            => Assert.Equal("85.100.1.2", BordroAcikHizSiniri.IstemciAnahtari(Istek("172.18.0.5", "85.100.1.2")));

        [Fact]
        public void X_Real_IP_yoksa_XFF_nin_son_girdisi_kullaniliyor()
            // İlk girdi istemcinin uydurduğu olabilir; nginx gerçek adresi SONA ekler.
            => Assert.Equal("85.100.1.2",
                BordroAcikHizSiniri.IstemciAnahtari(Istek("10.0.0.7", xff: "1.2.3.4, 85.100.1.2")));

        [Fact]
        public void Disaridan_dogrudan_gelen_istek_basliklarla_kova_degistiremez()
            => Assert.Equal("85.100.1.2",
                BordroAcikHizSiniri.IstemciAnahtari(Istek("85.100.1.2", "9.9.9.9", "8.8.8.8")));

        [Fact]
        public void Baslik_yoksa_karsi_adres_kullaniliyor()
            => Assert.Equal("172.18.0.5", BordroAcikHizSiniri.IstemciAnahtari(Istek("172.18.0.5")));

        [Fact]
        public void Bozuk_baslik_yok_sayiliyor()
            => Assert.Equal("172.18.0.5",
                BordroAcikHizSiniri.IstemciAnahtari(Istek("172.18.0.5", "abc", "xyz")));

        [Fact]
        public void IPv4_mapped_IPv6_normallestiriliyor()
            => Assert.Equal("85.100.1.2",
                BordroAcikHizSiniri.IstemciAnahtari(Istek("::ffff:172.18.0.5", "::ffff:85.100.1.2")));

        [Theory]
        [InlineData("127.0.0.1", true)]
        [InlineData("10.1.2.3", true)]
        [InlineData("172.16.0.1", true)]
        [InlineData("172.31.255.255", true)]
        [InlineData("192.168.1.1", true)]
        [InlineData("::1", true)]
        [InlineData("fd00::1", true)]
        [InlineData("172.32.0.1", false)]
        [InlineData("85.100.1.2", false)]
        [InlineData("2001:db8::1", false)]
        public void Ic_ag_ayrimi(string ip, bool beklenen)
            => Assert.Equal(beklenen, BordroAcikHizSiniri.IcAgMi(IPAddress.Parse(ip)));

        // ---------------- Hız sınırı (gerçek HTTP) ----------------

        /// <summary>
        /// Gerçek bir Kestrel + aynı politika: sınır ayardan okunuyor, aşılınca 429 +
        /// okunur Türkçe mesaj + Retry-After, ve kovalar X-Real-IP'ye göre ayrı.
        /// </summary>
        [Fact]
        public async Task Sinir_ayardan_okunuyor_asilinca_okunur_Turkce_429_donuyor()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BordroAcikHizSiniri.AyarAnahtari] = "3"
            });
            builder.Services.AddRateLimiter(o => BordroAcikHizSiniri.Ekle(o, builder.Configuration));

            await using var app = builder.Build();
            app.UseRateLimiter();
            app.MapGet("/x", () => "ok").RequireRateLimiting(BordroAcikHizSiniri.Politika);
            await app.StartAsync();

            var adres = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.First();
            using var http = new HttpClient { BaseAddress = new Uri(adres) };

            async Task<HttpResponseMessage> Gonder(string ip)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "/x");
                req.Headers.Add("X-Real-IP", ip);
                return await http.SendAsync(req);
            }

            for (var i = 0; i < 3; i++)
                Assert.Equal(HttpStatusCode.OK, (await Gonder("85.100.1.2")).StatusCode);

            var ret = await Gonder("85.100.1.2");
            Assert.Equal(HttpStatusCode.TooManyRequests, ret.StatusCode);
            Assert.Equal(BordroAcikHizSiniri.RetMesaji, await ret.Content.ReadAsStringAsync());
            Assert.Equal("utf-8", ret.Content.Headers.ContentType?.CharSet);
            Assert.Equal("60", ret.Headers.GetValues("Retry-After").Single());

            // Başka bir istemci (aynı gateway arkasından) etkilenmiyor.
            Assert.Equal(HttpStatusCode.OK, (await Gonder("85.100.9.9")).StatusCode);

            await app.StopAsync();
        }

        /// <summary>
        /// İki politika iki ayrı kova: dosya sınırına takılan IP hesap yapmaya devam eder,
        /// hesap isteği dosya kovasından yemez; dosya reddi "dosya isteği" mesajını taşır.
        /// </summary>
        [Fact]
        public async Task Dosya_siniri_hesap_sinirindan_bagimsiz_ve_kendi_mesajini_donuyor()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BordroAcikHizSiniri.AyarAnahtari] = "3",
                [BordroAcikHizSiniri.DosyaAyarAnahtari] = "2"
            });
            builder.Services.AddRateLimiter(o => BordroAcikHizSiniri.Ekle(o, builder.Configuration));

            await using var app = builder.Build();
            app.UseRateLimiter();
            app.MapGet("/hesap", () => "ok").RequireRateLimiting(BordroAcikHizSiniri.Politika);
            app.MapGet("/dosya", () => "ok").RequireRateLimiting(BordroAcikHizSiniri.DosyaPolitika);
            await app.StartAsync();

            var adres = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.First();
            using var http = new HttpClient { BaseAddress = new Uri(adres) };

            async Task<HttpResponseMessage> Gonder(string yol)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, yol);
                req.Headers.Add("X-Real-IP", "85.100.1.2");
                return await http.SendAsync(req);
            }

            // Dosya kovası (2) dolar; hesap kovasından hiçbir şey yenmez.
            Assert.Equal(HttpStatusCode.OK, (await Gonder("/dosya")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Gonder("/dosya")).StatusCode);
            var dosyaRet = await Gonder("/dosya");
            Assert.Equal(HttpStatusCode.TooManyRequests, dosyaRet.StatusCode);
            Assert.Equal(BordroAcikHizSiniri.DosyaRetMesaji, await dosyaRet.Content.ReadAsStringAsync());

            for (var i = 0; i < 3; i++)
                Assert.Equal(HttpStatusCode.OK, (await Gonder("/hesap")).StatusCode);
            var hesapRet = await Gonder("/hesap");
            Assert.Equal(HttpStatusCode.TooManyRequests, hesapRet.StatusCode);
            Assert.Equal(BordroAcikHizSiniri.RetMesaji, await hesapRet.Content.ReadAsStringAsync());

            await app.StopAsync();
        }

        [Fact]
        public void Ret_mesajlari_ayri_ve_dosya_mesaji_dosya_isteginden_bahsediyor()
        {
            Assert.NotEqual(BordroAcikHizSiniri.RetMesaji, BordroAcikHizSiniri.DosyaRetMesaji);
            Assert.Contains("dosya isteği", BordroAcikHizSiniri.DosyaRetMesaji);
            Assert.Equal(30, BordroAcikHizSiniri.VarsayilanDosyaDakikadaIstek);
            Assert.Equal(60, BordroAcikHizSiniri.VarsayilanDakikadaIstek);
        }
    }
}
