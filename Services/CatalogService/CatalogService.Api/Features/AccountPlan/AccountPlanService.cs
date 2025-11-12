using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.AccountPlan
{
    public class AccountPlanService : IAccountPlanService
    {
        private readonly CatalogContext _db;
        public AccountPlanService(CatalogContext db) => _db = db;

        public async Task<List<AccountNodeDto>> GetTreeAsync(CancellationToken ct = default)
        {
            // Entity’leri düz liste çek (tracking kapalı, hızlı)
            var all = await _db.AccountNodes
                .AsNoTracking()
                .OrderBy(x => x.Level).ThenBy(x => x.Order)
                .Select(x => new AccountNodeDto
                {
                    Id = x.Id,
                    Code = x.Code,
                    Name = x.Name,
                    Description = x.Description,
                    Notes = x.Notes,
                    Level = x.Level,
                    ParentId = x.ParentId,
                    Order = x.Order,
                    Children = new List<AccountNodeDto>() // sonra bağlayacağız
                })
                .ToListAsync(ct);

            // id -> dto
            var dict = all.ToDictionary(x => x.Id);
            // children bağla
            foreach (var n in all)
                if (n.ParentId is int p && dict.TryGetValue(p, out var parent))
                    parent.Children.Add(n);

            // kökleri dön
            return all.Where(x => x.ParentId == null).ToList();
        }

        public async Task<AccountNodeDto?> GetAsync(int id, CancellationToken ct = default)
        {
            return await _db.AccountNodes.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new AccountNodeDto
                {
                    Id = x.Id,
                    Code = x.Code,
                    Name = x.Name,
                    Description = x.Description,
                    Notes = x.Notes,
                    Level = x.Level,
                    ParentId = x.ParentId,
                    Order = x.Order
                })
                .FirstOrDefaultAsync(ct);
        }

        public async Task<List<AccountNodeDto>> SearchAsync(string q, CancellationToken ct = default)
        {
            q = q ?? string.Empty;
            return await _db.AccountNodes.AsNoTracking()
                .Where(x => x.Code.Contains(q) || x.Name.Contains(q))
                .OrderBy(x => x.Code)
                .Take(200)
                .Select(x => new AccountNodeDto
                {
                    Id = x.Id,
                    Code = x.Code,
                    Name = x.Name,
                    Description = x.Description,
                    Notes = x.Notes,
                    Level = x.Level,
                    ParentId = x.ParentId,
                    Order = x.Order
                })
                .ToListAsync(ct);
        }

        public async Task UpdateNotesAsync(int id, string? notes, CancellationToken ct = default)
        {
            var node = await _db.AccountNodes.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (node == null) throw new KeyNotFoundException("Account node not found");
            node.Notes = notes;
            await _db.SaveChangesAsync(ct);
        }
    }
}
