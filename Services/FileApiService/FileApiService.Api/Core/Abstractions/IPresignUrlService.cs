namespace FileApiService.Api.Core.Abstractions
{
    public interface IPresignUrlService
    {
        Task<string> GetReadUrlAsync(string key, TimeSpan ttl, CancellationToken ct);
    }
}
