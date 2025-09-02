using FileApiService.Api.Core.Queries;
using FileApiService.Api.Domain.Dtos;
using SmallApiToolkit.Core.Response;

namespace FileApiService.Api.Core.Abstractions
{
    public interface IGetCompanyDocsInfoQueryHandler
    {
        Task<HttpDataResponse<IEnumerable<CompanyDocInfoDto>>> HandleAsync(GetCompanyDocsInfoQuery q, CancellationToken ct);
    }
}
