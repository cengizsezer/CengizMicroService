namespace CatalogService.Api.Features.Jobs.Contracts
{
    public class UpdateJobRequest
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }

        // UI’den çoklu kişi geliyor (string id listesi)
        public List<string> AssigneeIds { get; set; } = new();

        public bool AlarmEnabled { get; set; } = true;
        public bool RemindOneDayBefore { get; set; } = true;
        public bool RemindOnDay { get; set; } = true;
        public string Timezone { get; set; } = "Europe/Istanbul";

        // Ekran görüntüsü / belge ekleri (opsiyonel). null = ek gönderilmedi (mevcutlara dokunma).
        public List<JobAttachmentRequest>? Attachments { get; set; }
    }
}
