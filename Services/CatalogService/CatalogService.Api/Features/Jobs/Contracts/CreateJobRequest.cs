namespace CatalogService.Api.Features.Jobs.Contracts
{
    public class CreateJobRequest
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public IList<string> AssigneeIds { get; set; } = new List<string>();

        public bool AlarmEnabled { get; set; }
        public bool RemindOneDayBefore { get; set; }
        public bool RemindOnDay { get; set; }
        public string Timezone { get; set; } = "Europe/Istanbul";

        // KULLANICININ GİRDİĞİ TEK ADRES
        public string RecipientEmail { get; set; } = "";

        // Ekran görüntüsü / belge ekleri (opsiyonel). null = ek gönderilmedi.
        // Yükleme akışı Parça 2-3'te bağlanacak; backend persist için hazır.
        public List<JobAttachmentRequest>? Attachments { get; set; }
    }
}
