using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    /// <summary>
    /// Ajan yönetimi. Tek ekran iki servise birden bakıyor: kayıtlar
    /// IdentityService'te (<c>/auth/admin/agents</c>), "şu an bağlı mı" bilgisi
    /// CatalogService'in hub'ında (<c>/catalog/agent/baglilar</c>).
    /// </summary>
    public interface IAjanApiClient
    {
        Task<List<AjanDto>> ListeleAsync(CancellationToken ct = default);

        Task<YeniAjanResponse> OlusturAsync(YeniAjanRequest req, CancellationToken ct = default);

        /// <summary>
        /// Ajanı iptal eder ve varsa açık hub bağlantısını düşürür. İkinci adım
        /// başarısız olursa iptal geri alınmıyor: kayıt kapalı, bağlantı en geç
        /// token'ın ömrü dolunca düşer.
        /// </summary>
        Task IptalEtAsync(int id, string neden, CancellationToken ct = default);

        Task<List<BagliAjanDto>> BaglilarAsync(CancellationToken ct = default);
    }
}
