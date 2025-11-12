namespace CatalogService.Api.Features.AccountPlan
{
    public interface IAccountPlanService
    {
        Task<List<AccountNodeDto>> GetTreeAsync(CancellationToken ct = default);
        Task<AccountNodeDto?> GetAsync(int id, CancellationToken ct = default);
        Task<List<AccountNodeDto>> SearchAsync(string q, CancellationToken ct = default);
        Task UpdateNotesAsync(int id, string? notes, CancellationToken ct = default);
    }
}
