using CatalogService.Api.Features.Jobs.Enum;

namespace CatalogService.Api.Features.Jobs.Contracts
{
    public class UpdateAssignmentStatusRequest
    {
        public JobStatus Status { get; set; }
    }
}
