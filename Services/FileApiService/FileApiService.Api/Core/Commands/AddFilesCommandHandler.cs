using Ardalis.GuardClauses;
using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Core.Extensions;
using FileApiService.Api.Core.Resources;
using FileApiService.Api.Domain.Commands;
using FileApiService.Api.Domain.Dtos;
using FileApiService.Api.Domain.Extensions;
using FileApiService.Api.Domain.Logging;
using SmallApiToolkit.Core.Extensions;
using SmallApiToolkit.Core.Response;

namespace FileApiService.Api.Core.Commands
{
    internal sealed class AddFilesCommandHandler : IAddFilesCommandHandler
    {
        private readonly IAddFilesCommandValidator _validator;
        private readonly IFileStorage _storage;
        private readonly IFileCommandsRepository _repo;

        public AddFilesCommandHandler(IAddFilesCommandValidator v, IFileStorage s, IFileCommandsRepository r)
        { _validator = v; _storage = s; _repo = r; }

        public async Task<HttpDataResponse<bool>> HandleAsync(AddFilesCommand req, CancellationToken ct)
        {
            var vr = _validator.Validate(req);
            if (vr.IsFailed) return HttpDataResponses.AsBadRequest<bool>(vr.Errors.ToErrorMessages());

            foreach (var f in req.Files)
            {
                var m = f.Metadata;
                foreach (var k in new[] { "CompanyId", "Year", "Month", "DeclType", "DocType" })
                    if (!m.ContainsKey(k) || string.IsNullOrWhiteSpace(m[k]))
                        return HttpDataResponses.AsBadRequest<bool>($"{k} zorunlu.");

                var key = FileKeyFactory.ForDeclaration(m["CompanyId"], m["Year"], m["Month"], m["DeclType"], m["DocType"], f.FileName);

                await f.UploadToAsync(_storage, key, ct);                 // MinIO
                await _repo.AddFileMetaAsync(f.ToMeta(key, m), ct);       // DB meta
            }
            return HttpDataResponses.AsOK(true);
        }
    }

}
