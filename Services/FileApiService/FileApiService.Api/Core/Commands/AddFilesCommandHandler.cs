using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Core.Extensions;
using FileApiService.Api.Domain.Abstractions; // IFileProxy
using FileApiService.Api.Domain.Commands;
using FileApiService.Api.Domain.Dtos;
using SmallApiToolkit.Core.Extensions;
using SmallApiToolkit.Core.Response;

// Normalizer'ın bulunduğu namespace'i ekle
// using FileApiService.Api.Core.Normalization;

internal sealed class AddFilesCommandHandler : IAddFilesCommandHandler
{
    private readonly IAddFilesCommandValidator _validator;
    private readonly IFileStorage _storage;                 // MinIO sarmalayıcın
    private readonly IFileCommandsRepository _repo;         // DB repo

    public AddFilesCommandHandler(IAddFilesCommandValidator v, IFileStorage s, IFileCommandsRepository r)
    {
        _validator = v; _storage = s; _repo = r;
    }

    public async Task<HttpDataResponse<bool>> HandleAsync(AddFilesCommand cmd, CancellationToken ct)
    {
        var vr = _validator.Validate(cmd);
        if (vr.IsFailed) return HttpDataResponses.AsBadRequest<bool>(vr.Errors.Select(e => e.Message).ToArray());

        foreach (IFileProxy file in cmd.Files)
        {
            var meta = file.Metadata;

            // 1) Zorunlu alan kontrolü
            foreach (var k in new[] { "CompanyId", "Year", "Month", "DeclType", "DocType" })
                if (!meta.ContainsKey(k) || string.IsNullOrWhiteSpace(meta[k]))
                    return HttpDataResponses.AsBadRequest<bool>($"{k} zorunlu.");

            // 2) Metadata oku
            var companyId = meta["CompanyId"].Trim();
            var year = meta["Year"].Trim();
            var monthRaw = meta["Month"];
            var declType = meta["DeclType"];
            var docType = meta["DocType"];

            // 3) Normalize
            var month2 = int.TryParse(monthRaw, out var mi) ? Normalizer.Month2(mi) : "01";
            var declTypeNorm = Normalizer.Canon(declType);   // "KDV-1" -> "KDV1"
            var docTypeNorm = Normalizer.Canon(docType);    // "Beyanname" -> "BEYANNAME"

            // 4) Güvenli dosya adı
            var safeFileName = MakeSafe(file.FileName);

            // 5) KANONİK KEY
            var key = $"{companyId}/{year}/{month2}/{declTypeNorm}/{docTypeNorm}/{safeFileName}";

            // 6) İçeriği MinIO'ya yaz
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, ct);
                ms.Position = 0; // PutObject öncesi başa sar
                var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType;

                await _storage.UploadAsync(key, ms, contentType, ct);
            }

            // 7) DB meta kaydı
            var dto = new FileMetaDto
            {
                CompanyId = companyId,
                Year = year,
                Month = month2,          // "01"
                DeclType = declType,        // etiketi sakla (UI için)
                DocType = docType,         // etiketi sakla (UI için)
                Key = key,
                FileName = safeFileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                Length = file.Length,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _repo.AddFileMetaAsync(dto, ct);
        }

        return HttpDataResponses.AsOK(true);
    }

    private static string MakeSafe(string name)
    {
        foreach (var ch in Path.GetInvalidFileNameChars())
            name = name.Replace(ch, '_');
        return name;
    }
}
