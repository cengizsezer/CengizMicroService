using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    public interface IMukellefApiClient
    {
        Task<PagedResult<MukellefDto>> GetPaginatedAsync(
            int page = 1,
            int pageSize = 50,
            string? search = null,
            MukellefDurumu? durum = null,
            CancellationToken ct = default);

        Task<MukellefDto?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<MukellefDto> CreateAsync(MukellefCreateDto dto, CancellationToken ct = default);
        Task<MukellefDto> UpdateAsync(int id, MukellefUpdateDto dto, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);

        Task<MukellefImportResultDto> ImportAsync(
            Stream xlsxStream,
            string fileName,
            MukellefImportMode mode,
            CancellationToken ct = default);
    }
}
