using FileApiService.Api.Core.Abstractions;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace FileApiService.Api.Infrastructure.Minio
{
    public sealed class MinioFileStorage : IFileStorage
    {
        private readonly IMinioClient _m;
        private readonly string _bucket;
        public MinioFileStorage(IMinioClient m, string bucket) { _m = m; _bucket = bucket; }

        public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct)
            => await _m.PutObjectAsync(new PutObjectArgs()
                .WithBucket(_bucket).WithObject(key)
                .WithStreamData(content).WithObjectSize(content.Length)
                .WithContentType(contentType), ct);

        public async Task<Stream> OpenReadAsync(string key, CancellationToken ct)
        {
            var ms = new MemoryStream();
            await _m.GetObjectAsync(new GetObjectArgs().WithBucket(_bucket).WithObject(key)
                .WithCallbackStream(s => s.CopyTo(ms)), ct);
            ms.Position = 0; return ms;
        }

        public Task DeleteAsync(string key, CancellationToken ct)
            => _m.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_bucket).WithObject(key), ct);
    }
}
