using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace WebApp.StartupExtensions.Configuration
{
    public static class AppConfiguration
    {
        public static AppSettings GetAppSettings(WebAssemblyHostBuilder builder)
        {
            return new AppSettings
            {
                BaseApiUrl = builder.HostEnvironment.IsDevelopment()
                    ? "http://localhost:5000/"
                    : "https://www.dijitalmasraf.com/",

                FileApiUrl = builder.HostEnvironment.IsDevelopment()
                    ? "http://localhost:5009/api/file/v1/"
                    : "api/file/v1/"
            };
        }
    }
}
