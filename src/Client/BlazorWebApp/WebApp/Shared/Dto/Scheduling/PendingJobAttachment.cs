namespace WebApp.Shared.Dto.Scheduling
{
    /// <summary>
    /// Randevu modalında yakalanan ama henüz sunucuya yüklenmemiş ek (ekran görüntüsü / belge).
    /// Görsel data-URL olarak (base64) client state'inde tutulur; önizleme buradan yapılır.
    /// FileApiService'e gerçek yükleme Parça 3'te bu modelden okunacak.
    /// </summary>
    public class PendingJobAttachment
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public JobAttachmentTur Tur { get; set; } = JobAttachmentTur.Resim;

        /// <summary>
        /// Mevcut (zaten yüklü) ek ise FileApiService dosya Id'si (>0). Yeni yakalananda 0.
        /// Submit'te FileId>0 olanlar yeniden yüklenmez, doğrudan geçirilir (eski ekler düşmez).
        /// </summary>
        public int FileId { get; set; }

        /// <summary>"data:&lt;contentType&gt;;base64,&lt;veri&gt;" — yeni yakalananın önizleme + yükleme kaynağı.</summary>
        public string DataUrl { get; set; } = string.Empty;

        /// <summary>Mevcut ekin önizleme kaynağı (presigned URL). Yeni yakalananda boş.</summary>
        public string? PreviewUrl { get; set; }

        /// <summary>Bu eke ait serbest metin not (her ekin kendi notu). Opsiyonel.</summary>
        public string? Not { get; set; }

        public long Size { get; set; }

        public bool IsImage => Tur == JobAttachmentTur.Resim;

        /// <summary>Küçük önizleme için kaynak: yeni yakalanan → base64 DataUrl; mevcut → presigned PreviewUrl.</summary>
        public string PreviewSrc => !string.IsNullOrEmpty(DataUrl) ? DataUrl : (PreviewUrl ?? "");
    }
}
