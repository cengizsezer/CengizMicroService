using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Domain.Dtos;
using SmallApiToolkit.Core.Extensions;
using SmallApiToolkit.Core.Response;

namespace FileApiService.Api.Core.Queries
{
    internal sealed class GetCompanyDocsInfoQueryHandler : IGetCompanyDocsInfoQueryHandler
    {
        private readonly IFileQueriesRepository _repo;
        public GetCompanyDocsInfoQueryHandler(IFileQueriesRepository repo) => _repo = repo;

        public async Task<HttpDataResponse<IEnumerable<CompanyDocInfoDto>>> HandleAsync(GetCompanyDocsInfoQuery q, CancellationToken ct)
        {
            var all = await _repo.GetFilesInfo(
                companyId: q.CompanyId,
                year: q.Year,
                month: null,
                declType: "COMPANYDOC",
                docType: q.DocCategory,
                
                ct: ct);

            // FileMetaDto -> CompanyDocInfoDto
            var mapped = all.Select(m => new CompanyDocInfoDto
            {
                Id = m.Id,
                DocCategory = m.DocType!,
                Year = m.Year!,
                Description = m.Description,
                SequenceNo = m.SequenceNo,
                FileName = m.FileName,
                CreatedAtUtc = m.CreatedAtUtc
              

            });

            if (q.LatestOnly)
            {
                mapped = mapped
                    .GroupBy(x => new { x.DocCategory, x.Year })
                    .Select(g => g.OrderByDescending(x => x.SequenceNo ?? 0)
                                  .ThenByDescending(x => x.CreatedAtUtc)
                                  .First());
            }

            // Grid için sıralama: SıraNo desc, Tarih desc, Kategori asc
            mapped = mapped
                .OrderByDescending(x => x.SequenceNo ?? 0)
                .ThenByDescending(x => x.CreatedAtUtc)
                .ThenBy(x => x.DocCategory);

            return HttpDataResponses.AsOK(mapped);
        }
    }
}
