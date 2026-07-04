using CatalogService.Api.Features.Jobs.Domain;

namespace CatalogService.Api.Features.Jobs.Contracts
{
    /// <summary>
    /// İstemcinin bir işe eklediği tek bir ekin metadata'sı. Görsel/dosya önce
    /// FileApiService'e yüklenir; dönen dosya Id'si <see cref="FileId"/> olarak buraya konur.
    /// (Yükleme akışı Parça 2-3 kapsamındadır; bu contract onun için hazırdır.)
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
