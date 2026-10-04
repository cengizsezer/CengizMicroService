using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen;
using WebApp.Layout;
using WebApp.Pages.Hesaplamalar;
using WebApp.Pages.Hesaplamalar.Bordro;
using WebApp.Pages.Hesaplamalar.Bordro.Component;
using WebApp.Pages.Hesaplamalar.Bordro.Model;
using WebApp.Pages.Hesaplamalar.Bordro.Services;

namespace WebApp.UnitTests.Hesaplamalar
{
    /// <summary>
    /// Bordro hesaplayıcısının iki girişi (KARARLAR §155): tek bileşen, iki modda aynı
    /// işlevler; açık modda bütün çağrılar (karşılaştırma penceresi dahil) açık uçlara
    /// gidiyor; açık sayfa girişsiz ve sade kabukta.
    /// Bileşen .NET'in kendi HtmlRenderer'ı ile gerçekten çiziliyor.
    /// </summary>
    public class BordroAcikErisimTests
    {
        // ---------------- Açık sayfa ----------------

        [Fact]
        public void Acik_sayfa_iki_adreste_girissiz_ve_sade_kabukta()
        {
            var t = typeof(BordroAcikSayfasi);

            var rotalar = t.GetCustomAttributes<RouteAttribute>().Select(r => r.Template).OrderBy(x => x);
            Assert.Equal(new[] { "/payroll-calculator", "/payrollcalculator" }, rotalar);

            // Login deseni: yetki bayrağı yok (ne Authorize ne AllowAnonymous) + kendi layout'u.
            Assert.Null(t.GetCustomAttribute<AuthorizeAttribute>());
            Assert.Null(t.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Equal(typeof(AcikLayout), t.GetCustomAttribute<LayoutAttribute>()?.LayoutType);
        }

        [Fact]
        public void Acik_adresleri_baska_hicbir_sayfa_sahiplenmiyor()
        {
            var sahipler = typeof(BordroAcikSayfasi).Assembly.GetTypes()
                .Where(t => t.GetCustomAttributes<RouteAttribute>()
                    .Any(r => r.Template is "/payroll-calculator" or "/payrollcalculator"))
                .ToList();

            Assert.Equal(new[] { typeof(BordroAcikSayfasi) }, sahipler);
        }

        [Fact]
        public void Uygulama_ici_sekme_ayni_bileseni_gosteriyor_ve_girisli()
        {
            Assert.Equal(typeof(BordroHesaplamasi), HesaplamaSekmesi.Bul("bordro")?.Bilesen);
            Assert.NotNull(typeof(HesaplamalarPage).GetCustomAttribute<AuthorizeAttribute>());
        }

        [Fact]
        public void Acik_istemci_girisli_DI_anahtarina_karismiyor_ve_bootstrap_parameters_yok()
        {
            Assert.True(typeof(IPayrollHesapApi).IsAssignableFrom(typeof(PayrollAcikApiService)));
            // Girişli taraf IPayrollApiService ile çözülüyor; açık istemci o anahtara düşmesin.
            Assert.False(typeof(IPayrollApiService).IsAssignableFrom(typeof(PayrollAcikApiService)));

            var metotlar = typeof(IPayrollHesapApi).GetMethods().Select(m => m.Name).ToList();
            Assert.DoesNotContain(metotlar, m => m.Contains("Bootstrap") || m.Contains("Parameters"));
            Assert.Empty(typeof(IPayrollApiService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        }

        [Fact]
        public async Task Acik_istemcinin_butun_metotlari_yalniz_acik_onek_altina_gidiyor()
        {
            var istekler = new List<(HttpMethod Metot, string Yol)>();
            var api = new PayrollAcikApiService(new HttpClient(new SabitHandler(req =>
            {
                istekler.Add((req.Method, req.RequestUri!.AbsolutePath));
                var govde = req.RequestUri!.AbsolutePath.EndsWith("law-types") ? "[]" : "{}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(govde, Encoding.UTF8, "application/json")
                };
            })) { BaseAddress = new Uri("http://gw/") });

            await api.GetLawTypesAsync(2026);
            await api.CalculateAsync(new CalculatePayrollRequest());
            await api.ExportExcelAsync(new CalculatePayrollRequest());
            await api.ExportPdfAsync(new CalculatePayrollRequest());
            await api.CompareDistributionAsync(new CompareDistributionRequest());
            await api.ExportComparisonExcelAsync(new CompareDistributionRequest());
            await api.ExportComparisonPdfAsync(new CompareDistributionRequest());

            Assert.Equal(new[]
            {
                (HttpMethod.Get,  "/catalog/payroll/acik/law-types"),
                (HttpMethod.Post, "/catalog/payroll/acik/calculate"),
                (HttpMethod.Post, "/catalog/payroll/acik/export-excel"),
                (HttpMethod.Post, "/catalog/payroll/acik/export-pdf"),
                (HttpMethod.Post, "/catalog/payroll/acik/compare-distribution"),
                (HttpMethod.Post, "/catalog/payroll/acik/compare-distribution/export-excel"),
                (HttpMethod.Post, "/catalog/payroll/acik/compare-distribution/export-pdf"),
            }, istekler);
        }

        // ---------------- Bileşen çizimi ----------------

        [Fact]
        public async Task Acik_modda_dugmeler_uygulama_iciyle_ayni_ve_yalniz_acik_uca_gidiliyor()
        {
            var (html, girisli, acikIstekler) = await CizAsync(acikErisim: true);

            Assert.Contains("Hesapla", html);
            Assert.Contains("Excel İndir", html);
            Assert.Contains("PDF İndir", html);

            Assert.Empty(girisli.Cagrilar);
            Assert.Contains(acikIstekler, u => u.AbsolutePath == "/catalog/payroll/acik/law-types");
            Assert.All(acikIstekler, u => Assert.StartsWith("/catalog/payroll/acik/", u.AbsolutePath));
        }

        [Fact]
        public async Task Uygulama_icinde_dugmeler_duruyor_ve_girisli_uca_gidiliyor()
        {
            var (html, girisli, acikIstekler) = await CizAsync(acikErisim: false);

            Assert.Contains("Excel İndir", html);
            Assert.Contains("PDF İndir", html);
            Assert.Contains("Hesapla", html);

            Assert.Contains(nameof(IPayrollHesapApi.GetLawTypesAsync), girisli.Cagrilar);
            Assert.Empty(acikIstekler);
        }

        [Fact]
        public void Karsilastirma_dugmesi_ve_disa_aktarma_acik_moda_bagli_degil()
        {
            // Karşılaştırma düğmesi yalnız Huzur Hakkı seçiliyken çiziliyor (model özel alanı,
            // dışarıdan seçilemiyor); koşulun AcikErisim'e bağlanmadığını kaynakta sabitliyoruz.
            var kaynak = File.ReadAllText(KaynakYolu("WebApp", "Pages", "Hesaplamalar", "Bordro", "BordroHesaplamasi.razor"));
            Assert.Contains("@if (IsHonorarium)", kaynak);
            Assert.DoesNotContain("!AcikErisim", kaynak);
            Assert.DoesNotContain("if (AcikErisim) return", kaynak);
        }

        // ---------------- Karşılaştırma penceresi ----------------

        [Fact]
        public void Karsilastirma_penceresi_sunucu_kapisini_parametre_olarak_aliyor()
        {
            var t = typeof(HonorariumComparisonDialog);

            var kapi = t.GetProperty("HesapApi");
            Assert.NotNull(kapi);
            Assert.Equal(typeof(IPayrollHesapApi), kapi!.PropertyType);
            Assert.NotNull(kapi.GetCustomAttribute<ParameterAttribute>());

            // Kendisi girişli servisi enjekte ETMİYOR — etseydi açık sayfada 401 alırdı.
            var enjekteler = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(p => p.GetCustomAttribute<InjectAttribute>() is not null)
                .Select(p => p.PropertyType);
            Assert.DoesNotContain(enjekteler, tip => typeof(IPayrollHesapApi).IsAssignableFrom(tip));
        }

        [Fact]
        public async Task Karsilastirma_penceresi_gecirilen_kapidan_hesapliyor()
        {
            var istekler = new List<string>();
            var acik = new PayrollAcikApiService(new HttpClient(new SabitHandler(req =>
            {
                istekler.Add(req.RequestUri!.AbsolutePath);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            })) { BaseAddress = new Uri("http://gw/") });
            var girisli = new SahteGirisliApi();

            await CizAsync<HonorariumComparisonDialog>(new Dictionary<string, object?>
            {
                ["GrossAmount"] = 100000m,
                ["TaxCost"] = 20000m,
                ["NetReceived"] = 80000m,
                ["Year"] = 2026,
                ["HesapApi"] = acik
            }, girisli, acik);

            Assert.Equal(new[] { "/catalog/payroll/acik/compare-distribution" }, istekler);
            Assert.Empty(girisli.Cagrilar);
        }

        // ---------------- Sade kabuk ----------------

        [Fact]
        public async Task Acik_kabukta_logo_boyutu_sabit_ve_hicbir_baglanti_yok()
        {
            var html = await CizAsync<AcikLayout>(new Dictionary<string, object?>(),
                new SahteGirisliApi(), new PayrollAcikApiService(new HttpClient()));

            // Stil paketi yüklenmese de logo büyümesin: öznitelik + satır içi stil.
            Assert.Contains("src=\"/img/pkf-logo.svg\"", html);
            Assert.Contains("height=\"48\"", html);
            Assert.Contains("height:48px;width:auto;", html);
            Assert.Contains("Bordro Hesaplama", html);

            // Uygulamanın hiçbir yerine yol yok.
            Assert.DoesNotContain("<a ", html);
            Assert.DoesNotContain("href=", html);
        }

        // ---------------- Hız sınırı mesajı ----------------

        [Fact]
        public async Task Dosya_sinirinda_sunucunun_dosya_mesaji_kullaniciya_gidiyor()
        {
            const string mesaj = "Kısa sürede çok fazla dosya isteği (Excel/PDF) gönderildi. " +
                                 "Lütfen bir dakika bekleyip tekrar deneyin.";
            var api = new PayrollAcikApiService(new HttpClient(new SabitHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent(mesaj, Encoding.UTF8, "text/plain")
                })) { BaseAddress = new Uri("http://gw/") });

            var hata = await Assert.ThrowsAsync<HttpRequestException>(
                () => api.ExportComparisonPdfAsync(new CompareDistributionRequest()));

            Assert.Equal(mesaj, hata.Message);
        }

