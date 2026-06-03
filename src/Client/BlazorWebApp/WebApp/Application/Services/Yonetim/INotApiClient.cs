using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    public interface INotApiClient
    {
        Task<List<NotDto>> GetByHesapAsync(int hesapId, int yil, int ay, CancellationToken ct = default);
        Task<NotDto> CreateAsync(NotCreateDto dto, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}
