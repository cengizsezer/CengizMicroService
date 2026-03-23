using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Infrastructure.Accessor;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Declarations.Services
{
    public class CustomerCompanyQueryService : ICustomerCompanyQueryService
    {
        private readonly CatalogContext _context;
        private readonly IHttpCurrentTenant _tenant;

        public CustomerCompanyQueryService(
            CatalogContext context,
            IHttpCurrentTenant tenant)
        {
            _context = context;
            _tenant = tenant;
        }

        public async Task<List<CustomerCompanyDto>> GetAllAsync()
        {
            var tenantNo = _tenant.CurrentTenantNo;

            return await _context.CustomerCompanies
                .Where(x => x.TenantNo == tenantNo && x.IsActive)
                .Select(x => new CustomerCompanyDto
                {
                    Id = x.Id,
                    CompanyName = x.CompanyName,
                    TaxNumber = x.TaxNumber
                })
                .ToListAsync();
        }
    }
}