        [Fact]
        public async Task Hiz_sinirinda_sunucunun_Turkce_mesaji_kullaniciya_gidiyor()
        {
            const string mesaj = "Bordro hesaplayıcısına kısa sürede çok fazla istek gönderildi. " +
                                 "Lütfen bir dakika bekleyip tekrar deneyin.";
            var api = new PayrollAcikApiService(new HttpClient(new SabitHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent(mesaj, Encoding.UTF8, "text/plain")
                })) { BaseAddress = new Uri("http://gw/") });

            var hata = await Assert.ThrowsAsync<HttpRequestException>(
                () => api.CalculateAsync(new CalculatePayrollRequest()));

            Assert.Equal(mesaj, hata.Message);
            Assert.Equal(HttpStatusCode.TooManyRequests, hata.StatusCode);
        }

        [Fact]
        public async Task Hiz_sinirinda_govde_bossa_yine_okunur_mesaj()
        {
            var api = new PayrollAcikApiService(new HttpClient(new SabitHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.TooManyRequests)))
                { BaseAddress = new Uri("http://gw/") });

            var hata = await Assert.ThrowsAsync<HttpRequestException>(() => api.GetLawTypesAsync(2026));

            Assert.Contains("Çok fazla istek", hata.Message);
        }

        // ---------------- Yardımcılar ----------------

        private static async Task<(string Html, SahteGirisliApi Girisli, List<Uri> AcikIstekler)> CizAsync(bool acikErisim)
        {
            var girisli = new SahteGirisliApi();
            var acikIstekler = new List<Uri>();
            var acik = new PayrollAcikApiService(new HttpClient(new SabitHandler(req =>
            {
                acikIstekler.Add(req.RequestUri!);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[{\"code\":\"00000\",\"label\":\"00000 - Standart\"}]",
                        Encoding.UTF8, "application/json")
                };
            })) { BaseAddress = new Uri("http://gw/") });

            var html = await CizAsync<BordroHesaplamasi>(new Dictionary<string, object?>
            {
                [nameof(BordroHesaplamasi.AcikErisim)] = acikErisim
            }, girisli, acik);

            return (html, girisli, acikIstekler);
        }

        private static async Task<string> CizAsync<TBilesen>(
            Dictionary<string, object?> parametreler, SahteGirisliApi girisli, PayrollAcikApiService acik)
            where TBilesen : IComponent
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IPayrollApiService>(girisli);
            services.AddSingleton(acik);
            services.AddSingleton<IJSRuntime, SahteJs>();
            services.AddSingleton<NavigationManager, SahteNav>();
            services.AddRadzenComponents();

            await using var sp = services.BuildServiceProvider();
            await using var renderer = new HtmlRenderer(sp, sp.GetRequiredService<ILoggerFactory>());

            return await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var sonuc = await renderer.RenderComponentAsync<TBilesen>(ParameterView.FromDictionary(parametreler));
                // HtmlRenderer Türkçe harfleri kodluyor (İ → &#x130;); çözmeden "içermiyor" kontrolü boşa geçerdi.
                return WebUtility.HtmlDecode(sonuc.ToHtmlString());
            });
        }

        private static string KaynakYolu(params string[] parcalar)
        {
            // bin/Debug/net8.0 → içinde WebApp klasörü olan BlazorWebApp kökü
            var dizin = new DirectoryInfo(AppContext.BaseDirectory);
            while (dizin is not null && !Directory.Exists(Path.Combine(dizin.FullName, "WebApp")))
                dizin = dizin.Parent;
            Assert.NotNull(dizin);
            return Path.Combine(new[] { dizin!.FullName }.Concat(parcalar).ToArray());
        }

        private sealed class SabitHandler(Func<HttpRequestMessage, HttpResponseMessage> yanit) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
                => Task.FromResult(yanit(request));
        }

        private sealed class SahteGirisliApi : IPayrollApiService
        {
            public List<string> Cagrilar { get; } = new();

            public Task<CalculatePayrollResponse?> CalculateAsync(CalculatePayrollRequest r, CancellationToken c = default)
            { Cagrilar.Add(nameof(CalculateAsync)); return Task.FromResult<CalculatePayrollResponse?>(null); }

            public Task<List<PayrollLawTypeDto>> GetLawTypesAsync(int y, CancellationToken c = default)
            {
                Cagrilar.Add(nameof(GetLawTypesAsync));
                return Task.FromResult(new List<PayrollLawTypeDto> { new() { Code = "00000", Label = "00000 - Standart" } });
            }

            public Task<byte[]> ExportExcelAsync(CalculatePayrollRequest r, CancellationToken c = default) => throw new InvalidOperationException();
            public Task<byte[]> ExportPdfAsync(CalculatePayrollRequest r, CancellationToken c = default) => throw new InvalidOperationException();
            public Task<DistributionComparisonResultDto?> CompareDistributionAsync(CompareDistributionRequest r, CancellationToken c = default) => throw new InvalidOperationException();
            public Task<byte[]> ExportComparisonExcelAsync(CompareDistributionRequest r, CancellationToken c = default) => throw new InvalidOperationException();
            public Task<byte[]> ExportComparisonPdfAsync(CompareDistributionRequest r, CancellationToken c = default) => throw new InvalidOperationException();
        }

        private sealed class SahteJs : IJSRuntime
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => default;
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => default;
        }

        private sealed class SahteNav : NavigationManager
        {
            public SahteNav() => Initialize("http://localhost/", "http://localhost/payroll-calculator");
        }
    }
}
