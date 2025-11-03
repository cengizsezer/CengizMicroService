using CatalogService.Api.Features.Jobs.Enum;

namespace CatalogService.Api.Features.Jobs.DTO
{
    public class JobAssignmentDto
    {
        public long Id { get; set; }
        public string UserId { get; set; } = default!;
        public string UserName { get; set; } = default!;
        public JobStatus Status { get; set; }
    }
}
