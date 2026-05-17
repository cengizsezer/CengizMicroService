using CatalogService.Api.Features.KdvBeyanname.Dtos;

namespace CatalogService.Api.Features.KdvBeyanname.Services
{
    public interface IKdvBeyannameQueryService
    {
        Task<List<KdvFirmaCardDto>> GetFirmaCardsAsync(CancellationToken ct);
        Task<List<TaramaDto>> GetTaramalarAsync(int firmaId, int take, CancellationToken ct);
        Task<List<GelenFaturaDto>> GetGelenFaturalarAsync(
            int firmaId, DateTime? baslangic, DateTime? bitis, CancellationToken ct);
        Task<List<MizanSatirDto>> GetMizanAsync(int firmaId, string donem, CancellationToken ct);
        Task<List<YevmiyeSatirDto>> GetYevmiyeAsync(int firmaId, string donem, CancellationToken ct);
    }

    public interface IDuzenleyenService
    {
        Task<List<DuzenleyenDto>> ListAsync(bool includeInactive, CancellationToken ct);
        Task<DuzenleyenDto?> GetAsync(int id, CancellationToken ct);
        Task<DuzenleyenDto> CreateAsync(DuzenleyenCreateDto dto, CancellationToken ct);
        Task<DuzenleyenDto?> UpdateAsync(int id, DuzenleyenUpdateDto dto, CancellationToken ct);
        Task<bool> SoftDeleteAsync(int id, CancellationToken ct);
    }
}
