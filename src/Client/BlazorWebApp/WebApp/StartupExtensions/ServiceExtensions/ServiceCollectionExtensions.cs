using Blazored.LocalStorage;
using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Radzen;
using WebApp.Application.Handler;
using WebApp.Application.Services;
using WebApp.Application.Services.Interfaces;
using WebApp.Infrastructure;
using WebApp.Manager;
using WebApp.Pages.Payroll.Services;
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
            services.AddSingleton<IFirmaKontrolService, MockFirmaKontrolService>();
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

            // Primary HTTP client
            services.AddScoped(sp =>
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiGatewayCorridor"));

            return services;
        }

        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<AppStateManager>();
            services.AddScoped<IAppSessionManager, AppSessionManager>();
            services.AddTransient<IIdentityService, IdentityService>();
            //services.AddScoped<LanguageService>();

            return services;
        }

        public static IServiceCollection AddApiClients(this IServiceCollection services)
        {
            services.AddScoped<IDeclarationApiService>(sp =>
    new DeclarationApiService(sp.GetRequiredService<HttpClient>()));


            services.AddScoped<ITaxPaymentClient>(sp =>new TaxPaymentClient(sp.GetRequiredService<HttpClient>()));

           
            // Factory pattern for API clients
            services.AddScoped<IEducationService>(sp =>
                new EducationService(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<IUserAdminService>(sp =>
                new UserAdminService(sp.GetRequiredService<HttpClient>()));

            services.AddScoped<ISovosAdminService>(sp =>
                new SovosAdminService(sp.GetRequiredService<HttpClient>()));

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
