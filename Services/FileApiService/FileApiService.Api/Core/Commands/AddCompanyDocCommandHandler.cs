using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Core.Extensions;
using FileApiService.Api.Domain.Commands;
using FileApiService.Api.Domain.Dtos;
using SmallApiToolkit.Core.Extensions;
using SmallApiToolkit.Core.Response;

namespace FileApiService.Api.Core.Commands
{
    internal sealed class AddCompanyDocCommandHandler : IAddCompanyDocCommandHandler
    {
        private readonly IAddCompanyDocCommandValidator _validator;
        private readonly IFileStorage _storage;
        private readonly IFileCommandsRepository _repo;

        public AddCompanyDocCommandHandler(
            IAddCompanyDocCommandValidator v,
            IFileStorage s,
            IFileCommandsRepository r)
        {
            _validator = v; _storage = s; _repo = r;
        }

        public async Task<HttpDataResponse<bool>> HandleAsync(AddCompanyDocCommand c, CancellationToken ct)
        {
            var vr = _validator.Validate(c);
            if (vr.IsFailed) return HttpDataResponses.AsBadRequest<bool>(vr.Errors.Select(e => e.Message).ToArray());

            // 1) Canonical category + güvenli ad
            var cat = Normalizer.Canon(c.DocCategory);     // "Vergi Levhası" -> "VERGILEVHASI"
            var safeName = MakeSafe(c.File.FileName);

            // 2) DETERMINISTIC KEY: aynı şirkette aynı yıl + kategori => aynı key -> overwrite
            var key = $"{c.CompanyId}/company-docs/{cat}/{c.Year}/{safeName}";

            // 3) Upload (MinIO overwrite ok)
            using var ms = new MemoryStream();
            await c.File.CopyToAsync(ms, ct);
            ms.Position = 0;

            var contentType = string.IsNullOrWhiteSpace(c.File.ContentType)
                ? "application/pdf"
                : c.File.ContentType;

            await _storage.UploadAsync(key, ms, contentType, ct);

            // 4) DB UPSERT — aynı (CompanyId, Year, DeclType='COMPANYDOC', DocType=cat) için tek kayıt
            var dto = new FileMetaDto
            {
                CompanyId = c.CompanyId,
                Year = c.Year,
                Month = null,              // şirket evraklarında yok
                DeclType = "COMPANYDOC",      // ayırt edici
                DocType = cat,               // kategori
                Description = c.Description,
                SequenceNo = c.SequenceNo,
                Key = key,
                FileName = safeName,
                ContentType = contentType,
                Length = c.File.Length,
                CreatedAtUtc = DateTime.UtcNow,
              
            };

            // Burada Add değil Upsert çağırıyoruz (aşağıda repo örneği var)
            await _repo.UpsertCompanyDocMetaAsync(dto, ct);

            return HttpDataResponses.AsOK(true);
        }

        private static string MakeSafe(string name)
        {
            foreach (var ch in Path.GetInvalidFileNameChars())
                name = name.Replace(ch, '_');
            return name;
        }
    }
}
