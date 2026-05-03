using CatalogService.Api.Features.Mukellefler.Domain;
using CatalogService.Api.Features.Mukellefler.Dtos;
using CatalogService.Api.Features.Mukellefler.Services;
using CatalogService.Api.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Mukellefler.Controllers
{
    [ApiController]
    [Route("api/catalog/[controller]")]
    public class MukelleflerController : ControllerBase
    {
        private const long MaxImportFileBytes = 10L * 1024 * 1024; // 10 MB

        private readonly IMukellefService _service;
        private readonly IMukellefImportService _importService;

        public MukelleflerController(IMukellefService service, IMukellefImportService importService)
        {
            _service = service;
            _importService = importService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<MukellefDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<MukellefDto>>> GetPaginated(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null,
            [FromQuery] MukellefDurumu? durum = null,
            CancellationToken ct = default)
        {
            var result = await _service.GetPaginatedAsync(page, pageSize, search, durum);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(MukellefDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MukellefDto>> GetById(int id, CancellationToken ct)
        {
            var mukellef = await _service.GetByIdAsync(id);
            return mukellef is null ? NotFound() : Ok(mukellef);
        }

        [HttpPost]
        [ProducesResponseType(typeof(MukellefDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<MukellefDto>> Create([FromBody] MukellefCreateDto dto, CancellationToken ct)
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
        [ProducesResponseType(typeof(MukellefDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<MukellefDto>> Update(int id, [FromBody] MukellefUpdateDto dto, CancellationToken ct)
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

        [HttpPost("import")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxImportFileBytes + 1_048_576)]
        [ProducesResponseType(typeof(MukellefImportResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
        public async Task<ActionResult<MukellefImportResultDto>> Import(
            IFormFile file,
            [FromQuery] MukellefImportMode mode = MukellefImportMode.UpdateExisting,
            CancellationToken ct = default)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { message = "Boş dosya." });

            if (file.Length > MaxImportFileBytes)
                return StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = "Dosya boyutu 10 MB sınırını aşıyor." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".xlsx" && ext != ".xls")
                return StatusCode(StatusCodes.Status415UnsupportedMediaType, new { message = "Sadece .xlsx veya .xls dosyaları desteklenir." });

            try
            {
                await using var stream = file.OpenReadStream();
                var result = await _importService.ImportAsync(stream, mode, ct);
                return Ok(result);
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex) when (ex is FormatException || ex.GetType().FullName?.Contains("ClosedXML", StringComparison.OrdinalIgnoreCase) == true)
            {
                return StatusCode(StatusCodes.Status415UnsupportedMediaType, new { message = $"Excel dosyası okunamadı: {ex.Message}" });
            }
        }
    }
}
