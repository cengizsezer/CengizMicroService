using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Domain.Commands;
using SmallApiToolkit.Core.Extensions;
using SmallApiToolkit.Core.Response;

namespace FileApiService.Api.Core.Commands
{
    internal sealed class DeleteFileCommandHandler : IDeleteFileCommandHandler
    {
        private readonly IFileQueriesRepository _qrepo; // meta okumak
        private readonly IFileCommandsRepository _crepo; // meta silmek
        private readonly IFileStorage _storage; // blob/object silmek

        public DeleteFileCommandHandler(IFileQueriesRepository q, IFileCommandsRepository c, IFileStorage s)
        { _qrepo = q; _crepo = c; _storage = s; }

        public async Task<HttpDataResponse<bool>> HandleAsync(DeleteFileCommand cmd, CancellationToken ct)
        {
            var metaRes = await _qrepo.GetMetaByIdAsync(cmd.Id, ct);
            if (metaRes.IsFailed) return HttpDataResponses.AsOK(false);

            var meta = metaRes.Value;

            // Multi-tenant kontrolü gerekiyorsa:
            if (!string.IsNullOrEmpty(cmd.CompanyId) &&
                !string.Equals(meta.CompanyId, cmd.CompanyId, StringComparison.OrdinalIgnoreCase))
                return HttpDataResponses.AsForbidden<bool>("Yetkisiz");

            await _storage.DeleteAsync(meta.Key, ct);      // 1) blob/object
            await _crepo.DeleteFileMetaAsync(cmd.Id, ct);  // 2) meta kaydı

            return HttpDataResponses.AsOK(true);
        }
    }
}
