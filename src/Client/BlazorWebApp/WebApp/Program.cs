using Blazored.LocalStorage;
using Blazored.SessionStorage;
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
            // Ortam bazlı API adresi
            var apiBaseAddress = builder.HostEnvironment.IsDevelopment()
                ? "http://localhost:5000/"
                : "https://www.dijitalmasraf.com/";

            // HttpClient yapılandırması
            builder.Services.AddHttpClient("ApiGatewayHttpClient", client =>
            {
                client.BaseAddress = new Uri(apiBaseAddress);
            }).AddHttpMessageHandler<AuthTokenHandler>().AddHttpMessageHandler<TenantHeaderHandler>();

            builder.Services.AddScoped(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                return factory.CreateClient("ApiGatewayHttpClient");
            });
           
            // Servisler
            builder.Services.AddTransient<IIdentityService, IdentityService>();
            builder.Services.AddTransient<IExpenseService, ExpenseService>();
            builder.Services.AddTransient<IOcrService, OcrService>();
            builder.Services.AddTransient<IVehicleService, VehicleService>();
            builder.Services.AddTransient<IFileApiService, FileApiService>();

            await builder.Build().RunAsync();
        }
    }
}
