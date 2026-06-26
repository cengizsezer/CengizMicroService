using CatalogService.Api.Features.PersonnelEmails.Contracts;
using CatalogService.Api.Features.PersonnelEmails.Domain;
using CatalogService.Api.Features.PersonnelEmails.DTO;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.PersonnelEmails.Service
{
    public class PersonnelEmailService : IPersonnelEmailService
    {
        private readonly CatalogContext _db;
        public PersonnelEmailService(CatalogContext db) => _db = db;

        public async Task<List<PersonnelEmailDto>> GetAllAsync(CancellationToken ct) =>
            await _db.Set<PersonnelEmail>()
                .AsNoTracking()
                .OrderBy(x => x.UserName)
                .Select(x => new PersonnelEmailDto
                {
                    UserId = x.UserId,
                    UserName = x.UserName,
                    Email = x.Email,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync(ct);

        public async Task<PersonnelEmailDto> UpsertAsync(UpsertPersonnelEmailRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.UserId))
                throw new ArgumentException("UserId zorunlu.");

            var entity = await _db.Set<PersonnelEmail>()
                .FirstOrDefaultAsync(x => x.UserId == req.UserId, ct);

            if (entity is null)
            {
                entity = new PersonnelEmail { UserId = req.UserId.Trim() };
                _db.Add(entity);
            }

            entity.UserName = req.UserName?.Trim();
            entity.Email = req.Email?.Trim();
            entity.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            return new PersonnelEmailDto
            {
                UserId = entity.UserId,
                UserName = entity.UserName,
                Email = entity.Email,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}
