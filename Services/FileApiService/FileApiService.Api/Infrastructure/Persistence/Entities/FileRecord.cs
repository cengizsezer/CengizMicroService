namespace FileApiService.Api.Infrastructure.Persistence.Entities
{
    public class FileRecord
    {
        public int Id { get; set; }
        public string CompanyId { get; set; } = default!;
        public string Year { get; set; } = default!;
        public string Month { get; set; } = default!;
        public string DeclType { get; set; } = default!;
        public string DocType { get; set; } = default!;
        public string Key { get; set; } = default!;
        public string FileName { get; set; } = default!;
        public string ContentType { get; set; } = "application/pdf";
        public long Length { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public string? Description { get; set; } = default!;
        public int? SequenceNo { get; set; } = default!;
    }
}
