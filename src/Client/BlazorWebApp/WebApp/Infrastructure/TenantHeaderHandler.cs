
using Blazored.LocalStorage;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WebApp.Infrastructure
{

    public class TenantHeaderHandler : DelegatingHandler
    {
        private readonly ILocalStorageService _localStorage;
        private readonly AppStateManager _appState;

        public TenantHeaderHandler(ILocalStorageService localStorage, AppStateManager appState)
        {
            _localStorage = localStorage;
            _appState = appState;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var firmId = _appState.SelectedFirmId ?? await _localStorage.GetItemAsync<string>("SelectedFirmId");

            if (!string.IsNullOrWhiteSpace(firmId))
            {
                request.Headers.Remove("X-Tenant-Id"); // Çakışma olmasın
                request.Headers.Add("X-Tenant-Id", firmId);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }

}
