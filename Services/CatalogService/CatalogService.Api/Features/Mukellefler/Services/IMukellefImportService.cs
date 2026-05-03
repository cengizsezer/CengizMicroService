using CatalogService.Api.Features.Mukellefler.Dtos;

namespace CatalogService.Api.Features.Mukellefler.Services
{
    public interface IMukellefImportService
    {
        Task<MukellefImportResultDto> ImportAsync(
            Stream xlsxStream,
            MukellefImportMode mode,
            CancellationToken ct = default);
    }
}
