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
            [FromQuery] int? firmaId,
            [FromQuery] string? declarationType,
            [FromQuery] bool pasifleriGoster = false)
        {
            // Firma = Firmalarım (FirmaId). Eski customerCompanyId parametresi kaldırıldı; pasif firmanın kayıtları
            // yalnız pasifleriGoster ile, FirmaId'si boş kayıtlar "Eşleşmeyen firma" grubunda.
            var result = await _queryService.GetMonthlySummaryAsync(year, month, firmaId, declarationType, pasifleriGoster);
            return Ok(result);
        }

        [HttpGet("yearly-summary")]
        public async Task<IActionResult> GetYearlySummary(
            [FromQuery] int year,
            [FromQuery] int? firmaId,
            [FromQuery] bool pasifleriGoster = false)
        {
            var result = await _queryService.GetYearlySummaryAsync(year, firmaId, pasifleriGoster);
            return Ok(result);
        }

        [HttpGet("company-yearly-summary")]
        public async Task<IActionResult> GetCompanyYearlySummary([FromQuery] int year, [FromQuery] bool pasifleriGoster = false)
        {
            var result = await _queryService.GetCompanyYearlySummaryAsync(year, pasifleriGoster);
            return Ok(result);
        }
         
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDeclarationRequest request)
        {
            try
            {
                var id = await _commandService.CreateAsync(request);
                return Ok(new { Id = id });
            }
            catch (BeyannameKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDeclarationRequest request)
        {
            try
            {
                await _commandService.UpdateAsync(id, request);
                return NoContent();
            }
            catch (BeyannameKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _commandService.DeleteAsync(id);
            return NoContent();
        }
    }
}
