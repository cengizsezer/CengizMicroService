using CatalogService.Api.Features.Jobs.Contracts;
using CatalogService.Api.Features.Jobs.DTO;
using CatalogService.Api.Features.Jobs.Enum;

namespace CatalogService.Api.Features.Jobs.Service
{
    public interface IJobService
    {
        Task<JobDto> CreateAsync(CreateJobRequest req, string creatorId, string? creatorName, string tenantNo, CancellationToken ct);
        Task<List<JobDto>> GetRangeAsync(DateTime from, DateTime to, string tenantNo, string? assigneeId, CancellationToken ct);
        Task UpdateAssignmentStatusAsync(long assignmentId, JobStatus status, string tenantNo, string currentUserId, CancellationToken ct);
        Task<JobDto> UpdateAsync(long id, CreateJobRequest req, string tenantNo, CancellationToken ct);
        Task DeleteAsync(long id, string tenantNo, string currentUserId, CancellationToken ct);
    }
}
