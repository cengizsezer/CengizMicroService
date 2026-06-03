using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    public interface IBankaTakipApiClient
    {
        Task<List<HesapTakipDto>> GetAyAsync(int year, int month, int? firmaId = null, CancellationToken ct = default);
        Task<IslemKaydiDto> IsaretleAsync(IsaretleRequestDto dto, CancellationToken ct = default);
    }
}
