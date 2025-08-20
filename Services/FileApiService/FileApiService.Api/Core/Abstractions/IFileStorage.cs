namespace FileApiService.Api.Core.Abstractions
{
    public interface IFileStorage
    {
        Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct);
        Task<Stream> OpenReadAsync(string key, CancellationToken ct);
        Task DeleteAsync(string key, CancellationToken ct);
    }
}
