using CatalogService.Api.Features.Firmalar.Dtos;
using CatalogService.Api.Features.Firmalar.Services;
using CatalogService.Api.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Firmalar.Controllers
{
    [ApiController]
    [Route("api/catalog/[controller]")]
    public class FirmalarController : ControllerBase
    {
        private readonly IFirmaService _service;

        public FirmalarController(IFirmaService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<FirmaDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<FirmaDto>>> GetAll(
            [FromQuery] bool includeInactive = false,
            CancellationToken ct = default)
        {
            var list = await _service.GetAllAsync(includeInactive);
            return Ok(list);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(FirmaDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<FirmaDto>> GetById(int id, CancellationToken ct)
        {
            var firma = await _service.GetByIdAsync(id);
            return firma is null ? NotFound() : Ok(firma);
        }

        [HttpPost]
        [ProducesResponseType(typeof(FirmaDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<FirmaDto>> Create([FromBody] FirmaCreateDto dto, CancellationToken ct)
        {
            try
            {
                var created = await _service.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (DuplicateRecordException ex)
            {
                return Conflict(new { field = ex.Field, message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(FirmaDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<FirmaDto>> Update(int id, [FromBody] FirmaUpdateDto dto, CancellationToken ct)
        {
            try
            {
                var updated = await _service.UpdateAsync(id, dto);
                return updated is null ? NotFound() : Ok(updated);
            }
            catch (DuplicateRecordException ex)
            {
                return Conflict(new { field = ex.Field, message = ex.Message });
            }
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var ok = await _service.SoftDeleteAsync(id);
            return ok ? NoContent() : NotFound();
        }
    }
}
