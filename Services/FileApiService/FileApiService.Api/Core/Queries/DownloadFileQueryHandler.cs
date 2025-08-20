using Ardalis.GuardClauses;
using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Domain.Dtos;
using FileApiService.Api.Domain.Logging;
using FileApiService.Api.Domain.Queries;
using SmallApiToolkit.Core.Extensions;
using SmallApiToolkit.Core.Response;
using Validot;

namespace FileApiService.Api.Core.Queries
{
    internal sealed class DownloadFileQueryHandler : IDownloadFileQueryHandler
    {
        private readonly IValidator<DownloadFileQuery> _validator;
        private readonly IFileQueriesRepository _repo;
        private readonly IPresignUrlService _presign;

        public DownloadFileQueryHandler(IValidator<DownloadFileQuery> v, IFileQueriesRepository r, IPresignUrlService p)
        { _validator = v; _repo = r; _presign = p; }

        public async Task<HttpDataResponse<FileDto>> HandleAsync(DownloadFileQuery req, CancellationToken ct)
        {
            var v = _validator.Validate(req);
            if (v.AnyErrors) return HttpDataResponses.AsBadRequest<FileDto>("Geçersiz istek.");

            var metaRes = await _repo.GetMetaByIdAsync(req.Id, ct);
            if (metaRes.IsFailed) return HttpDataResponses.AsBadRequest<FileDto>("Dosya yok.");

            var meta = metaRes.Value;
            var url = await _presign.GetReadUrlAsync(meta.Key, TimeSpan.FromMinutes(30), ct);

            return HttpDataResponses.AsOK(new FileDto { FileName = meta.FileName, ContentType = meta.ContentType, Length = meta.Length, PresignedUrl = url });
        }
    }
}

