namespace WebApp.Shared.Dto.Scheduling
{
    /// <summary>
    /// Randevu oluşturulurken bir eke ait metadata. Görsel/dosya önce FileApiService'e
    /// yüklenir; dönen dosya Id'si <see cref="FileId"/> olarak buraya konur.
    /// Backend CatalogService.Api.Features.Jobs.Contracts.JobAttachmentRequest ile aynı alanlar.
    /// </summary>
    public class JobAttachmentRequest
    {
        public int FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public JobAttachmentTur Tur { get; set; } = JobAttachmentTur.Resim;
        public string? Not { get; set; }
    }
}
