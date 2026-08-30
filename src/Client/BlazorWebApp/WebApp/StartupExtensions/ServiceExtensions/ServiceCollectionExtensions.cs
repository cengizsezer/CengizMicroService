using Blazored.LocalStorage;
using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Radzen;
using System.Reflection;
using WebApp.Application.Handler;
using WebApp.Application.RuleEngine;
using WebApp.Application.Services;
using WebApp.Application.Services.Interfaces;
using WebApp.Application.Services.KdvBeyanname;
using WebApp.Application.Services.Yonetim;
using WebApp.Infrastructure;
using WebApp.Manager;
using WebApp.Pages.Hesaplamalar.Bordro.Services;
using WebApp.Pages.Hesaplamalar.FinansmanGiderKisitlamasi.Services;
using WebApp.Pages.TaxPaymentPage.Client;
using WebApp.StartupExtensions.Culture;

namespace WebApp.StartupExtensions.ServiceExtensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBlazorServices(this IServiceCollection services)
        {
            services.AddBlazoredLocalStorage();
            services.AddBlazoredSessionStorage();
            services.AddRadzenComponents();
            services.AddScoped<DialogService>();

            return services;
        }

        public static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
        {
            services.AddScoped<AuthenticationStateProvider, AuthStateProvider>();
            services.AddAuthorizationCore();
            services.AddTransient<RefreshTokenCorridor>();
            services.AddTransient<AuthTokenHandler>();
            services.AddTransient<TenantHeaderHandler>();

            return services;
        }

        public static IServiceCollection AddPkfPageServices(this IServiceCollection services)
        {

            services.AddScoped<IPayrollApiService, PayrollApiService>();
            services.AddScoped<IFinansmanKisitlamaApiService, FinansmanKisitlamaApiService>();


            return services;
        }

        public static IServiceCollection AddFirmaKontrolServices(this IServiceCollection services)
        {
            services.AddSingleton<IHesapPlaniLoader>(sp =>
            {
                var nav = sp.GetRequiredService<NavigationManager>();
                var http = new HttpClient { BaseAddress = new Uri(nav.BaseUri) };
                return new HesapPlaniLoader(http);
            });
            services.AddSingleton<IExcelMizanParser, ExcelMizanParser>();

            services.AddMizanRuleEngine();

            // Scoped: WASM'de tek-scope olduğundan in-memory cache'ler app ömrü boyunca
            // korunur; ayrıca scoped IFirmaApiClient'ı (auth+tenant header pipeline'lı)
            // doğrudan enjekte edebilir. Singleton iken captive-dependency olur ve client
            // kök provider'dan auth'suz çözülürdü.
            services.AddScoped<IFirmaKontrolService, MockFirmaKontrolService>();
            return services;
        }

        public static IServiceCollection AddMizanRuleEngine(this IServiceCollection services)
        {
            var ruleInterface = typeof(IMizanRule);
            var ruleTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && ruleInterface.IsAssignableFrom(t));

            foreach (var ruleType in ruleTypes)
            {
                services.AddSingleton(ruleInterface, ruleType);
            }

            services.AddSingleton<MizanRuleEngine>();
            return services;
        }

        public static IServiceCollection AddHttpClients(this IServiceCollection services, string baseAddress)
        {
            services.AddHttpClient("GatewayBare", c =>
            {
                c.BaseAddress = new Uri(baseAddress);
            });

            services.AddHttpClient("ApiGatewayCorridor", c =>
            {
                c.BaseAddress = new Uri(baseAddress);
            })
            .AddHttpMessageHandler<AuthTokenHandler>()
            .AddHttpMessageHandler<TenantHeaderHandler>()
            .AddHttpMessageHandler<RefreshTokenCorridor>();

            // Sovos toplu tarama gibi uzun-süreli admin operasyonları için.
            // HttpClient.Timeout ilk request'ten sonra değiştirilemediği için,
            // 100sn default'u aşacak çağrılar bu named client üzerinden gitmeli.
            services.AddHttpClient("SovosAdminLong", c =>
            {
                c.BaseAddress = new Uri(baseAddress);
                c.Timeout = TimeSpan.FromMinutes(25);
            })
            .AddHttpMessageHandler<AuthTokenHandler>()
            .AddHttpMessageHandler<TenantHeaderHandler>()
            .AddHttpMessageHandler<RefreshTokenCorridor>();

            // Primary HTTP client
            services.AddScoped(sp =>
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiGatewayCorridor"));

            return services;
        }

        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<AppStateManager>();
            services.AddScoped<IAppSessionManager, AppSessionManager>();

            // Banka Otomasyon'un firma OTURUMU KALDIRILDI (KARARLAR §99): firma artık bir
            // oturum bağlamı değil, verinin bir boyutu. Kapsam her isteğe çağrı yerinden
            // geçiyor — listede kullanıcının seçtiği filtre, yazmada kaydın kendi firması.
            // Geriye kalan tek ortak ihtiyaç firma LİSTESİ: filtreler ve form alanları
            // için bir kez okunur.
            services.AddScoped<IFirmaSecenekleri, FirmaSecenekleri>();
            services.AddTransient<IIdentityService, IdentityService>();
            //services.AddScoped<LanguageService>();

            return services;
        }

        public static IServiceCollection AddApiClients(this IServiceCollection services)
        {
            services.AddScoped<IDeclarationApiService>(sp =>
    new DeclarationApiService(sp.GetRequiredService<HttpClient>()));

            // Beyanname özeti (firma × tür matrisi) ve beyanname belgeleri.
            services.AddScoped<IBeyannameOzetApiService>(sp =>
                new BeyannameOzetApiService(sp.GetRequiredService<HttpClient>()));

            // Beyanname türü tanımları: Takip, Özet ve Tanımlar ekranlarının ortak kaynağı.
            services.AddScoped<IBeyannameTuruApiService>(sp =>
                new BeyannameTuruApiService(sp.GetRequiredService<HttpClient>()));

            // Firma Bilgileri (sicil / ortaklık / imza yetkilileri / belgeler).
            services.AddScoped<IFirmaBilgiApiClient>(sp =>
                new FirmaBilgiApiClient(sp.GetRequiredService<HttpClient>()));

            // Anasayfa kartları + "son kullanılan firmalar" (tarayıcıda tutulur).
            services.AddScoped<IAnasayfaApiClient>(sp =>
                new AnasayfaApiClient(sp.GetRequiredService<HttpClient>()));
            services.AddScoped<ISonFirmalarStore>(sp =>
                new SonFirmalarStore(sp.GetRequiredService<Blazored.LocalStorage.ILocalStorageService>()));


            services.AddScoped<ITaxPaymentClient>(sp =>new TaxPaymentClient(sp.GetRequiredService<HttpClient>()));

           
            // Factory pattern for API clients
            services.AddScoped<IEducationService>(sp =>
                new EducationService(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<ITicaretSicilApi>(sp =>
                new TicaretSicilApi(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IMevzuatNotuApi>(sp =>
                new MevzuatNotuApi(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<ISmmmTakipApi>(sp =>
                new SmmmTakipApi(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IMuhasebeApi>(sp =>
                new MuhasebeApi(sp.GetRequiredService<HttpClient>()));

            // Firma kapsamı çağrı başına parametreyle geçer; istemcinin saklı bağlamı yok.
            services.AddScoped<IBankaEkstreApi>(sp =>
                new BankaEkstreApi(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IUserAdminService>(sp =>
                new UserAdminService(sp.GetRequiredService<HttpClient>()));

            // Ajanlar: kayıtlar IdentityService'te, "bağlı mı" CatalogService'te;
            // istemci ikisini de aynı gateway üzerinden okuyor.
            services.AddScoped<IAjanApiClient>(sp =>
                new AjanApiClient(sp.GetRequiredService<HttpClient>()));

            // Ajan isleri: Aktar ekranindaki "ORKA'ya Aktar" ve Ajanlar sayfasindaki
            // son isler listesi ayni istemciyi kullaniyor.
            services.AddScoped<IAjanIsApi>(sp =>
                new AjanIsApi(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<ISovosAdminService>(sp =>
                new SovosAdminService(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("SovosAdminLong")));

            services.AddScoped<IExpenseService>(sp =>
                new ExpenseService(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IOcrService>(sp =>
                new OcrService(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IVehicleService>(sp =>
                new VehicleService(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IJobsApi>(sp =>
                new JobsApi(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IUsersService>(sp =>
                new UsersService(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IAccountPlanClient>(sp =>
                new AccountPlanClient(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<ICustomerCompanyApiService>(sp =>
                new CustomerCompanyApiService(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IFirmaApiClient>(sp =>
                new FirmaApiClient(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<WebApp.Application.Services.FirmaKontrol.IFirmaKontrolApiClient>(sp =>
                new WebApp.Application.Services.FirmaKontrol.FirmaKontrolApiClient(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<WebApp.Application.Services.FirmaKontrol.IVergiBeyannameApiClient>(sp =>
                new WebApp.Application.Services.FirmaKontrol.VergiBeyannameApiClient(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IKdvBeyannameApiService>(sp =>
                new KdvBeyannameApiService(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IMukellefApiClient>(sp =>
                new MukellefApiClient(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IPersonelMailApiClient>(sp =>
                new PersonelMailApiClient(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IFinansApiClient>(sp =>
                new FinansApiClient(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IHesapApiClient>(sp =>
                new HesapApiClient(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IBankaTakipApiClient>(sp =>
                new BankaTakipApiClient(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<INotApiClient>(sp =>
                new NotApiClient(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IPermissionService, PermissionService>();



            return services;
        }

        public static IServiceCollection AddFileApiClient(this IServiceCollection services,
            WebAssemblyHostBuilder builder)
        {
            services.AddHttpClient<IFileApiService, FileApiService>((sp, http) =>
            {
                var nav = sp.GetRequiredService<NavigationManager>();
                var baseUrl = builder.HostEnvironment.IsDevelopment()
                    ? "http://localhost:5009/api/file/v1/"
                    : new Uri(new Uri(nav.BaseUri), "api/file/v1/").ToString();
                http.BaseAddress = new Uri(baseUrl);
            })
            .AddHttpMessageHandler<AuthTokenHandler>()
            .AddHttpMessageHandler<TenantHeaderHandler>()
            .AddHttpMessageHandler<RefreshTokenCorridor>();

            return services;
        }

        public static IServiceCollection AddCultureServices(this IServiceCollection services)
        {
            services.AddScoped<ICultureService, CultureService>();
            services.AddLocalization();
            return services;
        }
    }
}
