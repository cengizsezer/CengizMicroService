using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Features.Declarations.Services;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Declarations.Controllers
{
    [ApiController]
    [Route("api/catalog/[controller]")]
    public class DeclarationsController : ControllerBase
    {
        private readonly IDeclarationQueryService _queryService;
        private readonly IDeclarationCommandService _commandService;

        public DeclarationsController(
            IDeclarationQueryService queryService,
            IDeclarationCommandService commandService)
        {
            _queryService = queryService;
            _commandService = commandService;
        }

        [HttpGet("monthly-summary")]
        public async Task<IActionResult> GetMonthlySummary(
            [FromQuery] int year,
            [FromQuery] int month,
            [FromQuery] int? customerCompanyId ,
            [FromQuery] string? declarationType)
        {
            var result = await _queryService.GetMonthlySummaryAsync(year, month, customerCompanyId, declarationType);
            return Ok(result);
        }

        [HttpGet("yearly-summary")]
        public async Task<IActionResult> GetYearlySummary(
            [FromQuery] int year,
            [FromQuery] int? customerCompanyId)
        {
            var result = await _queryService.GetYearlySummaryAsync(year, customerCompanyId);
            return Ok(result);
        }

        [HttpGet("company-yearly-summary")]
        public async Task<IActionResult> GetCompanyYearlySummary([FromQuery] int year)
        {
            var result = await _queryService.GetCompanyYearlySummaryAsync(year);
            return Ok(result);
        }
         
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDeclarationRequest request)
        {
            var id = await _commandService.CreateAsync(request);
            return Ok(new { Id = id });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDeclarationRequest request)
        {
            await _commandService.UpdateAsync(id, request);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _commandService.DeleteAsync(id);
            return NoContent();
        }
    }
}
