using WebApp.Domain.Models.User;

namespace WebApp.Manager
{
    public interface IAppSessionManager
    {
        // State
        string Token { get; }
        string RefreshToken { get; }
        bool RememberMe { get; }
        FirmaDto SelectedFirm { get; }
        IReadOnlyList<FirmaDto> Firms { get; }
        bool IsAuthenticated { get; }

        // Events
        event Action AuthChanged;
        event Action<FirmaDto> FirmChanged;

        // Lifecycle
        Task InitializeFromLoginAsync(LoginResponseModel login, bool rememberMe);
        Task<bool> EnsureFirmSelectedAsync();                 // Gerekirse diyalog açar, token yeniler
        Task SelectFirmAsync(FirmaDto firm);                  // Programatik seçim
        Task RestoreAsync();                                  // Sayfa yenilemelerinde LocalStorage’tan yükler
        Task ClearAsync();                                    // Çıkış / temizle
    }

 
}
