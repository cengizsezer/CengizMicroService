using CatalogService.Api.Features.Jobs.Domain;

namespace CatalogService.Api.Features.Jobs.DTO
{
    public class JobAttachmentDto
    {
        public long Id { get; set; }
        public int FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public JobAttachmentTur Tur { get; set; }
        public string? Not { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
