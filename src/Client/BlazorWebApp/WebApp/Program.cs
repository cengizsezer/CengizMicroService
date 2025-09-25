using Blazored.LocalStorage;
using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using WebApp.Application.Services;
using WebApp.Application.Services.Interfaces;
using WebApp.Infrastructure;
using WebApp.Manager;
using WebApp.Utils;

namespace WebApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            // Blazor local storage & Radzen
            builder.Services.AddBlazoredLocalStorage();
            builder.Services.AddRadzenComponents();
            builder.Services.AddBlazoredSessionStorage();
            builder.Services.AddScoped<Radzen.DialogService>();
           
            // Auth
            builder.Services.AddScoped<AuthenticationStateProvider, AuthStateProvider>();
            builder.Services.AddAuthorizationCore();
            builder.Services.AddScoped<AuthTokenHandler>();
            builder.Services.AddSingleton<AppStateManager>();
            builder.Services.AddScoped<TenantHeaderHandler>();

            var apiBaseAddress = builder.HostEnvironment.IsDevelopment()
               ? "http://localhost:5000/"
               : "https://www.dijitalmasraf.com/";

            builder.Services.AddHttpClient("ApiGatewayCorridor", c =>
            {
                c.BaseAddress = new Uri(apiBaseAddress);
                // İstersen default header vb. ekleyebilirsin
                // c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddHttpMessageHandler<AuthTokenHandler>()
            .AddHttpMessageHandler<TenantHeaderHandler>();

            // Bu koridoru isteyenlere verelim:
            builder.Services.AddScoped(sp =>
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiGatewayCorridor"));

            // === Servislerin hepsi tek koridordan beslensin ===
            builder.Services.AddScoped<IEducationService>(sp =>
                new EducationService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiGatewayCorridor")));

            builder.Services.AddScoped<IUserAdminService>(sp =>
                new UserAdminService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiGatewayCorridor")));

            builder.Services.AddScoped<IExpenseService>(sp =>
                new ExpenseService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiGatewayCorridor")));

            builder.Services.AddScoped<IOcrService>(sp =>
                new OcrService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiGatewayCorridor")));

            builder.Services.AddScoped<IVehicleService>(sp =>
                new VehicleService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiGatewayCorridor")));






            builder.Services.AddHttpClient<IFileApiService, FileApiService>((sp, http) =>
            {
                var nav = sp.GetRequiredService<NavigationManager>();
                var baseUrl = builder.HostEnvironment.IsDevelopment()
                    ? "http://localhost:5009/api/file/v1/"                  // DEV: direkt FileApiService
                    : new Uri(new Uri(nav.BaseUri), "api/file/v1/").ToString(); // PROD: aynı origin (Nginx proxy)
                http.BaseAddress = new Uri(baseUrl);
            })
 .AddHttpMessageHandler<AuthTokenHandler>()
 .AddHttpMessageHandler<TenantHeaderHandler>();



            builder.Services.AddScoped<IAppSessionManager, AppSessionManager>();
            builder.Services.AddTransient<IIdentityService, IdentityService>();
            // Servisler

            //builder.Services.AddTransient<IExpenseService, ExpenseService>();
            //builder.Services.AddTransient<IOcrService, OcrService>();
            //builder.Services.AddTransient<IVehicleService, VehicleService>();
            //builder.Services.AddScoped<IUserAdminService, UserAdminService>();
            //builder.Services.AddScoped<IEducationService, EducationService>();

            await builder.Build().RunAsync();
        }
    }
}
