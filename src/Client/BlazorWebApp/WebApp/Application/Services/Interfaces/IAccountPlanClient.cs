using WebApp.Shared.Dto.AccountNodePlan;

namespace WebApp.Application.Services.Interfaces
{
    public interface IAccountPlanClient
    {
        Task<List<AccountNodeDto>> GetTreeAsync(CancellationToken ct = default);
        Task<AccountNodeDto?> GetAsync(int id, CancellationToken ct = default);
        Task UpdateNotesAsync(int id, string? notes, CancellationToken ct = default);
    }
}
