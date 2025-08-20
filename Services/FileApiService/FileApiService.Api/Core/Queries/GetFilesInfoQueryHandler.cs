using Ardalis.GuardClauses;
using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Domain.Dtos;
using FileApiService.Api.Domain.Queries;
using SmallApiToolkit.Core.Extensions;
using SmallApiToolkit.Core.Response;

namespace FileApiService.Api.Core.Queries
{
    internal sealed class GetFilesInfoQueryHandler : IGetFilesInfoQueryHandler
    {
        private readonly IFileQueriesRepository _repo;
        public GetFilesInfoQueryHandler(IFileQueriesRepository repo) => _repo = repo;

        public async Task<HttpDataResponse<IEnumerable<FileInfoDto>>> HandleAsync(GetFilesInfoQuery q, CancellationToken ct)
        {
            var month = string.IsNullOrWhiteSpace(q.Month) ? null : int.Parse(q.Month).ToString("00");
            var list = await _repo.GetFilesInfo(q.CompanyId, q.Year, month, ct);
            return HttpDataResponses.AsOK(list);
        }
    }
}
