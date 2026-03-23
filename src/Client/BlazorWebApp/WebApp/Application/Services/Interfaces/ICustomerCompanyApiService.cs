using CatalogService.Api.Features.Declarations.Dtos;

namespace WebApp.Application.Services.Interfaces
{
    public interface ICustomerCompanyApiService
    {
        Task<List<CustomerCompanyDto>> GetAllAsync();
    }
}
