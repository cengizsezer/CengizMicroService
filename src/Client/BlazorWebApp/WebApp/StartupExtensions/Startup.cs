using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WebApp.StartupExtensions.Configuration;
using WebApp.StartupExtensions.Culture;
using WebApp.StartupExtensions.ServiceExtensions;

namespace WebApp.StartupExtensions
{
    // Startup.cs
    public class Startup
    {
        private readonly WebAssemblyHostBuilder _builder;

        public Startup(WebAssemblyHostBuilder builder)
        {
            _builder = builder;
        }

        public void ConfigureServices()
        {
            var appSettings = AppConfiguration.GetAppSettings(_builder);

            _builder.Services
                .AddBlazorServices()
                .AddAuthenticationServices()
                .AddApplicationServices()
                .AddHttpClients(appSettings.BaseApiUrl)
                .AddApiClients()
                .AddFileApiClient(_builder)
                .AddLocalization()
                .AddCultureServices()
            .AddPkfPageServices();

            // Register culture service
            _builder.Services.AddScoped<ICultureService, CultureService>();
        }
    }
}
