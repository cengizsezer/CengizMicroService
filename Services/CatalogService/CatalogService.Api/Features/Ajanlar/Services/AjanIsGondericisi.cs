using CatalogService.Api.Features.Ajanlar.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace CatalogService.Api.Features.Ajanlar.Services
{
    /// <summary>
    /// İşin ajana ulaştırılması. Arayüz olmasının sebebi test: iş kuralları
    /// (tek iş, sahiplik, idempotens, zaman aşımı) gerçek bir soket kurmadan
    /// sınanabilsin.
    /// </summary>
    public interface IAjanIsGondericisi
    {
        /// <summary>Ajan bağlıysa paketi iletir ve true döner; bağlı değilse false.</summary>
        Task<bool> GonderAsync(string ajanId, AjanIsPaketiDto paket, CancellationToken ct = default);

        /// <summary>İptali bildirir. Ajan bağlı değilse sessizce geçilir.</summary>
        Task<bool> IptalBildirAsync(string ajanId, Guid isId, CancellationToken ct = default);
    }

    /// <summary>
    /// SignalR üzerinden gönderen uygulama.
    ///
    /// <b>Yayın değil tek bağlantı:</b> aynı ajan anahtarı iki makinede
    /// kullanılırsa <c>Clients.All</c> işi iki kez çalıştırırdı. Depodaki en son
    /// bağlantıya gönderiliyor — hangi makinenin çalıştığı belirsiz kalsa da işin
    /// bir kez çalışması garanti.
    /// </summary>
    public sealed class HubIsGondericisi : IAjanIsGondericisi
    {
        /// <summary>Ajan tarafında dinlenen metot adları.</summary>
        public const string IsGonderMetodu = "IsGonder";
        public const string IsIptalMetodu = "IsIptal";

        private readonly IHubContext<AgentHub> _hub;
        private readonly IAjanDeposu _depo;
        private readonly ILogger<HubIsGondericisi> _log;

        public HubIsGondericisi(IHubContext<AgentHub> hub, IAjanDeposu depo, ILogger<HubIsGondericisi> log)
        {
            _hub = hub;
            _depo = depo;
            _log = log;
        }

        public async Task<bool> GonderAsync(string ajanId, AjanIsPaketiDto paket, CancellationToken ct = default)
        {
            var kayit = _depo.AjanaGoreBul(ajanId);
            if (kayit is null)
            {
                _log.LogInformation("İş gönderilemedi, ajan bağlı değil: {AjanId} ({IsId})", ajanId, paket.IsId);
                return false;
            }

            await _hub.Clients.Client(kayit.ConnectionId).SendAsync(IsGonderMetodu, paket, ct);
            _log.LogInformation("İş ajana gönderildi: {IsId} -> {MakineAdi} ({AjanId})",
                paket.IsId, kayit.MakineAdi, ajanId);

            return true;
        }

        public async Task<bool> IptalBildirAsync(string ajanId, Guid isId, CancellationToken ct = default)
        {
            var kayit = _depo.AjanaGoreBul(ajanId);
            if (kayit is null) return false;

            await _hub.Clients.Client(kayit.ConnectionId).SendAsync(IsIptalMetodu, isId, ct);
            return true;
        }
    }
}
