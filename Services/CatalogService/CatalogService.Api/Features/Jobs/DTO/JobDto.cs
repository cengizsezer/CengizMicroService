using CatalogService.Api.Features.Jobs.Enum;

namespace CatalogService.Api.Features.Jobs.DTO
{
    public class JobDto
    {
        public long Id { get; set; }
        public string Title { get; set; } = default!;
        public string? Description { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public JobStatus Status { get; set; }
        public List<JobAssignmentDto> Assignments { get; set; } = new();
    }
}
