namespace WebApp.Shared.Dto.Declaration
{
    public class FileInfoDto
    {
        public int Id { get; init; }
        public string CompanyId { get; init; } = default!;
        public string Year { get; init; } = default!;
        public string Month { get; init; } = default!;
        public string DeclType { get; init; } = default!;
        public string DocType { get; init; } = default!;
        public string FileName { get; init; } = default!;
        public string ContentType { get; init; } = "application/pdf";
        public long Length { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public string Key { get; init; } = default!;
    }
}
