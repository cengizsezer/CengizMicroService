using FileApiService.Api.Domain.Commands;
using SmallApiToolkit.Core.Response;

namespace FileApiService.Api.Core.Abstractions
{
    public interface IAddCompanyDocCommandHandler
    {
        Task<HttpDataResponse<bool>> HandleAsync(AddCompanyDocCommand cmd, CancellationToken ct);
    }
}
