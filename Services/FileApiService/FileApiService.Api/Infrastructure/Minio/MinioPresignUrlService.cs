using FileApiService.Api.Core.Abstractions;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace FileApiService.Api.Infrastructure.Minio
{
    public sealed class MinioPresignUrlService : IPresignUrlService
    {
        private readonly IMinioClient _m;
        private readonly string _bucket;
        public MinioPresignUrlService(IMinioClient m, string bucket) { _m = m; _bucket = bucket; }

        public Task<string> GetReadUrlAsync(string key, TimeSpan ttl, CancellationToken ct)
            => _m.PresignedGetObjectAsync(new PresignedGetObjectArgs()
                .WithBucket(_bucket).WithObject(key).WithExpiry((int)ttl.TotalSeconds));
    }
}
