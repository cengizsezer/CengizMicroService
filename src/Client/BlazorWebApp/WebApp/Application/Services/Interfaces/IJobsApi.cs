using WebApp.Shared.Dto.Scheduling;

namespace WebApp.Application.Services.Interfaces
{
    public interface IJobsApi
    {
        Task<JobDto> CreateAsync(CreateJobRequest req, CancellationToken ct = default);
        Task<List<JobDto>> GetRangeAsync(DateTime from, DateTime to, string? assigneeId = null, CancellationToken ct = default);
        Task UpdateAssignmentStatusAsync(long assignmentId, JobStatus status, CancellationToken ct = default);

        Task<JobDto> UpdateAsync(long id, CreateJobRequest req, CancellationToken ct = default);

        Task DeleteAsync(long id, CancellationToken ct = default);
    }
}
