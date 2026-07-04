using CatalogService.Api.Features.Jobs.Enum;

namespace CatalogService.Api.Features.Jobs.Domain
{
    public class Job
    {
        public long Id { get; set; }
        public string Title { get; set; } = default!;
        public string? Description { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public JobStatus Status { get; set; } = JobStatus.NotStarted;

        public string CreatedById { get; set; } = default!;
        public string? CreatedByName { get; set; }

        public string TenantNo { get; set; } = default!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? UpdatedById { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
        public DateTime DeletedAtUtc { get; set; } = DateTime.UtcNow;
        public ICollection<JobAssignment> Assignments { get; set; } = new List<JobAssignment>();

        // Ekran görüntüsü / belge ekleri (asıl dosya FileApiService'te; burada metadata + not)
        public ICollection<JobAttachment> Attachments { get; set; } = new List<JobAttachment>();
    }
}
