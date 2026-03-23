using CatalogService.Api.Features.Declarations.Dtos;

namespace CatalogService.Api.Features.Declarations.Services
{
    public interface IDeclarationCommandService
    {
        Task<int> CreateAsync(CreateDeclarationRequest request);
        Task UpdateAsync(int id, UpdateDeclarationRequest request);
        Task DeleteAsync(int id);
    }
}
