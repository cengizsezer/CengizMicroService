using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Features.Declarations.Services;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Declarations.Controllers
{
    [ApiController]
    [Route("api/catalog/[controller]")]
    public class CustomerCompaniesController : ControllerBase
    {
        private readonly ICustomerCompanyQueryService _service;

        public CustomerCompaniesController(ICustomerCompanyQueryService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<List<CustomerCompanyDto>>> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result);
        }
    }
}
