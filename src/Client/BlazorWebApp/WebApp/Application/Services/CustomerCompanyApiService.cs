using CatalogService.Api.Features.Declarations.Dtos;
using System.Net.Http.Json;
using WebApp.Application.Services.Interfaces;

namespace WebApp.Application.Services
{
    public class CustomerCompanyApiService : ICustomerCompanyApiService
    {
        private readonly HttpClient _http;
        private const string Prefix = "/catalog/customercompanies";

        public CustomerCompanyApiService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<CustomerCompanyDto>> GetAllAsync()
        {
            return await _http.GetFromJsonAsync<List<CustomerCompanyDto>>(Prefix)
                   ?? new List<CustomerCompanyDto>();
        }
    }
}
