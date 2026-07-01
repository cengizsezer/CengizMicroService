using WebApp.Shared.Dto.FirmaKontrol;

namespace WebApp.Application.Services.FirmaKontrol
{
    /// <summary>
    /// CatalogService "firma-kontrol" endpoint'lerine erişim (kontrol maddesi durumları
    /// + özel maddeler). Auth+tenant header pipeline'lı HttpClient üzerinden gider.
    /// </summary>
    public interface IFirmaKontrolApiClient
    {
        Task<List<FirmaKontrolMaddeDto>> GetMaddelerAsync(int firmaId, CancellationToken ct = default);
        Task UpsertMaddeAsync(int firmaId, FirmaKontrolMaddeUpsertDto dto, CancellationToken ct = default);
        Task<FirmaKontrolMaddeDto> AddOzelAsync(int firmaId, OzelMaddeCreateDto dto, CancellationToken ct = default);
        Task<FirmaKontrolMaddeDto> UpdateOzelAsync(int firmaId, long id, OzelMaddeUpdateDto dto, CancellationToken ct = default);
        Task DeleteOzelAsync(int firmaId, long id, CancellationToken ct = default);

        // ── Ham mizan ───────────────────────────────────────────────────────
        Task<List<FirmaKontrolMizanSatirDto>> GetMizanAsync(int firmaId, int yil, CancellationToken ct = default);
        Task SaveMizanAsync(int firmaId, MizanKaydetRequest req, CancellationToken ct = default);
        Task DeleteMizanAsync(int firmaId, int yil, CancellationToken ct = default);

        // ── Vergi paneli girdileri ──────────────────────────────────────────
        Task<FirmaKontrolVergiDto?> GetVergiAsync(int firmaId, int donem, int yil, CancellationToken ct = default);
        Task SaveVergiAsync(int firmaId, FirmaKontrolVergiDto dto, CancellationToken ct = default);
    }
}
