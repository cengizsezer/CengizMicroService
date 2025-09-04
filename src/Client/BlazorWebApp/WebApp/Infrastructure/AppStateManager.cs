using System;

namespace WebApp.Infrastructure
{
    public enum AppStateEvent
    {
        UpdateBasket,
        Login,
        FirmChanged
    }

    public class AppStateManager
    {
        // Artık ComponentBase yok
        public event Action<AppStateEvent>? StateChanged;

        // Tipin sende string; istersen int? kullanabilirsin
        public string SelectedFirmId { get; private set; } = string.Empty;
        public string SelectedFirmName { get; private set; } = string.Empty;

        public void UpdateCart() => StateChanged?.Invoke(AppStateEvent.UpdateBasket);
        public void LoginChanged() => StateChanged?.Invoke(AppStateEvent.Login);
        public void FirmChanged() => StateChanged?.Invoke(AppStateEvent.FirmChanged);

        // ✅ Yardımcılar
        public void SetSelectedFirm(string firmId, string firmName)
        {
            SelectedFirmId = firmId ?? string.Empty;
            SelectedFirmName = string.IsNullOrWhiteSpace(firmName) ? string.Empty : firmName;
            FirmChanged();
        }

        public void ClearSelectedFirm()
        {
            SelectedFirmId = string.Empty;
            SelectedFirmName = string.Empty;
            FirmChanged();
        }
    }
}
