using CatalogService.Api.Features.Jobs.Enum;

namespace CatalogService.Api.Features.Jobs.Domain
{
    public class JobAssignment
    {
        public long Id { get; set; }

        public long JobId { get; set; }
        public Job Job { get; set; } = default!;

        public string UserId { get; set; } = default!;
        public string UserName { get; set; } = default!;

        // Kişiye özel durum (Kanban’da sürüklendiğinde bunu güncelleyeceğiz)
        public JobStatus Status { get; set; } = JobStatus.NotStarted;

        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
