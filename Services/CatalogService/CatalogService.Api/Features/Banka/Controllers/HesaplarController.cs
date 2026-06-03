using CatalogService.Api.Features.Banka.Dtos;
using CatalogService.Api.Features.Banka.Services;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Banka.Controllers
{
    [ApiController]
    [Route("api/catalog/[controller]")]
    public class HesaplarController : ControllerBase
    {
        private readonly IHesapService _service;

        public HesaplarController(IHesapService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<HesapDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<HesapDto>>> GetAll(
            [FromQuery] int? firmaId = null,
            [FromQuery] bool includeInactive = false,
            CancellationToken ct = default)
        {
            var list = await _service.GetAllAsync(firmaId, includeInactive);
            return Ok(list);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(HesapDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<HesapDto>> GetById(int id, CancellationToken ct)
        {
            var hesap = await _service.GetByIdAsync(id);
            return hesap is null ? NotFound() : Ok(hesap);
        }

        [HttpPost]
        [ProducesResponseType(typeof(HesapDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<HesapDto>> Create([FromBody] HesapCreateDto dto, CancellationToken ct)
        {
            try
            {
                var created = await _service.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(HesapDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<HesapDto>> Update(int id, [FromBody] HesapUpdateDto dto, CancellationToken ct)
        {
            try
            {
                var updated = await _service.UpdateAsync(id, dto);
                return updated is null ? NotFound() : Ok(updated);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
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
