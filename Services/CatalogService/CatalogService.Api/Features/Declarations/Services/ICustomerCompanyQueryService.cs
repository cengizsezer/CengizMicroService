using CatalogService.Api.Features.Declarations.Dtos;

namespace CatalogService.Api.Features.Declarations.Services
{
    public interface ICustomerCompanyQueryService
    {
        Task<List<CustomerCompanyDto>> GetAllAsync();
    }
}
