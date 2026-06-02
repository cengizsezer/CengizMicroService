using CatalogService.Api.Features.Finans.Dtos;
using CatalogService.Api.Features.Finans.Services;
using CatalogService.Api.Features.Mukellefler.Domain;
using CatalogService.Api.Features.Mukellefler.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Finans.Controllers
{
    [ApiController]
    [Route("api/catalog/[controller]")]
    public class FinansController : ControllerBase
    {
        private readonly IFinansService _service;

        public FinansController(IFinansService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<MukellefFinansDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<MukellefFinansDto>>> GetPaginated(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null,
            [FromQuery] FinansSinifi? finansSinifi = null,
            CancellationToken ct = default)
        {
            var result = await _service.GetPaginatedAsync(page, pageSize, search, finansSinifi);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(MukellefFinansDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MukellefFinansDto>> GetById(int id, CancellationToken ct)
        {
            var dto = await _service.GetByIdAsync(id);
            return dto is null ? NotFound() : Ok(dto);
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(MukellefFinansDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MukellefFinansDto>> UpdateFinans(int id, [FromBody] FinansUpdateDto dto, CancellationToken ct)
        {
            var updated = await _service.UpdateFinansAsync(id, dto);
            return updated is null ? NotFound() : Ok(updated);
        }
    }
}
