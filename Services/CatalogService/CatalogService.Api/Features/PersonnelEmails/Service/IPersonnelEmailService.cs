using CatalogService.Api.Features.PersonnelEmails.Contracts;
using CatalogService.Api.Features.PersonnelEmails.DTO;

namespace CatalogService.Api.Features.PersonnelEmails.Service
{
    public interface IPersonnelEmailService
    {
        Task<List<PersonnelEmailDto>> GetAllAsync(CancellationToken ct);
        Task<PersonnelEmailDto> UpsertAsync(UpsertPersonnelEmailRequest req, CancellationToken ct);
    }
}
