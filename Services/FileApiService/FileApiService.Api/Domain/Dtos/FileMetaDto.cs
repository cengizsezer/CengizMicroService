namespace FileApiService.Api.Domain.Dtos
{
    public sealed class FileMetaDto
    {
        public int Id { get; init; }
        public string CompanyId { get; init; } = default!;
        public string Year { get; init; } = default!;
        public string Month { get; init; } = default!;
        public string DeclType { get; init; } = default!;
        public string DocType { get; init; } = default!;
        public string Key { get; init; } = default!;
        public string FileName { get; init; } = default!;
        public string ContentType { get; init; } = "application/pdf";
        public long Length { get; init; }
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    }
}
