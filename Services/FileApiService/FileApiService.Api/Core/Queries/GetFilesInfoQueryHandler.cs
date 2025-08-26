using Ardalis.GuardClauses;
using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Core.Extensions;
using FileApiService.Api.Domain.Dtos;
using FileApiService.Api.Domain.Queries;
using SmallApiToolkit.Core.Extensions;
using SmallApiToolkit.Core.Response;
using System.ComponentModel.Design;

namespace FileApiService.Api.Core.Queries
{
    internal sealed class GetFilesInfoQueryHandler : IGetFilesInfoQueryHandler
    {
        private readonly IFileQueriesRepository _repo;
        public GetFilesInfoQueryHandler(IFileQueriesRepository repo) => _repo = repo;

        //public async Task<HttpDataResponse<IEnumerable<FileInfoDto>>> HandleAsync(GetFilesInfoQuery q, CancellationToken ct)
        //{
        //    var month = string.IsNullOrWhiteSpace(q.Month) ? null : int.Parse(q.Month).ToString("00");
        //    var list = await _repo.GetFilesInfo(q.CompanyId, q.Year, month, ct);
        //    return HttpDataResponses.AsOK(list);
        //}


        public async Task<HttpDataResponse<IEnumerable<FileInfoDto>>> HandleAsync(GetFilesInfoQuery q, CancellationToken ct)
        {
            var companyId = string.IsNullOrWhiteSpace(q.CompanyId) ? null : q.CompanyId.Trim();
            var year = string.IsNullOrWhiteSpace(q.Year) ? null : q.Year.Trim();

            string? month2 = null;
            if (!string.IsNullOrWhiteSpace(q.Month) && int.TryParse(q.Month.Trim(), out var m))
                month2 = Normalizer.Month2(m);

            // "KDV-1" → "KDV1"
            var declTypeNorm = string.IsNullOrWhiteSpace(q.DeclType) ? null : Normalizer.Canon(q.DeclType);

            // (Teşhis için faydalı log — sonra kapat)
            Console.WriteLine($"files-info: companyId={companyId}, year={year}, month={month2}, declTypeNorm={declTypeNorm}");

            var list = await _repo.GetFilesInfo(companyId, year, month2, declTypeNorm, ct);
            return HttpDataResponses.AsOK(list);
        }


    }
}
