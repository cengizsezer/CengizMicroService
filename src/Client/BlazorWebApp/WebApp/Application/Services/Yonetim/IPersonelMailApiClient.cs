using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    public interface IPersonelMailApiClient
    {
        Task<List<PersonelMailDto>> GetAllAsync(CancellationToken ct = default);
        Task<PersonelMailDto> UpsertAsync(UpsertPersonelMailRequest req, CancellationToken ct = default);
    }
}
