namespace CatalogService.Api.Features.Jobs.Domain
{
    public enum JobAttachmentTur
    {
        Pdf = 0,
        Resim = 1
    }

    /// <summary>
    /// Bir işe (Job) bağlı ekran görüntüsü / belge eki. Asıl dosya FileApiService'te
    /// saklanır; burada yalnızca FileApiService'in döndürdüğü <see cref="FileId"/>,
    /// görüntüleme için gerekli metadata ve eke ait not tutulur.
    /// (TicaretSicilEk deseni birebir örnek alınmıştır.)
    /// </summary>
    public class JobAttachment
    {
        public long Id { get; set; }

        public long JobId { get; set; }
        public Job Job { get; set; } = default!;

        /// <summary>FileApiService'teki dosya kaydının kimliği (/download?id=, /delete?id=).</summary>
        public int FileId { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public JobAttachmentTur Tur { get; set; } = JobAttachmentTur.Resim;

        /// <summary>Bu eke ait serbest metin not (her ekin kendi notu). Opsiyonel.</summary>
        public string? Not { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
