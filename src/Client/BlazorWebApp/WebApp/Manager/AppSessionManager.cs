using Blazored.LocalStorage;
using Radzen;
using WebApp.Application.Services.Interfaces;
using WebApp.Domain.Models.User;
using WebApp.Infrastructure;
using WebApp.Pages.SubPages;

namespace WebApp.Manager
{
    public sealed class AppSessionManager : IAppSessionManager
    {
        // LocalStorage keys
        private const string K_TOKEN = "Auth.Token";
        private const string K_REFRESH = "Auth.RefreshToken";
        private const string K_REMEMBER = "Auth.RememberMe";
        private const string K_FIRM_ID = "SelectedFirmId";
        private const string K_FIRM_NAME = "SelectedFirmName";
        private const string K_FIRMS_BAG = "User.Firms"; // opsiyonel: tüm firmalar

        private readonly IIdentityService _identity;
        private readonly ILocalStorageService _storage;
        private readonly DialogService _dialog;
        private readonly AppStateManager _appState;

        private List<FirmaDto> _firms = new();

        public AppSessionManager(
            IIdentityService identity,
            ILocalStorageService storage,
            DialogService dialog,
            AppStateManager appState)
        {
            _identity = identity;
            _storage = storage;
            _dialog = dialog;
            _appState = appState;
        }

        public string Token { get; private set; }
        public string RefreshToken { get; private set; }
        public bool RememberMe { get; private set; }
        public FirmaDto SelectedFirm { get; private set; }
        public IReadOnlyList<FirmaDto> Firms => _firms;
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token);

        public event Action AuthChanged;
        public event Action<FirmaDto> FirmChanged;

        public async Task InitializeFromLoginAsync(LoginResponseModel login, bool rememberMe)
        {
            RememberMe = rememberMe;
            _firms = login?.Firmalar?.ToList() ?? new List<FirmaDto>();

            // İstersen tüm firmaları sakla (kullanışlı olabilir)
            await _storage.SetItemAsync(K_FIRMS_BAG, _firms);

            // Login sonrası henüz firma seçmedik; Token da yok.
            await _storage.SetItemAsync(K_REMEMBER, rememberMe);
            AuthChanged?.Invoke();
        }

        public async Task<bool> EnsureFirmSelectedAsync()
        {
            if (_firms.Count == 0)
                return false;

            if (_firms.Count == 1)
            {
                await SelectFirmAsync(_firms[0]);
                return true;
            }

            // Çoklu firma -> diyalog
            var result = await _dialog.OpenAsync<FirmSelectDialog>(
    "Firma Seçimi",
    new() { ["Firms"] = _firms },
    new DialogOptions
    {
        Width = "60vw",
        Height = "80vh",
        CssClass = "firm-dialog",
        CloseDialogOnOverlayClick = true
    });

            if (result is FirmaDto chosen)
            {
                await SelectFirmAsync(chosen);
                return true;
            }

            return false; // iptal edildi
        }

        public async Task SelectFirmAsync(FirmaDto firm)
        {
            if (firm == null) return;

            // Backend’de tenant seçimi + token üretimi
            var selectRes = await _identity.SelectTenant(firm.FirmaNo);
            Token = selectRes.Token;
            RefreshToken = selectRes.RefreshToken;

            // Token’ları kim saklayacak? İster IdentityService.StoreTokens kullanın:
            await _identity.StoreTokens(Token, RefreshToken);

            SelectedFirm = firm;

            // App-wide state
            _appState.SetSelectedFirm(firm.FirmaNo, firm.Ad);

            // Kalıcılık
            await _storage.SetItemAsync(K_TOKEN, Token);
            await _storage.SetItemAsync(K_REFRESH, RefreshToken);
            await _storage.SetItemAsync(K_FIRM_ID, firm.FirmaNo);
            await _storage.SetItemAsync(K_FIRM_NAME, firm.Ad);

            FirmChanged?.Invoke(firm);
            AuthChanged?.Invoke();
        }

        public async Task RestoreAsync()
        {
            // sayfa yenilemelerinde oturumu ayağa kaldır
            RememberMe = await _storage.GetItemAsync<bool?>(K_REMEMBER) ?? false;
            Token = await _storage.GetItemAsync<string>(K_TOKEN);
            RefreshToken = await _storage.GetItemAsync<string>(K_REFRESH);

            // firmalar çantası (opsiyonel)
            var bag = await _storage.GetItemAsync<List<FirmaDto>>(K_FIRMS_BAG);
            _firms = bag ?? new List<FirmaDto>();

            var firmId = await _storage.GetItemAsync<string?>(K_FIRM_ID);
            var firmName = await _storage.GetItemAsync<string>(K_FIRM_NAME);

            if (!string.IsNullOrWhiteSpace(firmId) && !string.IsNullOrWhiteSpace(firmName))
            {
                SelectedFirm = new FirmaDto { FirmaNo = firmId, Ad = firmName };
                _appState.SetSelectedFirm(SelectedFirm.FirmaNo, SelectedFirm.Ad);
            }

            AuthChanged?.Invoke();
            if (SelectedFirm != null) FirmChanged?.Invoke(SelectedFirm);
        }

        public async Task ClearAsync()
        {
            Token = null;
            RefreshToken = null;
            SelectedFirm = null;
            _firms = new();

            await _storage.RemoveItemAsync(K_TOKEN);
            await _storage.RemoveItemAsync(K_REFRESH);
            await _storage.RemoveItemAsync(K_FIRM_ID);
            await _storage.RemoveItemAsync(K_FIRM_NAME);
            await _storage.RemoveItemAsync(K_FIRMS_BAG);
            await _storage.RemoveItemAsync(K_REMEMBER);

            AuthChanged?.Invoke();
        }
    }
}
