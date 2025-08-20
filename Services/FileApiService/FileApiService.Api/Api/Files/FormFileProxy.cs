using Ardalis.GuardClauses;
using FileApiService.Api.Domain.Abstractions;

namespace FileApiService.Api.Api.Files
{
    internal sealed class FormFileProxy : IFileProxy
    {
        private readonly IFormFile _file;
        public string ContentType => _file.ContentType;
        public long Length => _file.Length;
        public string FileName => _file.FileName;
        public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

        public FormFileProxy(IFormFile file) => _file = file;

        public Task CopyToAsync(Stream target, CancellationToken ct = default) => _file.CopyToAsync(target, ct);

        public async Task<byte[]> GetData(CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            await _file.CopyToAsync(ms, ct);
            return ms.ToArray();
        }
    }

}
