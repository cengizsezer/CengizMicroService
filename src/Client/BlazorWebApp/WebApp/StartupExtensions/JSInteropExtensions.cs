using Microsoft.JSInterop;

namespace WebApp.StartupExtensions
{
    public static class JSInteropExtensions
    {
        public static async Task<string?> GetBrowserCulture(this IJSRuntime jsRuntime)
        {
            try
            {
                return await jsRuntime.InvokeAsync<string>("blazorCulture.get");
            }
            catch
            {
                return null;
            }
        }
    }

}
