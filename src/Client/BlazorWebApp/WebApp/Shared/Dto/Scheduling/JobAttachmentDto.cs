namespace WebApp.Shared.Dto.Scheduling
{
    /// <summary>
    /// Backend'den (GET /catalog/jobs) dönen kayıtlı ek. Asıl dosya FileApiService'te;
    /// önizleme <see cref="FileId"/> ile presigned URL üzerinden getirilir.
    /// Backend CatalogService.Api.Features.Jobs.DTO.JobAttachmentDto ile aynı alanlar.
    /// </summary>
    public class JobAttachmentDto
    {
        public long Id { get; set; }
        public int FileId { get; set; }
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "application/octet-stream";
        public JobAttachmentTur Tur { get; set; }
        public string? Not { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
