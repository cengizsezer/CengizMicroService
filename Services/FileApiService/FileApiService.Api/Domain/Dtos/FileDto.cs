namespace FileApiService.Api.Domain.Dtos
{
    public sealed class FileDto
    {
        public string FileName { get; init; } = default!;
        public string ContentType { get; init; } = "application/pdf";
        public long? Length { get; init; }
        public byte[]? Data { get; init; }          // Presigned akışında null
        public string? PresignedUrl { get; init; }  // UI bununla açacak
    }
}
