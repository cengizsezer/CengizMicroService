using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radzen;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            builder.Services.AddBlazoredLocalStorage();

            builder.Services.AddRadzenComponents();

            builder.Services.AddTransient<IIdentityService, IdentityService>();
            builder.Services.AddTransient<IExpenseService, ExpenseService>();
            builder.Services.AddSingleton(new JsonSerializerOptions
            {
                TypeInfoResolver = AppJsonContext.Default
            });
            //            builder.Services.AddHttpClient<IExpenseService, ExpenseService>(client =>
            //            {
            //                client.BaseAddress = new Uri("http://localhost:5000/"); // API Gateway adresin neyse
            //            }).AddHttpMessageHandler<AuthTokenHandler>();


            //            builder.Services.AddHttpClient<IIdentityService, IdentityService>(client =>
            //            {
            //                client.BaseAddress = new Uri("http://localhost:5000/");
            //            })
            //.AddHttpMessageHandler<AuthTokenHandler>();


            builder.Services.AddScoped<AuthenticationStateProvider, AuthStateProvider>();

            builder.Services.AddAuthorizationCore();

            builder.Services.AddSingleton<AppStateManager>();

            builder.Services.AddScoped(sp =>
            {
                var clientFactory = sp.GetRequiredService<IHttpClientFactory>();

                return clientFactory.CreateClient("ApiGatewayHttpClient");
            });


            builder.Services.AddScoped<AuthTokenHandler>();

            var apiBaseAddress = builder.HostEnvironment.IsDevelopment()
                                ? "http://localhost:5000/"
                                : "https://www.dijitalmasraf.com/";

            builder.Services.AddHttpClient("ApiGatewayHttpClient", client =>
            {
                client.BaseAddress = new Uri(apiBaseAddress);
            })
            .AddHttpMessageHandler<AuthTokenHandler>();



            await builder.Build().RunAsync();
        }
    }
}