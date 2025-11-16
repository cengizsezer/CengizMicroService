using Blazored.LocalStorage;
using Microsoft.JSInterop;
using System.Globalization;

namespace WebApp.StartupExtensions.Culture
{
    public interface ICultureService
    {
        Task<string> GetCurrentCultureAsync();
        Task SetCultureAsync(string culture);
        Task InitializeCultureAsync();
    }

    public class CultureService : ICultureService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILocalStorageService _localStorage;

        public CultureService(IJSRuntime jsRuntime, ILocalStorageService localStorage)
        {
            _jsRuntime = jsRuntime;
            _localStorage = localStorage;
        }

        public async Task<string> GetCurrentCultureAsync()
        {
            try
            {
                // Önce localStorage'dan kontrol et
                var storedCulture = await _localStorage.GetItemAsStringAsync("BlazorCulture");
                if (!string.IsNullOrWhiteSpace(storedCulture))
                    return ValidateCulture(storedCulture);

                // Sonra JS interop ile tarayıcı dilini al
                var browserCulture = await _jsRuntime.InvokeAsync<string>("blazorCulture.get");
                return ValidateCulture(browserCulture);
            }
            catch
            {
                return "tr-TR"; // Fallback
            }
        }

        public async Task SetCultureAsync(string culture)
        {
            var validCulture = ValidateCulture(culture);

            // Hem localStorage'a kaydet
            await _localStorage.SetItemAsStringAsync("BlazorCulture", validCulture);

            // Hem JS tarafına bildir
            await _jsRuntime.InvokeVoidAsync("blazorCulture.set", validCulture);

            // Culture'ı uygula
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(validCulture);
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(validCulture);
        }

        public async Task InitializeCultureAsync()
        {
            var culture = await GetCurrentCultureAsync();
            await SetCultureAsync(culture);
        }

        private static string ValidateCulture(string culture)
        {
            if (string.IsNullOrWhiteSpace(culture))
                return "tr-TR";

            var supportedCultures = new[] { "tr-TR", "en-US" };

            // Tam eşleşme kontrolü
            if (supportedCultures.Contains(culture))
                return culture;

            // Kısmi eşleşme kontrolü (örn: "tr" -> "tr-TR")
            var language = culture.Split('-')[0];
            var matchedCulture = supportedCultures.FirstOrDefault(c => c.StartsWith(language));

            return matchedCulture ?? "tr-TR";
        }
    }
}