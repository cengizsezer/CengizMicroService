using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Domain.Commands;
using FileApiService.Api.Domain.Dtos;
using SmallApiToolkit.Core.Extensions;
using SmallApiToolkit.Core.Response;

namespace FileApiService.Api.Core.Commands
{
    internal sealed class AddGenericFileCommandHandler : IAddGenericFileCommandHandler
    {
        private readonly IFileStorage _storage;
        private readonly IFileCommandsRepository _repo;

        public AddGenericFileCommandHandler(IFileStorage storage, IFileCommandsRepository repo)
        {
            _storage = storage;
            _repo = repo;
        }

        public async Task<HttpDataResponse<GenericUploadResultDto>> HandleAsync(AddGenericFileCommand c, CancellationToken ct)
        {
            if (c.File is null || c.File.Length == 0)
                return HttpDataResponses.AsBadRequest<GenericUploadResultDto>("Dosya zorunlu.");

            var folder = SanitizeFolder(c.Folder);
            var safeName = MakeSafe(c.File.FileName);

            // Benzersiz key: aynı ada sahip dosyalar birbirini ezmesin (Key index unique).
            var key = $"{folder}/{Guid.NewGuid():N}_{safeName}";

            var contentType = string.IsNullOrWhiteSpace(c.File.ContentType)
                ? "application/octet-stream"
                : c.File.ContentType;

            using var ms = new MemoryStream();
            await c.File.CopyToAsync(ms, ct);
            ms.Position = 0;

            await _storage.UploadAsync(key, ms, contentType, ct);

            // Metadata kaydı — genel dosyalar modül alanlarını kullanmaz, ayırt edici sabitlerle doldurulur.
            var dto = new FileMetaDto
            {
                CompanyId = "GENERIC",
                Year = "0000",
                Month = null,
                DeclType = "GENERIC",
                DocType = Truncate(folder, 64),
                Key = key,
                FileName = safeName,
                ContentType = contentType,
                Length = c.File.Length,
                CreatedAtUtc = DateTime.UtcNow
            };

            var id = await _repo.AddFileMetaAsync(dto, ct);

            return HttpDataResponses.AsOK(new GenericUploadResultDto
            {
                Id = id,
                Key = key,
                FileName = safeName,
                ContentType = contentType,
                Length = c.File.Length
            });
        }

        private static string SanitizeFolder(string? folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return "genel";
            var parts = folder.Split('/', StringSplitOptions.RemoveEmptyEntries)
                              .Select(MakeSafe);
            var joined = string.Join('/', parts);
            return string.IsNullOrWhiteSpace(joined) ? "genel" : joined;
        }

        private static string MakeSafe(string name)
        {
            foreach (var ch in Path.GetInvalidFileNameChars())
                name = name.Replace(ch, '_');
            return name.Trim();
        }

        private static string Truncate(string value, int max)
            => value.Length <= max ? value : value[..max];
    }
}
