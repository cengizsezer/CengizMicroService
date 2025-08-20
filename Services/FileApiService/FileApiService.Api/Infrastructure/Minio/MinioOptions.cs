namespace FileApiService.Api.Infrastructure.Minio
{
    public sealed class MinioOptions
    {
        public string Endpoint { get; set; } = default!;
        public string Bucket { get; set; } = "dijitalmasraf";
        public bool WithSSL { get; set; }
    }
}
