using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Domain.Abstractions;

namespace FileApiService.Api.Core.Extensions
{
    internal static class FileUploadExtensions
    {
        public static async Task UploadToAsync(this IFileProxy file, IFileStorage storage, string key, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            ms.Position = 0;
            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/pdf" : file.ContentType;
            await storage.UploadAsync(key, ms, contentType, ct);
        }
    }
}
